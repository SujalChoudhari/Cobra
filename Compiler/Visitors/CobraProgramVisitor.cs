using Antlr4.Runtime;
using Cobra.Utils;
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
    internal readonly String ModuleName;
    internal readonly bool IsMainModule;
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

    public CobraProgramVisitor(LLVMModuleRef module, LLVMBuilderRef builder, string moduleName,
        bool isMainModule = true)
    {
        Module = module;
        Builder = builder;
        ModuleName = moduleName;
        IsMainModule = isMainModule;

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
        ProcessExternStatements(context);
        ProcessFunctionDeclarationStatements(context);
        ProcessGlobalDeclarationStatements(context);
        ProcessFunctionDeclaration2Statements(context);
        _statementVisitor.VisitProgram(context);

        return default;
    }

    void ProcessExternStatements(CobraParser.ProgramContext context)
    {
        foreach (var externDeclarationContext in context.externDeclaration())
        {
            _functionVisitor.VisitExternDeclaration(externDeclarationContext);
        }
    }

    void ProcessFunctionDeclarationStatements(CobraParser.ProgramContext context)
    {
        foreach (var funcDecl in context.functionDeclaration())
        {
            _functionVisitor.VisitFunctionDeclaration_Pass1(funcDecl);
        }
    }

    void ProcessFunctionDeclaration2Statements(CobraParser.ProgramContext context)
    {
        foreach (var funcDecl in context.functionDeclaration())
        {
            _functionVisitor.VisitFunctionDeclaration_Pass2(funcDecl);
        }
    }

    void ProcessGlobalDeclarationStatements(CobraParser.ProgramContext context)
    {
        foreach (var decl in context.declarationStatement()
                     .Where(d => d.GLOBAL() != null))
        {
            _statementVisitor.VisitDeclarationStatement(decl);
        }
    }

    public override LLVMValueRef VisitImportStatement(CobraParser.ImportStatementContext context)
    {
        var modulePath = context.qualifiedName().GetText();
        var moduleName = modulePath.Split('.').Last(); // "math"

        string
            baseDirectory =
                "."; // Assume relative to the execution directory for now. A more robust solution might track the current file's directory.
        string relativePath = modulePath.Replace('.', Path.DirectorySeparatorChar) + ".cb";
        string filePath = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Imported module not found: '{modulePath}' resolved to '{filePath}'");

        CobraLogger.Info($"Processing import: {modulePath}");

        // Parse the imported file to get its function declarations
        string source = File.ReadAllText(filePath);
        var inputStream = new AntlrInputStream(source);
        var lexer = new CobraLexer(inputStream);
        var commonTokenStream = new CommonTokenStream(lexer);
        var parser = new CobraParser(commonTokenStream);
        var programContext = parser.program();

        // Pass 1: Declare all functions from the imported module with the namespace
        ProcessExternStatements(programContext);
        ProcessFunctionDeclarationStatements(programContext);

        return default; // Import statements don't generate code directly.
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