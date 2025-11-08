using System.Collections.Generic;
using System.Linq;

namespace Cobra.Environment
{
    public class CobraClass
    {
        public string Name { get; }
        public List<CobraUserDefinedFunction> Constructors { get; }
        public CobraUserDefinedFunction? Destructor { get; }
        public Dictionary<string, CobraFunctionDefinition> Methods { get; }
        public Dictionary<string, (object? InitialValue, bool IsPublic)> Fields { get; }
        public CobraEnvironment StaticEnvironment { get; }

        public CobraClass(
            string name,
            List<CobraUserDefinedFunction> constructors,
            CobraUserDefinedFunction? destructor,
            Dictionary<string, CobraFunctionDefinition> methods,
            Dictionary<string, (object?, bool)> fields,
            CobraEnvironment staticEnvironment)
        {
            Name = name;
            Constructors = constructors;
            Destructor = destructor;
            Methods = methods;
            Fields = fields;
            StaticEnvironment = staticEnvironment;
        }

        public CobraUserDefinedFunction? GetConstructor(int argCount)
        {
            return Constructors.FirstOrDefault(c => c.Parameters.Count == argCount);
        }

        public override string ToString() => $"<class {Name}>";
    }
}
