using Cobra.Interpreter;

namespace Cobra.Environment;

public class CobraEnvironment(CobraEnvironment? parent = null)
{
    private readonly Dictionary<string, CobraVariableDefinition> _variables = new();
    private CobraEnvironment? Parent { get; } = parent;

    public static CobraEnvironment CreateGlobalEnvironment(string[] scriptArgs)
    {
        var env = new CobraEnvironment();

        // --- Built-in Functions ---
        var printFunc = new CobraBuiltinFunction("print", (args) =>
        {
            var output = string.Join(" ", args.Select(a => a?.ToString() ?? "null"));
            Console.WriteLine(output);
            return null;
        });

        var destroyFunc = new CobraBuiltinFunction("destroy", (args) =>
        {
            if (args.Count != 1)
                throw new Exception("destroy() expects exactly one argument.");

            if (args[0] is not CobraInstance instance)
                throw new Exception("destroy() can only be called on a class instance.");

            var destructor = instance.ClassDefinition.Destructor;
            if (destructor != null)
            {
                var interpreter = new CobraInterpreter(CreateGlobalEnvironment(new string[] { }));
                interpreter.ExecuteFunctionCall(destructor, new List<object?>(), "destructor", instance);
            }

            return null;
        });

        env.DefineVariable("print", printFunc, isConst: true);
        env.DefineVariable("destroy", destroyFunc, isConst: true);

        // --- Global Variables ---
        var argsList = scriptArgs.Select(s => (object)s).ToList<object?>();
        env.DefineVariable("ARGS", argsList, isConst: true);

        return env;
    }

    public void DefineVariable(string name, object? value, bool isConst = false, bool isArray = false)
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
            int or long => CobraRuntimeTypes.Int,
            float or double => CobraRuntimeTypes.Float,
            bool => CobraRuntimeTypes.Bool,
            string => CobraRuntimeTypes.String,
            Dictionary<string, object> => CobraRuntimeTypes.Dict,
            List<object> => CobraRuntimeTypes.List,
            CobraFunctionDefinition => CobraRuntimeTypes.Function,
            CobraMarkup => CobraRuntimeTypes.Markup,
            CobraNamespace => CobraRuntimeTypes.Namespace,
            CobraHandle => CobraRuntimeTypes.Handle,
            CobraClass => CobraRuntimeTypes.Class,
            CobraInstance => CobraRuntimeTypes.Instance,
            CobraEnum => CobraRuntimeTypes.Enum,
            CobraEnumMember => CobraRuntimeTypes.EnumMember,
            _ => throw new Exception($"Unsupported runtime type: {value.GetType().Name}")
        };
    }

    public void AssignVariable(string name, object? value)
    {
        if (_variables.TryGetValue(name, out var variable))
        {
            if (variable.IsConst)
                throw new Exception($"Cannot assign to constant '{name}'.");

            variable.Value = value;
            return;
        }

        if (Parent == null) throw new Exception($"Variable '{name}' not defined.");
        Parent.AssignVariable(name, value);
    }

    public object? GetVariable(string name)
    {
        if (_variables.TryGetValue(name, out var variable))
        {
            return variable.Value;
        }

        return Parent == null ? throw new Exception($"Variable '{name}' not found.") : Parent.GetVariable(name);
    }

    public CobraVariableDefinition GetVariableDefinition(string name)
    {
        if (_variables.TryGetValue(name, out var variable))
            return variable;

        return Parent == null
            ? throw new Exception($"Variable '{name}' not found.")
            : Parent.GetVariableDefinition(name);
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