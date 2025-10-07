namespace Cobra.Environment
{
    public class CobraExternalFunction(
        string name,
        CobraRuntimeTypes returnType,
        List<(CobraRuntimeTypes Type, string Name)> parameters,
        string libraryPath)
        : CobraFunctionDefinition(name, returnType, parameters, null!, null!)
    {
        public string LibraryPath { get; } = libraryPath;
    }
}