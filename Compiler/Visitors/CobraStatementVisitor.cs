using Antlr4.Runtime.Tree;
using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraStatementVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private LLVMBuilderRef _builder;

    internal CobraStatementVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _builder = mainVisitor.Builder;
    }

    public LLVMValueRef VisitProgram(CobraParser.ProgramContext context)
    {
        _visitor.ScopeManagement.EnterScope();
        try
        {
            foreach (var child in context.children)
            {
                if (child is ITerminalNode) continue;
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
        _visitor.ScopeManagement.DeclareVariable(variableName, allocatedValue);

        if (context.expression() != null)
        {
            var initialValue = _visitor.Visit(context.expression());
            _builder.BuildStore(initialValue, allocatedValue);
            CobraLogger.RuntimeVariableValue(_builder, _visitor.Module,
                $"Declared variable: {variableName} <{typeName}>",
                initialValue);
        }

        CobraLogger.Success($"Compiled declaration for variable: {variableName} with type {typeName}");
        return allocatedValue;
    }

    public LLVMValueRef VisitIfStatement(CobraParser.IfStatementContext context)
    {
        var condition = _visitor.Visit(context.expression());
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
            var condition = _visitor.Visit(context.expression());
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
            var condition = _visitor.Visit(context.expression());
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
                        ? _visitor.Visit(forControl.expression(0))
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
            throw new NotImplementedException("Return statements are not yet implemented.");
        }

        return default;
    }
}