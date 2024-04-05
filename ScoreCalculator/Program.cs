using System.Text.Json;
using System.Text.Json.Serialization;
using Cocona;
using Spectre.Console;

AnsiConsole.WriteLine("Starting up");
var app = CoconaApp.Create();
/* This part of the handle calculates the F1 Score for the result provided by the PackageAnalyzer 
 */
app.AddCommand("extract", async (string inputFolder, string resultsFile) =>
{
    var text = File.ReadLinesAsync(resultsFile);
    string currentMode = "Minhash";
    Result? currentLib = null;
    var resultSet = new Dictionary<string, Result?>();
    var truePositives = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
    var falsePositives = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
    var falseNegatives = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();

    foreach (string category in new[] { "detection", "major_version", "minor_version" })
    {
        truePositives[category] = new Dictionary<string, Dictionary<string, int>>
        {
            { "minhash", new Dictionary<string, int> { { "minified", 0 }, { "unminified", 0 } } },
            { "simhash", new Dictionary<string, int> { { "minified", 0 }, { "unminified", 0 } } }
        };

        falsePositives[category] = new Dictionary<string, Dictionary<string, int>>
        {
            { "minhash", new Dictionary<string, int> { { "minified", 0 }, { "unminified", 0 } } },
            { "simhash", new Dictionary<string, int> { { "minified", 0 }, { "unminified", 0 } } }
        };

        falseNegatives[category] = new Dictionary<string, Dictionary<string, int>>
        {
            { "minhash", new Dictionary<string, int> { { "minified", 0 }, { "unminified", 0 } } },
            { "simhash", new Dictionary<string, int> { { "minified", 0 }, { "unminified", 0 } } }
        };
    }

    await foreach (var line in text)
    {
        if (line.StartsWith("====="))
        {
            if (currentLib != null)
                resultSet.Add(currentLib.FolderName, currentLib);

            var folderName = line.Substring(line.LastIndexOf('/') +1, 36);
            currentLib = new Result() { FolderName = folderName };
            continue;
        }

        if (line.StartsWith("~~~~~ MINHASH ~~~~~~~"))
        {
            currentMode = "Minhash";
            continue;
        }

        if (line.StartsWith("~~~~~ SIMHASH ~~~~~~~"))
        {
            currentMode = "Simhash";
            continue;
        }

        if (line.StartsWith("NS"))
        {
            var splitted = line.Split(";");
            var namspaceName = splitted[1].Trim();
            var libName = splitted[3].Trim();
            var version = splitted[5].Trim();

            var finalName = (string.IsNullOrWhiteSpace(namspaceName)) ? libName : (namspaceName + libName);

            if (currentMode == "Simhash")
                currentLib!.FoundSimhashes.Add((finalName, version));
            else
                currentLib!.FoudMinhashes.Add((finalName, version));
        }
    }
    resultSet.Add(currentLib.FolderName, currentLib);

    var folders = new DirectoryInfo(inputFolder).GetDirectories();
    foreach (var folder in folders)
    {
        var jsonFile = folder.GetFiles("*.json").First();
        var root = JsonSerializer.Deserialize<Root>(File.ReadAllText(jsonFile.FullName));

        var result = resultSet[folder.Name];
        foreach (var (libName, version) in result.FoudMinhashes)
        {
            var attemptMatch = root.Libraries.FirstOrDefault(x => x.Name == libName);
            if (attemptMatch == null)
            {
                falsePositives["detection"]["minhash"][root.Minify ? "minified" : "unminified"]++;
                falsePositives["major_version"]["minhash"][root.Minify ? "minified" : "unminified"]++;
                falsePositives["minor_version"]["minhash"][root.Minify ? "minified" : "unminified"]++;
            }
            else
            {
                truePositives["detection"]["minhash"][root.Minify ? "minified" : "unminified"]++;

                if (version.Split(".")[0] == attemptMatch.Version.Split(".")[0])
                    truePositives["major_version"]["minhash"][root.Minify ? "minified" : "unminified"]++;
                else
                    falsePositives["major_version"]["minhash"][root.Minify ? "minified" : "unminified"]++;

                if (version == attemptMatch.Version)
                    truePositives["minor_version"]["minhash"][root.Minify ? "minified" : "unminified"]++;
                else
                    falsePositives["minor_version"]["minhash"][root.Minify ? "minified" : "unminified"]++;
            }
        }

        foreach (var (libName, version) in result.FoundSimhashes)
        {
            var attemptMatch = root.Libraries.FirstOrDefault(x => x.Name == libName);
            if (attemptMatch == null)
            {
                falsePositives["detection"]["simhash"][root.Minify ? "minified" : "unminified"]++;
                falsePositives["major_version"]["simhash"][root.Minify ? "minified" : "unminified"]++;
                falsePositives["minor_version"]["simhash"][root.Minify ? "minified" : "unminified"]++;
            }
            else
            {
                truePositives["detection"]["simhash"][root.Minify ? "minified" : "unminified"]++;

                if (version.Split(".")[0] == attemptMatch.Version.Split(".")[0])
                    truePositives["major_version"]["simhash"][root.Minify ? "minified" : "unminified"]++;
                else
                    falsePositives["major_version"]["simhash"][root.Minify ? "minified" : "unminified"]++;

                if (version == attemptMatch.Version)
                    truePositives["minor_version"]["simhash"][root.Minify ? "minified" : "unminified"]++;
                else
                    falsePositives["minor_version"]["simhash"][root.Minify ? "minified" : "unminified"]++;
            }
        }

        foreach (var lib in root.Libraries)
        {
            if (result.FoudMinhashes.All(x => x.Item1 != lib.Name))
            {
                falseNegatives["detection"]["minhash"][root.Minify ? "minified" : "unminified"]++;
                falseNegatives["major_version"]["minhash"][root.Minify ? "minified" : "unminified"]++;
                falseNegatives["minor_version"]["minhash"][root.Minify ? "minified" : "unminified"]++;
            }

            if (result.FoundSimhashes.All(x => x.Item1 != lib.Name))
            {
                falseNegatives["detection"]["simhash"][root.Minify ? "minified" : "unminified"]++;
                falseNegatives["major_version"]["simhash"][root.Minify ? "minified" : "unminified"]++;
                falseNegatives["minor_version"]["simhash"][root.Minify ? "minified" : "unminified"]++;
            }
        }
    }

    var metrics = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, double>>>>();
    foreach (string category in new[] { "detection", "major_version", "minor_version" })
    {
        metrics[category] = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();
        foreach (string algorithm in new[] { "minhash", "simhash" })
        {
            metrics[category][algorithm] = new Dictionary<string, Dictionary<string, double>>();
            foreach (string minified in new[] { "minified", "unminified" })
            {
                var totalPositives = truePositives[category][algorithm][minified] + falsePositives[category][algorithm][minified];
                var totalRelevant = truePositives[category][algorithm][minified] + falseNegatives[category][algorithm][minified];

                var precision = totalPositives > 0 ? (double)truePositives[category][algorithm][minified] / totalPositives : 0;
                var recall = totalRelevant > 0 ? (double)truePositives[category][algorithm][minified] / totalRelevant : 0;
                var f1Score = (precision + recall) > 0 ? 2 * (precision * recall) / (precision + recall) : 0;

                metrics[category][algorithm][minified] = new Dictionary<string, double>
                {
                    { "precision", precision },
                    { "recall", recall },
                    { "f1_score", f1Score }
                };
            }
        }
    }

    Console.WriteLine("{0,-20} {1,-10} {2,-10} {3,-10} {4,-10} {5,-10}", "Category", "Algorithm", "Minified",
        "Precision", "Recall", "F1 Score");
    foreach (string category in new[] { "detection", "major_version", "minor_version" })
    {
        foreach (string algorithm in new[] { "minhash", "simhash" })
        {
            foreach (string minified in new[] { "minified", "unminified" })
            {
                Console.WriteLine("{0,-20} {1,-10} {2,-10} {3,-10:F4} {4,-10:F4} {5,-10:F4}",
                    category,
                    algorithm,
                    minified,
                    metrics[category][algorithm][minified]["precision"],
                    metrics[category][algorithm][minified]["recall"],
                    metrics[category][algorithm][minified]["f1_score"]);
            }
        }
    }
});

await app.RunAsync();
return 0;

public class Result
{
    public string FolderName;
    public List<(string, string)> FoudMinhashes = new();
    public List<(string, string)> FoundSimhashes = new();
}

// Root myDeserializedClass = JsonSerializer.Deserialize<Root>(myJsonResponse);
public class Library
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; }
}

public class Root
{
    [JsonPropertyName("libraries")]
    public List<Library> Libraries { get; set; }

    [JsonPropertyName("minify")]
    public bool Minify { get; set; }

    [JsonPropertyName("bundler")]
    public string Bundler { get; set; }
}



