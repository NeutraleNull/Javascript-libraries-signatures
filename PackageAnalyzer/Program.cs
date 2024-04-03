using System.Diagnostics;
using Cocona;
using Infrastructure.Database;
using Infrastructure.PackageAnalyzer;
using Karambolo.Extensions.Logging.File;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Spectre.Console;


AnsiConsole.WriteLine("Starting up");
var builder = CoconaApp.CreateBuilder();
builder.Services.AddPostgresDB();
builder.Logging.ClearProviders();
Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs"));
builder.Logging.SetMinimumLevel(LogLevel.Critical);
builder.Logging.AddFile(options =>
{
    options.RootPath = Path.Combine(AppContext.BaseDirectory, "logs");
    options.Files = [new LogFileOptions { Path = "FileAnalyzer-<counter>.log" }];
});
var app = builder.Build();

app.AddCommand("extractFeatures", async (IServiceProvider serviceProvider, FunctionSignatureContext context,
    string inputDir, int parallelAnalysers = 20) =>
{
    if (!Directory.Exists(inputDir))
    {
        AnsiConsole.MarkupLine("[red]Input folder does not exist or is empty...[/]");
        return -1;
    }
    
    context.Database.EnsureCreated();
    await context.Database.MigrateAsync();
    
    
    var topLevel =  Directory.GetDirectories(inputDir).Reverse();
    var packages = new List<(DirectoryInfo Package, string? NamepaceName)>();

    foreach (var dir in topLevel)
    {
        var directoryInfo = new DirectoryInfo(dir);

        if (directoryInfo.Name.StartsWith("@"))
        {
            var packageDirectoryInfos = directoryInfo.GetDirectories();
            foreach (var packageDirectoryInfo in packageDirectoryInfos)
            {
                packages.Add((packageDirectoryInfo, directoryInfo.Name));
            }
        }
        else
        {
            packages.Add((directoryInfo, null));
        }
    }

    await AnsiConsole
        .Progress()
        .HideCompleted(true)
        .StartAsync(async ctx =>
        {
            AnsiConsole.MarkupLine("[green]Beginning to analyse.[/]");
            var progressTask =
                ctx.AddTask($"Total progress:");
            progressTask.MaxValue = packages.Count;
            progressTask.StartTask();

            await Parallel.ForEachAsync(packages, new ParallelOptions() {MaxDegreeOfParallelism = parallelAnalysers },
                async (package, cancellationToken) =>
                {
                    var task = ctx.AddTask($"Processing package: {package.NamepaceName}{(string.IsNullOrWhiteSpace(package.NamepaceName)? "" :  "/")}{package.Package.Name}");
                    var indexer = new PackageIndexer();
                    await indexer.IndexPackageAsync(package.Package, 10, package.NamepaceName,
                        serviceProvider, cancellationToken, task);
                    task.StopTask();
                    progressTask.Increment(1);
                });
            progressTask.StopTask();
            AnsiConsole.MarkupLine("[green]Processing completed.[/]");
        });
    return 0;
});

app.AddCommand("analyzeFolders", async (IServiceProvider serviceProvider, CancellationToken token, string inputDir, double minSimilarity = 0.9) =>
{
    var directoryInfo = new DirectoryInfo(inputDir);
    if (!directoryInfo.Exists)
    {
        AnsiConsole.MarkupLine("[red]Input folder does not exist or is empty...[/]");
        return -1;
    }

    var subFolders = directoryInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);

    var stopWatch = new Stopwatch();
    
    var packageRecognizers = new PackageRecognizer(serviceProvider);
    stopWatch.Start();
    AnsiConsole.MarkupLine("[yellow] Loading data.. please wait... [/]");
    await packageRecognizers.LoadDataAsync(token);
    AnsiConsole.MarkupLine("[yellow] Loading data completed in {0}! [/]", stopWatch.Elapsed);
    
    
    foreach (var subFolder in subFolders)
    {
        stopWatch.Restart();
        await packageRecognizers.AnalyseFolderAsync(subFolder, minSimilarity, 150, token);
        AnsiConsole.MarkupLine("[yellow] Folder analyze completed {0}! [/]", stopWatch.Elapsed);
    }
    
    AnsiConsole.MarkupLine("[green]Processing completed.[/]");
    
    return 0;
});
/*
app.AddCommand("analyzeFolder", async (string inputDir, float minSimilarity = 0.85f, int parallelAnalysers = 5) =>
{
    var directoryInfo = new DirectoryInfo(inputDir);
    if (!directoryInfo.Exists)
    {
        AnsiConsole.MarkupLine("[red]Input folder does not exist or is empty...[/]");
        return -1;
    }

    AnsiConsole.MarkupLine("[green]Caching data...[/]");
    using var context = new FunctionSignatureContext();
    var data = await context.FunctionSignatures.AsNoTracking().ToListAsync();
    AnsiConsole.MarkupLine("[green]Caching data completed...[/]");
    
    var files = Directory.GetFiles(inputDir, "*.*", SearchOption.AllDirectories)
            .Where(x => x.EndsWith(".js") || x.EndsWith(".mjs"))
            .Where(x => !x.Contains(".min.") && !x.Contains(".prod."));

    await AnalyzeFiles(files.ToArray(), minSimilarity, data);

    AnsiConsole.MarkupLine("[green]Processing completed.[/]");
    
    return 0;
});
*/
await app.RunAsync();
return 0;

