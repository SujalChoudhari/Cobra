using Cobra.Compiler;
using LLVMSharp.Interop;

namespace Cobra.Utils;

/// <summary>
/// Defines the different logging levels for the compiler's console output.
/// </summary>
public enum LogLevel
{
    Off = 0,
    Error = 1,
    Warn = 2,
    Success = 3,
    Info = 4,
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
    /// The core logging method that handles level checking and console color output.
    /// </summary>
    /// <param name="messageLevel">The log level of the message.</param>
    /// <param name="message">The message string to display.</param>
    /// <param name="color">The color to use for the message.</param>
    private static void Log(LogLevel messageLevel, string message, ConsoleColor color)
    {
        if (messageLevel > Level) return;

        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}