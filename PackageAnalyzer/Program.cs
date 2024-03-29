using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Transactions;
using Cocona;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MinHashSharp;
using PackageAnalyzer;
using SharedCommonStuff;
using Spectre.Console;


AnsiConsole.WriteLine("Starting up");
var app = CoconaApp.Create();

app.AddCommand("extractFeatures", async (string inputDir, int parallelAnalysers = 20) =>
{
    if (!Directory.Exists(inputDir))
    {
        AnsiConsole.MarkupLine("[red]Input folder does not exist or is empty...[/]");
        return -1;
    }
    /*
    // Create or overwrite the SQLite file
    if (overwrite && File.Exists(sqliteFile))
    {
        File.Delete(sqliteFile);
        sqliteFilePath = sqliteFile;
    }*/
    
    
    using (var context = new FunctionSignatureContext())
    {
        context.Database.EnsureCreated();
        await context.Database.MigrateAsync();
    }

    var topLevel = Directory.GetDirectories(inputDir).Reverse();

    var packages = new ConcurrentBag<(string LibName, string Namespace, DirectoryInfo[] VersionDirs)>();

    foreach (var dir in topLevel)
    {
        var directoryInfo = new DirectoryInfo(dir);

        if (directoryInfo.Name.StartsWith("@"))
        {
                var packageDirs = directoryInfo.GetDirectories();
                foreach (var packageDir in packageDirs)
                {
                    var libName = packageDir.Name;
                    var ns = directoryInfo.Name;
                    var versionDirs = packageDir.GetDirectories();
                    packages.Add((libName, ns, versionDirs));
                }
        }
        else
        {
            var libName = directoryInfo.Name;
            packages.Add((libName, null, directoryInfo.GetDirectories())!);
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
                    var task = ctx.AddTask($"Processing package: {package.LibName}");
                    await ProcessPackage(package.LibName, package.Namespace, package.VersionDirs, task);
                    task.StopTask();
                    progressTask.Increment(1);
                });
            progressTask.StopTask();
            AnsiConsole.MarkupLine("[green]Processing completed.[/]");
        });
    return 0;
});

app.AddCommand("analyzeFolders", async (string inputDir, string minSimilarity = "0.85", int parallelAnalysers = 5) =>
{
    double requiredSimilarity = double.Parse(minSimilarity);
    var directoryInfo = new DirectoryInfo(inputDir);
    if (!directoryInfo.Exists)
    {
        AnsiConsole.MarkupLine("[red]Input folder does not exist or is empty...[/]");
        return -1;
    }

    var subFolders = directoryInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
    
    AnsiConsole.MarkupLine("[green]Caching data...[/]");
    using var context = new FunctionSignatureContext();
    var data = await context.FunctionSignatures.AsNoTracking().ToListAsync();
    AnsiConsole.MarkupLine("[green]Caching data completed...[/]");
    
    foreach (var subFolder in subFolders)
    {
        var files = Directory.GetFiles(subFolder.FullName, "*.*", SearchOption.AllDirectories)
            .Where(x => x.EndsWith(".js") || x.EndsWith(".mjs"))
            .Where(x => !x.Contains(".min.") && !x.Contains(".prod."));

        await AnalyzeFiles(files.ToArray(), (float)requiredSimilarity, data);
    }
    
    AnsiConsole.MarkupLine("[green]Processing completed.[/]");
    
    return 0;
});

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

await app.RunAsync();
return 0;

async Task ProcessPackage(string libName, string ns, DirectoryInfo[] versionDirs, ProgressTask task)
{
    task.MaxValue = versionDirs.Length;

    await Parallel.ForEachAsync(versionDirs, async (versionDir, cancellationToken) =>
    {
        await ProcessLib(libName, ns, versionDir.Name, versionDir.FullName);
        task.Increment(1);
    });
}

async Task ProcessLib(string libName, string ns, string version, string versionDir)
{
    // Process the files in the version directory
    var files = Directory.GetFiles(versionDir, "*.*", SearchOption.AllDirectories)
        .Where(x => x.EndsWith(".js") || x.EndsWith(".mjs"))
        .Where(x => !x.Contains(".min.") && !x.Contains(".prod."));

    var libSignatures = new List<FunctionSignature>();
    
    foreach (var file in files)
    {
        try
        {
            var featureExtractor = new FeatureExtractor();
            var code = await File.ReadAllTextAsync(file);
            var features = featureExtractor.ExtractFeatures(code, IsModule(file, code));
            features.Namespace = ns;
            features.FileName = libName;
            features.Version = version;
            var signatures = await ProcessExtractedFeatures(features);
            libSignatures.AddRange(signatures);
        }
        catch (Exception ex)
        {
            //AnsiConsole.MarkupLine($"[red]Error handling file {file}[/]");
            //AnsiConsole.MarkupLine($"[green]{ex.Message}[/]");
        }
    }

    await using var context = new FunctionSignatureContext();
    try
    {
        var transaction = await context.Database.BeginTransactionAsync();
        await context.FunctionSignatures.AddRangeAsync(libSignatures);
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]{ex.InnerException}[/]");
    }
    //File.WriteAllText(Path.Combine(versionDir, "result.BEEBBOOB"), JsonSerializer.Serialize(libSignatures));
}

