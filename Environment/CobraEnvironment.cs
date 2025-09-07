namespace Cobra.Environment;

public class CobraEnvironment(CobraEnvironment? parent = null)
{
    private readonly Dictionary<string, CobraVariableDefinition> _variables = new();

    public void DefineVariable(string name, object value, bool isConst = false, bool isArray = false)
    {
        if (_variables.ContainsKey(name))
            throw new Exception($"Variable '{name}' is already defined in this scope.");

        var runtimeType = InferRuntimeType(value);
        _variables[name] = new CobraVariableDefinition(name, runtimeType, value, isConst, isArray);
    }

    private CobraRuntimeTypes InferRuntimeType(object? value)
    {
        if (value == null) return CobraRuntimeTypes.Null;
        return value switch
        {
            int => CobraRuntimeTypes.Int,
            float or double => CobraRuntimeTypes.Float,
            bool => CobraRuntimeTypes.Bool,
            string => CobraRuntimeTypes.String,
            Dictionary<string, object> => CobraRuntimeTypes.Dict,
            List<object> => CobraRuntimeTypes.List,
            CobraFunctionDefinition => CobraRuntimeTypes.Function,
            CobraMarkup => CobraRuntimeTypes.Markup,
            _ => throw new Exception($"Unsupported runtime type: {value.GetType().Name}")
        };
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
