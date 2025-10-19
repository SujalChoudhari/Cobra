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

        // Pre-load standard library
        LoadStdLib(interpreter, sourcePath);

        var inputStream = new AntlrInputStream(code);
        var lexer = new CobraLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new CobraParser(tokenStream);

        var tree = parser.program();

        var finalResult = interpreter.Interpret(tree, sourcePath);

        if (finalResult is CobraThrowValue throwValue)
        {
            CobraErrorHandler.PrintException(throwValue, sourcePath);
            // Propagate error exit code
            throw new CobraRuntimeException("Script terminated with an unhandled exception.");
        }
    }
    
    private void LoadStdLib(CobraInterpreter interpreter, string? callingScriptPath)
    {
        var assemblyLocation = AppDomain.CurrentDomain.BaseDirectory;
        var stdlibIncludeDir = Path.GetFullPath(Path.Combine(assemblyLocation, CobraConstants.StdlibDirectory));

        if (!Directory.Exists(stdlibIncludeDir))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: Standard library not found at '{stdlibIncludeDir}'. Skipping stdlib load.");
            Console.ResetColor();
            return;
        }

        var exceptionClassFile = Path.Combine(stdlibIncludeDir, "System", "Exception.cb");
        
        if (File.Exists(exceptionClassFile))
        {
            try
            {
                var code = File.ReadAllText(exceptionClassFile);
                var inputStream = new AntlrInputStream(code);
                var lexer = new CobraLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new CobraParser(tokenStream);
                var tree = parser.program();
                interpreter.Interpret(tree, exceptionClassFile);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: Failed to load standard library Exception class. Reason: {ex.Message}");
                Console.ResetColor();
            }
        }
    }


    public void StartRepl()
    {
        var globalEnvironment = CobraEnvironment.CreateGlobalEnvironment(Array.Empty<string>());
        var interpreter = new CobraInterpreter(globalEnvironment);
        
        LoadStdLib(interpreter, null);

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
                var result = interpreter.Interpret(tree, "REPL");

                if (result is CobraThrowValue tv)
                {
                    CobraErrorHandler.PrintException(tv, "REPL");
                }
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