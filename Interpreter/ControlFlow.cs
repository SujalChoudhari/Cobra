namespace Cobra.Interpreter;

// A wrapper for return values to differentiate them from other results.
public class ReturnValue
{
    public readonly object? Value;
    public ReturnValue(object? value) => Value = value;
}

// A singleton marker for break statements.
public class BreakValue
{
    public static readonly BreakValue Instance = new();
    private BreakValue() { }
}

// A singleton marker for continue statements.
public class ContinueValue
{
    public static readonly ContinueValue Instance = new();
    private ContinueValue() { }
}