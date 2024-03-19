using System.Text.Json;

namespace SharedCommonStuff;

public class FeatureExtraction
{
    public string FileName { get; set; }
    public string Version { get; set; }
    public string Namespace { get; set; }
    public List<Function> Functions { get; set; } = new();
}

public enum ExtractedFeatureType
{
    Syntax,
    ControlFlow,
    Types,
    ECMAObject,
    Strings,
    Async,
    Literals,
    VariableName,
    HostEnvironmentObject
}

public class Function
{
    public string FunctionName { get; set; }
    public ushort ArgumentCount { get; set; }
    public List<(string valueType, string value)> FixArgumentValues { get; set; } = new();
    public bool IsAsync { get; set; }
    public List<(ExtractedFeatureType featureType, string data)> ExtractedFeatures { get; set; } = new();

    public int GetFeatureCount()
    {
        int sum = 0;
        sum += FixArgumentValues.Count;
        sum += ExtractedFeatures.Count;
        return sum;
    }

    public bool CheckEnoughFeaturesForHashing(int minFeatureCount = 20)
    {
        return GetFeatureCount() > minFeatureCount;
    }
}