using LLVMSharp.Interop;

namespace Cobra.Compiler;

public class CobraPrimaryVisitor
{
    public static LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context, LLVMBuilderRef builder,
        Dictionary<string, LLVMValueRef> namedValues)
    {
        var literalContext = context.literal();
        if (literalContext.INTEGER() != null)
        {
            int value = int.Parse(literalContext.INTEGER().GetText());
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)value, false);
        }

        if (literalContext.FLOAT_LITERAL() != null)
        {
            float value = float.Parse(literalContext.FLOAT_LITERAL().GetText());
            return LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, value);
        }

        if (literalContext.BOOLEAN_LITERAL() != null)
        {
            bool value = literalContext.BOOLEAN_LITERAL().GetText() == "true";
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, (ulong)(value ? 1 : 0), false);
        }
        
        if (context.ID() != null)
        {
            string variableName = context.ID().GetText();
            if (namedValues.TryGetValue(variableName, out var varRef))
            {
                // Load the value from the variable's memory location
                return builder.BuildLoad2(varRef.TypeOf.ElementType, varRef, variableName);
            }

            throw new Exception($"Undeclared variable: {variableName}");
        }

        // Handle other primary types (string, boolean, etc.) here
        throw new Exception("Unsupported primary expression type");
    }
}