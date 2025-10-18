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
            if (result is CobraContinueValue) continue;
        }

        return null;
    }

    public override object? VisitDoWhileStatement(CobraParser.DoWhileStatementContext context)
    {
        do
        {
            var result = Visit(context.statement());
            if (result is CobraReturnValue) return result;
            if (result is CobraBreakValue) break;
            if (result is CobraContinueValue) continue;
        } while (CobraLiteralHelper.IsTruthy(Visit(context.assignmentExpression())));

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
                if (result is CobraContinueValue)
                {
                    if (context.assignmentExpression(1) != null) Visit(context.assignmentExpression(1));
                    continue;
                }

                if (context.assignmentExpression(1) != null) Visit(context.assignmentExpression(1));
            }
        }
        finally
        {
            _currentEnvironment = previous;
        }

        return null;
    }

    public override object? VisitForEachStatement(CobraParser.ForEachStatementContext context)
    {
        var collection = Visit(context.assignmentExpression());
        if (collection is not List<object?> list)
        {
            throw new Exception("foreach loop can only iterate over a list.");
        }

        foreach (var item in list)
        {
            var loopScope = _currentEnvironment.CreateChild();
            var previous = _currentEnvironment;
            _currentEnvironment = loopScope;

            try
            {
                _currentEnvironment.DefineVariable(context.ID().GetText(), item);
                var result = Visit(context.statement());

                if (result is CobraReturnValue) return result;
                if (result is CobraBreakValue) break;
                if (result is CobraContinueValue) continue;
            }
            finally
            {
                _currentEnvironment = previous;
            }
        }

        return null;
    }

    public override object? VisitSwitchStatement(CobraParser.SwitchStatementContext context)
    {
        object? switchValue = Visit(context.assignmentExpression());
        var switchBlocks = context.switchBlock();

        int? defaultBlockIndex = null;
        int startBlockIndex = -1;

        for (int i = 0; i < switchBlocks.Length; i++)
        {
            var block = switchBlocks[i];
            foreach (var label in block.switchLabel())
            {
                if (label.CASE() != null)
                {
                    var caseValue = Visit(label.assignmentExpression());
                    if (Equals(switchValue, caseValue))
                    {
                        startBlockIndex = i;
                        goto FoundStartBlock;
                    }
                }
                else if (label.DEFAULT() != null)
                {
                    defaultBlockIndex = i;
                }
            }
        }

        FoundStartBlock:
        if (startBlockIndex == -1)
        {
            if (defaultBlockIndex.HasValue)
            {
                startBlockIndex = defaultBlockIndex.Value;
            }
            else
            {
                return null; // No matching case and no default
            }
        }

        for (int i = startBlockIndex; i < switchBlocks.Length; i++)
        {
            var block = switchBlocks[i];
            foreach (var stmt in block.statement())
            {
                var result = Visit(stmt);
                if (result is CobraReturnValue) return result;
                if (result is CobraBreakValue) return null; // Exit switch
            }
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