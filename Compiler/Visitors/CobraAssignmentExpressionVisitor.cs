using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraAssignmentExpressionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private readonly LLVMModuleRef _module;
    private LLVMBuilderRef _builder;

    internal CobraAssignmentExpressionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _module = mainVisitor.Module;
        _builder = mainVisitor.Builder;
    }

    public LLVMValueRef VisitExpression(CobraParser.ExpressionContext context) => _visitor.Visit(context.assignmentExpression());

    public LLVMValueRef VisitAssignmentExpression(CobraParser.AssignmentExpressionContext context)
    {
        if (context.assignmentOperator() == null)
        {
            return _visitor.Visit(context.conditionalExpression());
        }

        var variableAddress = VisitLValue(context.postfixExpression());
        var rhs = _visitor.Visit(context.assignmentExpression());
        var op = context.assignmentOperator().GetText();
        LLVMValueRef valueToStore;

        if (op == "=")
        {
            valueToStore = rhs;
        }
        else
        {
            var loaded = _builder.BuildLoad2(variableAddress.TypeOf.ElementType, variableAddress, "compound_load");
            valueToStore = op switch
            {
                "+=" => _builder.BuildAdd(loaded, rhs, "add_assign"),
                "-=" => _builder.BuildSub(loaded, rhs, "sub_assign"),
                "*=" => _builder.BuildMul(loaded, rhs, "mul_assign"),
                "/=" => _builder.BuildSDiv(loaded, rhs, "div_assign"),
                _ => throw new Exception($"Unsupported assignment operator: {op}")
            };
        }

        _builder.BuildStore(valueToStore, variableAddress);
        CobraLogger.Success($"Compiled assignment expression");
        CobraLogger.RuntimeVariableValue(_builder, _module, "Assigned value:", valueToStore);

        return valueToStore;
    }

    private LLVMValueRef VisitLValue(CobraParser.PostfixExpressionContext context)
    {
        if (context.primary()?.ID() != null && context.ChildCount == 1)
        {
            return _visitor.ScopeManagement.FindVariable(context.primary().ID().GetText());
        }
        throw new Exception($"Invalid l-value for assignment.");
    }
}