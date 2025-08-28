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

    public LLVMValueRef VisitExpression(CobraParser.ExpressionContext context) =>
        _visitor.Visit(context.assignmentExpression());

    public LLVMValueRef VisitAssignmentExpression(CobraParser.AssignmentExpressionContext context)
    {
        if (context.assignmentOperator() == null)
        {
            return _visitor.Visit(context.conditionalExpression());
        }

        var variableAddress = VisitLValue(context.postfixExpression());
        var rhs = _visitor.Visit(context.assignmentExpression());
        var op = context.assignmentOperator().GetText();
        
        var lhsType = variableAddress.TypeOf.ElementType;
        var rhsType = rhs.TypeOf;

        if (lhsType.Kind != rhsType.Kind)
        {
            throw new Exception($"Type mismatch: cannot assign {rhsType} to {lhsType}");
        }
        
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
        CobraLogger.Success($"Compiled assignment expression for: {variableAddress}");

        return valueToStore;
    }

    private LLVMValueRef VisitArrayAccessLValue(CobraParser.PostfixExpressionContext context)
    {
        // Base variable name
        var baseVarName = context.primary().ID().GetText();
        var baseVarAddr = _visitor.ScopeManagement.FindVariable(baseVarName); // alloca of i32*

        // Load the array pointer (the result of `new int[...]`)
        var arrayPtr = _builder.BuildLoad2(baseVarAddr.TypeOf.ElementType, baseVarAddr, "load_array_ptr");

        var indexExpr = context.expression(0);
        var indexVal = _visitor.Visit(indexExpr);

        var elementType = arrayPtr.TypeOf.ElementType;
        return _builder.BuildGEP2(elementType, arrayPtr, new[] { indexVal }, "element_ptr");
    }



    private LLVMValueRef VisitLValue(CobraParser.PostfixExpressionContext context)
    {
        // Case 1: Simple variable assignment (e.g., x = 10)
        if (context.primary()?.ID() != null && context.ChildCount == 1)
        {
            return _visitor.ScopeManagement.FindVariable(context.primary().ID().GetText());
        }

        // Case 2: Array element assignment (e.g., arr[i] = 10)
        if (context.ChildCount > 1 && context.GetChild(1).GetText() == "[")
        {
            return VisitArrayAccessLValue(context);
        }


        throw new Exception("Invalid l-value for assignment.");
    }
}