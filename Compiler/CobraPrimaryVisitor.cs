using LLVMSharp.Interop;

namespace Cobra.Compiler;

public class CobraPrimaryVisitor
{
    public static LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context, LLVMBuilderRef builder,
        Dictionary<string, LLVMValueRef> namedValues)
    {
        if (context.INTEGER() != null)
        {
            int value = int.Parse(context.INTEGER().GetText());
            return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)value, false);
        }

        if (context.FLOAT_LITERAL() != null)
        {
            float value = float.Parse(context.FLOAT_LITERAL().GetText());
            return LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, value);
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