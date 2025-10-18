using System.Globalization;

namespace Cobra.Interpreter;

public partial class CobraInterpreter
{
    public override object? VisitLiteral(CobraParser.LiteralContext context)
    {
        if (context.INTEGER() != null) return long.Parse(context.GetText(), CultureInfo.InvariantCulture);
        if (context.FLOAT_LITERAL() != null) return double.Parse(context.GetText(), CultureInfo.InvariantCulture);
        if (context.STRING_LITERAL() != null) return CobraLiteralHelper.UnescapeString(context.GetText());
        if (context.BACKTICK_STRING() != null) return CobraLiteralHelper.UnescapeBacktickString(context.GetText());
        if (context.TRUE() != null) return true;
        if (context.FALSE() != null) return false;
        if (context.NULL() != null) return null;
        throw new NotSupportedException($"Unknown literal: {context.GetText()}");
    }

    public override object VisitArrayLiteral(CobraParser.ArrayLiteralContext context)
    {
        return context.assignmentExpression()?.Select(Visit).ToList() ?? new List<object?>();
    }

    public override object VisitDictLiteral(CobraParser.DictLiteralContext context)
    {
        return context.dictEntry()?.ToDictionary(
            entry => entry.STRING_LITERAL() != null
                ? CobraLiteralHelper.UnescapeString(entry.STRING_LITERAL().GetText())
                : entry.ID().GetText(),
            entry => Visit(entry.assignmentExpression())) ?? new Dictionary<string, object?>();
    }
}