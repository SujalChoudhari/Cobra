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
        var value = _visitor.Visit(context.primary());
        for (var i = 1; i < context.ChildCount; i++)
        {
            var op = context.GetChild(i).GetText();
            if (op is "++" or "--")
            {
                var varName = context.primary().ID()?.GetText() ??
                              throw new Exception("Invalid lvalue for postfix inc/dec");
                var addr = _visitor.ScopeManagement.FindVariable(varName);
                var oldVal = value;
                var newVal = op == "++"
                    ? _builder.BuildAdd(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "post_inc")
                    : _builder.BuildSub(oldVal, LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 1), "post_dec");
                _builder.BuildStore(newVal, addr);
                value = oldVal;
            }
            // TODO: Handle function calls, member access, etc.
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
            {
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)int.Parse(literal.INTEGER().GetText()));
            }
            if (literal.FLOAT_LITERAL() != null)
            {
                return LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, double.Parse(literal.FLOAT_LITERAL().GetText()));
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
            var varRef = _visitor.ScopeManagement.FindVariable(variableName);
            return _builder.BuildLoad2(varRef.TypeOf.ElementType, varRef, variableName);
        }

        throw new Exception("Unsupported primary expression");
    }
}