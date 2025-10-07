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
                CobraRuntimeTypes.Int => typeof(long),
                CobraRuntimeTypes.Float => typeof(double),
                CobraRuntimeTypes.Bool => typeof(bool),
                CobraRuntimeTypes.String => typeof(string),
                CobraRuntimeTypes.Handle => typeof(IntPtr),
                CobraRuntimeTypes.Null or
                    CobraRuntimeTypes.Dict or
                    CobraRuntimeTypes.List or
                    CobraRuntimeTypes.Function or
                    CobraRuntimeTypes.Markup or
                    CobraRuntimeTypes.Namespace =>
                    throw new NotSupportedException(
                        $"The complex type '{cobraType}' cannot be used in an external function signature. Only primitive types (int, float, bool, string, handle) are supported."),
                _ => throw new NotSupportedException($"Marshalling of type {cobraType} is not supported.")
            };
        }
    }
}