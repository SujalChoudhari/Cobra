using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Cobra.Interpreter;

namespace Cobra.Utils;

public class CobraRunner
{
    private readonly CobraInterpreter _interpreter;

    public CobraRunner()
    {
        _interpreter = new CobraInterpreter();
    }

    public void Run(string code)
    {
        var inputStream = new AntlrInputStream(code);
        var lexer = new CobraLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new CobraParser(tokenStream);

        var tree = parser.program();

        var parseTreeWalker = new ParseTreeWalker();
        parseTreeWalker.Walk(_interpreter, tree);
    }

    public void StartRepl()
    {
        Console.WriteLine("Cobra REPL started. Type 'exit' to quit.");
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
                Run(code);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}