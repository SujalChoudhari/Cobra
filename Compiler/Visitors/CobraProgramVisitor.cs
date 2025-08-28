using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

/// <summary>
/// The main orchestrator for visiting the Cobra parse tree. It holds the shared state (builder, scopes)
/// and delegates the compilation of specific constructs to specialized visitor classes.
/// </summary>
public class CobraProgramVisitor : CobraBaseVisitor<LLVMValueRef>
{
    internal readonly LLVMModuleRef Module;
    internal readonly LLVMBuilderRef Builder;
    internal readonly Stack<(LLVMBasicBlockRef ContinueTarget, LLVMBasicBlockRef BreakTarget)> LoopContexts = new();
    internal readonly CobraScopeManagement ScopeManagement = new();
    internal bool IsGlobalScope = true;

    internal readonly Dictionary<string, LLVMValueRef> Functions = new();

    internal LLVMValueRef CurrentFunction;

    // Visitors
    private readonly CobraFunctionVisitor _functionVisitor;
    private readonly CobraStatementVisitor _statementVisitor;
    private readonly CobraAssignmentExpressionVisitor _assignmentExpressionVisitor;
    private readonly CobraConditionalExpressionVisitor _conditionalExpressionVisitor;
    private readonly CobraLogicalExpressionVisitor _logicalExpressionVisitor;
    private readonly CobraBitwiseExpressionVisitor _bitwiseExpressionVisitor;
    private readonly CobraComparisonExpressionVisitor _comparisonExpressionVisitor;
    private readonly CobraArithmeticExpressionVisitor _arithmeticExpressionVisitor;
    private readonly CobraUnaryExpressionVisitor _unaryExpressionVisitor;
    private readonly CobraPrimaryExpressionVisitor _primaryExpressionVisitor;

    public CobraProgramVisitor(LLVMModuleRef module, LLVMBuilderRef builder)
    {
        Module = module;
        Builder = builder;
        _functionVisitor = new CobraFunctionVisitor(this);
        _statementVisitor = new CobraStatementVisitor(this);
        _assignmentExpressionVisitor = new CobraAssignmentExpressionVisitor(this);
        _conditionalExpressionVisitor = new CobraConditionalExpressionVisitor(this);
        _logicalExpressionVisitor = new CobraLogicalExpressionVisitor(this);
        _bitwiseExpressionVisitor = new CobraBitwiseExpressionVisitor(this);
        _comparisonExpressionVisitor = new CobraComparisonExpressionVisitor(this);
        _arithmeticExpressionVisitor = new CobraArithmeticExpressionVisitor(this);
        _unaryExpressionVisitor = new CobraUnaryExpressionVisitor(this);
        _primaryExpressionVisitor = new CobraPrimaryExpressionVisitor(this);
    }

    // --- Top-Level ---
    public override LLVMValueRef VisitProgram(CobraParser.ProgramContext context)
    {
        foreach (var externDeclarationContext in context.externDeclaration())
        {
            _functionVisitor.VisitExternDeclaration(externDeclarationContext);
        }
        foreach (var funcDecl in context.functionDeclaration())
        {
            _functionVisitor.VisitFunctionDeclaration_Pass1(funcDecl);
        }

        foreach (var decl in context.declarationStatement()
                     .Where(d => d.GLOBAL() != null))
        {
            _statementVisitor.VisitDeclarationStatement(decl);
        }

        foreach (var funcDecl in context.functionDeclaration())
        {
            _functionVisitor.VisitFunctionDeclaration_Pass2(funcDecl);
        }

        _statementVisitor.VisitProgram(context);

        return default;
    }


    public override LLVMValueRef VisitFunctionDeclaration(CobraParser.FunctionDeclarationContext context) =>
        _functionVisitor.VisitFunctionDeclaration_Pass2(context);

    // --- Statement Delegation ---
    public override LLVMValueRef VisitStatement(CobraParser.StatementContext context) =>
        _statementVisitor.VisitStatement(context);

    public override LLVMValueRef VisitBlock(CobraParser.BlockContext context) => _statementVisitor.VisitBlock(context);

