using Cobra.Compiler;
using LLVMSharp.Interop;

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
    Success
}

/// <summary>
/// A static utility class for logging compiler messages to the console with color-coding
/// and for injecting runtime logging into the generated LLVM IR.
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
    public static bool EnableRuntime { get; set; }

    /// <summary>
    /// Holds the reference to the LLVM `printf` function, declared in the module.
    /// This must be initialized before calling the Runtime log functions.
    /// </summary>
    public static LLVMValueRef PrintfFunction;

    /// <summary>
    /// Logs a success message in green.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Success(string message)
    {
        Log(LogLevel.Success, $"[SUCCESS] {message}", ConsoleColor.Green);
    }

    /// <summary>
    /// Logs a standard informational message in white.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Info(string message)
    {
        Log(LogLevel.Info, $"[INFO]    {message}", ConsoleColor.White);
    }

    /// <summary>
    /// Logs a warning message in yellow.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Warn(string message)
    {
        Log(LogLevel.Warn, $"[WARN]    {message}", ConsoleColor.Yellow);
    }

    /// <summary>
    /// Logs an error message in red.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Error(string? message)
    {
        Log(LogLevel.Error, $"[ERROR]   {message}", ConsoleColor.Red);
    }

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
            CobraVerboseRunnerHelper.AddPrintStatement(builder, module, PrintfFunction, $"[RUNTIME]  {message}");
        }
    }

    /// <summary>
    /// Injects a `printf` call to log a message and the value of a variable at runtime.
    /// </summary>
    /// <param name="builder">The LLVM IR builder.</param>
    /// <param name="module">The LLVM module.</param>
    /// <param name="message">The message to be printed.</param>
    /// <param name="variableValue">The LLVM value reference for the variable to be printed.</param>
    public static void RuntimeVariableValue(LLVMBuilderRef builder, LLVMModuleRef module, string message, LLVMValueRef variableValue)
    {
        if (EnableRuntime && PrintfFunction.Handle != IntPtr.Zero)
        {
            CobraVerboseRunnerHelper.AddPrintVariable(builder, module, PrintfFunction, $"[RUNTIME]  {message}", variableValue);
        }
    }

    /// <summary>
    /// The core logging method that handles level checking and console color output.
    /// </summary>
    /// <param name="messageLevel">The log level of the message.</param>
    /// <param name="message">The message string to display.</param>
    /// <param name="color">The color to use for the message.</param>
    private static void Log(LogLevel messageLevel, string message, ConsoleColor color)
    {
        if (Level <= messageLevel)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}