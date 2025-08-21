using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler;

public class CobraProgramVisitor(
    LLVMModuleRef module,
    LLVMBuilderRef builder,
    Dictionary<string, LLVMValueRef> namedValues)
    : CobraParserBaseVisitor<LLVMValueRef>
{
    private readonly LLVMModuleRef _module = module;
    private LLVMBuilderRef _builder = builder;

    public override LLVMValueRef VisitProgram(CobraParser.ProgramContext context)
    {
        // Visit each top-level statement in the program
        foreach (var statement in context.statement())
        {
            Visit(statement);
        }

        return null;
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
        return null;
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
        CobraLogger.Runtime(_builder, _module, $"Declared variable: {variableName}= {allocatedValue} of type {typeName}");
        return allocatedValue;
    }


    public override LLVMValueRef VisitAssignmentStatement(CobraParser.AssignmentStatementContext context)
    {
        string variableName = context.ID().GetText();

        // The fix: Check if the variable is declared, and throw an error if not.
        if (!namedValues.ContainsKey(variableName))
        {
            throw new Exception($"Undeclared variable: {variableName}");
        }

        LLVMValueRef valueToAssign = Visit(context.expression());

        // Store the value in the allocated variable
        _builder.BuildStore(valueToAssign, namedValues[variableName]);
        CobraLogger.Info($"Compiled assignment for variable: {variableName}");
        CobraLogger.Runtime(_builder, _module, $"Assigned value to variable: {variableName}= {valueToAssign}");
        return valueToAssign;
    }

    public override LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context)
    {
        return CobraPrimaryVisitor.VisitPrimary(context, _builder, namedValues);
    }
}