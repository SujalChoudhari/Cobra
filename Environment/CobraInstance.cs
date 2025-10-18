namespace Cobra.Environment
{
    public class CobraInstance(CobraClass classDefinition)
    {
        public CobraClass ClassDefinition { get; } = classDefinition;
        public CobraEnvironment Fields { get; } = new();

        public object? Get(string name)
        {
            try
            {
                return Fields.GetVariable(name);
            }
            catch (Exception)
            {
                if (ClassDefinition.Methods.TryGetValue(name, out var method))
                {
                    return method;
                }
            }

            throw new Exception($"Property '{name}' not found on instance of '{ClassDefinition.Name}'.");
        }
        
        public void Set(string name, object? value)
        {
            Fields.AssignVariable(name, value);
        }

        public override string ToString() => $"<instance of {ClassDefinition.Name}>";
    }
}