using System;

namespace Cobra.Environment
{
    public class CobraHandle(IntPtr pointer)
    {
        public IntPtr Pointer { get; } = pointer;

        public override string ToString() => $"<handle {Pointer}>";
    }
}