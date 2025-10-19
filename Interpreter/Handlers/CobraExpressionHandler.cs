using Cobra.Environment;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace Cobra.Interpreter;

public partial class CobraInterpreter
{
    public override object? VisitAssignmentExpression(CobraParser.AssignmentExpressionContext context)
    {
        if (context.leftHandSide() == null) return Visit(context.binaryExpression());

        var lhs = context.leftHandSide();
        var valueToAssign = Visit(context.assignmentExpression(0));
        var op = context.assignmentOperator().GetText();

        var lvalue = EvaluateLValue(lhs);

        if (op != "=")
        {
            var currentValue = lvalue.Container switch
            {
                CobraEnvironment env => env.GetVariable(lvalue.Key as string ?? ""),
                CobraInstance instance => instance.Get(lvalue.Key as string ?? ""),
                CobraClass classDef => classDef.StaticEnvironment.GetVariable(lvalue.Key as string ?? ""),
                _ => GetIndex(lvalue.Container, lvalue.Key)
            };

            valueToAssign = ApplyBinaryOperator(op.TrimEnd('='), currentValue, valueToAssign);
        }

        switch (lvalue.Container)
        {
            case CobraEnvironment env:
                env.AssignVariable(lvalue.Key as string ?? "", valueToAssign);
                break;
            case CobraInstance instance:
                instance.Set(lvalue.Key as string ?? "", valueToAssign);
                break;
            case CobraClass classDef:
                classDef.StaticEnvironment.AssignVariable(lvalue.Key as string ?? "", valueToAssign);
                break;
            default:
                lvalue.Set(valueToAssign);
                break;
        }

        return valueToAssign;
    }

    public override object? VisitPostfixExpression(CobraParser.PostfixExpressionContext context)
    {
        var currentObject = Visit(context.primary());

        var idIndex = 0;
        var argListIndex = 0;
        var exprIndex = 0;

        for (var i = 1; i < context.ChildCount;)
        {
            var opNode = context.GetChild(i);
            var opToken = (opNode as ITerminalNode)?.Symbol;

            switch (opNode)
            {
                case Antlr4.Runtime.Tree.ITerminalNode { Symbol: { Type: CobraLexer.LPAREN } }:
                {
                    var argListCtx = context.argumentList(argListIndex++);
                    var args = argListCtx?.assignmentExpression().Select(Visit).ToList() ?? [];
                    currentObject = ExecuteFunctionCall(currentObject, args, "function", null, opToken);
                    i += argListCtx != null ? 3 : 2;
                    break;
                }
                case Antlr4.Runtime.Tree.ITerminalNode { Symbol: { Type: CobraLexer.LBRACKET } }:
                {
                    var index = Visit(context.assignmentExpression(exprIndex++));
                    currentObject = GetIndex(currentObject, index);
                    i += 3;
                    break;
                }
                case Antlr4.Runtime.Tree.ITerminalNode { Symbol: { Type: CobraLexer.DOT } }:
                {
                    var memberName = context.ID(idIndex++).GetText();
                    var member = GetMember(currentObject, memberName);

                    if (i + 2 < context.ChildCount && context.GetChild(i + 2) is Antlr4.Runtime.Tree.ITerminalNode
                        {
                            Symbol: { Type: CobraLexer.LPAREN }
                        } nextOp)
                    {
                        var argListCtx = context.argumentList(argListIndex++);
                        var args = argListCtx?.assignmentExpression().Select(Visit).ToList() ?? new List<object?>();
                        currentObject = ExecuteFunctionCall(member, args, memberName, currentObject as CobraInstance,
                            nextOp.Symbol);
                        i += argListCtx != null ? 4 : 3;
                    }
                    else
                    {
                        currentObject = member;
                        i += 2;
                    }

                    break;
                }
                case Antlr4.Runtime.Tree.ITerminalNode op when op.Symbol.Type is CobraLexer.INC or CobraLexer.DEC:
                {
                    var varName = GetLValueName(context.primary());
                    object? originalValue = currentObject;
                    if (!CobraLiteralHelper.IsNumeric(originalValue))
                        throw new Exception("Postfix '++' and '--' can only be applied to numeric types.");
                    object newValue;
                    if (originalValue is double d) newValue = op.Symbol.Type == CobraLexer.INC ? d + 1.0 : d - 1.0;
                    else
                        newValue = op.Symbol.Type == CobraLexer.INC
                            ? Convert.ToInt64(originalValue) + 1
                            : Convert.ToInt64(originalValue) - 1;
                    _currentEnvironment.AssignVariable(varName, newValue);
                    currentObject = originalValue;
                    i++;
                    break;
                }
                default:
                    i++;
                    break;
            }
        }

        return currentObject;
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
        if (context.THIS() != null) return _currentEnvironment.GetVariable("this");
        if (context.newExpression() != null) return VisitNewExpression(context.newExpression());
        if (context.arrayLiteral() != null) return Visit(context.arrayLiteral());
        if (context.dictLiteral() != null) return Visit(context.dictLiteral());
        if (context.functionExpression() != null) return Visit(context.functionExpression());
        throw new NotSupportedException("This primary form is not supported yet.");
    }

    public object? VisitNewExpression(CobraParser.NewExpressionContext context)
    {
        var qualifiedNameCtx = context.qualifiedName();
        var classDefinition = ResolveQualifiedName(qualifiedNameCtx) as CobraClass;
        var callToken = (context.GetChild(2) as ITerminalNode)!.Symbol; // The '(' token

        if (classDefinition == null)
            throw new Exception($"Type '{qualifiedNameCtx.GetText()}' not found or is not a class.");

        var instance = new CobraInstance(classDefinition);

        foreach (var field in classDefinition.Fields)
        {
            instance.Fields.DefineVariable(field.Key, field.Value.InitialValue);
        }

        var args = context.argumentList()?.assignmentExpression().Select(Visit).ToList() ?? new List<object?>();

        if (classDefinition.Constructor != null)
        {
            ExecuteFunctionCall(classDefinition.Constructor, args, qualifiedNameCtx.GetText(), instance, callToken);
        }

        return instance;
    }

    public override object VisitFunctionExpression(CobraParser.FunctionExpressionContext context)
    {
        var parameters = context.parameterList()?.parameter()
                             .Select(p => (CobraRuntimeTypes.Void, p.ID().GetText())).ToList() ??
                         [];
        return new CobraUserDefinedFunction("", parameters, context.block(), _currentEnvironment);
    }
}