using Infrastructure.Database;
using Infrastructure.Parser;
using Infrastructure.SignatureGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.PackageAnalyzer;

public class PackageRecognizer(IServiceProvider serviceProvider)
{
    private List<FunctionSignature> DataSet { get; set; } = new();

    /// <summary>
    /// The idea here is to spread the query into chunks that multiple workers can handle in parallel because the bottleneck is the speed
    /// a database return results over the TCP Bus and entity framework doing the ORM.
    /// By doing this with multiple DatabaseContext we can archive a huge scaling, even though it is not linear.
    /// The time spend was reduced from 15min to 3min using roughly 20 workers. There isn't much speed gain after 10 workers still.
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<FunctionSignatureContext>();
        var maxId = await dbContext.FunctionSignatures.AsNoTracking().MaxAsync(x => x.Id, cancellationToken: cancellationToken);
        int numInstances = 100;

        int chunkSize = maxId / numInstances;

        var query = Enumerable.Range(0, numInstances)
            .AsParallel()
            .AsOrdered()
            .SelectMany(i =>
            {
                int fromId = (i * chunkSize) + 1;
                int toId = (i == numInstances - 1) ? maxId : ((i + 1) * chunkSize);
                using var subScope = serviceProvider.CreateScope();
                using var subContext = subScope.ServiceProvider.GetRequiredService<FunctionSignatureContext>();
                return subContext.FunctionSignatures.AsNoTracking().Where(x => x.Id > fromId && x.Id <= toId).ToList();
            });

