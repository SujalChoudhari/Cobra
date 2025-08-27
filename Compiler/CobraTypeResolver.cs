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

        // If there are array specifiers `[]`, this is a pointer to the base type.
        if (context.LBRACKET()?.Length > 0)
        {
            // For now, we only support single-dimensional arrays.
            // A local array variable is best represented as a pointer to its element type.
            return LLVMTypeRef.CreatePointer(baseType, 0);
        }

        return baseType;
    }
}