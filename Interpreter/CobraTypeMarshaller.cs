using Cobra.Environment;
using System;

namespace Cobra.Interpreter
{
    public static class CobraTypeMarshaller
    {
        public static Type ToDotNetType(CobraRuntimeTypes cobraType)
        {
            return cobraType switch
            {
                CobraRuntimeTypes.Void => typeof(void),
                CobraRuntimeTypes.Int8 => typeof(sbyte),
                CobraRuntimeTypes.UInt8 => typeof(byte),
                CobraRuntimeTypes.Int16 => typeof(short),
                CobraRuntimeTypes.UInt16 => typeof(ushort),
                CobraRuntimeTypes.Int32 => typeof(int),
                CobraRuntimeTypes.UInt32 => typeof(uint),
                CobraRuntimeTypes.Int64 => typeof(long),
                CobraRuntimeTypes.UInt64 => typeof(ulong),
                CobraRuntimeTypes.Float32 => typeof(float),
                CobraRuntimeTypes.Float64 => typeof(double),
                CobraRuntimeTypes.Bool => typeof(bool),
                CobraRuntimeTypes.String => typeof(string),
                CobraRuntimeTypes.Handle => typeof(IntPtr),
                _ => throw new NotSupportedException($"The type '{cobraType}' cannot be used in an external function signature.")
            };
        }
    }
}