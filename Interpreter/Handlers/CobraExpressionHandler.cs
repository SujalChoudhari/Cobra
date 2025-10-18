namespace Cobra.Interpreter;

public partial class CobraInterpreter
{
    public override object? VisitAssignmentExpression(CobraParser.AssignmentExpressionContext context)
    {
        if (context.leftHandSide() == null) return Visit(context.binaryExpression());
        var lhs = context.leftHandSide();
        var valueToAssign = Visit(context.assignmentExpression(0));
        var op = context.assignmentOperator().GetText();
        if (lhs.children.Count == 1 && lhs.primary()?.ID() != null)
        {
            var varName = lhs.GetText();
            if (op != "=")
            {
                var currentValue = _currentEnvironment.GetVariable(varName);
                valueToAssign = ApplyBinaryOperator(op.TrimEnd('='), currentValue, valueToAssign);
            }

            _currentEnvironment.AssignVariable(varName, valueToAssign);
            return valueToAssign;
        }

        var lvalue = EvaluateLValue(lhs);
        if (op != "=")
        {
            var currentValue = GetIndex(lvalue.Container, lvalue.Key);
            valueToAssign = ApplyBinaryOperator(op.TrimEnd('='), currentValue, valueToAssign);
        }

        lvalue.Set(valueToAssign);
        return valueToAssign;
    }

    public override object? VisitPostfixExpression(CobraParser.PostfixExpressionContext context)
    {
        object? result = Visit(context.primary());
        for (int i = 1; i < context.ChildCount;)
        {
            var opNode = context.GetChild(i);
            switch (opNode)
            {
                case Antlr4.Runtime.Tree.ITerminalNode { Symbol: { Type: CobraLexer.LPAREN } }:
                {
                    var argListCtx = context.argumentList(GetPostfixOperatorIndex(context, i));
                    var args = argListCtx?.assignmentExpression().Select(Visit).ToList() ?? new List<object?>();
                    result = ExecuteFunctionCall(result, args, context.primary().GetText());
                    i += (argListCtx != null ? 2 : 1) + 1;
                    break;
                }
                case Antlr4.Runtime.Tree.ITerminalNode { Symbol: { Type: CobraLexer.LBRACKET } }:
                {
                    var index = Visit(context.assignmentExpression(GetPostfixOperatorIndex(context, i)));
                    result = GetIndex(result, index);
                    i += 2;
                    break;
                }
                case Antlr4.Runtime.Tree.ITerminalNode { Symbol: { Type: CobraLexer.DOT } }:
                {
                    var memberName = context.ID(GetPostfixOperatorIndex(context, i)).GetText();
                    result = GetMember(result, memberName);
                    i += 2;
                    break;
                }
                case Antlr4.Runtime.Tree.ITerminalNode op when op.Symbol.Type is CobraLexer.INC or CobraLexer.DEC:
                {
                    var varName = GetLValueName(context.primary());
                    object? originalValue = result;
                    if (!CobraLiteralHelper.IsNumeric(originalValue))
                        throw new Exception("Postfix '++' and '--' can only be applied to numeric types.");
                    object newValue;
                    if (originalValue is double d) newValue = op.Symbol.Type == CobraLexer.INC ? d + 1.0 : d - 1.0;
                    else
                        newValue = op.Symbol.Type == CobraLexer.INC
                            ? Convert.ToInt64(originalValue) + 1
                            : Convert.ToInt64(originalValue) - 1;
                    _currentEnvironment.AssignVariable(varName, newValue);
                    result = originalValue;
                    i++;
                    break;
                }
                default:
                    i++;
                    break;
            }
        }

        return result;
    }

    public override object? VisitBinaryExpression(CobraParser.BinaryExpressionContext context)
    {
        if (context.ChildCount == 1) return Visit(context.GetChild(0));
        if (context.GetChild(1) is Antlr4.Runtime.Tree.ITerminalNode firstOpNode)
        {
            var firstOp = firstOpNode.GetText();
            if (firstOp is "&&" or "||")
            {
                var left = Visit(context.GetChild(0));
                if (firstOp == "&&" && !CobraLiteralHelper.IsTruthy(left)) return false;
                if (firstOp == "||" && CobraLiteralHelper.IsTruthy(left)) return true;
                return CobraLiteralHelper.IsTruthy(Visit(context.GetChild(2)));
            }
        }

        int i = 0;
        object? result = EvaluateUnaryThenPostfix(context, ref i);
        while (i < context.ChildCount)
        {
            var op = context.GetChild(i).GetText();
            i++;
            var right = EvaluateUnaryThenPostfix(context, ref i);
            result = ApplyBinaryOperator(op, result, right);
        }

        return result;
    }

    public override object? VisitPrimary(CobraParser.PrimaryContext context)
    {
        if (context.assignmentExpression() != null) return Visit(context.assignmentExpression());
        if (context.literal() != null) return Visit(context.literal());
        if (context.ID() != null) return _currentEnvironment.GetVariable(context.ID().GetText());
        if (context.arrayLiteral() != null) return Visit(context.arrayLiteral());
        if (context.dictLiteral() != null) return Visit(context.dictLiteral());
        if (context.functionExpression() != null) return Visit(context.functionExpression());
        throw new NotSupportedException("This primary form is not supported yet.");
    }

    public override object VisitFunctionExpression(CobraParser.FunctionExpressionContext context)
    {
        var parameters = context.parameterList()?.parameter()
                             .Select(p => (Environment.CobraRuntimeTypes.Void, p.ID().GetText())).ToList() ??
                         new List<(Environment.CobraRuntimeTypes, string)>();
        return new Environment.CobraUserDefinedFunction("", parameters, context.block(), _currentEnvironment);
    }
}
