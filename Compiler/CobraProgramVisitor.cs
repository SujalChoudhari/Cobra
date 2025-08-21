using LLVMSharp.Interop;
using Antlr4.Runtime.Tree;
using System.Text.RegularExpressions;
using Cobra.Utils;

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
        foreach (var child in context.children)
        {
            if (child is ITerminalNode) continue;
            Visit(child);
        }

        return default;
    }

    public override LLVMValueRef VisitStatement(CobraParser.StatementContext context)
    {
        if (context.declarationStatement() != null)
        {
            return VisitDeclarationStatement(context.declarationStatement());
        }
        else if (context.assignmentStatement() != null)
        {
            return VisitAssignmentStatement(context.assignmentStatement());
        }
        else if (context.expressionStatement() != null)
        {
            return VisitExpressionStatement(context.expressionStatement());
        }

        return default;
    }

    public override LLVMValueRef VisitExpressionStatement(CobraParser.ExpressionStatementContext context)
    {
        return Visit(context.expression());
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

        if (namedValues.ContainsKey(variableName))
        {
            throw new Exception($"Variable '{variableName}' is already declared.");
        }

        LLVMValueRef allocatedValue = _builder.BuildAlloca(varType, variableName);
        namedValues[variableName] = allocatedValue;

        if (context.expression() != null)
        {
            LLVMValueRef initialValue = Visit(context.expression());
            _builder.BuildStore(initialValue, allocatedValue);
            CobraLogger.RuntimeVariableValue(_builder, _module,
                $"Declared variable: {variableName} <{typeName}>", initialValue);
        }

        CobraLogger.Success($"Compiled declaration for variable: {variableName} with type {typeName}");
        return allocatedValue;
    }

    public override LLVMValueRef VisitAssignmentStatement(CobraParser.AssignmentStatementContext context)
    {
        string variableName = context.postfixExpression().GetText(); // Assume simple ID for LHS
        if (!namedValues.ContainsKey(variableName))
        {
            throw new Exception($"Undeclared variable: '{variableName}'");
        }

        LLVMValueRef variableAddress = namedValues[variableName];
        LLVMValueRef rhs = Visit(context.expression());
        string op = context.assignmentOperator().GetText();
        LLVMValueRef valueToStore;
        if (op == "=")
        {
            valueToStore = rhs;
        }
        else
        {
            LLVMValueRef loaded = _builder.BuildLoad2(variableAddress.TypeOf.ElementType, variableAddress, "loadtmp");
            switch (op)
            {
                case "+=":
                    valueToStore = _builder.BuildAdd(loaded, rhs, "addassign");
                    break;
                case "-=":
                    valueToStore = _builder.BuildSub(loaded, rhs, "subassign");
                    break;
                case "*=":
                    valueToStore = _builder.BuildMul(loaded, rhs, "mulassign");
                    break;
                case "/=":
                    valueToStore = _builder.BuildSDiv(loaded, rhs, "divassign");
                    break;
                default:
                    throw new Exception($"Unsupported assignment operator: {op}");
            }
            CobraLogger.RuntimeVariableValue(_builder, _module, $"Assigned value to variable: {variableName}:",loaded);
        }

        _builder.BuildStore(valueToStore, variableAddress);
        CobraLogger.Success($"Compiled assignment for variable: {variableName}");
        
        return valueToStore;
    }

    public override LLVMValueRef VisitExpression(CobraParser.ExpressionContext context)
    {
        return Visit(context.conditionalExpression());
    }

    public override LLVMValueRef VisitConditionalExpression(CobraParser.ConditionalExpressionContext context)
    {
        if (context.QUESTION_MARK() == null)
        {
            return Visit(context.logicalOrExpression());
        }

        LLVMValueRef cond = Visit(context.logicalOrExpression());
        LLVMBasicBlockRef currentBB = _builder.InsertBlock;
        LLVMBasicBlockRef trueBB = currentBB.Parent.AppendBasicBlock("ternary_true");
        LLVMBasicBlockRef falseBB = currentBB.Parent.AppendBasicBlock("ternary_false");
        LLVMBasicBlockRef endBB = currentBB.Parent.AppendBasicBlock("ternary_end");

        _builder.BuildCondBr(cond, trueBB, falseBB);

        _builder.PositionAtEnd(trueBB);
        LLVMValueRef trueVal = Visit(context.expression(0));
        LLVMBasicBlockRef trueEndBB = _builder.InsertBlock;
        _builder.BuildBr(endBB);

        _builder.PositionAtEnd(falseBB);
        LLVMValueRef falseVal = Visit(context.expression(1));
        LLVMBasicBlockRef falseEndBB = _builder.InsertBlock;
        _builder.BuildBr(endBB);

        _builder.PositionAtEnd(endBB);
        LLVMValueRef phi = _builder.BuildPhi(trueVal.TypeOf, "ternary_phi");
        phi.AddIncoming(new[] { trueVal, falseVal }, new[] { trueEndBB, falseEndBB }, 2);
        return phi;
    }

    public override LLVMValueRef VisitLogicalOrExpression(CobraParser.LogicalOrExpressionContext context)
    {
        return VisitLogicalOrHelper(context, 0);
    }

    private LLVMValueRef VisitLogicalOrHelper(CobraParser.LogicalOrExpressionContext context, int index)
    {
        LLVMValueRef left = Visit(context.logicalAndExpression(index));
        if (index == context.logicalAndExpression().Length - 1)
        {
            return left;
        }

        LLVMBasicBlockRef currentBB = _builder.InsertBlock;
        LLVMBasicBlockRef rightBB = currentBB.Parent.AppendBasicBlock("or_right");
        LLVMBasicBlockRef endBB = currentBB.Parent.AppendBasicBlock("or_end");

        _builder.BuildCondBr(left, endBB, rightBB);

        _builder.PositionAtEnd(rightBB);
        LLVMValueRef right = VisitLogicalOrHelper(context, index + 1);
        LLVMBasicBlockRef rightEndBB = _builder.InsertBlock;
        _builder.BuildBr(endBB);

        _builder.PositionAtEnd(endBB);
        LLVMValueRef phi = _builder.BuildPhi(LLVMTypeRef.Int1, "or_phi");
        phi.AddIncoming(new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 1), right },
            new[] { currentBB, rightEndBB }, 2);
        return phi;
    }

    public override LLVMValueRef VisitLogicalAndExpression(CobraParser.LogicalAndExpressionContext context)
    {
        return VisitLogicalAndHelper(context, 0);
    }

    private LLVMValueRef VisitLogicalAndHelper(CobraParser.LogicalAndExpressionContext context, int index)
    {
        LLVMValueRef left = Visit(context.bitwiseOrExpression(index));
        if (index == context.bitwiseOrExpression().Length - 1)
        {
            return left;
        }

        LLVMBasicBlockRef currentBB = _builder.InsertBlock;
        LLVMBasicBlockRef rightBB = currentBB.Parent.AppendBasicBlock("and_right");
        LLVMBasicBlockRef endBB = currentBB.Parent.AppendBasicBlock("and_end");

        _builder.BuildCondBr(left, rightBB, endBB);

        _builder.PositionAtEnd(rightBB);
        LLVMValueRef right = VisitLogicalAndHelper(context, index + 1);
        LLVMBasicBlockRef rightEndBB = _builder.InsertBlock;
        _builder.BuildBr(endBB);

        _builder.PositionAtEnd(endBB);
        LLVMValueRef phi = _builder.BuildPhi(LLVMTypeRef.Int1, "and_phi");
        phi.AddIncoming(new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, 0), right },
            new[] { currentBB, rightEndBB }, 2);
        return phi;
    }

    public override LLVMValueRef VisitBitwiseOrExpression(CobraParser.BitwiseOrExpressionContext context)
    {
        LLVMValueRef left = Visit(context.bitwiseXorExpression(0));
        for (int i = 1; i < context.bitwiseXorExpression().Length; i++)
        {
            LLVMValueRef right = Visit(context.bitwiseXorExpression(i));
            left = _builder.BuildOr(left, right, "bitor");
        }

        return left;
    }

    public override LLVMValueRef VisitBitwiseXorExpression(CobraParser.BitwiseXorExpressionContext context)
    {
        LLVMValueRef left = Visit(context.bitwiseAndExpression(0));
        for (int i = 1; i < context.bitwiseAndExpression().Length; i++)
        {
            LLVMValueRef right = Visit(context.bitwiseAndExpression(i));
            left = _builder.BuildXor(left, right, "bitxor");
        }

        return left;
    }

    public override LLVMValueRef VisitBitwiseAndExpression(CobraParser.BitwiseAndExpressionContext context)
    {
        LLVMValueRef left = Visit(context.equalityExpression(0));
        for (int i = 1; i < context.equalityExpression().Length; i++)
        {
            LLVMValueRef right = Visit(context.equalityExpression(i));
            left = _builder.BuildAnd(left, right, "bitand");
        }

        return left;
    }

    public override LLVMValueRef VisitEqualityExpression(CobraParser.EqualityExpressionContext context)
    {
        LLVMValueRef left = Visit(context.comparisonExpression(0));
        for (int i = 1; i < context.comparisonExpression().Length; i++)
        {
            string op = context.GetChild(2 * i - 1).GetText();
            LLVMValueRef right = Visit(context.comparisonExpression(i));
            LLVMIntPredicate pred = op == "==" ? LLVMIntPredicate.LLVMIntEQ : LLVMIntPredicate.LLVMIntNE;
            left = _builder.BuildICmp(pred, left, right, "eqcmp");
        }

        return left;
    }

    public override LLVMValueRef VisitComparisonExpression(CobraParser.ComparisonExpressionContext context)
    {
        LLVMValueRef left = Visit(context.bitwiseShiftExpression(0));
        for (int i = 1; i < context.bitwiseShiftExpression().Length; i++)
        {
            string op = context.GetChild(2 * i - 1).GetText();
            LLVMValueRef right = Visit(context.bitwiseShiftExpression(i));
            LLVMIntPredicate pred = op switch
            {
                ">" => LLVMIntPredicate.LLVMIntSGT,
                "<" => LLVMIntPredicate.LLVMIntSLT,
                ">=" => LLVMIntPredicate.LLVMIntSGE,
                "<=" => LLVMIntPredicate.LLVMIntSLE,
                _ => throw new Exception($"Invalid comparison op: {op}")
            };
            left = _builder.BuildICmp(pred, left, right, "cmp");
        }

        return left;
    }

    public override LLVMValueRef VisitBitwiseShiftExpression(CobraParser.BitwiseShiftExpressionContext context)
    {
        LLVMValueRef left = Visit(context.additiveExpression(0));
        for (int i = 1; i < context.additiveExpression().Length; i++)
        {
            string op = context.GetChild(2 * i - 1).GetText();
            LLVMValueRef right = Visit(context.additiveExpression(i));
            left = op == "<<" ? _builder.BuildShl(left, right, "shl") : _builder.BuildAShr(left, right, "ashr");
        }

        return left;
    }

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

    public override LLVMValueRef VisitMultiplicativeExpression(CobraParser.MultiplicativeExpressionContext context)
    {
        LLVMValueRef left = Visit(context.unaryExpression(0));
        for (int i = 1; i < context.unaryExpression().Length; i++)
        {
            string op = context.GetChild(2 * i - 1).GetText();
            LLVMValueRef right = Visit(context.unaryExpression(i));
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

    public override LLVMValueRef VisitUnaryExpression(CobraParser.UnaryExpressionContext context)
    {
        if (context.postfixExpression() != null)
        {
            return Visit(context.postfixExpression());
        }

        string op = context.GetChild(0).GetText();
        LLVMValueRef operand = Visit(context.unaryExpression());

        if (op == "+")
            return operand;
        else if (op == "-")
            return _builder.BuildNeg(operand, "neg");
        else if (op == "!")
            return _builder.BuildNot(operand, "lnot");
        else if (op == "~")
            return _builder.BuildNot(operand, "bnot");
        else if (op == "++" || op == "--")
        {
            string varName = context.unaryExpression().postfixExpression()?.primary()?.ID()?.GetText()
                             ?? throw new Exception("Invalid lvalue for prefix inc/dec");
            LLVMValueRef addr = namedValues[varName];
            LLVMValueRef oldVal = _builder.BuildLoad2(LLVMTypeRef.Int32, addr, varName);
            LLVMValueRef newVal = op == "++"
                ? _builder.BuildAdd(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "preinc")
                : _builder.BuildSub(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "predec");
            _builder.BuildStore(newVal, addr);
            return newVal;
        }
        else
            throw new Exception($"Invalid unary op: {op}");
    }

    public override LLVMValueRef VisitPostfixExpression(CobraParser.PostfixExpressionContext context)
    {
        LLVMValueRef value = Visit(context.primary());
        for (int i = 0; i < context.INC().Length + context.DEC().Length; i++)
        {
            // Assume only inc/dec suffixes, one at a time, on simple ID primary
            string op = context.GetChild(context.primary().ChildCount + i - 1).GetText();
            string varName = context.primary().ID()?.GetText() ??
                             throw new Exception("Invalid lvalue for postfix inc/dec");
            LLVMValueRef addr = namedValues[varName];
            LLVMValueRef oldVal = value; // Already loaded
            LLVMValueRef newVal = op == "++"
                ? _builder.BuildAdd(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "postinc")
                : _builder.BuildSub(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "postdec");
            _builder.BuildStore(newVal, addr);
            value = oldVal; // Postfix returns old
        }

        // TODO: Handle other postfix like calls, array, dot (not in scope)
        return value;
    }

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
                int value = int.Parse(literal.INTEGER().GetText());
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)value);
            }

            if (literal.FLOAT_LITERAL() != null)
            {
                double value = double.Parse(literal.FLOAT_LITERAL().GetText());
                return LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, value);
            }

            if (literal.BOOLEAN_LITERAL() != null)
            {
                bool value = literal.BOOLEAN_LITERAL().GetText() == "true";
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, (ulong)(value ? 1 : 0));
            }

            if (literal.STRING_LITERAL() != null)
            {
                string rawString = literal.STRING_LITERAL().GetText();
                string unquotedString = rawString.Substring(1, rawString.Length - 2);
                string finalString = Regex.Unescape(unquotedString);
                return _builder.BuildGlobalStringPtr(finalString, ".str");
            }

            if (literal.NULL() != null)
            {
                return LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
            }
        }

        if (context.ID() != null)
        {
            string variableName = context.ID().GetText();
            if (namedValues.TryGetValue(variableName, out var varRef))
            {
                return _builder.BuildLoad2(varRef.TypeOf.ElementType, varRef, variableName);
            }

            throw new Exception($"Undeclared variable: {variableName}");
        }

        // TODO: Handle THIS, NEW
        throw new Exception("Unsupported primary expression");
    }
}