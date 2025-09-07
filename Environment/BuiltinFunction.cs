namespace Cobra.Environment;
public class BuiltinFunction(string name, Func<List<object?>, object?> action) : CobraFunctionDefinition(name,
    CobraRuntimeTypes.Function, [], null!, null!)
{
    public readonly Func<List<object?>, object?> Action = action;
}