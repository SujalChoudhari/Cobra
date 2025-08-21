using Cobra.Compiler;

namespace Cobra.Utils;

using System;
using LLVMSharp.Interop;

// Define the different logging levels.
public enum LogLevel
{
    Off,
    Error,
    Warn,
    Info
}

public static class CobraLogger
{
    // A single property to control the logging level.
    public static LogLevel Level = LogLevel.Info;

    // A flag to enable or disable runtime logging in the generated code.
    public static bool EnableRuntime = false;

    public static LLVMValueRef printfFunction = null;

    public static void Info(string message)
    {
        if (Level >= LogLevel.Info)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[INFO] {message}");
            Console.ResetColor();
        }
    }

    public static void Warn(string message)
    {
        if (Level >= LogLevel.Warn)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARN] {message}");
            Console.ResetColor();
        }
    }

    public static void Error(string? message)
    {
        if (Level >= LogLevel.Error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Adds a print statement directly to the generated LLVM IR code.
    /// This log will appear when the final compiled binary is executed.
    /// </summary>
    /// <param name="builder">The LLVM IR builder.</param>
    /// <param name="module">The LLVM module.</param>
    /// <param name="printfFunction">The LLVM value for the printf function.</param>
    /// <param name="message">The string message to be printed at runtime.</param>
    public static void Runtime(LLVMBuilderRef builder, LLVMModuleRef module,
        string message)
    {
        if (EnableRuntime)
        {
            // Use the CobraVerboseRunnerHelper to add a print statement to the IR
            CobraVerboseRunnerHelper.AddPrintStatement(builder, module, printfFunction, message);
        }
    }
}