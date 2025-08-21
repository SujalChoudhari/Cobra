using System;
using LLVMSharp.Interop;

namespace Cobra.Compiler;

public static class CobraVerboseRunnerHelper
{
    // Helper: get the real function type for BuildCall2
    private static LLVMTypeRef GetFnType(LLVMValueRef fn)
    {
        var ty = fn.TypeOf;
        return ty.Kind == LLVMTypeKind.LLVMPointerTypeKind ? ty.ElementType : ty;
    }

    // Helper: load if pointer-to-scalar
    private static LLVMValueRef EnsureLoadedScalar(LLVMBuilderRef builder, LLVMValueRef v)
    {
        var k = v.TypeOf.Kind;
        if (k == LLVMTypeKind.LLVMPointerTypeKind)
        {
            var elem = v.TypeOf.ElementType.Kind;
            if (elem is LLVMTypeKind.LLVMIntegerTypeKind
                or LLVMTypeKind.LLVMFloatTypeKind
                or LLVMTypeKind.LLVMDoubleTypeKind
                or LLVMTypeKind.LLVMPointerTypeKind)
            {
                return builder.BuildLoad2(v.TypeOf.ElementType, v, "ld");
            }
        }
        return v;
    }

    /// Print static message.
    public static void AddPrintStatement(
        LLVMBuilderRef builder,
        LLVMModuleRef module,
        LLVMValueRef printfFunction,
        string message)
    {
        var printfFnTy = GetFnType(printfFunction);
        var fmt = builder.BuildGlobalStringPtr(message + "\n", "fmt_msg");
        LLVMValueRef[] args = new[] { fmt };
        builder.BuildCall2(printfFnTy, printfFunction, args, "call_printf_msg");
    }

    /// Print "message: <value>" with correct promotions.
    public static void AddPrintVariable(
        LLVMBuilderRef builder,
        LLVMModuleRef module,
        LLVMValueRef printfFunction,
        string message,
        LLVMValueRef variableValue)
    {
        var printfFnTy = GetFnType(printfFunction);

        // Load if needed
        var v = EnsureLoadedScalar(builder, variableValue);
        var vt = v.TypeOf;

        // Integers
        if (vt.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            uint w = vt.IntWidth;
            LLVMValueRef vPromoted;
            string spec;

            if (w <= 32)
            {
                // default argument promotion to int
                if (w < 32)
                    vPromoted = builder.BuildSExt(v, LLVMTypeRef.Int32, "sext_i32");
                else
                    vPromoted = v; // i32
                spec = "%d";
                var fmt = builder.BuildGlobalStringPtr($"{message}: {spec}\n", "fmt_int");
                LLVMValueRef[] args = new[] { fmt, vPromoted };
                builder.BuildCall2(printfFnTy, printfFunction, args, "call_printf_int");
                return;
            }
            else if (w == 64)
            {
                // print i64 as long long
                spec = "%lld";
                var fmt = builder.BuildGlobalStringPtr($"{message}:\t{spec}\n", "fmt_i64");
                LLVMValueRef[] args = new[] { fmt, v };
                builder.BuildCall2(printfFnTy, printfFunction, args, "call_printf_i64");
                return;
            }
        }

        // Float -> promote to double for %f
        if (vt.Kind == LLVMTypeKind.LLVMFloatTypeKind)
        {
            var vd = builder.BuildFPExt(v, LLVMTypeRef.Double, "fpext");
            var fmt = builder.BuildGlobalStringPtr($"{message}: %f\n", "fmt_float");
            LLVMValueRef[] args = new[] { fmt, vd };
            builder.BuildCall2(printfFnTy, printfFunction, args, "call_printf_f32");
            return;
        }

        // Double
        if (vt.Kind == LLVMTypeKind.LLVMDoubleTypeKind)
        {
            var fmt = builder.BuildGlobalStringPtr($"{message}: %f\n", "fmt_double");
            LLVMValueRef[] args = new[] { fmt, v };
            builder.BuildCall2(printfFnTy, printfFunction, args, "call_printf_f64");
            return;
        }

        // Pointer -> %p, cast to i8*
        if (vt.Kind == LLVMTypeKind.LLVMPointerTypeKind)
        {
            var i8Ptr = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            var vp = builder.BuildBitCast(v, i8Ptr, "bitcast_i8p");
            var fmt = builder.BuildGlobalStringPtr($"{message}: %p\n", "fmt_ptr");
            LLVMValueRef[] args = new[] { fmt, vp };
            builder.BuildCall2(printfFnTy, printfFunction, args, "call_printf_ptr");
            return;
        }

        // Fallback: just message
        {
            var fmt = builder.BuildGlobalStringPtr(message + "\n", "fmt_default");
            LLVMValueRef[] args = new[] { fmt };
            builder.BuildCall2(printfFnTy, printfFunction, args, "call_printf_default");
        }
    }
}
