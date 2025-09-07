// Function definition structure
namespace Cobra.Environment;

public class CobraFunctionDefinition(
    string name,
    string returnType,
    List<(string, string)> parameters,
    CobraParser.BlockContext body,
    CobraEnvironment closure)
{
    public string Name { get; } = name;
    public string ReturnType { get; } = returnType;
    public List<(string Type, string Name)> Parameters { get; } = parameters;
    public CobraParser.BlockContext Body { get; } = body;
    public CobraEnvironment Closure { get; } = closure;
}