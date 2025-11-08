using System.Text;

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

    public static string Stringify(object? obj)
    {
        if (obj == null) return "null";

        switch (obj)
        {
            case List<object?> list:
                var listBuilder = new StringBuilder("[");
                for (int j = 0; j < list.Count; j++)
                {
                    listBuilder.Append(Stringify(list[j]));
                    if (j < list.Count - 1)
                        listBuilder.Append(", ");
                }
                listBuilder.Append("]");
                return listBuilder.ToString();

            case Dictionary<string, object?> dict:
                var dictBuilder = new StringBuilder("{");
                var i = 0;
                foreach (var (key, value) in dict)
                {
                    dictBuilder.Append($"\"{key}\": {Stringify(value)}");
                    if (i < dict.Count - 1)
                        dictBuilder.Append(", ");
                    i++;
                }
                dictBuilder.Append("}");
                return dictBuilder.ToString();
            
            case string s:
                return s;

            default:
                return obj.ToString() ?? "null";
        }
    }
}