        DataSet = query.ToList();
    }
    
    /// <summary>
    /// Analyse the folder provided by finding first all required JS files.
    /// In a next step all files are parsed in parallel and the features get extracted and min and simhashes are created.
    /// Then we query the previously downloaded DataSet (also in parallel!) and try to match ever type of signature with the as argument provided minSimilarities
    /// At last the most likely version is calculated and returned to the user as verbose console logging.
    /// </summary>
    /// <param name="folderDirectory"></param>
    /// <param name="minSimilarityMinHash"></param>
    /// <param name="minSimilaritySimHash"></param>
    /// <param name="minOccurrencesMinHash"></param>
    /// <param name="minOccurrencesSimHash"></param>
    /// <param name="extractionThreshold"></param>
    /// <param name="cancellationToken"></param>
    public async Task AnalyseFolderAsync(DirectoryInfo folderDirectory, double minSimilarityMinHash, double minSimilaritySimHash, int minOccurrencesMinHash, int minOccurrencesSimHash, int extractionThreshold, CancellationToken cancellationToken)
    {
        var files = HelperFunctions.GetJavascriptFilesFromFolder(folderDirectory);

        var featureExtractor = new JavascriptFeatureExtractor();

        // semaphores to provide thread-safety as there isn't a thread-safe implementation by .net like ConcurrentBag for Lists.
        var semaphoreMinhash = new SemaphoreSlim(1);
        var semaphoreSimhash = new SemaphoreSlim(1);
        var allFoundMinHashes = new Dictionary<string, List<(string? Namespace, string LibName, string Version, double Similarity)>>();
        var allFoundSimHashes = new Dictionary<string, List<(string? Namespace, string LibName, string Version, double Similarity)>>();

        await Parallel.ForEachAsync(files, cancellationToken, async (file, token) =>
        {
            var code = await File.ReadAllTextAsync(file, cancellationToken);
            var extractFeatures = featureExtractor.ExtractFeatures(code, file, HelperFunctions.IsModule(file, code))
                .Where(x => x.ExtractedFeatures.Count > extractionThreshold)
                .ToList();

            foreach (var feature in extractFeatures)
            {
                var signatureMinHash = MinHash.ComputeMinHash(feature.ExtractedFeatures.Select(x => x.data).ToList());
                var signatureSimHash = SimHash.ComputeSimHash(feature.ExtractedFeatures, Weights.DefaultWeights);

                var similarSimHashes = DataSet
                    .AsParallel()
                    .Where(x => SimHash.SimilarityPercentage(x.SignatureSimhash, signatureSimHash) >
                                minSimilaritySimHash)
                    .ToList();
                var similarMinHashes = DataSet
                    .AsParallel()
                    .Where(x => MinHash.GetSimilarity(x.SignatureMinhash, signatureMinHash) > minSimilarityMinHash)
                    .ToList();

                foreach (var functionSignature in similarSimHashes)
                {
                    var similarity = SimHash.SimilarityPercentage(functionSignature.SignatureSimhash, signatureSimHash);
                    var libName = functionSignature.LibName;
                    var namespaceName = functionSignature.Namespace;
                    var version = functionSignature.Version;

                    // this is the parallel accessed part protected by semaphores
                    await semaphoreSimhash.WaitAsync(token);
                    if (!allFoundSimHashes.ContainsKey(libName))
                    {
                        allFoundSimHashes[libName] = [];
                    }

                    allFoundSimHashes[libName].Add((namespaceName, libName, version, similarity));
                    semaphoreSimhash.Release();
                }

                foreach (var functionSignature in similarMinHashes)
                {
                    var similarity = MinHash.GetSimilarity(functionSignature.SignatureMinhash, signatureMinHash);
                    var libName = functionSignature.LibName;
                    var namespaceName = functionSignature.Namespace;
                    var version = functionSignature.Version;
    
                    // same here for minhash
                    await semaphoreMinhash.WaitAsync(token);
                    if (!allFoundMinHashes.ContainsKey(libName))
                    {
                        allFoundMinHashes[libName] = [];
                    }

                    allFoundMinHashes[libName].Add((namespaceName, libName, version, similarity));
                    semaphoreMinhash.Release();
                }
            }
        });

        var mostLikelyVersionsMinHashes = GetMostLikelyVersions(allFoundMinHashes, minOccurrencesMinHash);
        var mostLikelyVersionsSimHashes = GetMostLikelyVersions(allFoundSimHashes, minOccurrencesSimHash);
        
        // you could also update the code to return it to the function caller instead of printing it here.
        // I did this just for simplicity... Kinda dumb because it needed it to pass later again... oh well
        Console.WriteLine("===== Folder: {0} =======", folderDirectory);
        Console.WriteLine("~~~~~ MINHASH ~~~~~~~");
        foreach (var element in mostLikelyVersionsMinHashes)
        {
            Console.WriteLine("NS; {0}; Library; {1}; Version; {4}; Occurence; {2}; Confidence; {3}", element.Value.Namespace, element.Value.LibName, element.Value.Occurrences, element.Value.Similarity, element.Value.Version);
        }
        Console.WriteLine("~~~~~ SIMHASH ~~~~~~~");
        foreach (var element in mostLikelyVersionsSimHashes)
        {
            Console.WriteLine("NS; {0}; Library; {1}; Version; {4}; Occurence; {2}; Confidence; {3}", element.Value.Namespace, element.Value.LibName, element.Value.Occurrences, element.Value.Similarity, element.Value.Version);
        }
    }
    
    Dictionary<string, (string LibName, string? Namespace, string Version, double Similarity, int Occurrences)>
        GetMostLikelyVersions(
            Dictionary<string, List<(string? Namespace, string LibName, string Version, double Similarity)>> extractedFeatures, int minOccurrences)
    {
        // this semaphore is to prevent conflicts with the mostLikelyVersions dic in threaded scenario
        // consider removing the parallel as it isn't really a hot path
        var semaphore = new SemaphoreSlim(1);
        var mostLikelyVersions = new Dictionary<string, (string LibName, string? Namespace, string Version, double Similarity, int occurrences)>();
        
        Parallel.ForEach(extractedFeatures, (feature) =>
        {
            // group together versions and count the occurences
            var groupedVersions = feature.Value.GroupBy(x => x.Version);
            var versionStats = groupedVersions.Select(g => (
                Version: g.Key,
                Similarity: g.Average(x => x.Similarity),
                Occurrences: g.Count()
            ));

            var filteredVersions = versionStats.Where(x => x.Occurrences >= minOccurrences);
            
            var valueTuples = filteredVersions as (string Version, double Similarity, int Occurrences)[] ??
                              filteredVersions.ToArray();
            if (valueTuples.Any())
            {
                var maxOccurrences = valueTuples.Max(x => x.Occurrences);
                var mostLikelyVersion = valueTuples.FirstOrDefault(x => x.Occurrences == maxOccurrences);

                if (mostLikelyVersion != default)
                {
                    // here is where the parallel access to the dic kicks in
                    semaphore.Wait();
                    mostLikelyVersions[feature.Key] = (
                        feature.Key,
                        feature.Value.First().Namespace,
                        mostLikelyVersion.Version,
                        mostLikelyVersion.Similarity,
                        mostLikelyVersion.Occurrences
                    );
                    semaphore.Release();
                }
            }
        });

        return mostLikelyVersions;
    }
}