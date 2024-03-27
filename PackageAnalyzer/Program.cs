using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Transactions;
using Cocona;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
            packages.Add((libName, null, directoryInfo.GetDirectories ())!);
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