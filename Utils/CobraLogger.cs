using LLVMSharp.Interop;
using System;

namespace Cobra.Utils;

/// <summary>
/// Defines the different logging levels for the compiler's console output.
/// </summary>
public enum LogLevel
{
    Off,
    Error,
    Warn,
    Info,
    Success // Added for positive feedback
}

/// <summary>
/// A static utility class for logging compiler messages to the console with color-coding.
/// </summary>
public static class CobraLogger
{
    /// <summary>
    /// Controls the minimum level of messages to be displayed.
    /// </summary>
    public static LogLevel Level { get; set; } = LogLevel.Info;

    /// <summary>
    /// Enables or disables the injection of `printf` calls into the generated code for runtime debugging.
    /// </summary>
    public static bool EnableRuntime { get; set; } = false;

    /// <summary>
    /// Holds the reference to the LLVM `printf` function, declared in the module.
    /// This must be initialized before calling the Runtime log function.
    /// </summary>
    public static LLVMValueRef PrintfFunction;

    // --- Public Logging Methods ---

    /// <summary>
    /// Logs a success message in green. Use this for successful compilation stages.
    /// </summary>
    public static void Success(string message)
    {
        Log(LogLevel.Success, $"[SUCCESS] {message}", ConsoleColor.Green);
    }

    /// <summary>
    /// Logs a standard informational message in white.
    /// </summary>
    public static void Info(string message)
    {
        Log(LogLevel.Info, $"[INFO]    {message}", ConsoleColor.White);
    }

    /// <summary>
    /// Logs a warning message in yellow. Use this for non-critical issues.
    /// </summary>
    public static void Warn(string message)
    {
        Log(LogLevel.Warn, $"[WARN]    {message}", ConsoleColor.Yellow);
    }

    /// <summary>
    /// Logs an error message in red. Use this for critical, compilation-halting issues.
    ///<_summary>
    public static void Error(string? message)
    {
        Log(LogLevel.Error, $"[ERROR]   {message}", ConsoleColor.Red);
    }

    // --- Runtime Logging ---

    /// <summary>
    /// Injects a `printf` call into the LLVM IR to log a message when the compiled program is executed.
    /// </summary>
    /// <param name="builder">The LLVM IR builder.</param>
    /// <param name="module">The LLVM module.</param>
    /// <param name="message">The message to be printed at runtime.</param>
    public static void Runtime(LLVMBuilderRef builder, LLVMModuleRef module, string message)
    {
        if (EnableRuntime && PrintfFunction.Handle != IntPtr.Zero)
        {
            // Assuming CobraVerboseRunnerHelper exists and contains this method.
            // This structure remains as you provided.
            Cobra.Compiler.CobraVerboseRunnerHelper.AddPrintStatement(builder, module, PrintfFunction, message);
        }
    }

    // --- Private Core Logic ---

    /// <summary>
    /// The core logging method that handles level checking and console color output.
    /// </summary>
    private static void Log(LogLevel level, string message, ConsoleColor color)
    {
        // Only log if the specified level is at or above the class's current log level.
        if (Level >= level)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}