namespace Cobra.Environment
{
    public class CobraEnumMember(string name, long value, CobraEnum enumType)
    {
        public string Name { get; } = name;
        public long Value { get; } = value;
        public CobraEnum EnumType { get; } = enumType;

        public override string ToString() => $"{EnumType.Name}.{Name}";
    }
}