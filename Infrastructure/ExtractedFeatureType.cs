namespace Infrastructure;

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
    HostEnvironmentObject,
    FunctionName,
    FunctionArgumentCount,
    FunctionArgumentLiterals,
    ClassDeclarationName,
    CodeStructure
}