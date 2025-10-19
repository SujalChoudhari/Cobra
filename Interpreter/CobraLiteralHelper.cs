namespace Cobra.Interpreter;

public static class CobraLiteralHelper
{
    public static bool IsNumeric(object? o) => o != null && CobraTypeHelper.IsNumeric(o);

    public static bool IsTruthy(object? o)
    {
        return o switch
        {
            null => false,
            bool b => b,
            sbyte i => i != 0,
            byte i => i != 0,
            short i => i != 0,
            ushort i => i != 0,
            int i => i != 0,
            uint i => i != 0,
            long i => i != 0,
            ulong i => i != 0,
            float d => d != 0.0f,
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