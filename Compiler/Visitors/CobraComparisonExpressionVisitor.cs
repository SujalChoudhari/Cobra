using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraComparisonExpressionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private LLVMBuilderRef _builder;

    internal CobraComparisonExpressionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _builder = mainVisitor.Builder;
    }

    public LLVMValueRef VisitEqualityExpression(CobraParser.EqualityExpressionContext context)
    {
        var left = _visitor.Visit(context.comparisonExpression(0));
        for (var i = 1; i < context.comparisonExpression().Length; i++)
        {
            var op = context.GetChild(2 * i - 1).GetText();
            var right = _visitor.Visit(context.comparisonExpression(i));
            var pred = op == "==" ? LLVMIntPredicate.LLVMIntEQ : LLVMIntPredicate.LLVMIntNE;
            left = _builder.BuildICmp(pred, left, right, "equality_cmp");
        }
        return left;
    }

    public LLVMValueRef VisitComparisonExpression(CobraParser.ComparisonExpressionContext context)
    {
        var left = _visitor.Visit(context.bitwiseShiftExpression(0));
        for (var i = 1; i < context.bitwiseShiftExpression().Length; i++)
        {
            var op = context.GetChild(2 * i - 1).GetText();
            var right = _visitor.Visit(context.bitwiseShiftExpression(i));
            var pred = op switch
            {
                ">" => LLVMIntPredicate.LLVMIntSGT,
                "<" => LLVMIntPredicate.LLVMIntSLT,
                ">=" => LLVMIntPredicate.LLVMIntSGE,
                "<=" => LLVMIntPredicate.LLVMIntSLE,
                _ => throw new Exception($"Invalid comparison op: {op}")
            };
            left = _builder.BuildICmp(pred, left, right, "comparison_cmp");
        }
        return left;
    }
}