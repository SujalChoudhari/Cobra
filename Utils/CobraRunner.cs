using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Cobra.Interpreter;

namespace Cobra.Utils;

public class CobraRunner
{
    public static void Run(string code)
    {
        var inputStream = new AntlrInputStream(code);
        var lexer = new CobraLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new CobraParser(tokenStream);

        var tree = parser.program();

        var parseTreeWalker = new ParseTreeWalker();
        var listener = new CobraInterpreter();
        parseTreeWalker.Walk(listener, tree);
    }
}