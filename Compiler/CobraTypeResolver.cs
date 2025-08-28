using LLVMSharp.Interop;

namespace Cobra.Compiler;

public abstract class CobraTypeResolver
{
    public static LLVMTypeRef ResolveType(CobraParser.TypeContext context)
    {
        var typeName = context.typeSpecifier().GetText();
        LLVMTypeRef baseType = typeName switch
        {
            "int" => LLVMTypeRef.Int32,
            "float" => LLVMTypeRef.Float,
            "string" => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
            "bool" => LLVMTypeRef.Int1,
            "void" => LLVMTypeRef.Void,
            _ => throw new Exception($"Invalid type specified: {typeName}") // TODO: Handle custom class types
        };

        if (context.MUL() != null)
        {
            return LLVMTypeRef.CreatePointer(baseType, 0);
        }

        if (context.LBRACKET()?.Length > 0)
        {
            // TODO: Support multiple dimensions array
            // TODO: Make arrays as struct and store the size
            return LLVMTypeRef.CreatePointer(baseType, 0);
        }

        return baseType;
    }
}