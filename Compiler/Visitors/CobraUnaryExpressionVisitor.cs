// /Compiler/Visitors/Expressions/CobraUnaryExpressionVisitor.cs

using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors.Expressions;

internal class CobraUnaryExpressionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private LLVMBuilderRef _builder;

    internal CobraUnaryExpressionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _builder = mainVisitor.Builder;
    }

    public LLVMValueRef VisitUnaryExpression(CobraParser.UnaryExpressionContext context)
    {
        if (context.postfixExpression() != null)
        {
            return _visitor.Visit(context.postfixExpression());
        }

        var op = context.GetChild(0).GetText();
        var operand = _visitor.Visit(context.unaryExpression());

        if (op == "+") return operand;
        if (op == "-") return _builder.BuildNeg(operand, "neg");
        if (op == "!") return _builder.BuildNot(operand, "logical_not");
        if (op == "~") return _builder.BuildNot(operand, "bitwise_not");

        if (op != "++" && op != "--") throw new Exception($"Invalid unary op: {op}");

        string varName = context.unaryExpression().postfixExpression()?.primary()?.ID()?.GetText()
                         ?? throw new Exception("Invalid lvalue for prefix inc/dec");
        var addr = _visitor.ScopeManagement.FindVariable(varName);
        var oldVal = _builder.BuildLoad2(addr.TypeOf.ElementType, addr, varName);
        var newVal = op == "++"
            ? _builder.BuildAdd(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "pre_inc")
            : _builder.BuildSub(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "pre_dec");
        _builder.BuildStore(newVal, addr);
        return newVal;
    }
}