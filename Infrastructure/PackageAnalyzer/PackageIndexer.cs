using System.ComponentModel;
using EFCore.BulkExtensions;
using Infrastructure.Database;
using Infrastructure.Parser;
using Infrastructure.SignatureGeneration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Infrastructure.PackageAnalyzer;

public class PackageIndexer
{
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

    private async Task ProcessVersionAsync(DirectoryInfo packageDirectory, DirectoryInfo versionDir,
        string? namespaceName, IServiceProvider serviceProvider, CancellationToken cancellationToken,
        ProgressTask progressTask)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PackageIndexer>>();
        await using var database = scope.ServiceProvider.GetRequiredService<FunctionSignatureContext>();
        
        // Get all javascript files that matter in the version folder
        // we filter out already minimized production ready stuff (we don't want to index twice, prevent duplicates yk)
        var files = HelperFunctions.GetJavascriptFilesFromFolder(packageDirectory);

        var librarySignatures = new List<FunctionSignature>();

        foreach (var file in files)
        {
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
        
        try
        {
            await database.FunctionSignatures.AddRangeAsync(librarySignatures, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
        } 
        catch (Exception ex)
        {
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