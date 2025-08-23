using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraLogicalExpressionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private readonly LLVMModuleRef _module;
    private LLVMBuilderRef _builder;

    internal CobraLogicalExpressionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _module = mainVisitor.Module;
        _builder = mainVisitor.Builder;
    }

    public LLVMValueRef VisitLogicalOrExpression(CobraParser.LogicalOrExpressionContext context) => VisitLogicalOrHelper(context, 0);

    private LLVMValueRef VisitLogicalOrHelper(CobraParser.LogicalOrExpressionContext context, int index)
    {
        var left = _visitor.Visit(context.logicalAndExpression(index));
        if (index == context.logicalAndExpression().Length - 1) return left;
        
        var parentFunction = _builder.InsertBlock.Parent;
        var rightBlock = LLVMBasicBlockRef.AppendInContext(_module.Context, parentFunction, "or_rhs");
        var endBlock = LLVMBasicBlockRef.AppendInContext(_module.Context, parentFunction, "or_end");

        var currentBlock = _builder.InsertBlock;
        _builder.BuildCondBr(left, endBlock, rightBlock);

        _builder.PositionAtEnd(rightBlock);
        var right = VisitLogicalOrHelper(context, index + 1);
        var rightEndBlock = _builder.InsertBlock;
        _builder.BuildBr(endBlock);

        _builder.PositionAtEnd(endBlock);
        var phi = _builder.BuildPhi(LLVMTypeRef.Int1, "or_result");
        phi.AddIncoming(new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1), right }, new[] { currentBlock, rightEndBlock }, 2);
        return phi;
    }

    public LLVMValueRef VisitLogicalAndExpression(CobraParser.LogicalAndExpressionContext context) => VisitLogicalAndHelper(context, 0);

    private LLVMValueRef VisitLogicalAndHelper(CobraParser.LogicalAndExpressionContext context, int index)
    {
        var left = _visitor.Visit(context.bitwiseOrExpression(index));
        if (index == context.bitwiseOrExpression().Length - 1) return left;
        
        var parentFunction = _builder.InsertBlock.Parent;
        var rightBlock = LLVMBasicBlockRef.AppendInContext(_module.Context, parentFunction, "and_rhs");
        var endBlock = LLVMBasicBlockRef.AppendInContext(_module.Context, parentFunction, "and_end");

        var currentBlock = _builder.InsertBlock;
        _builder.BuildCondBr(left, rightBlock, endBlock);

        _builder.PositionAtEnd(rightBlock);
        var right = VisitLogicalAndHelper(context, index + 1);
        var rightEndBlock = _builder.InsertBlock;
        _builder.BuildBr(endBlock);

        _builder.PositionAtEnd(endBlock);
        var phi = _builder.BuildPhi(LLVMTypeRef.Int1, "and_result");
        phi.AddIncoming(new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 0), right }, new[] { currentBlock, rightEndBlock }, 2);
        return phi;
    }
}