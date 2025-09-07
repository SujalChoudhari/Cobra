namespace Cobra.Environment;

public class CobraVariableDefinition(
    string name,
    CobraRuntimeTypes runtimeType,
    object? value = null,
    bool isConst = false,
    bool isArray = false)
{
    public string Name { get; } = name;
    public CobraRuntimeTypes RuntimeType { get; } = runtimeType;
    public object? Value { get; set; } = value;
    public bool IsConst { get; } = isConst;
    public bool IsArray { get; } = isArray;

    public bool IsFunction => RuntimeType == CobraRuntimeTypes.Function;
    public bool IsMarkup => RuntimeType == CobraRuntimeTypes.Markup;
}