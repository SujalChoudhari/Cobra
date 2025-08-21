using System;
using LLVMSharp.Interop;

namespace Cobra.Compiler;

public class CobraVerboseRunnerHelper
{
    /// <summary>
    /// Adds a printf call to the LLVM IR to log a message during program execution.
    /// </summary>
    /// <param name="builder">The LLVM IR builder.</param>
    /// <param name="module">The LLVM module.</param>
    /// <param name="printfFunction">The LLVM value for the printf function.</param>
    /// <param name="message">The string message to be printed.</param>
    public static void AddPrintStatement(LLVMBuilderRef builder, LLVMModuleRef module, LLVMValueRef printfFunction, string message)
    {
        // Create a global string constant for the message.
        LLVMValueRef formatString = builder.BuildGlobalStringPtr(message + "\n", "format_string");
        
        // Build the call to the printf function.
        LLVMValueRef[] args = [formatString];
        builder.BuildCall2(printfFunction.TypeOf, printfFunction, args, "calltmp");
    }
    
    /// <summary>
    /// Adds a printf call to the LLVM IR to log a message and the value of a variable.
    /// </summary>
    /// <param name="builder">The LLVM IR builder.</param>
    /// <param name="module">The LLVM module.</param>
    /// <param name="printfFunction">The LLVM value for the printf function.</param>
    /// <param name="message">The string message to be printed.</param>
    /// <param name="variableValue">The LLVM value of the variable to print.</param>
    public static void AddPrintVariable(LLVMBuilderRef builder, LLVMModuleRef module, LLVMValueRef printfFunction, string message, LLVMValueRef variableValue)
    {
        LLVMTypeRef varType = variableValue.TypeOf;
        LLVMValueRef formatString;

        // Determine the correct format specifier based on the variable's type.
        if (varType.Kind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            formatString = builder.BuildGlobalStringPtr(message + ": %d\n", "format_string_int");
        }
        else if (varType.Kind == LLVMTypeKind.LLVMFloatTypeKind || varType.Kind == LLVMTypeKind.LLVMDoubleTypeKind)
        {
            formatString = builder.BuildGlobalStringPtr(message + ": %f\n", "format_string_float");
        }
        else
        {
            // Default to a generic message if the type is not recognized.
            formatString = builder.BuildGlobalStringPtr(message + "\n", "format_string_default");
            
            // Revert to a simple print statement if we don't have a format specifier.
            LLVMValueRef[] simpleArgs = new LLVMValueRef[] { formatString };
            builder.BuildCall2(printfFunction.TypeOf, printfFunction, simpleArgs, "calltmp");
            return;
        }

        LLVMValueRef[] args = new LLVMValueRef[] { formatString, variableValue };
        builder.BuildCall2(printfFunction.TypeOf, printfFunction, args, "calltmp");
    }
}