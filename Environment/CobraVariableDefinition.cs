namespace Cobra.Environment;

public class CobraVariableDefinition(
    string name,
    Type type,
    object? value = null,
    bool isConst = false,
    bool isArray = false)
{
    public string Name { get; } = name;
    public Type Type { get; } = type;
    public object? Value { get; set; } = value;
    public bool IsConst { get; } = isConst;
    public bool IsArray { get; } = isArray;

    public bool IsFunction => Value is CobraFunctionDefinition;
}