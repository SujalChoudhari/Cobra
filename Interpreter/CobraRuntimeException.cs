namespace Cobra.Interpreter;

public class CobraRuntimeException(string message, CobraStackTrace? stackTrace = null) : Exception(message)
{
    public CobraStackTrace? StackTraceValue { get; } = stackTrace;
}