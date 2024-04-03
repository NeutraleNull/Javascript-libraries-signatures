using System.Collections.Concurrent;
using Infrastructure.Database;
using Infrastructure.Parser;
using Infrastructure.SignatureGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.PackageAnalyzer;

public class PackageRecognizer(IServiceProvider serviceProvider)
{
    private List<FunctionSignature> DataSet { get; set; } = new();

    public async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        await using var dbContext = scope.ServiceProvider.GetRequiredService<FunctionSignatureContext>();
        DataSet = await dbContext.FunctionSignatures.AsNoTracking().ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task AnalyseFolderAsync(DirectoryInfo folderDirectory, double minSimilarity, int extractionThreshold, CancellationToken cancellationToken)
    {
        var files = HelperFunctions.GetJavascriptFilesFromFolder(folderDirectory);

        var featureExtractor = new JavascriptFeatureExtractor();

        var allFoundMinHashes = new Dictionary<string, List<(string? Namespace, string LibName, string Version, double Similarity)>>();
        var allFoundSimHashes = new Dictionary<string, List<(string? Namespace, string LibName, string Version, double Similarity)>>();
        
        foreach (var file in files)
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
                    .Where(x => SimHash.SimilarityPercentage(x.SignatureSimhash, signatureSimHash) > minSimilarity)
                    .ToList();
                var similarMinHashes = DataSet
                    .AsParallel()
                    .Where(x => MinHash.GetSimilarity(x.SignatureMinhash, signatureMinHash) > minSimilarity)
                    .ToList();

                Console.WriteLine(similarSimHashes.Count + similarMinHashes.Count);
                
                foreach (var functionSignature in similarSimHashes)
                {
                    var similarity = SimHash.SimilarityPercentage(functionSignature.SignatureSimhash, signatureSimHash);
                    var libName = functionSignature.LibName;
                    var namespaceName = functionSignature.Namespace;
                    var version = functionSignature.Version;
                    
                    if (!allFoundSimHashes.ContainsKey(libName))
                    {
                        allFoundSimHashes[libName] = [];
                    }

                    allFoundSimHashes[libName].Add((namespaceName, libName, version, similarity));
                }
                
                foreach (var functionSignature in similarMinHashes)
                {
                    var similarity = MinHash.GetSimilarity(functionSignature.SignatureMinhash, signatureMinHash);
                    var libName = functionSignature.LibName;
                    var namespaceName = functionSignature.Namespace;
                    var version = functionSignature.Version;
                    
                    if (!allFoundMinHashes.ContainsKey(libName))
                    {
                        allFoundMinHashes[libName] = [];
                    }
                    allFoundMinHashes[libName].Add((namespaceName, libName, version, similarity));
                }
            }
        }

        var mostLikelyVersionsMinHashes = GetMostLikelyVersions(allFoundMinHashes, 5);
        var mostLikelyVersionsSimHashes = GetMostLikelyVersions(allFoundSimHashes, 5);
        
        Console.WriteLine("===== Folder: {0} =======", folderDirectory);
        Console.WriteLine("~~~~~ MINHASH ~~~~~~~");
        foreach (var element in mostLikelyVersionsMinHashes)
        {
            Console.WriteLine("NS; {0}; Library; {1}; Occurence; {2}; Confidence; {3}", element.Value.Namespace, element.Value.LibName, element.Value.Occurrences, element.Value.Similarity);
        }
        Console.WriteLine("~~~~~ SIMHASH ~~~~~~~");
        foreach (var element in mostLikelyVersionsSimHashes)
        {
            Console.WriteLine("NS; {0}; Library; {1}; Occurence; {2}; Confidence; {3}", element.Value.Namespace, element.Value.LibName, element.Value.Occurrences, element.Value.Similarity);
        }
    }
    
    Dictionary<string, (string LibName, string? Namespace, string Version, double Similarity, int Occurrences)>
        GetMostLikelyVersions(
            Dictionary<string, List<(string? Namespace, string LibName, string Version, double Similarity)>> extractedFeatures, int minOccurrences)
    {
        var mostLikelyVersions = new Dictionary<string, (string LibName, string? Namespace, string Version, double Similarity, int occurrences)>();

        foreach (var feature  in extractedFeatures)
        {
            var groupedVersions = feature.Value.GroupBy(x => x.Version);
            var versionStats = groupedVersions.Select(g => (
                Version: g.Key,
                Similarity: g.Average(x => x.Similarity),
                Occurrences: g.Count()
            ));

            var filteredVersions = versionStats.Where(x => x.Occurrences >= minOccurrences);

            var valueTuples = filteredVersions as (string Version, double Similarity, int Occurrences)[] ?? filteredVersions.ToArray();
            if (valueTuples.Any())
            {
                var maxOccurrences = valueTuples.Max(x => x.Occurrences);
                var mostLikelyVersion = valueTuples.FirstOrDefault(x => x.Occurrences == maxOccurrences);

                if (mostLikelyVersion != default)
                {
                    mostLikelyVersions[feature.Key] = (
                        feature.Key,
                        feature.Value.First().Namespace,
                        mostLikelyVersion.Version,
                        mostLikelyVersion.Similarity,
                        mostLikelyVersion.Occurrences
                    );
                }
            }
        }

        return mostLikelyVersions;
    }
}