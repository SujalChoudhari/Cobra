using LLVMSharp.Interop;

namespace Cobra.Compiler;

public class CobraTypeResolver
{
    public static LLVMTypeRef ResolveType(CobraParser.TypeContext context)
    {
        // TODO: Handle array types `(LBRACKET RBRACKET)*`
        var typeName = context.typeSpecifier().GetText();
        return typeName switch
        {
            "int" => LLVMTypeRef.Int32,
            "float" => LLVMTypeRef.Float,
            "string" => LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0),
            "bool" => LLVMTypeRef.Int1,
            "void" => LLVMTypeRef.Void,
            _ => throw new Exception($"Invalid type specified: {typeName}") // TODO: Handle custom class types
        };
    }
}