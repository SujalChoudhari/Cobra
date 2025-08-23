using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraConditionalExpressionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private readonly LLVMModuleRef _module;
    private LLVMBuilderRef _builder;

    internal CobraConditionalExpressionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _module = mainVisitor.Module;
        _builder = mainVisitor.Builder;
    }

    public LLVMValueRef VisitConditionalExpression(CobraParser.ConditionalExpressionContext context)
    {
        if (context.QUESTION_MARK() == null)
        {
            return _visitor.Visit(context.logicalOrExpression());
        }

        var cond = _visitor.Visit(context.logicalOrExpression());
        var parentFunction = _builder.InsertBlock.Parent;

        var trueBlock = LLVMBasicBlockRef.AppendInContext(_module.Context, parentFunction, "ternary_true");
        var falseBlock = LLVMBasicBlockRef.AppendInContext(_module.Context, parentFunction, "ternary_false");
        var endBlock = LLVMBasicBlockRef.AppendInContext(_module.Context, parentFunction, "ternary_end");

        _builder.BuildCondBr(cond, trueBlock, falseBlock);

        _builder.PositionAtEnd(trueBlock);
        var trueVal = _visitor.Visit(context.expression(0));
        var trueEndBlock = _builder.InsertBlock;
        _builder.BuildBr(endBlock);

        _builder.PositionAtEnd(falseBlock);
        var falseVal = _visitor.Visit(context.expression(1));
        var falseEndBlock = _builder.InsertBlock;
        _builder.BuildBr(endBlock);

        _builder.PositionAtEnd(endBlock);
        var phi = _builder.BuildPhi(trueVal.TypeOf, "ternary_result");
        phi.AddIncoming(new[] { trueVal, falseVal }, new[] { trueEndBlock, falseEndBlock }, 2);
        return phi;
    }
}