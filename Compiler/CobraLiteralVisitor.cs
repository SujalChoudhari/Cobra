using LLVMSharp.Interop;
using System.Text.RegularExpressions; // Added for processing escape sequences in strings

namespace Cobra.Compiler;

public class CobraLiteralVisitor
{
    public static LLVMValueRef VisitLiteral(CobraParser.PrimaryContext context, LLVMBuilderRef builder,
        Dictionary<string, LLVMValueRef> namedValues)
    {
        // First, check if the primary expression is a literal value
        var literalContext = context.literal();
        if (literalContext != null)
        {
            if (literalContext.INTEGER() != null)
            {
                int value = int.Parse(literalContext.INTEGER().GetText());
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)value, false);
            }

            if (literalContext.FLOAT_LITERAL() != null)
            {
                double value = double.Parse(literalContext.FLOAT_LITERAL().GetText());
                return LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, value);
            }

            if (literalContext.BOOLEAN_LITERAL() != null)
            {
                bool value = literalContext.BOOLEAN_LITERAL().GetText() == "true";
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, (ulong)(value ? 1 : 0), false);
            }

            if (literalContext.STRING_LITERAL() != null)
            {
                // Get the literal text, which includes the quotes (e.g., "\"hello\"")
                string rawString = literalContext.STRING_LITERAL().GetText();

                // Remove the surrounding quotes
                string unquotedString = rawString.Substring(1, rawString.Length - 2);

                // Process standard escape sequences like \n, \t, \\, etc.
                string finalString = Regex.Unescape(unquotedString);
                
                // Create a constant global string in the LLVM module and get a pointer to it.
                // This returns an LLVMValueRef of type i8* (pointer to a character).
                return builder.BuildGlobalStringPtr(finalString, ".str");
            }
        }

        // If not a literal, check if it's a variable identifier
        if (context.ID() != null)
        {
            string variableName = context.ID().GetText();
            if (namedValues.TryGetValue(variableName, out var varRef))
            {
                // Load the value from the variable's memory location to use it in an expression
                return builder.BuildLoad2(varRef.TypeOf.ElementType, varRef, variableName);
            }

            throw new Exception($"Undeclared variable: {variableName}");
        }

        // Handle other primary types (like parenthesized expressions) here
        throw new Exception("Unsupported primary expression type");
    }
}