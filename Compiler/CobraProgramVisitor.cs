using LLVMSharp.Interop;

namespace Cobra.Compiler;

public class CobraProgramVisitor : CobraParserBaseVisitor<LLVMValueRef>
{
    private readonly LLVMModuleRef _module;
    private LLVMBuilderRef _builder;
    private readonly Dictionary<string, LLVMValueRef> _namedValues;

    public CobraProgramVisitor(LLVMModuleRef module, LLVMBuilderRef builder,
        Dictionary<string, LLVMValueRef> namedValues)
    {
        _module = module;
        _builder = builder;
        _namedValues = namedValues;
    }

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
        // We only care about assignment statements for now
        if (context.assignmentStatement() != null)
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
        if (typeName == "int")
        {
            varType = LLVMTypeRef.Int32;
        }
        else if (typeName == "float")
        {
            varType = LLVMTypeRef.Float;
        }
        else
        {
            throw new Exception($"Unsupported type for declaration: {typeName}");
        }

        // Check if the variable is already declared to prevent re-declaration
        if (_namedValues.ContainsKey(variableName))
        {
            throw new Exception($"Variable '{variableName}' is already declared.");
        }

        // Allocate memory for the new variable
        LLVMValueRef alloca = _builder.BuildAlloca(varType, variableName);
        _namedValues[variableName] = alloca;
        
        // If an initial value is provided, store it
        if (context.expression() != null)
        {
            LLVMValueRef initialValue = Visit(context.expression());
            _builder.BuildStore(initialValue, alloca);
        }

        Console.WriteLine($"Compiled declaration for variable: {variableName} with type {typeName}");
        return alloca;
    }


    public override LLVMValueRef VisitAssignmentStatement(CobraParser.AssignmentStatementContext context)
    {
        string variableName = context.ID().GetText();
        
        // The fix: Check if the variable is declared, and throw an error if not.
        if (!_namedValues.ContainsKey(variableName))
        {
            throw new Exception($"Undeclared variable: {variableName}");
        }
        
        LLVMValueRef valueToAssign = Visit(context.expression());

        // Store the value in the allocated variable
        _builder.BuildStore(valueToAssign, _namedValues[variableName]);
        Console.WriteLine($"Compiled assignment for variable: {variableName}");
        return valueToAssign;
    }
    
    public override LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context)
    {
        return CobraPrimaryVisitor.VisitPrimary(context, _builder, _namedValues);
    }
}