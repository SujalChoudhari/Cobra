using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraBitwiseExpressionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private LLVMBuilderRef _builder;

    internal CobraBitwiseExpressionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _builder = mainVisitor.Builder;
    }

    public LLVMValueRef VisitBitwiseOrExpression(CobraParser.BitwiseOrExpressionContext context)
    {
        var left = _visitor.Visit(context.bitwiseXorExpression(0));
        for (var i = 1; i < context.bitwiseXorExpression().Length; i++)
        {
            var right = _visitor.Visit(context.bitwiseXorExpression(i));
            left = _builder.BuildOr(left, right, "bitwise_or");
        }
        return left;
    }

    public LLVMValueRef VisitBitwiseXorExpression(CobraParser.BitwiseXorExpressionContext context)
    {
        var left = _visitor.Visit(context.bitwiseAndExpression(0));
        for (var i = 1; i < context.bitwiseAndExpression().Length; i++)
        {
            var right = _visitor.Visit(context.bitwiseAndExpression(i));
            left = _builder.BuildXor(left, right, "bitwise_xor");
        }
        return left;
    }
    
    public LLVMValueRef VisitBitwiseAndExpression(CobraParser.BitwiseAndExpressionContext context)
    {
        var left = _visitor.Visit(context.equalityExpression(0));
        for (var i = 1; i < context.equalityExpression().Length; i++)
        {
            var right = _visitor.Visit(context.equalityExpression(i));
            left = _builder.BuildAnd(left, right, "bitwise_and");
        }
        return left;
    }

    public LLVMValueRef VisitBitwiseShiftExpression(CobraParser.BitwiseShiftExpressionContext context)
    {
        LLVMValueRef left = _visitor.Visit(context.additiveExpression(0));
        for (int i = 1; i < context.additiveExpression().Length; i++)
        {
            string op = context.GetChild(2 * i - 1).GetText();
            LLVMValueRef right = _visitor.Visit(context.additiveExpression(i));
            left = op == "<<"
                ? _builder.BuildShl(left, right, "shift_left")
                : _builder.BuildAShr(left, right, "shift_right");
        }
        return left;
    }
}