using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infrastructure.Parser;
using Infrastructure.SignatureGeneration;
using Spectre.Console;

//AnsiConsole.WriteLine("Provide file path: ");
//var filePath = Console.ReadLine();

var jsQuery = "D:\\uni\\projektgruppe\\Ausarbeitung\\jquery.js";


var featureExtractor = new JavascriptFeatureExtractor();
var watch = new Stopwatch();
watch.Start();
var stuff = featureExtractor.ExtractFeatures(File.ReadAllText(jsQuery), jsQuery, false);
watch.Stop();
Console.WriteLine("Time: {0}", watch.Elapsed);
//stuff = stuff.Where(x => x.ExtractedFeatures.Count).ToList();


var previousSimHash = new ulong[512 / sizeof(ulong)];
var defaultSimHash = new ulong[512 / sizeof(ulong)];

var previousMinHash = new int[256];
var defaultMinHash = new int[256];
foreach (var element in stuff.Where(x => x.ExtractedFeatures.Count > 150))
{
    var simHash = SimHash.ComputeSimHash(element.ExtractedFeatures, Weights.DefaultWeights);
    Console.WriteLine("Function: {0}\nSimHash: {1}\nSimilarity previous: {2}\n Similarity default: {3}",
        element.FunctionName,
        ConvertUlongToHex(simHash),
        SimHash.SimilarityPercentage(previousSimHash, simHash),
        SimHash.SimilarityPercentage(defaultSimHash, simHash));

    var minHash = MinHash.ComputeMinHash(element.ExtractedFeatures.Select(x => x.data).ToList());
    Console.WriteLine("-----------\nMinHash: {0}\nSimilarity previous: {1}\n Similarity default: {2}",
        ConvertIntToHex(minHash),
        MinHash.GetSimilarity(previousMinHash, minHash),
        MinHash.GetSimilarity(defaultMinHash, minHash));
    
    Console.WriteLine("");
    previousSimHash = simHash;
    previousMinHash = minHash;
    //Console.WriteLine(".................\n{0}\n{1}\n{2}\n", element.FunctionName, element.ArgumentCount,
    //    string.Join(";", element.ExtractedFeatures.Select(x => x.data)));
}

string ConvertUlongToHex (ulong[] values)
{
    StringBuilder sb = new StringBuilder();

    foreach(ulong value in values)
    {
        // Converts the ulong value to hex and appends it to the StringBuilder
        sb.Append(value.ToString("X"));
    }

    return sb.ToString();
}

string ConvertIntToHex (int[] values)
{
    StringBuilder sb = new StringBuilder();

    foreach(int value in values)
    {
        // Converts the ulong value to hex and appends it to the StringBuilder
        sb.Append(value.ToString("X"));
    }

    return sb.ToString();
}