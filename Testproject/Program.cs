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

var generator = new SimHashGenerator(JavascriptHelper.DefaultWeights, 10, 3, 50);
var lastHash = new byte[64];
var lastHash2 = new byte[64];
foreach (var export in exported.Functions)
{
    if (export.ExtractedFeatures.Count < 100) continue;
    var hash = generator.Generate(export.ExtractedFeatures);
    var similarity = SimHashGenerator.CalculateSimilarity(hash, lastHash);
    lastHash = hash;
    AnsiConsole.WriteLine(
        $"Function: {export.FunctionName} Hash:{BitConverter.ToString(hash).Replace("-", "")}, Similarity to last hash: {similarity}");
}

AnsiConsole.WriteLine("[yellow] Alternate simhash [/yellow]");
foreach (var export in exported.Functions)
{
    var simhash = new SimHash(512 / 8, JavascriptHelper.DefaultWeights);
    var hash = simhash.GenerateSimHash(export.ExtractedFeatures);
    var similarity = SimHash.GetSimilarity(hash, lastHash2);
    lastHash2 = hash;
    AnsiConsole.WriteLine($"Function: {export.FunctionName} Hash: {BitConverter.ToString(hash).Replace("-","")} Similarity: {similarity}");
}


MinHashBoxed lastMinhash = null;
var minhashGenerator = new MinhashGenerator();
foreach (var export in exported.Functions)
{
    if (export.ExtractedFeatures.Count < 100) continue;

    var minhash = new MinHashBoxed(minhashGenerator.GetHash(export.ExtractedFeatures));
    if (lastMinhash != null)
        AnsiConsole.WriteLine($"Function: {export.FunctionName} Similarity to last hash: {minhash.GetDistance(lastMinhash)}");
    lastMinhash = minhash;
}
Console.WriteLine();
