using Antlr4.Runtime.Tree;
using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraStatementVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private LLVMModuleRef _module;
    private LLVMBuilderRef _builder;

    internal CobraStatementVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _builder = mainVisitor.Builder;
        _module = mainVisitor.Module;
    }

    public LLVMValueRef VisitProgram(CobraParser.ProgramContext context)
    {
        _visitor.ScopeManagement.EnterScope();
        try
        {
            foreach (var child in context.children)
            {
                if (child is ITerminalNode) continue;
                if (child is CobraParser.FunctionDeclarationContext) continue;
                if (child is CobraParser.DeclarationStatementContext decl && decl.GLOBAL() != null) continue;
                _visitor.Visit(child);
            }
        }
        finally
        {
            _visitor.ScopeManagement.ExitScope();
        }

        return default;
    }


    public LLVMValueRef VisitStatement(CobraParser.StatementContext context)
    {
        if (context.block() != null) return _visitor.VisitBlock(context.block());
        if (context.declarationStatement() != null)
            return _visitor.VisitDeclarationStatement(context.declarationStatement());
        if (context.ifStatement() != null) return _visitor.VisitIfStatement(context.ifStatement());
        if (context.whileStatement() != null) return _visitor.VisitWhileStatement(context.whileStatement());
        if (context.doWhileStatement() != null) return _visitor.VisitDoWhileStatement(context.doWhileStatement());
        if (context.forStatement() != null) return _visitor.VisitForStatement(context.forStatement());
        if (context.jumpStatement() != null) return _visitor.VisitJumpStatement(context.jumpStatement());
        if (context.expressionStatement() != null)
            return _visitor.VisitExpressionStatement(context.expressionStatement());

        // TODO: Add support for switchStatement
        return default;
    }

    public LLVMValueRef VisitBlock(CobraParser.BlockContext context)
    {
        _visitor.ScopeManagement.EnterScope();
        try
        {
            foreach (var stmt in context.statement())
            {
                _visitor.Visit(stmt);
            }
        }
        finally
        {
            _visitor.ScopeManagement.ExitScope();
        }

        return default;
    }

    public LLVMValueRef VisitExpressionStatement(CobraParser.ExpressionStatementContext context)
    {
        return _visitor.Visit(context.expression());
    }

    public LLVMValueRef VisitDeclarationStatement(CobraParser.DeclarationStatementContext context)
    {
        var variableName = context.ID().GetText();
        var typeName = context.type().GetText();

        var varType = CobraTypeResolver.ResolveType(context.type());

        bool isGlobal = context.GLOBAL() != null;
        if (_visitor.IsGlobalScope && isGlobal)
        {
            if (typeName == "void") throw new Exception("Variables cannot have type 'void'");
            var g = _module.AddGlobal(varType, variableName);

            if (context.expression() != null)
            {
                var init = _visitor.Visit(context.expression());
                if (!init.IsConstant)
                    throw new Exception($"Global '{variableName}' requires a constant initializer");

                g.Initializer = init;
            }
            else
            {
                g.Initializer = varType.Kind switch
                {
                    LLVMTypeKind.LLVMIntegerTypeKind => LLVMValueRef.CreateConstInt(varType, 0, false),
                    LLVMTypeKind.LLVMFloatTypeKind => LLVMValueRef.CreateConstReal(varType, 0.0),
                    LLVMTypeKind.LLVMPointerTypeKind => LLVMValueRef.CreateConstNull(varType),
                    _ => throw new Exception($"Unsupported global type for variable '{variableName}'")
                };
            }

            _visitor.ScopeManagement.DeclareVariable(variableName, g, isGlobal: true);
            CobraLogger.Success($"Declared global variable: {variableName} <{typeName}>");
            return g;
        }

        // Local variable
        var allocatedVariable = _builder.BuildAlloca(varType, variableName);
        _visitor.ScopeManagement.DeclareVariable(variableName, allocatedVariable);

        if (context.expression() != null)
        {
            var initialValue = _visitor.Visit(context.expression());

            // Validate type: if varType is pointer (like int[]), RHS must also be pointer
            if (varType.Kind == LLVMTypeKind.LLVMPointerTypeKind &&
                initialValue.TypeOf.Kind != LLVMTypeKind.LLVMPointerTypeKind)
            {
                throw new Exception($"Type mismatch: cannot assign non-pointer value '{initialValue}' to pointer '{variableName}'");
            }

            _builder.BuildStore(initialValue, allocatedVariable);

            CobraLogger.RuntimeVariableValue(_builder, _visitor.Module,
                $"Declared variable: {variableName} <{typeName}>",
                initialValue);
        }
        else
        {
            // Default init
            LLVMValueRef defaultValue = varType.Kind switch
            {
                LLVMTypeKind.LLVMIntegerTypeKind => LLVMValueRef.CreateConstInt(varType, 0, false),
                LLVMTypeKind.LLVMFloatTypeKind => LLVMValueRef.CreateConstReal(varType, 0.0),
                LLVMTypeKind.LLVMPointerTypeKind => LLVMValueRef.CreateConstNull(varType),
                _ => throw new Exception($"Unsupported local type for variable '{variableName}'")
            };
            _builder.BuildStore(defaultValue, allocatedVariable);
        }

        CobraLogger.Success($"Compiled local variable: {variableName} <{typeName}>");
        return allocatedVariable;
    }
    
    

    public LLVMValueRef VisitIfStatement(CobraParser.IfStatementContext context)
    {
        var condition = ToI1(_visitor.Visit(context.expression()));
        var parentFunction = _builder.InsertBlock.Parent;

        var thenBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "if_then");
        var elseBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "if_else");
        var mergeBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "if_cont");

        var hasElse = context.ELSE() != null;
        _builder.BuildCondBr(condition, thenBlock, hasElse ? elseBlock : mergeBlock);

        _builder.PositionAtEnd(thenBlock);
        _visitor.Visit(context.statement(0));
        if (_builder.InsertBlock.Terminator == default)
        {
            _builder.BuildBr(mergeBlock);
        }

        if (hasElse)
        {
            _builder.PositionAtEnd(elseBlock);
            _visitor.Visit(context.statement(1));
            if (_builder.InsertBlock.Terminator == default)
            {
                _builder.BuildBr(mergeBlock);
            }
        }

        _builder.PositionAtEnd(mergeBlock);
        return default;
    }

    public LLVMValueRef VisitWhileStatement(CobraParser.WhileStatementContext context)
    {
        var parentFunction = _builder.InsertBlock.Parent;

        var condBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "while_cond");
        var loopBodyBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "while_body");
        var afterLoopBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "while_end");

        _visitor.LoopContexts.Push((condBlock, afterLoopBlock));
        try
        {
            _builder.BuildBr(condBlock);
            _builder.PositionAtEnd(condBlock);
            var condition = ToI1(_visitor.Visit(context.expression()));
            _builder.BuildCondBr(condition, loopBodyBlock, afterLoopBlock);
            _builder.PositionAtEnd(loopBodyBlock);
            _visitor.Visit(context.statement());
            if (_builder.InsertBlock.Terminator == default)
            {
                _builder.BuildBr(condBlock);
            }

            _builder.PositionAtEnd(afterLoopBlock);
        }
        finally
        {
            _visitor.LoopContexts.Pop();
        }

        return default;
    }

    public LLVMValueRef VisitDoWhileStatement(CobraParser.DoWhileStatementContext context)
    {
        var parentFunction = _builder.InsertBlock.Parent;

        var loopBodyBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "do_body");
        var condBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "do_cond");
        var afterLoopBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "do_end");

        _visitor.LoopContexts.Push((condBlock, afterLoopBlock));
        try
        {
            _builder.BuildBr(loopBodyBlock);

            _builder.PositionAtEnd(loopBodyBlock);
            _visitor.Visit(context.statement());
            if (_builder.InsertBlock.Terminator == default)
            {
                _builder.BuildBr(condBlock);
            }

            _builder.PositionAtEnd(condBlock);
            var condition = ToI1(_visitor.Visit(context.expression()));
            _builder.BuildCondBr(condition, loopBodyBlock, afterLoopBlock);

            _builder.PositionAtEnd(afterLoopBlock);
        }
        finally
        {
            _visitor.LoopContexts.Pop();
        }

        return default;
    }

    public LLVMValueRef VisitForStatement(CobraParser.ForStatementContext context)
    {
        var forControl = context.forControl();
        if (forControl.declarationStatement() != null)
        {
            var parentFunction = _builder.InsertBlock.Parent;
            var condBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "for_cond");
            var loopBodyBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "for_body");
            var incBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "for_inc");
            var afterLoopBlock = LLVMBasicBlockRef.AppendInContext(_visitor.Module.Context, parentFunction, "for_end");

            _visitor.ScopeManagement.EnterScope();
            try
            {
                _visitor.LoopContexts.Push((incBlock, afterLoopBlock));
                try
                {
                    _visitor.Visit(forControl.declarationStatement());
                    _builder.BuildBr(condBlock);

                    _builder.PositionAtEnd(condBlock);
                    var condition = forControl.expression(0) != null
                        ? ToI1(_visitor.Visit(forControl.expression(0)))
                        : LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1);
                    _builder.BuildCondBr(condition, loopBodyBlock, afterLoopBlock);

                    _builder.PositionAtEnd(loopBodyBlock);
                    _visitor.Visit(context.statement());
                    if (_builder.InsertBlock.Terminator == default)
                    {
                        _builder.BuildBr(incBlock);
                    }

                    _builder.PositionAtEnd(incBlock);
                    if (forControl.expression(1) != null)
                    {
                        _visitor.Visit(forControl.expression(1));
                    }

                    _builder.BuildBr(condBlock);

                    _builder.PositionAtEnd(afterLoopBlock);
                }
                finally
                {
                    _visitor.LoopContexts.Pop();
                }
            }
            finally
            {
                _visitor.ScopeManagement.ExitScope();
            }
        }
        else throw new NotImplementedException("For-each loops are not yet implemented.");

        return default;
    }

    public LLVMValueRef VisitJumpStatement(CobraParser.JumpStatementContext context)
    {
        if (context.BREAK() != null)
        {
            if (_visitor.LoopContexts.Count == 0) throw new Exception("'break' statement not within a loop.");
            _builder.BuildBr(_visitor.LoopContexts.Peek().BreakTarget);
        }
        else if (context.CONTINUE() != null)
        {
            if (_visitor.LoopContexts.Count == 0) throw new Exception("'continue' statement not within a loop.");
            _builder.BuildBr(_visitor.LoopContexts.Peek().ContinueTarget);
        }
        else if (context.RETURN() != null)
        {
            if (_visitor.CurrentFunction == default)
            {
                throw new Exception("'return' statement not within a function.");
            }

            if (context.expression() != null)
            {
                var returnValue = _visitor.Visit(context.expression());
                _builder.BuildRet(returnValue);
            }
            else
            {
                _builder.BuildRetVoid();
            }
        }

        return default;
    }


    private LLVMValueRef ToI1(LLVMValueRef v)
    {
        var k = v.TypeOf.Kind;

        if (k == LLVMTypeKind.LLVMIntegerTypeKind && v.TypeOf.IntWidth == 1) return v;

        if (k == LLVMTypeKind.LLVMIntegerTypeKind)
            return _builder.BuildICmp(
                LLVMIntPredicate.LLVMIntNE,
                v,
                LLVMValueRef.CreateConstInt(v.TypeOf, 0),
                "truthy");

        if (k == LLVMTypeKind.LLVMFloatTypeKind || k == LLVMTypeKind.LLVMDoubleTypeKind)
            return _builder.BuildFCmp(
                LLVMRealPredicate.LLVMRealONE,
                v,
                LLVMValueRef.CreateConstReal(v.TypeOf, 0.0),
                "truthy");

        throw new Exception("Condition must be scalar (int/float/bool).");
    }
}