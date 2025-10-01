namespace Cobra.Environment;

public class CobraUserDefinedFunction(
    string name,
    List<(CobraRuntimeTypes Type, string Name)> parameters,
    CobraParser.BlockContext body,
    CobraEnvironment closure)
    : CobraFunctionDefinition(name, CobraRuntimeTypes.Void, parameters, body, closure);