using Infrastructure.Database;
using Infrastructure.Parser;
using Infrastructure.SignatureGeneration;
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
        await Parallel.ForEachAsync(versionDirs, token,  async (versionDir, cancellationToken) =>
            await ProcessVersionAsync(packageDirectory, versionDir, namespaceName, serviceProvider, cancellationToken, progressTask));
        progressTask.StopTask();
    }

    private async Task ProcessVersionAsync(DirectoryInfo packageDirectory, DirectoryInfo versionDir,
        string? namespaceName, IServiceProvider serviceProvider, CancellationToken cancellationToken,
        ProgressTask progressTask)
    {
        var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<PackageIndexer>>();
        var database = scope.ServiceProvider.GetRequiredService<FunctionSignatureContext>();
        
        // Get all javascript files that matter in the version folder
        // we filter out already minimized production ready stuff (we don't want to index twice, prevent duplicates yk)
        var files = Directory.GetFiles(versionDir.FullName, "*.*", SearchOption.AllDirectories)
            .Where(x => x.EndsWith(".js") || x.EndsWith(".mjs"))
            .Where(x => !x.Contains(".min.") && !x.Contains(".prod."));

        var librarySignatures = new List<FunctionSignature>();

        foreach (var file in files)
        {
            try
            {

                var featureExtractor = new JavascriptFeatureExtractor();
                var code = await File.ReadAllTextAsync(file, cancellationToken);
                var features = featureExtractor.ExtractFeatures(code, file, HelperFunctions.IsModule(file, code));
                librarySignatures.AddRange(ProcessFunctions(features));
                
            }
            catch (Exception ex)
            {
                logger.LogWarning("Failed to index file: {0}!", file);
                logger.LogError(ex.Message);
            }
        }

        foreach (var librarySignature in librarySignatures)
        {
            librarySignature.Namespace = namespaceName;
            librarySignature.LibName = packageDirectory.Name;
        }
        
        try
        {
            var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            await database.FunctionSignatures.AddRangeAsync(librarySignatures, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        } 
        catch (Exception ex)
        {
            logger.LogWarning("Failed insert signatures to database for folder: {0}!", versionDir.FullName);
            logger.LogError(ex.Message);
        }
        finally
        {
            progressTask.Increment(1);
        }
        
    }

    private IEnumerable<FunctionSignature> ProcessFunctions(List<Function> functions)
    {
        return functions.Select(function => new FunctionSignature()
        {
            CreationDateTime = DateTime.UtcNow,
            FunctionName = function.FunctionName,
            SignatureSimhash = SimHash.ComputeSimHash(function.ExtractedFeatures, Weights.DefaultWeights),
            SignatureMinhash = MinHash.ComputeMinHash(function.ExtractedFeatures.Select(x => x.data).ToList())
        });
    }
}