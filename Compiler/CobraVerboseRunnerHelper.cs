using LLVMSharp.Interop;

namespace Cobra.Compiler;

/// <summary>
/// Provides helper methods for generating LLVM IR to produce verbose runtime output.
/// This includes printing static messages and variable values with correct type handling
/// and C-style `printf` format specifiers.
/// </summary>
public static class CobraVerboseRunnerHelper
{
    /// <summary>
    /// Retrieves the underlying function type from a function value.
    /// This handles cases where the function value is a pointer to the function type.
    /// </summary>
    /// <param name="function">The LLVM function value.</param>
    /// <returns>The <see cref="LLVMTypeRef"/> of the function.</returns>
    private static LLVMTypeRef GetFnType(LLVMValueRef function)
    {
        var type = function.TypeOf;
        return type.Kind == LLVMTypeKind.LLVMPointerTypeKind ? type.ElementType : type;
    }

    /// <summary>
    /// Ensures that a given LLVM value is a scalar value (not a pointer to one).
    /// If the value is a pointer to a primitive type (integer, float, double, or another pointer),
    /// it generates a `load` instruction to get the value itself.
    /// </summary>
    /// <param name="builder">The LLVM IR builder.</param>
    /// <param name="value">The LLVM 'value' to check and potentially load.</param>
    /// <returns>The scalar <see cref="LLVMValueRef"/>.</returns>
    private static LLVMValueRef EnsureLoadedScalar(LLVMBuilderRef builder, LLVMValueRef value)
    {
        var type = value.TypeOf;
        if (type.Kind != LLVMTypeKind.LLVMPointerTypeKind)
        {
            return value;
        }

        var elementType = type.ElementType.Kind;
        return elementType is LLVMTypeKind.LLVMIntegerTypeKind
            or LLVMTypeKind.LLVMFloatTypeKind
            or LLVMTypeKind.LLVMDoubleTypeKind
            or LLVMTypeKind.LLVMPointerTypeKind ? builder.BuildLoad2(type.ElementType, value, "loaded_val") : value;
    }

    /// <summary>
    /// Generates LLVM IR to print a static, null-terminated string to standard output.
    /// </summary>
    /// <param name="builder">The LLVM IR builder instance.</param>
    /// <param name="module">The LLVM module.</param>
    /// <param name="printfFunction">The LLVM value for the `printf` function.</param>
    /// <param name="message">The string message to be printed.</param>
    public static void AddPrintStatement(
        LLVMBuilderRef builder,
        LLVMModuleRef module,
        LLVMValueRef printfFunction,
        string message)
    {
        var printfFnType = GetFnType(printfFunction);
        var formatString = builder.BuildGlobalStringPtr(message + "\n", "fmt_msg");
        LLVMValueRef[] args = [formatString];
        builder.BuildCall2(printfFnType, printfFunction, args, "call_printf_msg");
    }

    /// <summary>
    /// Generates LLVM IR to print a message followed by a variable's value, handling different data types
    /// and their corresponding `printf` format specifiers. It correctly promotes smaller integer types to 32-bit
    /// and `float` to `double` as per C calling conventions.
    /// </summary>
    /// <param name="builder">The LLVM IR builder instance.</param>
    /// <param name="module">The LLVM module.</param>
    /// <param name="printfFunction">The LLVM value for the `printf` function.</param>
    /// <param name="message">The message to prefix the variable value.</param>
    /// <param name="variableValue">The LLVM value reference for the variable to print.</param>
    public static void AddPrintVariable(
        LLVMBuilderRef builder,
        LLVMModuleRef module,
        LLVMValueRef printfFunction,
        string message,
        LLVMValueRef variableValue)
    {
        var printfFnType = GetFnType(printfFunction);

        var value = EnsureLoadedScalar(builder, variableValue);
        var valueType = value.TypeOf;

        var formatString = string.Empty;
        var args = new LLVMValueRef[] { };
        var callName = string.Empty;

        if (valueType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            var width = valueType.IntWidth;
            if (width <= 32)
            {
                var promotedValue = width < 32
                    ? builder.BuildSExt(value, LLVMTypeRef.Int32, "s_ext_i32")
                    : value;

                formatString = $"{message}: %d\n";
                args = [builder.BuildGlobalStringPtr(formatString, "fmt_int"), promotedValue];
                callName = "call_printf_int";
            }
            else if (width == 64)
            {
                formatString = $"{message}: %lld\n";
                args = [builder.BuildGlobalStringPtr(formatString, "fmt_i64"), value];
                callName = "call_printf_i64";
            }
        }
        else if (valueType.Kind == LLVMTypeKind.LLVMFloatTypeKind)
        {
            var doubleValue = builder.BuildFPExt(value, LLVMTypeRef.Double, "fp_ext_f64");
            formatString = $"{message}: %f\n";
            args = [builder.BuildGlobalStringPtr(formatString, "fmt_float"), doubleValue];
            callName = "call_printf_f32";
        }
        else if (valueType.Kind == LLVMTypeKind.LLVMDoubleTypeKind)
        {
            formatString = $"{message}: %f\n";
            args = [builder.BuildGlobalStringPtr(formatString, "fmt_double"), value];
            callName = "call_printf_f64";
        }
        else if (valueType.Kind == LLVMTypeKind.LLVMPointerTypeKind)
        {
            var i8Ptr = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            var castPointer = builder.BuildBitCast(value, i8Ptr, "bit_cast_i8p");
            formatString = $"{message}: %p\n";
            args = [builder.BuildGlobalStringPtr(formatString, "fmt_ptr"), castPointer];
            callName = "call_printf_ptr";
        }

        if (string.IsNullOrEmpty(formatString))
        {
            formatString = $"{message}\n";
            args = [builder.BuildGlobalStringPtr(formatString, "fmt_default")];
            callName = "call_printf_default";
        }

        builder.BuildCall2(printfFnType, printfFunction, args, callName);
    }
}