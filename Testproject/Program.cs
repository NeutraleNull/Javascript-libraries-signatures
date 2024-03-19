using System.Security.AccessControl;
using Esprima;
using Esprima.Utils;
using Spectre.Console;

using System.Windows;
using SharedCommonStuff;

AnsiConsole.WriteLine("Provide file path: ");
//var filePath = Console.ReadLine();

var code = File.ReadAllText("D:\\uni\\projektgruppe\\Ausarbeitung\\jquery.js");

JavaScriptParser parser = new JavaScriptParser();
var stuff = parser.ParseScript(code);
//Console.WriteLine(stuff.ToJsonString());

await File.WriteAllTextAsync("D:\\uni\\projektgruppe\\Ausarbeitung\\ast.json", stuff.ToJsonString());

FeatureExtractor extractor = new FeatureExtractor();
var exported = extractor.ExtractFeatures(code);

var generator = new SimHashGenerator(JavascriptHelper.DefaultWeights, 6, 1, 50);
var lastHash = new byte[64];
foreach (var export in exported.Functions)
{
    if (export.ExtractedFeatures.Count < 30) continue;
    var hash = generator.Generate(export.ExtractedFeatures);
    var similarity = SimHashGenerator.CalculateSimilarity(hash, lastHash);
    lastHash = hash;
    Console.WriteLine($"Function: {export.FunctionName} Hash:{BitConverter.ToString(hash).Replace("-", "")}, Similarity to last hash: {similarity}");
}


Console.WriteLine();
