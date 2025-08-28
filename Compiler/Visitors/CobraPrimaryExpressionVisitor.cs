using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraPrimaryExpressionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private LLVMBuilderRef _builder;
    private LLVMModuleRef _module;

    internal CobraPrimaryExpressionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _builder = mainVisitor.Builder;
        _module = mainVisitor.Module;
    }

    public LLVMValueRef VisitPostfixExpression(CobraParser.PostfixExpressionContext context)
    {
        var primaryCtx = context.primary();
        var idNode = primaryCtx?.ID();

        if (idNode != null && context.ChildCount > 1 && context.GetChild(1).GetText() == "(")
        {
            // Function call logic (unchanged)
            var functionName = idNode.GetText();
            if (!_visitor.Functions.TryGetValue(functionName, out var function))
                throw new Exception($"Undeclared function: '{functionName}'");

            var argList = context.argumentList(0);
            var args = new List<LLVMValueRef>();
            if (argList != null)
                args.AddRange(argList.expression().Select(argExpr => _visitor.Visit(argExpr)));

            return _builder.BuildCall2(function.TypeOf.ElementType, function, args.ToArray(), "call_tmp");
        }

        // Base value: either a literal, or a loaded variable
        LLVMValueRef baseValue = _visitor.Visit(primaryCtx);

        // Process postfix operators (++, --, [...])
        for (int i = 1; i < context.ChildCount; i++)
        {
            string op = context.GetChild(i).GetText();

            switch (op)
            {
                case "++":
                case "--":
                {
                    var varName = idNode?.GetText() ?? throw new Exception("Invalid lvalue for postfix inc/dec");
                    var addr = _visitor.ScopeManagement.FindVariable(varName);

                    var oldVal = baseValue; // Postfix returns the original value
                    var one = LLVMValueRef.CreateConstInt(baseValue.TypeOf, 1);
                    var newVal = op == "++"
                        ? _builder.BuildAdd(oldVal, one, "post_inc")
                        : _builder.BuildSub(oldVal, one, "post_dec");
                    _builder.BuildStore(newVal, addr);
                    baseValue = oldVal; // The expression's value is the old value
                    break;
                }
                case "[":
                {
                    // This is an R-value access (reading from arr[i])
                    var indexExpr = context.expression(i - 1);
                    var indexVal = _visitor.Visit(indexExpr);

                    var elementType = baseValue.TypeOf.ElementType; // baseValue is the loaded pointer (i32*)

                    var ptr = _builder.BuildGEP2(elementType, baseValue, new[] { indexVal }, "element_ptr");
                    baseValue = _builder.BuildLoad2(elementType, ptr, "load_element");
                    i++; // Manually advance past the matching ']'
                    break;
                }
            }
        }

        return baseValue;
    }

    public LLVMValueRef VisitPrimary(CobraParser.PrimaryContext context)
    {
        if (context.expression() != null && context.NEW() == null) return _visitor.Visit(context.expression());

        if (context.BITWISE_AND() != null)
        {
            if (context.ID() != null)
            {
                var variableName = context.ID().GetText();
                var varRef = _visitor.ScopeManagement.FindVariable(variableName);
                // Return the pointer (do NOT load)
                return varRef;
            }

            throw new Exception("Address-of operator currently only supports variables");
        }

        if (context.NEW() != null)
        {
            // Handle array allocation: new int[5]
            var typeSpecifierCtx = context.typeSpecifier();
            var sizeExpr = context.expression(); // Inside [size]
            var sizeVal = _visitor.Visit(sizeExpr); // Should be i32

            // Resolve element type (int, float, etc.)
            var tempTypeCtx = new CobraParser.TypeContext((ParserRuleContext)typeSpecifierCtx.Parent,
                typeSpecifierCtx.invokingState)
            {
                children = new List<IParseTree> { typeSpecifierCtx }
            };

            var elementType = CobraTypeResolver.ResolveType(tempTypeCtx);

            var pointerType = LLVMTypeRef.CreatePointer(elementType, 0);

            // Convert size to i64
            var size64 = _builder.BuildZExt(sizeVal, LLVMTypeRef.Int64, "size64");

            // Compute total allocation size = size * sizeof(elementType)
            ulong elementSize = (elementType.IntWidth / 8); // Works for int/float
            var elementSizeConst = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int64, elementSize);
            var totalSize = _builder.BuildMul(size64, elementSizeConst, "total_alloc_size");

            // Call malloc
            var mallocType = LLVMTypeRef.CreateFunction(
                LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), // Return type: i8*
                [LLVMTypeRef.Int64]); // Argument: size (i64);

            var mallocFunc = _module.GetNamedFunction("malloc");
            if (mallocFunc.Handle == IntPtr.Zero)
            {
                mallocFunc = _module.AddFunction("malloc", mallocType);
            }

            // Correct call with explicit type
            var rawMem = _builder.BuildCall2(mallocType, mallocFunc, new[] { totalSize }, "raw_mem");


            // Cast to element pointer type (int* for int[])
            var arrayPtr = _builder.BuildBitCast(rawMem, pointerType, "array_ptr");
            return arrayPtr;
        }

        if (context.literal() != null)
        {
            var literal = context.literal();
            if (literal.INTEGER() != null)
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)int.Parse(literal.INTEGER().GetText()));
            if (literal.FLOAT_LITERAL() != null)
                return LLVMValueRef.CreateConstReal(LLVMTypeRef.Float, double.Parse(literal.FLOAT_LITERAL().GetText()));
            if (literal.BOOLEAN_LITERAL() != null)
            {
                var value = literal.BOOLEAN_LITERAL().GetText() == "true";
                return LLVMValueRef.CreateConstInt(LLVMTypeRef.Int1, (ulong)(value ? 1 : 0));
            }

            if (literal.STRING_LITERAL() != null)
            {
                var rawString = literal.STRING_LITERAL().GetText();
                var unquotedString = rawString.Substring(1, rawString.Length - 2);
                var finalString = Regex.Unescape(unquotedString);
                return _builder.BuildGlobalStringPtr(finalString, ".str");
            }

            if (literal.NULL() != null)
                return LLVMValueRef.CreateConstNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
        }


        if (context.ID() != null)
        {
            var variableName = context.ID().GetText();
            // This is now safe because we know from the logic in VisitPostfixExpression
            // that if we are here, it must be a variable, not a function call.
            var varRef = _visitor.ScopeManagement.FindVariable(variableName);
            return _builder.BuildLoad2(varRef.TypeOf.ElementType, varRef, variableName);
        }

        throw new Exception("Unsupported primary expression");
    }
}