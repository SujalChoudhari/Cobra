using Antlr4.Runtime;
using Cobra.Environment;
using Cobra.Interpreter;

namespace Cobra.Utils;

public class CobraRunner
{
    public void Run(string code, string? sourcePath = null, string[]? scriptArgs = null)
    {
        scriptArgs ??= Array.Empty<string>();
        var globalEnvironment = CobraEnvironment.CreateGlobalEnvironment(scriptArgs);
        var interpreter = new CobraInterpreter(globalEnvironment);
        
        var inputStream = new AntlrInputStream(code);
        var lexer = new CobraLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new CobraParser(tokenStream);

        var tree = parser.program();

        var finalResult = interpreter.Interpret(tree, sourcePath);

        if (finalResult is CobraThrowValue throwValue)
        {
            throw new CobraRuntimeException($"Unhandled Exception: {throwValue.ThrownObject}");
        }
    }

    public void StartRepl()
    {
        // For REPL, args are always empty
        var globalEnvironment = CobraEnvironment.CreateGlobalEnvironment(Array.Empty<string>());
        var interpreter = new CobraInterpreter(globalEnvironment);

        while (true)
        {
            Console.Write("> ");
            var code = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(code))
                continue;
            if (code.Trim().ToLower() == "exit")
                break;

            try
            {
                var inputStream = new AntlrInputStream(code);
                var lexer = new CobraLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new CobraParser(tokenStream);
                var tree = parser.program();
                interpreter.Interpret(tree, null);
            }
            catch (Exception ex)
            {
                var previousColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ForegroundColor = previousColor;
            }
        }
    }
}