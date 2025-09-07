using System.Globalization;
using Antlr4.Runtime.Tree;
using Cobra.Environment;

namespace Cobra.Interpreter
{
    public class CobraInterpreter : CobraBaseVisitor<object?>
    {
        private CobraEnvironment _currentEnvironment = CobraEnvironment.CreateGlobalEnvironment();

        private class LValue(object? container, object? key)
        {
            public object? Container { get; } = container;
            public object? Key { get; } = key;

            public void Set(object? value)
            {
                switch (Container)
                {
                    case List<object?> list when Key is long or int:
                        list[Convert.ToInt32(Key)] = value;
                        break;
                    case Dictionary<string, object?> dict when Key is string key:
                        dict[key] = value;
                        break;
                    default:
                        throw new InvalidOperationException("Invalid LValue target for setting value.");
                }
            }
        }

        public override object? VisitProgram(CobraParser.ProgramContext context)
        {
            foreach (var statement in context.children)
            {
                Visit(statement);
            }

            return null;
        }

        #region Scoping and Blocks

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
                if (result is ReturnValue or BreakValue or ContinueValue)
                {
                    return result;
                }
            }

            return null;
        }

        #endregion

        #region Declarations

        public override object? VisitVarDeclaration(CobraParser.VarDeclarationContext context)
        {
            var varName = context.ID().GetText();
            object? value = null;
            if (context.assignmentExpression() != null)
            {
                value = Visit(context.assignmentExpression());
            }

            _currentEnvironment.DefineVariable(varName, value);
            return null;
        }

        public override object? VisitConstDeclaration(CobraParser.ConstDeclarationContext context)
        {
            var constName = context.ID().GetText();
            var value = Visit(context.assignmentExpression());
            _currentEnvironment.DefineVariable(constName, value, isConst: true);
            return null;
        }

        public override object? VisitFunctionDeclaration(CobraParser.FunctionDeclarationContext context)
        {
            var funcName = context.ID().GetText();
            var parameters = context.parameterList()?.parameter()
                                 .Select(p => (CobraRuntimeTypes.Void, p.ID().GetText())).ToList() ??
                             new List<(CobraRuntimeTypes, string)>();
            var function = new UserDefinedFunction(funcName, parameters, context.block(), _currentEnvironment);
            _currentEnvironment.DefineVariable(funcName, function, isConst: true);
            return null;
        }

        #endregion

        #region Statements

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
                if (result is ReturnValue) return result;
                if (result is BreakValue) break;
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
                    if (result is ReturnValue) return result;
                    if (result is BreakValue) break;

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
                return new ReturnValue(context.assignmentExpression() != null
                    ? Visit(context.assignmentExpression())
                    : null);
            }

            if (context.BREAK() != null) return BreakValue.Instance;
            if (context.CONTINUE() != null) return ContinueValue.Instance;
            return null;
        }

        #endregion

        #region Expressions

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
                    case ITerminalNode { Symbol: { Type: CobraLexer.LPAREN } }:
                    {
                        var argListCtx = context.argumentList(GetPostfixOperatorIndex(context, i));
                        var args = argListCtx?.assignmentExpression().Select(Visit).ToList() ?? new List<object?>();
                        result = ExecuteFunctionCall(result, args, context.primary().GetText());
                        i += (argListCtx != null ? 2 : 1) + 1;
                        break;
                    }
                    case ITerminalNode { Symbol: { Type: CobraLexer.LBRACKET } }:
                    {
                        var index = Visit(context.assignmentExpression(GetPostfixOperatorIndex(context, i)));
                        result = GetIndex(result, index);
                        i += 2;
                        break;
                    }
                    case ITerminalNode { Symbol: { Type: CobraLexer.DOT } }:
                    {
                        var memberName = context.ID(GetPostfixOperatorIndex(context, i)).GetText();
                        result = GetMember(result, memberName);
                        i += 2;
                        break;
                    }
                    case ITerminalNode op when op.Symbol.Type is CobraLexer.INC or CobraLexer.DEC:
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
            if (context.GetChild(1) is ITerminalNode firstOpNode)
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
                                 .Select(p => (CobraRuntimeTypes.Void, p.ID().GetText())).ToList() ??
                             new List<(CobraRuntimeTypes, string)>();
            return new UserDefinedFunction("", parameters, context.block(), _currentEnvironment);
        }

        #endregion

        #region Literals

        public override object? VisitLiteral(CobraParser.LiteralContext context)
        {
            if (context.INTEGER() != null) return long.Parse(context.GetText(), CultureInfo.InvariantCulture);
            if (context.FLOAT_LITERAL() != null) return double.Parse(context.GetText(), CultureInfo.InvariantCulture);
            if (context.STRING_LITERAL() != null) return CobraLiteralHelper.UnescapeString(context.GetText());
            if (context.BACKTICK_STRING() != null) return CobraLiteralHelper.UnescapeBacktickString(context.GetText());
            if (context.TRUE() != null) return true;
            if (context.FALSE() != null) return false;
            if (context.NULL() != null) return null;
            throw new NotSupportedException($"Unknown literal: {context.GetText()}");
        }

        public override object VisitArrayLiteral(CobraParser.ArrayLiteralContext context)
        {
            return context.assignmentExpression()?.Select(Visit).ToList() ?? new List<object?>();
        }

        public override object VisitDictLiteral(CobraParser.DictLiteralContext context)
        {
            return context.dictEntry()?.ToDictionary(
                entry => entry.STRING_LITERAL() != null
                    ? CobraLiteralHelper.UnescapeString(entry.STRING_LITERAL().GetText())
                    : entry.ID().GetText(),
                entry => Visit(entry.assignmentExpression())) ?? new Dictionary<string, object?>();
        }

        #endregion

        #region Helpers

        private LValue EvaluateLValue(CobraParser.LeftHandSideContext context)
        {
            object? container = Visit(context.primary());
            if (context.children.Count == 1) throw new Exception("Invalid LValue target.");
            for (int i = 1; i < context.ChildCount - 1;)
            {
                var op = context.GetChild(i);
                if (op.GetText() == "[")
                {
                    container = GetIndex(container, Visit(context.assignmentExpression((i - 1) / 2)));
                    i += 2;
                }
                else if (op.GetText() == ".")
                {
                    container = GetMember(container, context.ID((i - 1) / 2).GetText());
                    i++;
                }

                i++;
            }

            var lastOp = context.GetChild(context.ChildCount - 2);
            if (lastOp.GetText() == "[")
            {
                var indexExpr = context.assignmentExpression().Last();
                return new LValue(container, Visit(indexExpr));
            }

            var idExpr = context.ID().Last();
            return new LValue(container, idExpr.GetText());
        }

        private int GetPostfixOperatorIndex(CobraParser.PostfixExpressionContext context, int opNodeIndex)
        {
            int count = 0;
            for (int i = 1; i < opNodeIndex; i++)
            {
                if (context.GetChild(i).GetText() == context.GetChild(opNodeIndex).GetText())
                    count++;
            }

            return count;
        }

        private string GetLValueName(CobraParser.PrimaryContext context)
        {
            if (context.ID() != null)
            {
                return context.ID().GetText();
            }

            throw new Exception("Invalid target for postfix operator. Must be a simple variable.");
        }

        private object? GetIndex(object? collection, object? key)
        {
            if (collection is List<object?> list && (key is long or int)) return list[Convert.ToInt32(key)];
            if (collection is Dictionary<string, object?> dict && key is string sKey) return dict[sKey];
            throw new Exception("Object is not indexable or key is of wrong type.");
        }

        private object? GetMember(object? collection, string key)
        {
            if (collection is Dictionary<string, object?> dict)
            {
                return dict.GetValueOrDefault(key);
            }

            throw new Exception("Object does not have members access with '.' operator.");
        }

        private object? ExecuteFunctionCall(object? funcObject, List<object?> args, string funcNameForError)
        {
            switch (funcObject)
            {
                case UserDefinedFunction userFunc:
                {
                    if (args.Count != userFunc.Parameters.Count)
                        throw new Exception(
                            $"Function '{userFunc.Name}' expects {userFunc.Parameters.Count} arguments but got {args.Count}.");
                    var previous = _currentEnvironment;
                    _currentEnvironment = new CobraEnvironment(userFunc.Closure);
                    try
                    {
                        for (var i = 0; i < userFunc.Parameters.Count; i++)
                        {
                            _currentEnvironment.DefineVariable(userFunc.Parameters[i].Name, args[i]);
                        }

                        var result = ExecuteBlockStmts(userFunc.Body);
                        if (result is ReturnValue rv) return rv.Value;
                        return null;
                    }
                    finally
                    {
                        _currentEnvironment = previous;
                    }
                }
                case BuiltinFunction builtinFunc:
                {
                    return builtinFunc.Action(args);
                }
                default:
                    throw new Exception($"'{funcNameForError}' is not a function.");
            }
        }

        private object? EvaluateUnaryThenPostfix(CobraParser.BinaryExpressionContext context, ref int i)
        {
            var prefixOps = new List<string>();
            while (i < context.ChildCount && context.GetChild(i) is CobraParser.UnaryOpContext)
            {
                prefixOps.Add(context.GetChild(i).GetText());
                i++;
            }

            var value = Visit(context.GetChild(i));
            i++;
            for (var j = prefixOps.Count - 1; j >= 0; j--)
            {
                value = ApplyPrefixUnaryOperator(prefixOps[j], value);
            }

            return value;
        }

        private object? ApplyPrefixUnaryOperator(string op, object? value)
        {
            switch (op)
            {
                case "+": return value;
                case "-":
                    if (value is long lv) return -lv;
                    if (value is double dv) return -dv;
                    throw new Exception($"Unary '-' not supported for {value?.GetType().Name}");
                case "!":
                    return !CobraLiteralHelper.IsTruthy(value);
                default:
                    throw new NotSupportedException($"Unary operator '{op}' not supported yet.");
            }
        }

        private object? ApplyBinaryOperator(string op, object? left, object? right)
        {
            if (op == "+" && (left is string || right is string))
                return (left?.ToString() ?? "") + (right?.ToString() ?? "");
            if (op is "==" or "!=" or "<" or ">" or "<=" or ">=")
            {
                if (left == null || right == null)
                {
                    return op switch
                    {
                        "==" => left == right, "!=" => left != right,
                        _ => throw new Exception("Cannot compare null with '<', '>', '<=', or '>='.")
                    };
                }

                if (left is string lStr && right is string rStr)
                {
                    var comparison = string.Compare(lStr, rStr, StringComparison.Ordinal);
                    return op switch
                    {
                        "==" => comparison == 0, "!=" => comparison != 0, "<" => comparison < 0, ">" => comparison > 0,
                        "<=" => comparison <= 0, ">=" => comparison >= 0,
                        _ => throw new InvalidOperationException()
                    };
                }

                if (CobraLiteralHelper.IsNumeric(left) && CobraLiteralHelper.IsNumeric(right))
                {
                }
                else if (left.GetType() != right.GetType())
                {
                    if (!CobraLiteralHelper.IsNumeric(left) || !CobraLiteralHelper.IsNumeric(right))
                        throw new Exception(
                            $"Cannot compare values of different types: {left.GetType().Name} and {right.GetType().Name}");
                }
            }

            if (!CobraLiteralHelper.IsNumeric(left) || !CobraLiteralHelper.IsNumeric(right))
                throw new Exception($"Operator '{op}' requires numeric operands for operands '{left}' and '{right}'.");
            if (left is double || right is double || op == "/")
            {
                double l = Convert.ToDouble(left, CultureInfo.InvariantCulture);
                double r = Convert.ToDouble(right, CultureInfo.InvariantCulture);
                return op switch
                {
                    "+" => l + r, "-" => l - r, "*" => l * r,
                    "/" => r == 0 ? throw new DivideByZeroException() : l / r,
                    "%" => r == 0 ? throw new DivideByZeroException() : l % r,
                    ">" => l > r, "<" => l < r, ">=" => l >= r, "<=" => l <= r, "==" => Math.Abs(l - r) < 0.000001,
                    "!=" => Math.Abs(l - r) > 0.000001,
                    _ => throw new NotSupportedException($"Operator '{op}' not supported.")
                };
            }
            else
            {
                long l = Convert.ToInt64(left, CultureInfo.InvariantCulture);
                long r = Convert.ToInt64(right, CultureInfo.InvariantCulture);
                return op switch
                {
                    "+" => l + r, "-" => l - r, "*" => l * r,
                    "/" => r == 0 ? throw new DivideByZeroException() : (double)l / r,
                    "%" => r == 0 ? throw new DivideByZeroException() : l % r,
                    ">" => l > r, "<" => l < r, ">=" => l >= r, "<=" => l <= r, "==" => l == r, "!=" => l != r,
                    _ => throw new NotSupportedException($"Operator '{op}' not supported.")
                };
            }
        }

        #endregion
    }
}