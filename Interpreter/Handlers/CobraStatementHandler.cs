namespace Cobra.Interpreter;

public partial class CobraInterpreter
{
    public override object? VisitExpressionStatement(CobraParser.ExpressionStatementContext context)
    {
        return Visit(context.assignmentExpression());
    }

    public override object? VisitIfStatement(CobraParser.IfStatementContext context)
    {
        var condition = Visit(context.assignmentExpression());
        if (CobraLiteralHelper.IsTruthy(condition))
        {
            return Visit(context.statement(0));
        }

        if (context.statement().Length > 1)
        {
            return Visit(context.statement(1));
        }

        return null;
    }

    public override object? VisitWhileStatement(CobraParser.WhileStatementContext context)
    {
        while (CobraLiteralHelper.IsTruthy(Visit(context.assignmentExpression())))
        {
            var result = Visit(context.statement());
            if (result is CobraReturnValue) return result;
            if (result is CobraBreakValue) break;
        }

        return null;
    }

    public override object? VisitForStatement(CobraParser.ForStatementContext context)
    {
        var previous = _currentEnvironment;
        _currentEnvironment = _currentEnvironment.CreateChild();
        try
        {
            if (context.varDeclaration() != null) Visit(context.varDeclaration());
            else if (context.expressionStatement() != null) Visit(context.expressionStatement());
            while (context.assignmentExpression(0) == null ||
                   CobraLiteralHelper.IsTruthy(Visit(context.assignmentExpression(0))))
            {
                var result = Visit(context.statement());
                if (result is CobraReturnValue) return result;
                if (result is CobraBreakValue) break;

                if (context.assignmentExpression(1) != null) Visit(context.assignmentExpression(1));
            }
        }
        finally
        {
            _currentEnvironment = previous;
        }

        return null;
    }

    public override object? VisitJumpStatement(CobraParser.JumpStatementContext context)
    {
        if (context.RETURN() != null)
        {
            return new CobraReturnValue(context.assignmentExpression() != null
                ? Visit(context.assignmentExpression())
                : null);
        }

        if (context.BREAK() != null) return CobraBreakValue.Instance;
        if (context.CONTINUE() != null) return CobraContinueValue.Instance;
        return null;
    }
}