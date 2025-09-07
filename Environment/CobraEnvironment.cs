namespace Cobra.Environment;

public class CobraEnvironment(CobraEnvironment? parent = null)
{
    private readonly Dictionary<string, CobraVariableDefinition> _variables = new();

    public void DefineVariable(string name, object value, bool isConst = false, bool isArray = false)
    {
        if (_variables.ContainsKey(name))
            throw new Exception($"Variable '{name}' is already defined in this scope.");

        _variables[name] = new CobraVariableDefinition(name, value?.GetType() ?? typeof(object), value, isConst, isArray);
    }

    public void AssignVariable(string name, object value)
    {
        if (_variables.TryGetValue(name, out var variable))
        {
            if (variable.IsConst)
                throw new Exception($"Cannot assign to constant '{name}'.");

            variable.Value = value;
            return;
        }

        if (parent != null)
        {
            parent.AssignVariable(name, value);
            return;
        }

        throw new Exception($"Variable '{name}' not defined.");
    }

    public object? GetVariable(string name)
    {
        if (_variables.TryGetValue(name, out var variable))
        {
            return variable.Value;
        }

        if (parent != null)
        {
            return parent.GetVariable(name);
        }

        throw new Exception($"Variable '{name}' not found.");
    }

    public CobraVariableDefinition GetVariableDefinition(string name)
    {
        if (_variables.TryGetValue(name, out var variable))
            return variable;

        if (parent != null)
            return parent.GetVariableDefinition(name);

        throw new Exception($"Variable '{name}' not found.");
    }

    public bool IsFunction(string name)
    {
        var variable = GetVariable(name);
        return variable is CobraFunctionDefinition;
    }

    public CobraEnvironment CreateChild()
    {
        return new CobraEnvironment(this);
    }
}
