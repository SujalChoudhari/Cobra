namespace Cobra.Environment;

public class CobraMarkup(string content)
{
    public string RawContent { get; } = content;

    public override string ToString() => RawContent;
}
