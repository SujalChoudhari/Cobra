using System;
using System.Globalization;
using Antlr4.Runtime.Misc;
using Cobra.Environment;

namespace Cobra.Interpreter
{
    public class CobraInterpreter : CobraBaseVisitor<object?>
    {
        private CobraEnvironment GlobalEnvironment { get; } = new();

        public override object? VisitProgram(CobraParser.ProgramContext context)
        {
            object? last = null;
            foreach (var child in context.children)
            {
                last = Visit(child);
            }

            return last;
        }

        public override object? VisitAssignmentExpression(CobraParser.AssignmentExpressionContext context)
        {
            if (context.binaryExpression() != null)
                return Visit(context.binaryExpression());

            throw new NotSupportedException("Assignments not implemented yet.");
        }

        public override object? VisitBinaryExpression(CobraParser.BinaryExpressionContext context)
        {
            // If only one child, it's unary/postfix
            if (context.ChildCount == 1)
                return Visit(context.GetChild(0));

            int i = 0;
            object? left = EvaluateUnaryThenPostfix(context, ref i);

            while (i < context.ChildCount)
            {
                string op = context.GetChild(i).GetText();
                i++;
                object? right = EvaluateUnaryThenPostfix(context, ref i);
                left = ApplyBinaryOperator(op, left, right);
            }

            return left;
        }

        public override object? VisitPrimary(CobraParser.PrimaryContext context)
        {
            if (context.assignmentExpression() != null)
                return Visit(context.assignmentExpression());

            if (context.literal() != null)
                return Visit(context.literal());

            if (context.ID() != null)
                throw new NotSupportedException("Variables not implemented yet.");

            throw new NotSupportedException("Primary form not supported yet.");
        }

        public override object? VisitLiteral(CobraParser.LiteralContext context)
        {
            if (context.INTEGER() != null)
                return long.Parse(context.GetText(), CultureInfo.InvariantCulture);

            if (context.FLOAT_LITERAL() != null)
                return double.Parse(context.GetText(), CultureInfo.InvariantCulture);

            if (context.STRING_LITERAL() != null)
                return CobraLiteralHelper.UnescapeString(context.GetText());

            if (context.BACKTICK_STRING() != null)
                return CobraLiteralHelper.UnescapeBacktickString(context.GetText());

            if (context.TRUE() != null) return true;
            if (context.FALSE() != null) return false;
            if (context.NULL() != null) return null;

            throw new NotSupportedException($"Unknown literal: {context.GetText()}");
        }

        public override object? VisitPostfixExpression(CobraParser.PostfixExpressionContext context)
        {
            if (context.ChildCount == 1)
                return Visit(context.primary());

            throw new NotSupportedException("Postfix operations not implemented yet.");
        }

        // --- helpers ---

        private object? EvaluateUnaryThenPostfix(CobraParser.BinaryExpressionContext context, ref int i)
        {
            var prefixOps = new List<string>();
            while (i < context.ChildCount && context.GetChild(i) is CobraParser.UnaryOpContext)
            {
                prefixOps.Add(context.GetChild(i).GetText());
                i++;
            }

            var postfixExpr = context.GetChild(i) as CobraParser.PostfixExpressionContext
                              ?? throw new Exception("Expected postfix expression.");

            var value = Visit(postfixExpr);
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
                default:
                    throw new NotSupportedException($"Unary operator '{op}' not supported yet.");
            }
        }

        private object? ApplyBinaryOperator(string op, object? left, object? right)
        {
            if (op == "+" && (left is string || right is string))
                return (left?.ToString() ?? "null") + (right?.ToString() ?? "null");

            if (!CobraLiteralHelper.IsNumeric(left) || !CobraLiteralHelper.IsNumeric(right))
                throw new Exception($"Operator '{op}' requires numeric operands.");

            bool useDouble = left is double || right is double || op == "/";

            if (useDouble)
            {
                double l = Convert.ToDouble(left, CultureInfo.InvariantCulture);
                double r = Convert.ToDouble(right, CultureInfo.InvariantCulture);

                return op switch
                {
                    "+" => l + r,
                    "-" => l - r,
                    "*" => l * r,
                    "/" => r == 0 ? throw new DivideByZeroException() : l / r,
                    "%" => r == 0 ? throw new DivideByZeroException() : l % r,
                    _ => throw new NotSupportedException($"Operator '{op}' not supported.")
                };
            }
            else
            {
                long l = Convert.ToInt64(left, CultureInfo.InvariantCulture);
                long r = Convert.ToInt64(right, CultureInfo.InvariantCulture);

                return op switch
                {
                    "+" => l + r,
                    "-" => l - r,
                    "*" => l * r,
                    "/" => r == 0 ? throw new DivideByZeroException() : l / r,
                    "%" => r == 0 ? throw new DivideByZeroException() : l % r,
                    _ => throw new NotSupportedException($"Operator '{op}' not supported.")
                };
            }
        }


    }
}