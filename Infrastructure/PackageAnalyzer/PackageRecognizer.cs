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
        DataSet = await dbContext.FunctionSignatures.ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task AnalyseFolderAsync(DirectoryInfo folderDirectory, double minSimilarity, int extractionThreshold, CancellationToken cancellationToken)
    {
        var files = HelperFunctions.GetJavascriptFilesFromFolder(folderDirectory);

        var featureExtractor = new JavascriptFeatureExtractor();

        var functionSignatures = new List<FunctionSignature>();
        
        foreach (var file in files)
        {
            var code = await File.ReadAllTextAsync(file, cancellationToken);
            var extractFeatures = featureExtractor.ExtractFeatures(code, file, HelperFunctions.IsModule(file, code))
                .Where(x => x.ExtractedFeatures.Count > extractionThreshold)
                .ToList();

            functionSignatures.AddRange(extractFeatures.Select(x => new FunctionSignature
            {
                FunctionName = x.FunctionName,
                SignatureMinhash = MinHash.ComputeMinHash(x.ExtractedFeatures.Select(y => y.data).ToList()),
                SignatureSimhash = SimHash.ComputeSimHash(x.ExtractedFeatures, Weights.DefaultWeights)
            }));
        }
        
        var similarByMinHash = await FindSimilarMinHash(functionSignatures, minSimilarity, cancellationToken);
        var similarBySimHash = await FindSimilarSimHash(functionSignatures, minSimilarity, cancellationToken);

        var minHashSolution = FilterMatches(similarByMinHash, 5);
        var simHashSolution = FilterMatches(similarBySimHash, 5);

        Console.WriteLine("===== Folder: {0} =======", folderDirectory);
        Console.WriteLine("~~~~~ MINHASH ~~~~~~~");
        foreach (var element in minHashSolution)
        {
            Console.WriteLine("NS; {0}; Library; {1}; Occurence; {2}; Confidence; {3}", element.NamespaceName, element.Library, element.Occurences, element.Confidence);
        }
    }


    private List<RecognizerResult> FilterMatches(
        List<(FunctionSignature Provided, FunctionSignature Database, double Confidence)> similarBySimHash,
        int minOccurrence)
    {
        var preFiltered = similarBySimHash
            .GroupBy(x => new { x.Database.LibName, x.Database.Version })
            .Where(group => group.Count() >= minOccurrence)
            .ToList();

        var intermediateResults = new List<RecognizerResult>();
        foreach (var group in preFiltered)
        {
            foreach (var signature in group)
            {
                var result = new RecognizerResult(
                    group.Key.LibName,
                    signature.Database.Namespace,
                    signature.Database.Version,
                    signature.Confidence,
                    group.Count());

                intermediateResults.Add(result);
            }
        }

        var recognizerResults = intermediateResults
            .GroupBy(x => x.Library)
            .Select(libraryGroup => libraryGroup
                .GroupBy(x => x.Version)
                .OrderByDescending(versionGroup => versionGroup.Average(x => x.Confidence))
                .First()
                .First())
            .ToList();

        return recognizerResults;
    }

    private async Task<List<(FunctionSignature signatureFromFile, FunctionSignature similarDatabaseSignature, double Similarity)>> FindSimilarSimHash(
        List<FunctionSignature> functionSignaturesFromFile, double minSimilarity, CancellationToken cancellationToken)
    {
        var concurrentBag = new ConcurrentBag<(FunctionSignature signatureFromFile, FunctionSignature similarDatabaseSignature, double)>();
        await Parallel.ForEachAsync(functionSignaturesFromFile, cancellationToken, (signatureFromFile, token) =>
        {
            var similarDatabaseSignatures = DataSet.Where(x =>
                SimHash.SimilarityPercentage(x.SignatureSimhash, signatureFromFile.SignatureSimhash) > minSimilarity).ToList();
            foreach (var similarDatabaseSignature in similarDatabaseSignatures)
            {
                concurrentBag.Add((signatureFromFile, similarDatabaseSignature, SimHash.SimilarityPercentage(similarDatabaseSignature.SignatureSimhash, signatureFromFile.SignatureSimhash)));
            }
            
            return ValueTask.CompletedTask;
        });
        return concurrentBag.ToList();
    }
    
    private async Task<List<(FunctionSignature signatureFromFile, FunctionSignature similarDatabaseSignature, double Similarity)>> FindSimilarMinHash(
        List<FunctionSignature> functionSignaturesFromFile, double minSimilarity, CancellationToken cancellationToken)
    {
        var concurrentBag = new ConcurrentBag<(FunctionSignature signatureFromFile, FunctionSignature similarDatabaseSignature, double Similarity)>();
        await Parallel.ForEachAsync(functionSignaturesFromFile, cancellationToken, (signatureFromFile, token) =>
        {
            var similarDatabaseSignatures = DataSet.Where(x =>
                MinHash.GetSimilarity(x.SignatureMinhash, signatureFromFile.SignatureMinhash) > minSimilarity).ToList();
            foreach (var similarDatabaseSignature in similarDatabaseSignatures)
            {
                concurrentBag.Add((signatureFromFile, similarDatabaseSignature, MinHash.GetSimilarity(similarDatabaseSignature.SignatureMinhash, signatureFromFile.SignatureMinhash)));
            }

            return ValueTask.CompletedTask;
        });
        return concurrentBag.ToList();
    }

    public record RecognizerResult(string Library, string? NamespaceName, string Version, double Confidence, int Occurences);
}