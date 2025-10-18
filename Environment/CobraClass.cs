namespace Cobra.Environment
{
    public class CobraClass(
        string name,
        CobraUserDefinedFunction? constructor,
        CobraUserDefinedFunction? destructor,
        Dictionary<string, CobraFunctionDefinition> methods,
        Dictionary<string, (object?, bool)> fields,
        CobraEnvironment staticEnvironment)
    {
        public string Name { get; } = name;
        public CobraUserDefinedFunction? Constructor { get; } = constructor;
        public CobraUserDefinedFunction? Destructor { get; } = destructor;
        public Dictionary<string, CobraFunctionDefinition> Methods { get; } = methods;
        public Dictionary<string, (object? InitialValue, bool IsPublic)> Fields { get; } = fields;
        public CobraEnvironment StaticEnvironment { get; } = staticEnvironment;

        public override string ToString() => $"<class {Name}>";
    }
}