namespace Cobra.Interpreter;

public class CobraReturnValue(object? value)
{
    public readonly object? Value = value;
}

public class CobraBreakValue
{
    public static readonly CobraBreakValue Instance = new();

    private CobraBreakValue()
    {
    }
}

public class CobraContinueValue
{
    public static readonly CobraContinueValue Instance = new();

    private CobraContinueValue()
    {
    }
}

public class CobraThrowValue(object thrownObject, CobraStackTrace? stackTrace)
{
    public readonly object ThrownObject = thrownObject;
    public readonly CobraStackTrace? StackTrace = stackTrace;
}