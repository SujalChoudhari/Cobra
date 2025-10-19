using Cobra.Environment;
using System;
using System.Globalization;

namespace Cobra.Interpreter
{
    public static class CobraTypeHelper
    {
        public static bool IsNumeric(object? obj) => obj is sbyte or byte or short or ushort or int or uint or long or ulong or float or double;
        public static bool IsInteger(object? obj) => obj is sbyte or byte or short or ushort or int or uint or long or ulong;
        public static bool IsFloatingPoint(object? obj) => obj is float or double;

        public static CobraRuntimeTypes GetRuntimeType(object? val)
        {
            return val switch
            {
                null => CobraRuntimeTypes.Null,
                sbyte => CobraRuntimeTypes.Int8,
                byte => CobraRuntimeTypes.UInt8,
                short => CobraRuntimeTypes.Int16,
                ushort => CobraRuntimeTypes.UInt16,
                int => CobraRuntimeTypes.Int32,
                uint => CobraRuntimeTypes.UInt32,
                long => CobraRuntimeTypes.Int64,
                ulong => CobraRuntimeTypes.UInt64,
                float => CobraRuntimeTypes.Float32,
                double => CobraRuntimeTypes.Float64,
                bool => CobraRuntimeTypes.Bool,
                string => CobraRuntimeTypes.String,
                _ => throw new ArgumentOutOfRangeException(nameof(val), $"Unsupported C# type for GetRuntimeType: {val?.GetType().Name}")
            };
        }

        public static object? Cast(object? value, CobraRuntimeTypes targetType)
        {
            if (value == null) return null;

            try
            {
                return targetType switch
                {
                    CobraRuntimeTypes.Int8 => Convert.ToSByte(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.UInt8 => Convert.ToByte(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.Int16 => Convert.ToInt16(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.UInt16 => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.Int32 => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.UInt32 => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.Int64 => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.UInt64 => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.Float32 => Convert.ToSingle(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.Float64 => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                    CobraRuntimeTypes.Bool => CobraLiteralHelper.IsTruthy(value),
                    CobraRuntimeTypes.String => value.ToString(),
                    _ => value // No cast needed for other types
                };
            }
            catch (OverflowException)
            {
                throw new CobraRuntimeException($"Cannot cast value '{value}' to type '{targetType}', it is out of range.");
            }
            catch (Exception)
            {
                throw new CobraRuntimeException($"Cannot cast value '{value}' to type '{targetType}'.");
            }
        }

        public static (object Left, object Right, CobraRuntimeTypes ResultType) PromoteNumericsForBinaryOp(object? left, object? right)
        {
            if (left == null || right == null || !IsNumeric(left) || !IsNumeric(right))
                throw new CobraRuntimeException("Numeric promotion requires two non-null numeric types.");

            var leftType = GetRuntimeType(left);
            var rightType = GetRuntimeType(right);
            
            if (leftType == CobraRuntimeTypes.Float64 || rightType == CobraRuntimeTypes.Float64)
                return (Convert.ToDouble(left), Convert.ToDouble(right), CobraRuntimeTypes.Float64);
            if (leftType == CobraRuntimeTypes.Float32 || rightType == CobraRuntimeTypes.Float32)
                return (Convert.ToSingle(left), Convert.ToSingle(right), CobraRuntimeTypes.Float32);
            
            if (leftType == CobraRuntimeTypes.UInt64 || rightType == CobraRuntimeTypes.UInt64)
            {
                // Promote to ulong, but if one is signed, promote to double to avoid wrapping
                if (left is sbyte or short or int or long || right is sbyte or short or int or long)
                    return (Convert.ToDouble(left), Convert.ToDouble(right), CobraRuntimeTypes.Float64);
                return (Convert.ToUInt64(left), Convert.ToUInt64(right), CobraRuntimeTypes.UInt64);
            }

            if (leftType == CobraRuntimeTypes.Int64 || rightType == CobraRuntimeTypes.Int64)
                return (Convert.ToInt64(left), Convert.ToInt64(right), CobraRuntimeTypes.Int64);
            if (leftType == CobraRuntimeTypes.UInt32 || rightType == CobraRuntimeTypes.UInt32)
                return (Convert.ToInt64(left), Convert.ToInt64(right), CobraRuntimeTypes.Int64);

            if (leftType == CobraRuntimeTypes.Int32 || rightType == CobraRuntimeTypes.Int32)
                return (Convert.ToInt32(left), Convert.ToInt32(right), CobraRuntimeTypes.Int32);
            if (leftType == CobraRuntimeTypes.UInt16 || rightType == CobraRuntimeTypes.UInt16)
                return (Convert.ToInt32(left), Convert.ToInt32(right), CobraRuntimeTypes.Int32);

            if (leftType == CobraRuntimeTypes.Int16 || rightType == CobraRuntimeTypes.Int16)
                return (Convert.ToInt16(left), Convert.ToInt16(right), CobraRuntimeTypes.Int16);
            if (leftType == CobraRuntimeTypes.UInt8 || rightType == CobraRuntimeTypes.UInt8)
                return (Convert.ToInt16(left), Convert.ToInt16(right), CobraRuntimeTypes.Int16);

            return (Convert.ToSByte(left), Convert.ToSByte(right), CobraRuntimeTypes.Int8);
        }
    }
}