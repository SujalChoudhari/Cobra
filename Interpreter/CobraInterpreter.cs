namespace Cobra.Interpreter;

using Antlr4.Runtime.Misc;
using System;

public class CobraInterpreter : CobraParserBaseListener
{
    // A simple example of handling a function declaration
    public override void EnterFunctionDeclaration([NotNull] CobraParser.FunctionDeclarationContext context)
    {
        // Get the name of the function
        string functionName = context.ID().GetText();
        Console.WriteLine($"Found a function named: {functionName}");
    }

    // A simple example of handling an if statement
    public override void EnterIfStatement([NotNull] CobraParser.IfStatementContext context)
    {
        Console.WriteLine("Found an 'if' statement.");
    }
}