async Task<IEnumerable<FunctionSignature>> ProcessExtractedFeatures(FeatureExtraction featureExtraction)
{

    var result = new ConcurrentBag<FunctionSignature>();
    var simHashGenerator = new SimHashGenerator(64, JavascriptHelper.DefaultWeights);

    await Parallel.ForEachAsync(featureExtraction.Functions.Where(x => x.ExtractedFeatures.Count > 200), (function, token) =>
    {
        var functionSignature = new FunctionSignature
        {
            Namespace = featureExtraction.Namespace,
            Version = featureExtraction.Version,
            LibName = featureExtraction.FileName,
            FunctionName = function.FunctionName,
            SignatureSimhash = simHashGenerator.GenerateSimHash(function.ExtractedFeatures),
            SignatureMinhash = MinHashGenerator.GenerateMinHash(function.ExtractedFeatures, 64).GetByteHash()
        };
        result.Add(functionSignature);
        return ValueTask.CompletedTask;
    });
    return result.AsEnumerable();
}

static bool IsModule(string filePath, string code)
{
    // Check for common module file extensions
    if (filePath.EndsWith(".mjs") || filePath.EndsWith(".esm.js"))
    {
        return true;
    }

    // Check for the presence of import or export statements
    if (code.Contains("import") || code.Contains("export"))
    {
        // Exclude cases where import or export are used as variable or function names
        if (Regex.IsMatch(code, @"\b(var|let|const)\s+import\b", RegexOptions.Compiled) ||
            Regex.IsMatch(code, @"\b(var|let|const)\s+export\b", RegexOptions.Compiled) ||
            Regex.IsMatch(code, @"\bfunction\s+import\b", RegexOptions.Compiled) ||
            Regex.IsMatch(code, @"\bfunction\s+export\b", RegexOptions.Compiled))
        {
            return false;
        }

        // Exclude cases where import or export are used inside comments or strings
        var commentRegex = new Regex(@"(/\*[\s\S]*?\*/|//.*$)", RegexOptions.Multiline | RegexOptions.Compiled);
        var stringRegex = new Regex("\"(?:\\\\\"|[^\"])*\"|'(?:\\\\'|[^'])*'", RegexOptions.Compiled);
        var codeWithoutCommentsAndStrings = commentRegex.Replace(code, "");
        codeWithoutCommentsAndStrings = stringRegex.Replace(codeWithoutCommentsAndStrings, "");

        if (codeWithoutCommentsAndStrings.Contains("import") ||
            codeWithoutCommentsAndStrings.Contains("export"))
        {
            return true;
        }
    }

    // Check for the presence of require or module.exports
    if (code.Contains("require") || code.Contains("module.exports"))
    {
        // Exclude cases where require or module.exports are used as variable or function names
        if (Regex.IsMatch(code, @"\b(var|let|const)\s+require\b", RegexOptions.Compiled) ||
            Regex.IsMatch(code, @"\b(var|let|const)\s+module\.exports\b", RegexOptions.Compiled) ||
            Regex.IsMatch(code, @"\bfunction\s+require\b", RegexOptions.Compiled) ||
            Regex.IsMatch(code, @"\bfunction\s+module\.exports\b", RegexOptions.Compiled))
        {
            return false;
        }

        // Exclude cases where require or module.exports are used inside comments or strings
        var commentRegex = new Regex(@"(/\*[\s\S]*?\*/|//.*$)", RegexOptions.Multiline | RegexOptions.Compiled);
        var stringRegex = new Regex("\"(?:\\\\\"|[^\"])*\"|'(?:\\\\'|[^'])*'", RegexOptions.Compiled);
        var codeWithoutCommentsAndStrings = commentRegex.Replace(code, "");
        codeWithoutCommentsAndStrings = stringRegex.Replace(codeWithoutCommentsAndStrings, "");

        if (codeWithoutCommentsAndStrings.Contains("require") ||
            codeWithoutCommentsAndStrings.Contains("module.exports"))
        {
            return true;
        }
    }

    return false;
}

async Task AnalyzeFiles(string[] files, float minSimilarity, List<FunctionSignature> data)
{
    var allExtractedFeatures = new Dictionary<string, List<(string Version, double Similarity)>>();
    
    foreach (var file in files)
    {
        var featureExtractor = new FeatureExtractor();
        var code = await File.ReadAllTextAsync(file);
        var features = featureExtractor.ExtractFeatures(code, IsModule(file, code));
        
        foreach (var feature in features.Functions)
        {
            var simHashGenerator = new SimHashGenerator(64, JavascriptHelper.DefaultWeights);
            var simHash = simHashGenerator.GenerateSimHash(feature.ExtractedFeatures);
            
            var similarSimHashes = 
                data.AsParallel().AsOrdered().Where(x => SimHashGenerator.GetSimilarity(x.SignatureSimhash, simHash) > minSimilarity).ToList();
            
            foreach (var functionSignature in similarSimHashes)
            {
                var similarity = SimHashGenerator.GetSimilarity(functionSignature.SignatureSimhash, simHash);
                var libName = functionSignature.LibName;
                var version = functionSignature.Version;

                if (!allExtractedFeatures.ContainsKey(libName))
                {
                    allExtractedFeatures[libName] = new List<(string Version, double Similarity)>();
                }

                allExtractedFeatures[libName].Add((version, similarity));
            }
        }
    }

    var mostLikelyVersions = new Dictionary<string, (string Version, double Similarity, int Occurrences)>();

    foreach (var (libName, versions) in allExtractedFeatures)
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

    AnsiConsole.MarkupLine($"[yellow]Most Likely Versions across all files and functions (with at least 5 occurrences):[/]");
    foreach (var kvp in mostLikelyVersions)
    {
        AnsiConsole.MarkupLine($"Library: {kvp.Key}, Version: {kvp.Value.Version}, Similarity: {kvp.Value.Similarity}, Occurrences: {kvp.Value.Occurrences}");
    }
}