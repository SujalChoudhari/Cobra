namespace Cobra.Interpreter;

using Antlr4.Runtime.Misc;
using LLVMSharp.Interop;
using System.Collections.Generic;
using System;

public class CobraCompiler : CobraParserBaseVisitor<LLVMValueRef>
{
    private readonly LLVMContextRef _context;
    private readonly LLVMModuleRef _module;
    private readonly LLVMBuilderRef _builder;
    private readonly Dictionary<string, LLVMValueRef> _namedValues;

    public LLVMModuleRef Module => _module;
    public LLVMBuilderRef Builder => _builder;

    public CobraCompiler()
    {
        Console.WriteLine("Initializing CobraCompiler...");
        _context = LLVMContextRef.Create();
        _module = _context.CreateModuleWithName("CobraModule");
        _builder = _context.CreateBuilder();
        _namedValues = new Dictionary<string, LLVMValueRef>();
        Console.WriteLine("CobraCompiler initialized.");
    }

    private LLVMTypeRef GetLLVMType(string typeName)
    {
        Console.WriteLine($"Getting LLVM type for: {typeName}");
        return typeName switch
        {
            "int" => LLVMTypeRef.Int32,
            "float" => LLVMTypeRef.Float,
            "bool" => LLVMTypeRef.Int1,
            "string" => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
            "void" => LLVMTypeRef.Void,
            _ => throw new System.Exception($"Unknown type: {typeName}"),
        };
    }