    public override LLVMValueRef VisitExpressionStatement(CobraParser.ExpressionStatementContext context) =>
        _statementVisitor.VisitExpressionStatement(context);

    public override LLVMValueRef VisitDeclarationStatement(CobraParser.DeclarationStatementContext context) =>
        _statementVisitor.VisitDeclarationStatement(context);

    public override LLVMValueRef VisitIfStatement(CobraParser.IfStatementContext context) =>
        _statementVisitor.VisitIfStatement(context);

    public override LLVMValueRef VisitWhileStatement(CobraParser.WhileStatementContext context) =>
        _statementVisitor.VisitWhileStatement(context);

    public override LLVMValueRef VisitDoWhileStatement(CobraParser.DoWhileStatementContext context) =>
        _statementVisitor.VisitDoWhileStatement(context);

    public override LLVMValueRef VisitForStatement(CobraParser.ForStatementContext context) =>
        _statementVisitor.VisitForStatement(context);

    public override LLVMValueRef VisitJumpStatement(CobraParser.JumpStatementContext context) =>
        _statementVisitor.VisitJumpStatement(context);

    // --- Expression Delegation ---
    public override LLVMValueRef VisitExpression(CobraParser.ExpressionContext context) =>
        _assignmentExpressionVisitor.VisitExpression(context);

    public override LLVMValueRef VisitAssignmentExpression(CobraParser.AssignmentExpressionContext context) =>
        _assignmentExpressionVisitor.VisitAssignmentExpression(context);

    public override LLVMValueRef VisitConditionalExpression(CobraParser.ConditionalExpressionContext context) =>
        _conditionalExpressionVisitor.VisitConditionalExpression(context);

    public override LLVMValueRef VisitLogicalOrExpression(CobraParser.LogicalOrExpressionContext context) =>
        _logicalExpressionVisitor.VisitLogicalOrExpression(context);

    public override LLVMValueRef VisitLogicalAndExpression(CobraParser.LogicalAndExpressionContext context) =>
        _logicalExpressionVisitor.VisitLogicalAndExpression(context);

    public override LLVMValueRef VisitBitwiseOrExpression(CobraParser.BitwiseOrExpressionContext context) =>
        _bitwiseExpressionVisitor.VisitBitwiseOrExpression(context);

    public override LLVMValueRef VisitBitwiseXorExpression(CobraParser.BitwiseXorExpressionContext context) =>
        _bitwiseExpressionVisitor.VisitBitwiseXorExpression(context);

    public override LLVMValueRef VisitBitwiseAndExpression(CobraParser.BitwiseAndExpressionContext context) =>
        _bitwiseExpressionVisitor.VisitBitwiseAndExpression(context);

    public override LLVMValueRef VisitEqualityExpression(CobraParser.EqualityExpressionContext context) =>
        _comparisonExpressionVisitor.VisitEqualityExpression(context);

    public override LLVMValueRef VisitComparisonExpression(CobraParser.ComparisonExpressionContext context) =>
        _comparisonExpressionVisitor.VisitComparisonExpression(context);

    public override LLVMValueRef VisitBitwiseShiftExpression(CobraParser.BitwiseShiftExpressionContext context) =>
        _bitwiseExpressionVisitor.VisitBitwiseShiftExpression(context);

    public override LLVMValueRef VisitAdditiveExpression(CobraParser.AdditiveExpressionContext context) =>
        _arithmeticExpressionVisitor.VisitAdditiveExpression(context);

    public override LLVMValueRef VisitMultiplicativeExpression(CobraParser.MultiplicativeExpressionContext context) =>
        _arithmeticExpressionVisitor.VisitMultiplicativeExpression(context);

    public override LLVMValueRef VisitUnaryExpression(CobraParser.UnaryExpressionContext context) =>
        _unaryExpressionVisitor.VisitUnaryExpression(context);

    public override LLVMValueRef VisitPostfixExpression(CobraParser.PostfixExpressionContext context) =>
        _primaryExpressionVisitor.VisitPostfixExpression(context);

    public override LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context) =>
        _primaryExpressionVisitor.VisitPrimary(context);
}