/*
async Task AnalyzeFiles(string[] files, float minSimilarity, List<FunctionSignature> data)
{
    var allExtractedFeaturesSimHash = new Dictionary<string, List<(string Version, double Similarity)>>();
    var allExtractedFeaturesMinHash = new Dictionary<string, List<(string Version, double Similarity)>>();
    
    foreach (var file in files)
    {
        var featureExtractor = new JavascriptFeatureExtractor();
        var code = await File.ReadAllTextAsync(file);
        var features = await featureExtractor.ExtractFeaturesAsync(code, file, IsModule(file, code));
        
        foreach (var feature in features.Functions)
        {
            var simHashGenerator = new SimHashGenerator(64, Weights.DefaultWeights);
            //var minHashGenerator = new MinHashGenerator();
            var simHash = simHashGenerator.GenerateSimHash(feature.ExtractedFeatures);
            var minHash = MinHashGenerator.GenerateMinHash(feature.ExtractedFeatures, 256); 
            
            var similarSimHashes = 
                data.AsParallel().AsOrdered().Where(x => SimHashGenerator.GetSimilarity(x.SignatureSimhash, simHash) > minSimilarity).ToList();
            
            var similarMinHashes = 
                data.AsParallel().AsOrdered().Where(x => MinHashGenerator.GetMinHashFromBytes(256, x.SignatureMinhash).Jaccard(minHash) > minSimilarity).ToList();
            //AnsiConsole.MarkupLine($"MINHASH MATCHES: {similarMinHashes.Count}");
            
            foreach (var functionSignature in similarSimHashes)
            {
                var similarity = SimHashGenerator.GetSimilarity(functionSignature.SignatureSimhash, simHash);
                var libName = functionSignature.LibName;
                var version = functionSignature.Version;

                if (!allExtractedFeaturesSimHash.ContainsKey(libName))
                {
                    allExtractedFeaturesSimHash[libName] = new List<(string Version, double Similarity)>();
                }

                allExtractedFeaturesSimHash[libName].Add((version, similarity));
            }

            foreach (var functionSignature in similarMinHashes)
            {
                var similarity = MinHashGenerator.GetMinHashFromBytes(256, functionSignature.SignatureSimhash).Jaccard(minHash);
                var libName = functionSignature.LibName;
                var version = functionSignature.Version;

                if (!allExtractedFeaturesMinHash.ContainsKey(libName))
                {
                    allExtractedFeaturesMinHash[libName] = new List<(string Version, double Similarity)>();
                }

                allExtractedFeaturesMinHash[libName].Add((version, similarity));
            }
        }
    }

    var mostLikelyVersionsSimHash = GetMostLikelyVersions(allExtractedFeaturesSimHash);
    var mostLikelyVersionsMinHash = GetMostLikelyVersions(allExtractedFeaturesMinHash);

    AnsiConsole.MarkupLine($"[yellow]Most Likely Versions (SimHash) across all files and functions (with at least 5 occurrences):[/]");
    foreach (var kvp in mostLikelyVersionsSimHash)
    {
        AnsiConsole.MarkupLine($"Library: {kvp.Key}, Version: {kvp.Value.Version}, Similarity: {kvp.Value.Similarity}, Occurrences: {kvp.Value.Occurrences}");
    }

    AnsiConsole.MarkupLine($"[yellow]Most Likely Versions (MinHash) across all files and functions (with at least 5 occurrences):[/]");
    foreach (var kvp in mostLikelyVersionsMinHash)
    {
        AnsiConsole.MarkupLine($"Library: {kvp.Key}, Version: {kvp.Value.Version}, Similarity: {kvp.Value.Similarity}, Occurrences: {kvp.Value.Occurrences}");
    }

    var combinedMostLikelyVersions = new Dictionary<string, (string Version, double Similarity, int Occurrences)>();
    foreach (var kvp in mostLikelyVersionsSimHash)
    {
        var libName = kvp.Key;
        var simHashVersion = kvp.Value.Version;

        if (mostLikelyVersionsMinHash.TryGetValue(libName, out var minHashResult) && minHashResult.Version == simHashVersion)
        {
            combinedMostLikelyVersions[libName] = kvp.Value;
        }
    }

    AnsiConsole.MarkupLine($"[yellow]Combined Most Likely Versions (SimHash and MinHash) across all files and functions:[/]");
    foreach (var kvp in combinedMostLikelyVersions)
    {
        AnsiConsole.MarkupLine($"Library: {kvp.Key}, Version: {kvp.Value.Version}, Similarity: {kvp.Value.Similarity}, Occurrences: {kvp.Value.Occurrences}");
    }
}

Dictionary<string, (string Version, double Similarity, int Occurrences)> GetMostLikelyVersions(Dictionary<string, List<(string Version, double Similarity)>> extractedFeatures)
{
    var mostLikelyVersions = new Dictionary<string, (string Version, double Similarity, int Occurrences)>();

    foreach (var (libName, versions) in extractedFeatures)
    {
        var groupedVersions = versions.GroupBy(x => x.Version);
        var versionStats = groupedVersions.Select(g => (
            Version: g.Key,
            Similarity: g.Average(x => x.Similarity),
            Occurrences: g.Count()
        ));

        var filteredVersions = versionStats.Where(x => x.Occurrences >= 5);

        var valueTuples = filteredVersions as (string Version, double Similarity, int Occurrences)[] ?? filteredVersions.ToArray();
        if (valueTuples.Any())
        {
            var maxOccurrences = valueTuples.Max(x => x.Occurrences);
            var mostLikelyVersion = valueTuples.FirstOrDefault(x => x.Occurrences == maxOccurrences);

            if (mostLikelyVersion != default)
            {
                mostLikelyVersions[libName] = (
                    mostLikelyVersion.Version,
                    mostLikelyVersion.Similarity,
                    mostLikelyVersion.Occurrences
                );
            }
        }
    }

    return mostLikelyVersions;
}*/