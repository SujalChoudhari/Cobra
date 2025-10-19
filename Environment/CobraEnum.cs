namespace Cobra.Environment
{
    public class CobraEnum(string name, Dictionary<string, CobraEnumMember> members)
    {
        public string Name { get; } = name;
        public Dictionary<string, CobraEnumMember> Members { get; } = members;

        public override string ToString() => $"<enum {Name}>";
    }
}