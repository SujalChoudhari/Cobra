namespace Cobra.Interpreter;

public class ReturnValue
{
    public readonly object? Value;
    public ReturnValue(object? value) => Value = value;
}

public class BreakValue
{
    public static readonly BreakValue Instance = new();

    private BreakValue()
    {
    }
}

public class ContinueValue
{
    public static readonly ContinueValue Instance = new();

    private ContinueValue()
    {
    }
}