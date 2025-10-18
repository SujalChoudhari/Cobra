using Antlr4.Runtime.Tree;
using Cobra.Environment;
using System.Runtime.InteropServices;

namespace Cobra.Interpreter
{
    public partial class CobraInterpreter : CobraBaseVisitor<object?>
    {
        private CobraEnvironment _currentEnvironment = CobraEnvironment.CreateGlobalEnvironment();
        private readonly Stack<string> _sourceFileStack = new();
        private readonly HashSet<string> _alreadyImported = new();

        private readonly Dictionary<string, IntPtr> _loadedLibraries = new();
        private string? _currentLinkingLibraryPath;

        private class CobraLValue(object? container, object? key)
        {
            public object? Container { get; } = container;
            public object? Key { get; } = key;

            public void Set(object? value)
            {
                switch (Container)
                {
                    case CobraNamespace ns when Key is string name:
                        ns.Environment.AssignVariable(name, value);
                        break;
                    case List<object?> list when Key is long or int:
                        list[Convert.ToInt32(Key)] = value;
                        break;
                    case Dictionary<string, object?> dict when Key is string key:
                        dict[key] = value;
                        break;
                    default:
                        throw new InvalidOperationException("Invalid LValue target for setting value.");
                }
            }
        }

        public object? Interpret(IParseTree tree, string? sourcePath)
        {
            string? fullPath = null;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                fullPath = Path.GetFullPath(sourcePath);
                _sourceFileStack.Push(fullPath);
                _alreadyImported.Add(fullPath);
            }

            try
            {
                return Visit(tree);
            }
            finally
            {
                if (fullPath != null)
                {
                    _sourceFileStack.Pop();
                }
            }
        }

        public override object? VisitProgram(CobraParser.ProgramContext context)
        {
            foreach (var statement in context.children)
            {
                var result = Visit(statement);
                if (result is CobraThrowValue)
                {
                    // An uncaught exception reached the top level. Stop everything.
                    return result;
                }
            }

            return null;
        }
    }
}