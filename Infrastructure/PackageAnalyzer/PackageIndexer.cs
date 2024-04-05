using Infrastructure.Database;
using Infrastructure.Parser;
using Infrastructure.SignatureGeneration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Infrastructure.PackageAnalyzer;

public class PackageIndexer
{
    /// <summary>
    /// First abstraction layer, we split the package into its version folders. and process these versions in parallel.
    /// </summary>
    /// <param name="packageDirectory"></param>
    /// <param name="maxParallelVersionIndexer"></param>
    /// <param name="namespaceName"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="token"></param>
    /// <param name="progressTask"></param>
    public async Task IndexPackageAsync(DirectoryInfo packageDirectory, int maxParallelVersionIndexer, string? namespaceName, IServiceProvider serviceProvider, CancellationToken token,  ProgressTask progressTask)
    {
        var versionDirs = packageDirectory.GetDirectories();
        progressTask.MaxValue = versionDirs.Length;
        await Parallel.ForEachAsync(versionDirs, new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = maxParallelVersionIndexer
        },  async (versionDir, cancellationToken) =>
            await ProcessVersionAsync(packageDirectory, versionDir, namespaceName, serviceProvider, cancellationToken, progressTask));
        progressTask.StopTask();
    }

    /// <summary>
    /// Proccesses a version folder by first getting the JS-Files and then calculating the signatures.
    /// </summary>
    /// <param name="packageDirectory"></param>
    /// <param name="versionDir"></param>
    /// <param name="namespaceName"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="cancellationToken"></param>
    /// <param name="progressTask"></param>
    private async Task ProcessVersionAsync(DirectoryInfo packageDirectory, DirectoryInfo versionDir,
        string? namespaceName, IServiceProvider serviceProvider, CancellationToken cancellationToken,
        ProgressTask progressTask)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PackageIndexer>>();
        await using var database = scope.ServiceProvider.GetRequiredService<FunctionSignatureContext>();
        
        var files = HelperFunctions.GetJavascriptFilesFromFolder(packageDirectory);

        var librarySignatures = new List<FunctionSignature>();

        foreach (var file in files)
        {
            // the underlying Lib for building the AST can fail for various reasons. We need to make sure we catch it here
            try
            {
                var featureExtractor = new JavascriptFeatureExtractor();
                var code = await File.ReadAllTextAsync(file, cancellationToken);
                var features = featureExtractor.ExtractFeatures(code, file, HelperFunctions.IsModule(file, code));
                //filter
                features = features.Where(x => x.ExtractedFeatures.Count > 100).ToList();
                librarySignatures.AddRange(ProcessFunctions(features));
                
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to index file: {0}!", file);
                logger.LogWarning(ex.Message);
            }
        }

        foreach (var librarySignature in librarySignatures)
        {
            librarySignature.Namespace = namespaceName;
            librarySignature.LibName = packageDirectory.Name;
            librarySignature.Version = versionDir.Name;
        }
        
        int retryAttempt = 0;
        repeat:
        
        // Database operations can fail or slow down on a heavily beaten system
        try
        {
            await database.FunctionSignatures.AddRangeAsync(librarySignatures, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        } 
        catch (Exception ex)
        {
            // we need to make sure we never lose any data! We already have a retry policy with random backoffs, but we
            // need to make absolutely sure we will not lose any data to a database hiccup.
            // we still provide logging to notify for any critical database fails.
            if (retryAttempt++ > 10)
            {
                logger.LogCritical("Failed insert signatures to database for folder: {0} for {1} times!", versionDir.FullName, retryAttempt);
                logger.LogError(ex.InnerException != null ? ex.InnerException.Message : ex.Message);
            }
            goto repeat;
        }
        finally
        {
            progressTask.Increment(1);
        }
        
    }

    /// <summary>
    /// This function translate the found data into the Database FunctionSignature Model.
    /// </summary>
    /// <param name="functions"></param>
    /// <returns></returns>
    private IEnumerable<FunctionSignature> ProcessFunctions(List<Function> functions)
    {
        foreach (var function in functions)
        {
            var signature = new FunctionSignature()
            {
                CreationDateTime = DateTime.UtcNow, FunctionName = function.FunctionName,
                SignatureSimhash = SimHash.ComputeSimHash(function.ExtractedFeatures, Weights.DefaultWeights),
                SignatureMinhash = MinHash.ComputeMinHash(function.ExtractedFeatures.Select(x => x.data).ToList())
            };

            yield return signature;
        }
    }
}