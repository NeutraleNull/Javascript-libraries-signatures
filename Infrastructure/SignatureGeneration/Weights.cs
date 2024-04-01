namespace Infrastructure.SignatureGeneration;

public static class Weights
{
    /// <summary>
    /// Default set of weights to be used for the SimHash Generator.
    /// CodeStructure is set low because this content doesn't matter for SimHash because the SimHash in our form
    /// does not care about the order of strings, but it is helpful for the longer MinHash that takes the order into account 
    /// </summary>
    public static readonly Dictionary<ExtractedFeatureType, double> DefaultWeights = new()
    {
        { ExtractedFeatureType.Syntax, 1 },
        { ExtractedFeatureType.Async, 1.5 },
        { ExtractedFeatureType.Literals, 1.5 },
        { ExtractedFeatureType.HostEnvironmentObject, 1 },
        { ExtractedFeatureType.ControlFlow, 2 },
        { ExtractedFeatureType.Strings, 3 },
        { ExtractedFeatureType.ECMAObject, 3 },
        { ExtractedFeatureType.VariableName, 0.5 },
        { ExtractedFeatureType.CodeStructure, 0.1 },
    };
}