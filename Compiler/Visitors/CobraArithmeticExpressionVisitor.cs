using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraArithmeticExpressionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private LLVMBuilderRef _builder;

    internal CobraArithmeticExpressionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _builder = mainVisitor.Builder;
    }

    public LLVMValueRef VisitAdditiveExpression(CobraParser.AdditiveExpressionContext context)
    {
        LLVMValueRef left = _visitor.Visit(context.multiplicativeExpression(0));
        for (int i = 1; i < context.multiplicativeExpression().Length; i++)
        {
            string op = context.GetChild(2 * i - 1).GetText();
            LLVMValueRef right = _visitor.Visit(context.multiplicativeExpression(i));
            left = op == "+" ? _builder.BuildAdd(left, right, "add") : _builder.BuildSub(left, right, "sub");
        }
        return left;
    }

    public LLVMValueRef VisitMultiplicativeExpression(CobraParser.MultiplicativeExpressionContext context)
    {
        var left = _visitor.Visit(context.unaryExpression(0));
        for (var i = 1; i < context.unaryExpression().Length; i++)
        {
            var op = context.GetChild(2 * i - 1).GetText();
            var right = _visitor.Visit(context.unaryExpression(i));
            left = op switch
            {
                "*" => _builder.BuildMul(left, right, "mul"),
                "/" => _builder.BuildSDiv(left, right, "div"),
                "%" => _builder.BuildSRem(left, right, "rem"),
                _ => throw new Exception($"Invalid mul op: {op}")
            };
        }
        return left;
    }
}