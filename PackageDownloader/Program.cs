using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cocona;
using ICSharpCode.SharpZipLib.Tar;
using Infrastructure.Database;
using Spectre.Console;

AnsiConsole.WriteLine("Starting up");

var app = CoconaApp.Create();

// handles the code path for choosing the download method
app.AddCommand("download",
    async (string inputFile, string outputFolder, DateTime maxVersionAge, ushort parallelDownloads = 5,
        bool noPreRelease = false, bool extract = false) =>
{
    if (!File.Exists(inputFile))
    {
        AnsiConsole.MarkupLine("[yellow]Warning:[/] File does not exist: " + inputFile);
        return 1;
    }

    if (Directory.Exists(outputFolder))
    {
        AnsiConsole.MarkupLine("[yellow]Warning:[/] The output folder is not empty.");
    
        if (AnsiConsole.Confirm("Do you want to remove the contents of the output folder before continuing?"))
        {
            try
            {
                Directory.Delete(outputFolder, true);
                AnsiConsole.MarkupLine("[green]Output folder cleaned successfully.[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
                return -1;
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled by the user.[/]");
            return -1;
        }
    }

    var watch = new Stopwatch();
    watch.Start();
    
    try
    {
        List<DatasetEntry> packages;
        await using (var fileStream = File.OpenRead(inputFile))
        {
            await using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress, false))
            {
                packages = await ParseDatasetEntriesAsync(gzipStream);
            }
        }
        
        packages = packages.Where(x => x.MostDepended).ToList(); 
        
        await AnsiConsole
            .Progress()
            .HideCompleted(true)
            .StartAsync(async ctx =>
        {
            var downloadTask  = ctx.AddTask("[green]Downloading libs [/]");
            downloadTask.MaxValue = packages.Count;
            downloadTask.StartTask();
            
            await Parallel.ForEachAsync(packages, new ParallelOptions { MaxDegreeOfParallelism = parallelDownloads }, async (entry, token) =>
            {
                var libDownloadTask = ctx.AddTask($"Downloading library: {entry.Name}");
                libDownloadTask.StartTask();
                await DownloadLibraryVersions(entry.Name, outputFolder, maxVersionAge, libDownloadTask, noPreRelease, extract, token);
                libDownloadTask.StopTask();
                downloadTask.Increment(1);
            });
            
            downloadTask.StopTask();
        });
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
        return -1;
    }
    
    watch.Stop();
    AnsiConsole.MarkupLine($"[blue]Completed![/] Took {watch.Elapsed:G}");
    return 0;
});

// handles the code path for choosing the extract method
app.AddCommand("extract", async (string folder, ushort parallelExtractions = 50) =>
{
    AnsiConsole.WriteLine($"Start extraction. Scanning folder: {folder}");
    var files = Directory.GetFiles(folder, "*.tgz", SearchOption.AllDirectories);
    AnsiConsole.WriteLine($"Found {files.Length} archives!");
    
    await AnsiConsole
        .Progress()
        .HideCompleted(true)
        .StartAsync(async ctx =>
        {
            var downloadTask  = ctx.AddTask("[green]Unpacking libs: [/]");
            downloadTask.MaxValue = files.Length;
            downloadTask.StartTask();

            await Parallel.ForEachAsync(files, new ParallelOptions() { MaxDegreeOfParallelism = parallelExtractions }, async (file, token) =>
            {
                var fileInfo = new FileInfo(file);
                try
                {
                    await ExtractTarball(file, fileInfo.DirectoryName!);
                    AnsiConsole.MarkupLine($"Completed {fileInfo.DirectoryName}");
                }
                catch
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {fileInfo.DirectoryName}");
                }
                finally
                {
                    downloadTask.Increment(1);
                }
            });
        });
    return 0;
});

await app.RunAsync();

async Task DownloadLibraryVersions(string libraryName, string outputDir, DateTime minReleaseDate,
    ProgressTask progressTask, bool noPreRelease, bool extract,
    CancellationToken token)
{
    using var httpClient = new HttpClient();
    // Get the list of versions for the library from the npm registry
    var response = await httpClient.GetAsync($"https://registry.npmjs.org/{libraryName}", token);
    var json = await response.Content.ReadAsStringAsync(token);
    var document = JsonDocument.Parse(json);
    var versions = document.RootElement.GetProperty("versions").EnumerateObject();

    // Create the directory for the library
    var libraryDir = Path.Combine(outputDir, libraryName);
    Directory.CreateDirectory(libraryDir);
    
    var filteredVersions = versions
        .Where(v => DateTime.TryParse(document.RootElement.GetProperty("time").GetProperty(v.Name).GetString(), out var releaseDate) &&
                    releaseDate >= minReleaseDate)
        .ToList();
    
    if (noPreRelease)
    {
        filteredVersions = filteredVersions.Where(v =>
            {
                var regex = new Regex(@"^[0-9]+\.[0-9]+\.[0-9]+-.*",
                    RegexOptions.Compiled | RegexOptions.CultureInvariant);
                return !regex.IsMatch(v.Name);
            })
            .ToList();
    }
    
    var downloadedVersions = 0;

    progressTask.MaxValue = filteredVersions.Count;
        
    foreach (var version in filteredVersions)
    {
        try
        {
            var versionNumber = version.Name;
            var tarballUrl = version.Value.GetProperty("dist").GetProperty("tarball").GetString();

            // Download the tarball for the specific version
            var tarballResponse = await httpClient.GetAsync(tarballUrl, token);
            var tarballStream = await tarballResponse.Content.ReadAsStreamAsync(token);

            // Create the directory for the version
            var versionDir = Path.Combine(libraryDir, versionNumber);
            Directory.CreateDirectory(versionDir);

            // Save the tarball to the version directory
            var tarballPath = Path.Combine(versionDir, $"package.tgz");
            await using (var fileStream = new FileStream(tarballPath, FileMode.Create))
            {
                await tarballStream.CopyToAsync(fileStream, token);
            }
            
            if (extract)
                await ExtractTarball(tarballPath, versionDir);
            downloadedVersions++;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] An exception occurred while downloading package {libraryName} version {version.Name}: {ex.Message}");
        }
        finally
        {
            progressTask.Increment(1);
            progressTask.Description = $"Downloaded {libraryName} v{version.Name} ({downloadedVersions}/{filteredVersions.Count})";
        }
    }
}

async Task ExtractTarball(string tarballPath, string extractPath)
{
    await using var fileStream = File.OpenRead(tarballPath);
    await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
    using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream);
    tarArchive.ExtractContents(extractPath);
}

async Task<List<DatasetEntry>> ParseDatasetEntriesAsync(Stream jsonStream)
{
    var options = new JsonSerializerOptions
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    var entries = new List<DatasetEntry>();

    using var streamReader = new StreamReader(jsonStream);
    
    string line;
    while ((line = await streamReader.ReadLineAsync()) != null)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            var entry = JsonSerializer.Deserialize<DatasetEntry>(line, options);
            if (entry == null) continue;
            entries.Add(entry);
        }
    }
    streamReader.Close();
    return entries;
}
