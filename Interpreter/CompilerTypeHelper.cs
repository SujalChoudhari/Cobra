using LLVMSharp.Interop;

namespace Cobra.Interpreter;

public class CompilerTypeHelper
{
    public static LLVMValueRef GetNull()
    {
        return LLVMValueRef.CreateConstNull(LLVMTypeRef.Void);
    }
}