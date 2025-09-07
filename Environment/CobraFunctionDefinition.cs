namespace Cobra.Environment;

public abstract class CobraFunctionDefinition(
    string name,
    CobraRuntimeTypes returnType,
    List<(CobraRuntimeTypes Type, string Name)> parameters,
    CobraParser.BlockContext body,
    CobraEnvironment closure)
{
    public string Name { get; } = name;
    public CobraRuntimeTypes ReturnType { get; } = returnType;
    public List<(CobraRuntimeTypes Type, string Name)> Parameters { get; } = parameters;
    public CobraParser.BlockContext Body { get; } = body;
    public CobraEnvironment Closure { get; } = closure;
}