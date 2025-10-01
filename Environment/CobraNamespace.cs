namespace Cobra.Environment
{
    public class CobraNamespace
    {
        public string Name { get; }
        public CobraEnvironment Environment { get; }

        public CobraNamespace(string name, CobraEnvironment parentScope)
        {
            Name = name;
            Environment = new CobraEnvironment(parentScope);
        }

        public override string ToString() => $"<namespace {Name}>";
    }
}