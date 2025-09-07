namespace Cobra.Interpreter;

public static class CobraLiteralHelper
{
    public static bool IsNumeric(object? o) => o is long or double or int or float;

    public static bool IsTruthy(object? o)
    {
        return o switch
        {
            null => false,
            bool b => b,
            long i => i != 0,
            double d => d != 0.0,
            string s => !string.IsNullOrEmpty(s),
            _ => true
        };
    }

    public static string UnescapeString(string token)
    {
        var inner = token.Substring(1, token.Length - 2);
        return inner
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");
    }

    public static string UnescapeBacktickString(string token)
    {
        var inner = token.Substring(1, token.Length - 2);
        return inner.Replace("``", "`");
    }
}