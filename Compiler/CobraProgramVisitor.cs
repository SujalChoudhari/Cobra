using Antlr4.Runtime.Tree;
using Cobra.Utils;
using LLVMSharp.Interop;
// Assuming Antlr4.Runtime is used for the parser context objects

namespace Cobra.Compiler;

public class CobraProgramVisitor(
    LLVMModuleRef module,
    LLVMBuilderRef builder,
    Dictionary<string, LLVMValueRef> namedValues)
    : CobraBaseVisitor<LLVMValueRef>
{
    private readonly LLVMModuleRef _module = module;
    private LLVMBuilderRef _builder = builder;

    public override LLVMValueRef VisitProgram(CobraParser.ProgramContext context)
    {
        // FIX: The original loop was incomplete.
        // This iterates through all top-level constructs (imports, functions, classes, etc.)
        // in the order they appear in the source file.
        foreach (var child in context.children)
        {
            // We ignore the final EOF token, which is represented as a terminal node.
            if (child is ITerminalNode) continue;

            Visit(child);
        }

        // A program as a whole doesn't return a value.
        return default;
    }

    public override LLVMValueRef VisitStatement(CobraParser.StatementContext context)
    {
        // Check if the statement is a declaration statement
        if (context.declarationStatement() != null)
        {
            return VisitDeclarationStatement(context.declarationStatement());
        }
        // Then check if it's an assignment statement
        else if (context.assignmentStatement() != null)
        {
            return VisitAssignmentStatement(context.assignmentStatement());
        }

        // You would add other statement types here as you expand the compiler
        return default;
    }

    public override LLVMValueRef VisitDeclarationStatement(CobraParser.DeclarationStatementContext context)
    {
        string variableName = context.ID().GetText();
        string typeName = context.type().GetText();

        LLVMTypeRef varType;
        switch (typeName)
        {
            case "int":
                varType = LLVMTypeRef.Int32;
                break;
            case "float":
                varType = LLVMTypeRef.Float;
                break;
            case "string":
                varType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
                break;
            case "bool":
                varType = LLVMTypeRef.Int1;
                break;
            case "void":
                varType = LLVMTypeRef.Void;
                break;
            default:
                throw new Exception($"Invalid type specified: {typeName}");
        }

        // Check if the variable is already declared to prevent re-declaration
        if (namedValues.ContainsKey(variableName))
        {
            throw new Exception($"Variable '{variableName}' is already declared.");
        }

        // Allocate memory for the new variable
        LLVMValueRef allocatedValue = _builder.BuildAlloca(varType, variableName);
        namedValues[variableName] = allocatedValue;

        // If an initial value is provided, store it
        if (context.expression() != null)
        {
            LLVMValueRef initialValue = Visit(context.expression());
            _builder.BuildStore(initialValue, allocatedValue);
        }

        CobraLogger.Info($"Compiled declaration for variable: {variableName} with type {typeName}");
        CobraLogger.Runtime(_builder, _module,
            $"Declared variable: {variableName}= {allocatedValue} of type {typeName}");
        return allocatedValue;
    }


    public override LLVMValueRef VisitAssignmentStatement(CobraParser.AssignmentStatementContext context)
    {
        // FIX: The grammar specifies the left-hand side of an assignment is a `postfixExpression`, not a simple `ID`.
        // The original code `context.ID().GetText()` would fail because `AssignmentStatementContext` has no `ID()` method.
        // We get the variable's name by accessing the text of the `postfixExpression`.
        // Note: This simplified version assumes the expression is a simple variable name (e.g., `x = 5`).
        // A full implementation would need to handle assignments to members (e.g., `obj.field = 5`) or array elements (`arr[0] = 5`).
        string variableName = context.postfixExpression().GetText();

        // Check if the variable is declared, and throw an error if not.
        if (!namedValues.ContainsKey(variableName))
        {
            throw new Exception($"Undeclared variable: '{variableName}'");
        }

        LLVMValueRef valueToAssign = Visit(context.expression());
        LLVMValueRef variableAddress = namedValues[variableName];

        // Store the value in the allocated variable
        _builder.BuildStore(valueToAssign, variableAddress);
        CobraLogger.Info($"Compiled assignment for variable: {variableName}");
        CobraLogger.Runtime(_builder, _module, $"Assigned value to variable: {variableName}= {valueToAssign}");

        // Per language conventions, an assignment expression evaluates to the assigned value.
        return valueToAssign;
    }

    public override LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context)
    {
        return CobraLiteralVisitor.VisitLiteral(context, _builder, namedValues);
    }
}