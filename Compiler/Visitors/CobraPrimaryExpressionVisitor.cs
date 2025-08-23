using System.Text.RegularExpressions;
using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraPrimaryExpressionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private LLVMBuilderRef _builder;

    internal CobraPrimaryExpressionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _builder = mainVisitor.Builder;
    }

    public LLVMValueRef VisitPostfixExpression(CobraParser.PostfixExpressionContext context)
    {
        // Check if the primary expression is a simple identifier. This is the most common case.
        var idNode = context.primary()?.ID();
        
        // Case 1: Function Call (e.g., "myFunc(...)").
        // This is identified by a primary ID followed by an '('.
        if (idNode != null && context.ChildCount > 1 && context.GetChild(1).GetText() == "(")
        {
            var functionName = idNode.GetText();
            if (!_visitor.Functions.TryGetValue(functionName, out var function))
            {
                throw new Exception($"Undeclared function: '{functionName}'");
            }

            var argList = context.argumentList(0); // There can only be one argument list per call
            var args = new List<LLVMValueRef>();
            var functionType = function.TypeOf.ElementType;

            if (argList != null)
            {
                foreach (var argExpr in argList.expression())
                {
                    args.Add(_visitor.Visit(argExpr));
                }
            }

            // TODO: Validate that the number and types of args match the function signature.

            var callResult = _builder.BuildCall2(functionType, function, args.ToArray(),
                "call_tmp");

            // TODO: Handle chained calls or operations after a call if the grammar allows (e.g., myFunc().field).
            return callResult;
        }

        // Case 2: All other postfix expressions (variable access, literals, parenthesized expressions, etc.).
        // We first get the base value, then apply any operators like '++' or '--'.
        var value = _visitor.Visit(context.primary());

        for (var i = 1; i < context.ChildCount; i++)
        {
            var opNode = context.GetChild(i);
            var op = opNode.GetText();

            if (op is "++" or "--")
            {
                var varName = context.primary().ID()?.GetText() ??
                              throw new Exception("Invalid lvalue for postfix inc/dec");
                var addr = _visitor.ScopeManagement.FindVariable(varName);

                var oldVal = value; // Postfix returns the original value before modification.
                var newVal = op == "++"
                    ? _builder.BuildAdd(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "post_inc")
                    : _builder.BuildSub(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "post_dec");
                _builder.BuildStore(newVal, addr);
                value = oldVal;
            }
            // TODO: Handle array access '[...]' and member access '.' here.
        }

        return value;
    }

    public LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context)
    {
        if (context.expression() != null) return _visitor.Visit(context.expression());

        if (context.literal() != null)
        {
            var literal = context.literal();
            if (literal.INTEGER() != null)
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)int.Parse(literal.INTEGER().GetText()));
            if (literal.FLOAT_LITERAL() != null)
                return LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, double.Parse(literal.FLOAT_LITERAL().GetText()));
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
                return LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
        }

        if (context.ID() != null)
        {
            var variableName = context.ID().GetText();
            // This is now safe because we know from the logic in VisitPostfixExpression
            // that if we are here, it must be a variable, not a function call.
            var varRef = _visitor.ScopeManagement.FindVariable(variableName);
            return _builder.BuildLoad2(varRef.TypeOf.ElementType, varRef, variableName);
        }

        throw new Exception("Unsupported primary expression");
    }
    

}