    public override LLVMValueRef VisitProgram(CobraParser.ProgramContext context)
    {
        Console.WriteLine("Entering VisitProgram...");
        // Define an external 'puts' function for our 'print' statement
        LLVMTypeRef[] putsParams = { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) };
        LLVMTypeRef putsFuncType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, putsParams, false);
        _module.AddFunction("puts", putsFuncType);
        Console.WriteLine("Defined puts function.");

        // Visit all statements in the program
        foreach (var statement in context.statement())
        {
            if (statement != null)
            {
                Console.WriteLine($"Visiting statement: {statement.GetText()}");
                Visit(statement);
            }
        }

        Console.WriteLine("Exiting VisitProgram...");
        return CompilerTypeHelper.GetNull();
    }

    public override LLVMValueRef VisitFunctionDeclaration([NotNull] CobraParser.FunctionDeclarationContext context)
    {
        string funcName = context.ID()?.GetText() ?? throw new System.Exception("Function name is null");
        Console.WriteLine($"Entering VisitFunctionDeclaration: {funcName}");
        LLVMTypeRef returnType = GetLLVMType(context.type()?.GetText() ?? throw new System.Exception("Return type is null"));

        // Handle parameters (none in this example)
        LLVMTypeRef[] paramTypes = { };
        LLVMTypeRef funcType = LLVMTypeRef.CreateFunction(returnType, paramTypes, false);
        LLVMValueRef function = _module.AddFunction(funcName, funcType);

        // Create the entry block for the function
        LLVMBasicBlockRef entryBlock = function.AppendBasicBlock("entry");
        _builder.PositionAtEnd(entryBlock);

        // Visit the function's body
        if (context.block() != null)
        {
            Visit(context.block());
        }

        // Add a default return if none is provided
        if (returnType != LLVMTypeRef.Void && _builder.InsertBlock.Terminator == null)
        {
            _builder.BuildRet(LLVMValueRef.CreateConstInt(returnType, 0));
        }
        else if (_builder.InsertBlock.Terminator == null)
        {
            _builder.BuildRetVoid();
        }

        Console.WriteLine($"Exiting VisitFunctionDeclaration: {funcName}");
        return function;
    }

    public override LLVMValueRef VisitBlock([NotNull] CobraParser.BlockContext context)
    {
        Console.WriteLine("Entering VisitBlock...");
        foreach (var statement in context.statement())
        {
            if (statement != null)
            {
                Console.WriteLine($"Visiting block statement: {statement.GetText()}");
                Visit(statement);
            }
        }

        Console.WriteLine("Exiting VisitBlock...");
        return CompilerTypeHelper.GetNull();
    }

    public override LLVMValueRef VisitDeclarationStatement(CobraParser.DeclarationStatementContext context)
    {
        string varName = context.ID()?.GetText() ?? throw new Exception("Variable name is null");
        Console.WriteLine($"Entering VisitDeclarationStatement: {varName}");
        LLVMTypeRef type = GetLLVMType(context.type()?.GetText() ?? throw new System.Exception("Type is null"));
        LLVMValueRef alloca = _builder.BuildAlloca(type, varName);
        _namedValues[varName] = alloca;

        if (context.expression() != null)
        {
            LLVMValueRef value = Visit(context.expression());
            _builder.BuildStore(value, alloca);
        }

        Console.WriteLine($"Exiting VisitDeclarationStatement: {varName}");
        return alloca;
    }

    public override LLVMValueRef VisitAssignmentStatement(CobraParser.AssignmentStatementContext context)
    {
        string varName = context.ID()?.GetText() ?? throw new System.Exception("Variable name is null");
        Console.WriteLine($"Entering VisitAssignmentStatement: {varName}");
        if (!_namedValues.TryGetValue(varName, out LLVMValueRef variable))
        {
            throw new System.Exception($"Unknown variable: {varName}");
        }
        LLVMValueRef value = Visit(context.expression() ?? throw new System.Exception("Assignment expression is null"));
        _builder.BuildStore(value, variable);
        Console.WriteLine($"Exiting VisitAssignmentStatement: {varName}");
        return value;
    }

    public override LLVMValueRef VisitExpressionStatement(CobraParser.ExpressionStatementContext context)
    {
        Console.WriteLine("Entering VisitExpressionStatement...");
        LLVMValueRef result = Visit(context.expression() ?? throw new System.Exception("Expression is null"));
        Console.WriteLine("Exiting VisitExpressionStatement...");
        return result;
    }

    public override LLVMValueRef VisitFunctionCallStatement([NotNull] CobraParser.FunctionCallStatementContext context)
    {
        Console.WriteLine("Entering VisitFunctionCallStatement...");
        LLVMValueRef result = Visit(context.functionCall() ?? throw new System.Exception("Function call is null"));
        Console.WriteLine("Exiting VisitFunctionCallStatement...");
        return result;
    }

    public override LLVMValueRef VisitIfStatement([NotNull] CobraParser.IfStatementContext context)
    {
        Console.WriteLine("Entering VisitIfStatement...");
        LLVMValueRef cond = Visit(context.expression() ?? throw new System.Exception("If condition is null"));
        LLVMValueRef func = _builder.InsertBlock.Parent;

        LLVMBasicBlockRef thenBlock = func.AppendBasicBlock("then");
        LLVMBasicBlockRef elseBlock = func.AppendBasicBlock("else");
        LLVMBasicBlockRef mergeBlock = func.AppendBasicBlock("ifcont");

        _builder.BuildCondBr(cond, thenBlock, elseBlock);

        // Emit then block
        _builder.PositionAtEnd(thenBlock);
        if (context.block(0) != null)
        {
            Visit(context.block(0));
        }
        if (_builder.InsertBlock.Terminator == null)
        {
            _builder.BuildBr(mergeBlock);
        }

        // Emit else block (if it exists)
        if (context.block().Length > 1 && context.block(1) != null)
        {
            _builder.PositionAtEnd(elseBlock);
            Visit(context.block(1));
            if (_builder.InsertBlock.Terminator == null)
            {
                _builder.BuildBr(mergeBlock);
            }
        }
        else
        {
            _builder.PositionAtEnd(elseBlock);
            _builder.BuildBr(mergeBlock);
        }

        _builder.PositionAtEnd(mergeBlock);
        Console.WriteLine("Exiting VisitIfStatement...");
        return CompilerTypeHelper.GetNull();
    }

    public override LLVMValueRef VisitReturnStatement(CobraParser.ReturnStatementContext context)
    {
        Console.WriteLine("Entering VisitReturnStatement...");
        LLVMValueRef retVal = CompilerTypeHelper.GetNull();
        if (context.expression() != null)
        {
            retVal = Visit(context.expression());
        }

        _builder.BuildRet(retVal);
        Console.WriteLine("Exiting VisitReturnStatement...");
        return retVal;
    }

    public override LLVMValueRef VisitFunctionCall([NotNull] CobraParser.FunctionCallContext context)
    {
        string funcName = context.ID()?.GetText() ?? throw new System.Exception("Function name is null");
        Console.WriteLine($"Entering VisitFunctionCall: {funcName}");
        LLVMValueRef func = _module.GetNamedFunction(funcName);
        if (func.IsNull)
        {
            throw new System.Exception($"Unknown function: {funcName}");
        }

        if (funcName == "print")
        {
            if (context.argumentList()?.expression(0) == null)
            {
                throw new System.Exception("Print function requires one argument");
            }
            LLVMValueRef printArg = Visit(context.argumentList().expression(0));
            LLVMValueRef result = _builder.BuildCall2(func.TypeOf, func, new[] { printArg }, "");
            Console.WriteLine($"Exiting VisitFunctionCall: {funcName}");
            return result;
        }

        LLVMValueRef callResult = _builder.BuildCall2(func.TypeOf, func, Array.Empty<LLVMValueRef>(), "");
        Console.WriteLine($"Exiting VisitFunctionCall: {funcName}");
        return callResult;
    }

    public override LLVMValueRef VisitComparisonExpression(CobraParser.ComparisonExpressionContext context)
    {
        Console.WriteLine("Entering VisitComparisonExpression...");
        if (context == null || context.arithmeticExpression() == null || context.arithmeticExpression().Length < 2)
        {
            Console.WriteLine("Exiting VisitComparisonExpression (single expression)...");
            return Visit(context.arithmeticExpression(0));
        }

        LLVMValueRef left = Visit(context.arithmeticExpression(0));
        LLVMValueRef right = Visit(context.arithmeticExpression(1));

        if (left.IsNull || right.IsNull)
        {
            throw new System.Exception("Invalid operands in comparison expression");
        }

        string op = context.GetChild(1)?.GetText() ?? throw new System.Exception("Comparison operator is null");
        LLVMIntPredicate pred = op switch
        {
            ">" => LLVMIntPredicate.LLVMIntSGT,
            "<" => LLVMIntPredicate.LLVMIntSLT,
            "==" => LLVMIntPredicate.LLVMIntEQ,
            "!=" => LLVMIntPredicate.LLVMIntNE,
            ">=" => LLVMIntPredicate.LLVMIntSGE,
            "<=" => LLVMIntPredicate.LLVMIntSLE,
            _ => throw new System.Exception($"Unknown comparison operator: {op}")
        };

        LLVMValueRef result = _builder.BuildICmp(pred, left, right, "cmptmp");
        Console.WriteLine("Exiting VisitComparisonExpression...");
        return result;
    }

    public override LLVMValueRef VisitArithmeticExpression(CobraParser.ArithmeticExpressionContext context)
    {
        Console.WriteLine("Entering VisitArithmeticExpression...");
        LLVMValueRef left = Visit(context.multiplicationExpression(0));

        for (int i = 1; i < context.ChildCount; i += 2)
        {
            string op = context.GetChild(i)?.GetText() ?? throw new System.Exception("Arithmetic operator is null");
            LLVMValueRef right = Visit(context.multiplicationExpression((i + 1) / 2));

            switch (op)
            {
                case "+": left = _builder.BuildAdd(left, right, "addtmp"); break;
                case "-": left = _builder.BuildSub(left, right, "subtmp"); break;
                default: throw new System.Exception($"Unknown arithmetic operator: {op}");
            }
        }

        Console.WriteLine("Exiting VisitArithmeticExpression...");
        return left;
    }

    public override LLVMValueRef VisitMultiplicationExpression(CobraParser.MultiplicationExpressionContext context)
    {
        Console.WriteLine("Entering VisitMultiplicationExpression...");
        LLVMValueRef left = Visit(context.unaryExpression(0));

        for (int i = 1; i < context.ChildCount; i += 2)
        {
            string op = context.GetChild(i)?.GetText() ?? throw new System.Exception("Multiplication operator is null");
            LLVMValueRef right = Visit(context.unaryExpression((i + 1) / 2));

            switch (op)
            {
                case "*": left = _builder.BuildMul(left, right, "multmp"); break;
                case "/": left = _builder.BuildSDiv(left, right, "divtmp"); break;
                case "%": left = _builder.BuildSRem(left, right, "modtmp"); break;
                default: throw new System.Exception($"Unknown multiplication operator: {op}");
            }
        }

        Console.WriteLine("Exiting VisitMultiplicationExpression...");
        return left;
    }

    public override LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context)
    {
        Console.WriteLine($"Entering VisitPrimary: {context.GetText()}");
        if (context.INTEGER() != null)
        {
            LLVMValueRef result = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)int.Parse(context.INTEGER().GetText()), false);
            Console.WriteLine("Exiting VisitPrimary (INTEGER)...");
            return result;
        }
        else if (context.STRING_LITERAL() != null)
        {
            string str = context.STRING_LITERAL().GetText().Trim('"');
            LLVMValueRef result = _builder.BuildGlobalStringPtr(str, ".str");
            Console.WriteLine("Exiting VisitPrimary (STRING_LITERAL)...");
            return result;
        }
        else if (context.ID() != null)
        {
            string varName = context.ID().GetText();
            Console.WriteLine($"Processing variable: {varName}");
            if (!_namedValues.TryGetValue(varName, out LLVMValueRef variable))
            {
                throw new System.Exception($"Unknown variable: {varName}");
            }
            if (variable.IsNull)
            {
                throw new System.Exception($"Null variable reference for: {varName}");
            }
            LLVMTypeRef varType = variable.TypeOf;
            if (varType.GetType() == null)
            {
                throw new System.Exception($"Invalid type for variable: {varName}");
            }
            LLVMTypeRef elementType = varType.ElementType;
            if (elementType.GetType() == null)
            {
                throw new System.Exception($"Invalid element type for variable: {varName}");
            }
            LLVMValueRef result = _builder.BuildLoad2(elementType, variable, $"load_{varName}");
            Console.WriteLine($"Exiting VisitPrimary (ID: {varName})...");
            return result;
        }
        else if (context.expression() != null)
        {
            LLVMValueRef result = Visit(context.expression());
            Console.WriteLine("Exiting VisitPrimary (expression)...");
            return result;
        }
        else if (context.functionCall() != null)
        {
            LLVMValueRef result = Visit(context.functionCall());
            Console.WriteLine("Exiting VisitPrimary (functionCall)...");
            return result;
        }

        throw new System.Exception($"Invalid primary expression: {context.GetText()}");
    }
}