namespace Cobra.Interpreter;

public static class CobraLiteralHelper
{
    public static bool IsNumeric(object? o) => o is long || o is double || o is int || o is float;

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