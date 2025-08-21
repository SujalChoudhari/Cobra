using LLVMSharp.Interop;

namespace Cobra.Compiler;

public class CobraProgramVisitor : CobraParserBaseVisitor<LLVMValueRef>
{
    private readonly LLVMModuleRef _module;
    private readonly LLVMBuilderRef _builder;
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

    public override LLVMValueRef VisitAssignmentStatement(CobraParser.AssignmentStatementContext context)
    {
        string variableName = context.ID().GetText();
        LLVMValueRef valueToAssign = Visit(context.expression());

        // Check if the variable has already been declared
        if (!_namedValues.ContainsKey(variableName))
        {
            // If not, we'll allocate space for it.
            // For simplicity, we'll assume integer for now based on the expression type.
            LLVMTypeRef varType = valueToAssign.TypeOf;
            LLVMValueRef alloca = _builder.BuildAlloca(varType, variableName);
            _namedValues[variableName] = alloca;
        }

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