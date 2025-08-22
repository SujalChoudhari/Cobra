using System.Text.RegularExpressions;
using Antlr4.Runtime.Tree;
using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler;

/// <summary>
/// The <see cref="CobraProgramVisitor"/> class is responsible for traversing the Cobra language's parse tree
/// and generating corresponding LLVM IR constructs. It extends the base visitor implementation
/// and overrides methods for various language constructs to handle their translation into LLVM IR.
/// </summary>
/// <param name="module">The compilation module.</param>
/// <param name="builder">The LLVM IR builder instance.</param>
public class CobraProgramVisitor(
    LLVMModuleRef module,
    LLVMBuilderRef builder)
    : CobraBaseVisitor<LLVMValueRef>
{
    private LLVMBuilderRef _builder = builder;
    private readonly Stack<Dictionary<string, LLVMValueRef>> _scopes = new();
    private readonly Stack<(LLVMBasicBlockRef ContinueTarget, LLVMBasicBlockRef BreakTarget)> _loopContexts = new();

    // =============================================================================
    // Scope Management Helpers
    // =============================================================================

    /// <summary>
    /// Enters a new lexical scope by pushing a new symbol table onto the scope stack.
    /// </summary>
    private void EnterScope()
    {
        _scopes.Push(new Dictionary<string, LLVMValueRef>());
    }

    /// <summary>
    /// Exits the current lexical scope by popping the current symbol table from the scope stack.
    /// </summary>
    private void ExitScope()
    {
        _scopes.Pop();
    }

    /// <summary>
    /// Declares a variable in the current scope.
    /// Throws an exception if the variable is already declared in this scope.
    /// </summary>
    /// <param name="name">The name of the variable to declare.</param>
    /// <param name="value">The LLVM value (alloca instruction) for the variable.</param>
    private void DeclareVariable(string name, LLVMValueRef value)
    {
        if (_scopes.Count == 0)
        {
            EnterScope(); // Ensure at least a global scope exists
        }

        if (_scopes.Peek().ContainsKey(name))
        {
            CobraLogger.Error($"Variable '{name}' is already declared in the current scope.");
            throw new Exception($"Cannot declare variable. Variable '{name}' is already declared.");
        }

        _scopes.Peek()[name] = value;
    }

    /// <summary>
    /// Finds a variable by searching from the innermost scope outwards.
    /// </summary>
    /// <param name="name">The name of the variable to find.</param>
    /// <returns>The LLVM value (alloca instruction) for the variable.</returns>
    /// <exception cref="Exception">Thrown if the variable is not declared in any accessible scope.</exception>
    private LLVMValueRef FindVariable(string name)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        CobraLogger.Error($"Undeclared variable: '{name}'");
        throw new Exception($"Undeclared variable: '{name}'");
    }

    // =============================================================================
    // Top-Level and Statement Visitors
    // =============================================================================

    /// <summary>
    /// Visits the program's root node, which consists of a list of statements.
    /// It iterates through each statement and processes it to generate LLVM IR.
    /// </summary>
    /// <param name="context">The parse tree context representing the program.</param>
    /// <returns>A default <see cref="LLVMValueRef"/>, as a program visit does not yield a value.</returns>
    public override LLVMValueRef VisitProgram(CobraParser.ProgramContext context)
    {
        EnterScope(); // Establish the global scope
        try
        {
            foreach (var child in context.children)
            {
                if (child is ITerminalNode) continue;
                Visit(child);
            }
        }
        finally
        {
            ExitScope(); // Clean up the global scope
        }

        return default;
    }

    /// <summary>
    /// Processes a statement in the parse tree by delegating to the appropriate handler
    /// based on the type of statement (declaration, assignment, control flow, etc.).
    /// </summary>
    /// <param name="context">The parse tree context representing the statement.</param>
    /// <returns>A value representing the result of visiting the statement, or a default value.</returns>
    public override LLVMValueRef VisitStatement(CobraParser.StatementContext context)
    {
        if (context.block() != null) return VisitBlock(context.block());
        if (context.declarationStatement() != null) return VisitDeclarationStatement(context.declarationStatement());
        if (context.ifStatement() != null) return VisitIfStatement(context.ifStatement());
        if (context.whileStatement() != null) return VisitWhileStatement(context.whileStatement());
        if (context.doWhileStatement() != null) return VisitDoWhileStatement(context.doWhileStatement());
        if (context.forStatement() != null) return VisitForStatement(context.forStatement());
        if (context.jumpStatement() != null) return VisitJumpStatement(context.jumpStatement());
        if (context.expressionStatement() != null) return VisitExpressionStatement(context.expressionStatement());

        // TODO: Add support for switchStatement
        return default;
    }

    /// <summary>
    /// Visits a block statement, which introduces a new lexical scope.
    /// </summary>
    /// <param name="context">The parse tree context for the block.</param>
    /// <returns>A default value, as a block itself does not produce a value.</returns>
    public override LLVMValueRef VisitBlock(CobraParser.BlockContext context)
    {
        EnterScope();
        try
        {
            foreach (var stmt in context.statement())
            {
                Visit(stmt);
            }
        }
        finally
        {
            ExitScope();
        }

        return default;
    }

    /// <summary>
    /// Visits an expression statement in the parse tree, evaluates the contained expression,
    /// and returns the corresponding LLVM value.
    /// </summary>
    /// <param name="context">The parse tree context representing the expression statement to visit and evaluate.</param>
    /// <returns>The <see cref="LLVMValueRef"/> resulting from evaluating the expression within the statement.</returns>
    public override LLVMValueRef VisitExpressionStatement(CobraParser.ExpressionStatementContext context)
    {
        return Visit(context.expression());
    }


    /// <summary>
    /// Processes a declaration statement, allocating memory for a variable in the current scope.
    /// </summary>
    /// <param name="context">The context of the declaration statement node in the parse tree.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the allocated variable.</returns>
    public override LLVMValueRef VisitDeclarationStatement(CobraParser.DeclarationStatementContext context)
    {
        var variableName = context.ID().GetText();
        var typeName = context.type().GetText();

        var varType = typeName switch
        {
            "int" => LLVMTypeRef.Int32,
            "float" => LLVMTypeRef.Float,
            "string" => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
            "bool" => LLVMTypeRef.Int1,
            "void" => LLVMTypeRef.Void,
            _ => throw new Exception($"Invalid type specified: {typeName}")
        };

        var allocatedValue = _builder.BuildAlloca(varType, variableName);
        DeclareVariable(variableName, allocatedValue);

        if (context.expression() != null)
        {
            var initialValue = Visit(context.expression());
            _builder.BuildStore(initialValue, allocatedValue);
            CobraLogger.RuntimeVariableValue(_builder, module, $"Declared variable: {variableName} <{typeName}>",
                initialValue);
        }

        CobraLogger.Success($"Compiled declaration for variable: {variableName} with type {typeName}");
        return allocatedValue;
    }

    /// <summary>
    /// Handles an assignment statement, storing the result of an expression into a variable.
    /// </summary>
    /// <param name="context">The context for the assignment statement.</param>
    /// <returns>The resulting value stored in the left-hand side variable.</returns>
    public override LLVMValueRef VisitAssignmentExpression(CobraParser.AssignmentExpressionContext context)
    {
        // If no assignment operator, just evaluate the conditional expression (non-assignment case)
        if (context.assignmentOperator() == null)
        {
            return Visit(context.conditionalExpression());
        }

        // Handle assignment
        var variableName = context.postfixExpression().GetText();
        // TODO: Add resolvers for member variables (e.g., obj.field) and array elements (e.g., arr[i]).
        var variableAddress = FindVariable(variableName);

        var rhs = Visit(context.assignmentExpression()); // Right-associative assignment
        var op = context.assignmentOperator().GetText();
        LLVMValueRef valueToStore;

        if (op == "=")
        {
            valueToStore = rhs;
        }
        else
        {
            var loaded = _builder.BuildLoad2(variableAddress.TypeOf.ElementType, variableAddress,
                $"{variableName}_current_val");
            valueToStore = op switch
            {
                "+=" => _builder.BuildAdd(loaded, rhs, $"{variableName}_add_assign"),
                "-=" => _builder.BuildSub(loaded, rhs, $"{variableName}_sub_assign"),
                "*=" => _builder.BuildMul(loaded, rhs, $"{variableName}_mul_assign"),
                "/=" => _builder.BuildSDiv(loaded, rhs, $"{variableName}_div_assign"),
                _ => throw new Exception($"Unsupported assignment operator: {op}")
            };
        }

        _builder.BuildStore(valueToStore, variableAddress);
        CobraLogger.Success($"Compiled assignment for variable: {variableName}");
        CobraLogger.RuntimeVariableValue(_builder, module, $"Assigned value to variable: {variableName}", valueToStore);

        return valueToStore;
    }

    // =============================================================================
    // Control Flow Visitors
    // =============================================================================

    /// <summary>
    /// Visits an if-else statement and generates the corresponding conditional branching logic.
    /// </summary>
    /// <param name="context">The parse tree context for the if statement.</param>
    /// <returns>A default value, as the statement itself, produces no value.</returns>
    public override LLVMValueRef VisitIfStatement(CobraParser.IfStatementContext context)
    {
        var condition = Visit(context.expression());
        var parentFunction = _builder.InsertBlock.Parent;

        var thenBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "if_then");
        var elseBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "if_else");
        var mergeBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "if_cont");

        var hasElse = context.ELSE() != null;
        _builder.BuildCondBr(condition, thenBlock, hasElse ? elseBlock : mergeBlock);

        // --- Then Block ---
        _builder.PositionAtEnd(thenBlock);
        Visit(context.statement(0));

        _builder.BuildBr(mergeBlock);

        // --- Else Block ---
        if (hasElse)
        {
            _builder.PositionAtEnd(elseBlock);
            Visit(context.statement(1));
            _builder.BuildBr(mergeBlock);
        }

        // --- Continue ---
        _builder.PositionAtEnd(mergeBlock);
        return default;
    }

    /// <summary>
    /// Visits a while loop and generates the corresponding loop structure.
    /// </summary>
    /// <param name="context">The parse tree context for the while statement.</param>
    /// <returns>A default value.</returns>
    public override LLVMValueRef VisitWhileStatement(CobraParser.WhileStatementContext context)
    {
        var parentFunction = _builder.InsertBlock.Parent;

        var condBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "while_cond");
        var loopBodyBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "while_body");
        var afterLoopBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "while_end");

        _loopContexts.Push((condBlock, afterLoopBlock));
        try
        {
            _builder.BuildBr(condBlock);

            _builder.PositionAtEnd(condBlock);
            var condition = Visit(context.expression());

            _builder.BuildCondBr(condition, loopBodyBlock, afterLoopBlock);

            _builder.PositionAtEnd(loopBodyBlock);
            Visit(context.statement());
            _builder.BuildBr(condBlock);

            _builder.PositionAtEnd(afterLoopBlock);
        }
        finally
        {
            _loopContexts.Pop();
        }

        return default;
    }

    /// <summary>
    /// Visits a do-while loop, ensuring the body executes at least once.
    /// </summary>
    /// <param name="context">The parse tree context for the do-while statement.</param>
    /// <returns>A default value.</returns>
    public override LLVMValueRef VisitDoWhileStatement(CobraParser.DoWhileStatementContext context)
    {
        var parentFunction = _builder.InsertBlock.Parent;

        var loopBodyBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "do_body");
        var condBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "do_cond");
        var afterLoopBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "do_end");

        _loopContexts.Push((condBlock, afterLoopBlock));
        try
        {
            _builder.BuildBr(loopBodyBlock);

            _builder.PositionAtEnd(loopBodyBlock);
            Visit(context.statement());
            _builder.BuildBr(condBlock);

            _builder.PositionAtEnd(condBlock);
            var condition = Visit(context.expression());
            _builder.BuildCondBr(condition, loopBodyBlock, afterLoopBlock);

            _builder.PositionAtEnd(afterLoopBlock);
        }
        finally
        {
            _loopContexts.Pop();
        }

        return default;
    }

    /// <summary>
    /// Visits a for loop, handling both C-style and for-each loops.
    /// </summary>
    /// <param name="context">The parse tree context for the for statement.</param>
    /// <returns>A default value.</returns>
    public override LLVMValueRef VisitForStatement(CobraParser.ForStatementContext context)
    {
        var forControl = context.forControl();

        if (forControl.declarationStatement() != null) // C-style for loop
        {
            var parentFunction = _builder.InsertBlock.Parent;
            var condBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "for_cond");
            var loopBodyBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "for_body");
            var incBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "for_inc");
            var afterLoopBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "for_end");

            EnterScope();
            try
            {
                _loopContexts.Push((incBlock, afterLoopBlock));
                try
                {
                    Visit(forControl.declarationStatement());
                    _builder.BuildBr(condBlock);

                    _builder.PositionAtEnd(condBlock);
                    if (forControl.expression(0) != null)
                    {
                        var condition = Visit(forControl.expression(0));
                        _builder.BuildCondBr(condition, loopBodyBlock, afterLoopBlock);
                    }
                    else
                    {
                        _builder.BuildBr(loopBodyBlock); // No condition = infinite loop
                    }

                    _builder.PositionAtEnd(loopBodyBlock);
                    Visit(context.statement());
                    _builder.BuildBr(incBlock);

                    _builder.PositionAtEnd(incBlock);
                    if (forControl.expression(1) != null)
                    {
                        Visit(forControl.expression(1));
                    }

                    _builder.BuildBr(condBlock);

                    _builder.PositionAtEnd(afterLoopBlock);
                }
                finally
                {
                    _loopContexts.Pop();
                }
            }
            finally
            {
                ExitScope();
            }
        }
        else // for-each loop
        {
            throw new NotImplementedException("For-each loops are not yet implemented.");
        }

        return default;
    }

    /// <summary>
    /// Visits a jump statement (break, continue, return).
    /// </summary>
    /// <param name="context">The parse tree context for the jump statement.</param>
    /// <returns>A default value.</returns>
    public override LLVMValueRef VisitJumpStatement(CobraParser.JumpStatementContext context)
    {
        if (context.BREAK() != null)
        {
            if (_loopContexts.Count == 0) throw new Exception("'break' statement not within a loop.");
            _builder.BuildBr(_loopContexts.Peek().BreakTarget);
        }
        else if (context.CONTINUE() != null)
        {
            if (_loopContexts.Count == 0) throw new Exception("'continue' statement not within a loop.");
            _builder.BuildBr(_loopContexts.Peek().ContinueTarget);
        }
        else if (context.RETURN() != null)
        {
            // TODO: Implement return when functions are added.
            throw new NotImplementedException("Return statements are not yet implemented.");
        }

        return default;
    }

    // =============================================================================
    // Expression Visitors
    // =============================================================================

    /// <summary>
    /// Visits the given expression context in the parse tree and evaluates it by visiting the associated conditional expression.
    /// This method acts as a dispatcher for the expression hierarchy.
    /// </summary>
    /// <param name="context">The expression context to visit, which represents an expression node in the parse tree.</param>
    /// <returns>The LLVM value resulting from the evaluation of the visited expression context.</returns>
    public override LLVMValueRef VisitExpression(CobraParser.ExpressionContext context)
    {
        return Visit(context.assignmentExpression());
    }

    /// <summary>
    /// Visits a conditional expression in the parse tree and evaluates it.
    /// Handles the ternary conditional operator by visiting and generating LLVM IR
    /// for the condition, true branch, and false branch, and then combining their results using a Phi node.
    /// </summary>
    /// <param name="context">The parse tree context representing a conditional expression.</param>
    /// <returns>A <see cref="LLVMValueRef"/> representing the result of the evaluated conditional expression.</returns>
    public override LLVMValueRef VisitConditionalExpression(CobraParser.ConditionalExpressionContext context)
    {
        if (context.QUESTION_MARK() == null)
        {
            return Visit(context.logicalOrExpression());
        }

        var cond = Visit(context.logicalOrExpression());
        var parentFunction = _builder.InsertBlock.Parent;

        var trueBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "ternary_true");
        var falseBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "ternary_false");
        var endBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "ternary_end");

        _builder.BuildCondBr(cond, trueBlock, falseBlock);

        _builder.PositionAtEnd(trueBlock);
        var trueVal = Visit(context.expression(0));
        var trueEndBlock = _builder.InsertBlock;
        _builder.BuildBr(endBlock);

        _builder.PositionAtEnd(falseBlock);
        var falseVal = Visit(context.expression(1));
        var falseEndBlock = _builder.InsertBlock;
        _builder.BuildBr(endBlock);

        _builder.PositionAtEnd(endBlock);
        var phi = _builder.BuildPhi(trueVal.TypeOf, "ternary_result");
        phi.AddIncoming(new[] { trueVal, falseVal }, new[] { trueEndBlock, falseEndBlock }, 2);
        return phi;
    }

    /// <summary>
    /// Visits a logical OR expression in the parse tree and evaluates it using short-circuiting logic.
    /// </summary>
    /// <param name="context">The parse tree context for the logical OR expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the evaluated result of the logical OR expression.</returns>
    public override LLVMValueRef VisitLogicalOrExpression(CobraParser.LogicalOrExpressionContext context)
    {
        return VisitLogicalOrHelper(context, 0);
    }

    /// <summary>
    /// Recursively evaluates a logical OR expression by processing each operand and combining their results.
    /// Uses short-circuit evaluation by creating new basic blocks for the right-hand side and the end.
    /// </summary>
    /// <param name="context">The context of the logical OR expression being evaluated.</param>
    /// <param name="index">The current index of the logical AND expression within the logical OR expression.</param>
    /// <returns>The LLVM representation of the result of the logical OR evaluation.</returns>
    private LLVMValueRef VisitLogicalOrHelper(CobraParser.LogicalOrExpressionContext context, int index)
    {
        var left = Visit(context.logicalAndExpression(index));
        if (index == context.logicalAndExpression().Length - 1)
        {
            return left;
        }

        var parentFunction = _builder.InsertBlock.Parent;
        var rightBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "or_rhs");
        var endBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "or_end");

        var currentBlock = _builder.InsertBlock;
        _builder.BuildCondBr(left, endBlock, rightBlock);

        _builder.PositionAtEnd(rightBlock);
        var right = VisitLogicalOrHelper(context, index + 1);
        var rightEndBlock = _builder.InsertBlock;
        _builder.BuildBr(endBlock);

        _builder.PositionAtEnd(endBlock);
        var phi = _builder.BuildPhi(LLVMTypeRef.Int1, "or_result");
        phi.AddIncoming(new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1), right },
            new[] { currentBlock, rightEndBlock }, 2);

        return phi;
    }

    /// <summary>
    /// Visits a logical AND expression node in the parse tree and evaluates it using short-circuiting logic.
    /// </summary>
    /// <param name="context">The context of the logical AND expression node to be visited.</param>
    /// <returns>The evaluated result of the logical AND expression as an <see cref="LLVMValueRef"/>.</returns>
    public override LLVMValueRef VisitLogicalAndExpression(CobraParser.LogicalAndExpressionContext context)
    {
        return VisitLogicalAndHelper(context, 0);
    }

    /// <summary>
    /// Evaluates a logical "AND" expression by recursively evaluating the sub-expressions
    /// and applying short-circuiting logic to determine the result.
    /// </summary>
    /// <param name="context">The parser context representing the logical "AND" expression.</param>
    /// <param name="index">The current index of the sub-expression being evaluated within the context.</param>
    /// <returns>The LLVM value representing the evaluated result of the logical "AND" expression.</returns>
    private LLVMValueRef VisitLogicalAndHelper(CobraParser.LogicalAndExpressionContext context, int index)
    {
        var left = Visit(context.bitwiseOrExpression(index));
        if (index == context.bitwiseOrExpression().Length - 1)
        {
            return left;
        }

        var parentFunction = _builder.InsertBlock.Parent;
        var rightBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "and_rhs");
        var endBlock = LLVMBasicBlockRef.AppendInContext(module.Context, parentFunction, "and_end");

        var currentBlock = _builder.InsertBlock;
        _builder.BuildCondBr(left, rightBlock, endBlock);

        _builder.PositionAtEnd(rightBlock);
        var right = VisitLogicalAndHelper(context, index + 1);
        var rightEndBlock = _builder.InsertBlock;
        _builder.BuildBr(endBlock);

        _builder.PositionAtEnd(endBlock);
        var phi = _builder.BuildPhi(LLVMTypeRef.Int1, "and_result");
        phi.AddIncoming(new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 0), right },
            new[] { currentBlock, rightEndBlock }, 2);
        return phi;
    }

    /// <summary>
    /// Visits a bitwise OR expression node in the abstract syntax tree (AST) and generates corresponding LLVM IR code.
    /// Combines the left-hand and right-hand sides of each bitwise OR operation using the LLVM OR instruction.
    /// </summary>
    /// <param name="context">The parser context for the bitwise OR expression node.</param>
    /// <returns>A <see cref="LLVMValueRef"/> representing the result of the bitwise OR operation in LLVM IR.</returns>
    public override LLVMValueRef VisitBitwiseOrExpression(CobraParser.BitwiseOrExpressionContext context)
    {
        var left = Visit(context.bitwiseXorExpression(0));
        for (var i = 1; i < context.bitwiseXorExpression().Length; i++)
        {
            var right = Visit(context.bitwiseXorExpression(i));
            left = _builder.BuildOr(left, right, "bitwise_or");
        }

        return left;
    }

    /// <summary>
    /// Visits a bitwise XOR expression node and generates LLVM IR for it.
    /// </summary>
    /// <param name="context">The parse tree context for the bitwise XOR expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the result of the bitwise XOR operation.</returns>
    public override LLVMValueRef VisitBitwiseXorExpression(CobraParser.BitwiseXorExpressionContext context)
    {
        var left = Visit(context.bitwiseAndExpression(0));
        for (var i = 1; i < context.bitwiseAndExpression().Length; i++)
        {
            var right = Visit(context.bitwiseAndExpression(i));
            left = _builder.BuildXor(left, right, "bitwise_xor");
        }

        return left;
    }

    /// <summary>
    /// Visits a bitwise AND expression and generates LLVM IR for it.
    /// </summary>
    /// <param name="context">The parse tree context for the bitwise AND expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the result of the bitwise AND operation.</returns>
    public override LLVMValueRef VisitBitwiseAndExpression(CobraParser.BitwiseAndExpressionContext context)
    {
        var left = Visit(context.equalityExpression(0));
        for (var i = 1; i < context.equalityExpression().Length; i++)
        {
            var right = Visit(context.equalityExpression(i));
            left = _builder.BuildAnd(left, right, "bitwise_and");
        }

        return left;
    }

    /// <summary>
    /// Visits an equality expression (e.g., `==`, `!=`) and generates LLVM IR.
    /// </summary>
    /// <param name="context">The parse tree context for the equality expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the boolean result of the equality comparison.</returns>
    public override LLVMValueRef VisitEqualityExpression(CobraParser.EqualityExpressionContext context)
    {
        var left = Visit(context.comparisonExpression(0));
        for (var i = 1; i < context.comparisonExpression().Length; i++)
        {
            var op = context.GetChild(2 * i - 1).GetText();
            var right = Visit(context.comparisonExpression(i));
            var pred = op == "==" ? LLVMIntPredicate.LLVMIntEQ : LLVMIntPredicate.LLVMIntNE;
            left = _builder.BuildICmp(pred, left, right, "equality_cmp");
        }

        return left;
    }

    /// <summary>
    /// Visits a comparison expression (e.g., `&gt;`, `&lt;`, `&gt;=`, `&lt;=`) and generates LLVM IR.
    /// </summary>
    /// <param name="context">The parse tree context for the comparison expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the boolean result of the comparison.</returns>
    /// <exception cref="Exception">Thrown if an invalid comparison operator is found.</exception>
    public override LLVMValueRef VisitComparisonExpression(CobraParser.ComparisonExpressionContext context)
    {
        var left = Visit(context.bitwiseShiftExpression(0));
        for (var i = 1; i < context.bitwiseShiftExpression().Length; i++)
        {
            var op = context.GetChild(2 * i - 1).GetText();
            var right = Visit(context.bitwiseShiftExpression(i));
            var pred = op switch
            {
                ">" => LLVMIntPredicate.LLVMIntSGT,
                "<" => LLVMIntPredicate.LLVMIntSLT,
                ">=" => LLVMIntPredicate.LLVMIntSGE,
                "<=" => LLVMIntPredicate.LLVMIntSLE,
                _ => throw new Exception($"Invalid comparison op: {op}")
            };
            left = _builder.BuildICmp(pred, left, right, "comparison_cmp");
        }

        return left;
    }

    /// <summary>
    /// Visits a bitwise shift expression (e.g., `&lt;&lt;`, `&gt;&gt;`) and generates LLVM IR.
    /// </summary>
    /// <param name="context">The parse tree context for the bitwise shift expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the result of the bitwise shift operation.</returns>
    public override LLVMValueRef VisitBitwiseShiftExpression(CobraParser.BitwiseShiftExpressionContext context)
    {
        LLVMValueRef left = Visit(context.additiveExpression(0));
        for (int i = 1; i < context.additiveExpression().Length; i++)
        {
            string op = context.GetChild(2 * i - 1).GetText();
            LLVMValueRef right = Visit(context.additiveExpression(i));
            left = op == "<<"
                ? _builder.BuildShl(left, right, "shift_left")
                : _builder.BuildAShr(left, right, "shift_right");
        }

        return left;
    }

    /// <summary>
    /// Visits an additive expression (e.g., `+`, `-`) and generates LLVM IR.
    /// </summary>
    /// <param name="context">The parse tree context for the additive expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the result of the addition or subtraction.</returns>
    public override LLVMValueRef VisitAdditiveExpression(CobraParser.AdditiveExpressionContext context)
    {
        LLVMValueRef left = Visit(context.multiplicativeExpression(0));
        for (int i = 1; i < context.multiplicativeExpression().Length; i++)
        {
            string op = context.GetChild(2 * i - 1).GetText();
            LLVMValueRef right = Visit(context.multiplicativeExpression(i));
            left = op == "+" ? _builder.BuildAdd(left, right, "add") : _builder.BuildSub(left, right, "sub");
        }

        return left;
    }

    /// <summary>
    /// Visits a multiplicative expression (e.g., `*`, `/`, `%`) and generates LLVM IR.
    /// </summary>
    /// <param name="context">The parse tree context for the multiplicative expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the result of the multiplication, division, or remainder operation.</returns>
    /// <exception cref="Exception">Thrown if an invalid multiplicative operator is found.</exception>
    public override LLVMValueRef VisitMultiplicativeExpression(CobraParser.MultiplicativeExpressionContext context)
    {
        var left = Visit(context.unaryExpression(0));
        for (var i = 1; i < context.unaryExpression().Length; i++)
        {
            var op = context.GetChild(2 * i - 1).GetText();
            var right = Visit(context.unaryExpression(i));
            left = op switch
            {
                "*" => _builder.BuildMul(left, right, "mul"),
                "/" => _builder.BuildSDiv(left, right, "div"),
                "%" => _builder.BuildSRem(left, right, "rem"),
                _ => throw new Exception($"Invalid mul op: {op}")
            };
        }

        return left;
    }

    /// <summary>
    /// Visits a unary expression (e.g., `+`, `-`, `!`, `~`, `++`, `--`) and generates LLVM IR.
    /// Handles prefix increment/decrement and logical/bitwise NOT.
    /// </summary>
    /// <param name="context">The parse tree context for the unary expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the result of the unary operation.</returns>
    /// <exception cref="Exception">Thrown for invalid unary operators or an invalid lvalue for increment/decrement.</exception>
    public override LLVMValueRef VisitUnaryExpression(CobraParser.UnaryExpressionContext context)
    {
        if (context.postfixExpression() != null)
        {
            return Visit(context.postfixExpression());
        }

        var op = context.GetChild(0).GetText();
        var operand = Visit(context.unaryExpression());

        if (op == "+") return operand;
        if (op == "-") return _builder.BuildNeg(operand, "neg");
        if (op == "!") return _builder.BuildNot(operand, "logical_not");
        if (op == "~") return _builder.BuildNot(operand, "bitwise_not");

        if (op != "++" && op != "--") throw new Exception($"Invalid unary op: {op}");

        string varName = context.unaryExpression().postfixExpression()?.primary()?.ID()?.GetText()
                         ?? throw new Exception("Invalid lvalue for prefix inc/dec");
        var addr = FindVariable(varName);
        var oldVal = _builder.BuildLoad2(addr.TypeOf.ElementType, addr, varName);
        var newVal = op == "++"
            ? _builder.BuildAdd(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "pre_inc")
            : _builder.BuildSub(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "pre_dec");
        _builder.BuildStore(newVal, addr);
        return newVal;
    }

    /// <summary>
    /// Visits a postfix expression (e.g., `++`, `--`) and generates LLVM IR.
    /// Handles postfix increment/decrement operations.
    /// </summary>
    /// <param name="context">The parse tree context for the postfix expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the result of the postfix operation.</returns>
    /// <exception cref="Exception">Thrown if an invalid lvalue is used for increment/decrement.</exception>
    public override LLVMValueRef VisitPostfixExpression(CobraParser.PostfixExpressionContext context)
    {
        var value = Visit(context.primary());
        for (var i = 1; i < context.ChildCount; i++) // Start at 1 to skip primary
        {
            var op = context.GetChild(i).GetText();
            if (op is "++" or "--")
            {
                var varName = context.primary().ID()?.GetText() ??
                              throw new Exception("Invalid lvalue for postfix inc/dec");
                var addr = FindVariable(varName);
                var oldVal = value;
                var newVal = op == "++"
                    ? _builder.BuildAdd(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "post_inc")
                    : _builder.BuildSub(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "post_dec");
                _builder.BuildStore(newVal, addr);
                value = oldVal; // Postfix returns the old value
            }
            // TODO: Handle other postfix expressions like function calls, member access, etc.
        }

        return value;
    }

    /// <summary>
    /// Visits a primary expression, which is the most basic building block of expressions.
    /// It handles literals (integer, float, boolean, string, null) and variable IDs.
    /// </summary>
    /// <param name="context">The parse tree context for the primary expression.</param>
    /// <returns>An <see cref="LLVMValueRef"/> representing the evaluated primary expression.</returns>
    /// <exception cref="Exception">Thrown if an unsupported primary expression or an undeclared variable is encountered.</exception>
    public override LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context)
    {
        if (context.expression() != null)
        {
            return Visit(context.expression());
        }

        if (context.literal() != null)
        {
            var literal = context.literal();
            if (literal.INTEGER() != null)
            {
                var value = int.Parse(literal.INTEGER().GetText());
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)value);
            }

            if (literal.FLOAT_LITERAL() != null)
            {
                var value = double.Parse(literal.FLOAT_LITERAL().GetText());
                return LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, value);
            }

            if (literal.BOOLEAN_LITERAL() != null)
            {
                var value = literal.BOOLEAN_LITERAL().GetText() == "true";
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, (ulong)(value ? 1 : 0));
            }

            if (literal.STRING_LITERAL() != null)
            {
                var rawString = literal.STRING_LITERAL().GetText();
                var unquotedString = rawString.Substring(1, rawString.Length - 2);
                var finalString = Regex.Unescape(unquotedString);
                return _builder.BuildGlobalStringPtr(finalString, ".str");
            }

            if (literal.NULL() != null)
            {
                return LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
            }
        }

        if (context.ID() != null)
        {
            var variableName = context.ID().GetText();
            var varRef = FindVariable(variableName);
            return _builder.BuildLoad2(varRef.TypeOf.ElementType, varRef, variableName);
        }

        throw new Exception("Unsupported primary expression");
    }
}