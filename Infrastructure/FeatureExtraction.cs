namespace Infrastructure;

public class Function
{
    public string FunctionName { get; set; }
    public ushort ArgumentCount { get; set; }
    public List<(string valueType, string value)> FixArgumentValues { get; set; } = new();
    public List<(ExtractedFeatureType featureType, string data)> ExtractedFeatures { get; set; } = new();
            
}