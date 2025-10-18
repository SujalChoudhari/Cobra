namespace Cobra.Interpreter;

public partial class CobraInterpreter
{
    public override object? VisitBlock(CobraParser.BlockContext context)
    {
        var previous = _currentEnvironment;
        _currentEnvironment = _currentEnvironment.CreateChild();
        try
        {
            return ExecuteBlockStmts(context);
        }
        finally
        {
            _currentEnvironment = previous;
        }
    }

    private object? ExecuteBlockStmts(CobraParser.BlockContext context)
    {
        foreach (var statement in context.declarationStatement() ?? [])
        {
            Visit(statement);
        }

        foreach (var statement in context.statement() ?? [])
        {
            var result = Visit(statement);
            // If any statement returns a control flow breaker, stop execution and propagate it.
            if (result is CobraReturnValue or CobraBreakValue or CobraContinueValue or CobraThrowValue)
            {
                return result;
            }
        }

        return null;
    }
}