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
            if (result is CobraReturnValue or CobraBreakValue or CobraContinueValue)
            {
                return result;
            }
        }

        return null;
    }
}