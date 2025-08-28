using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler.Visitors;

internal class CobraFunctionVisitor
{
    private readonly CobraProgramVisitor _visitor;
    private LLVMModuleRef _module;
    private LLVMBuilderRef _builder;

    internal CobraFunctionVisitor(CobraProgramVisitor mainVisitor)
    {
        _visitor = mainVisitor;
        _module = mainVisitor.Module;
        _builder = mainVisitor.Builder;
    }

    /// <summary>
    /// PASS 1: Declares the function signature (prototype) without the body.
    ///This allows functions to be called before they are defined.
    /// </summary>
    public void VisitFunctionDeclaration_Pass1(CobraParser.FunctionDeclarationContext context)
    {
        var functionName = context.ID().GetText();
        // NEW: Prepend namespace if provided
        
        if (_visitor.Functions.ContainsKey(functionName))
        {
            // It's okay to re-declare a function prototype if it's identical, but for now we can just skip.
            // This happens when multiple files import the same module.
            return;
        }

        var returnType = CobraTypeResolver.ResolveType(context.type());
        var paramTypes = new List<LLVMTypeRef>();
        if (context.parameterList() != null)
        {
            paramTypes.AddRange(context.parameterList().parameter()
                .Select(param => CobraTypeResolver.ResolveType(param.type())));
        }

        var functionType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray());
        // Use the qualified name for the function in the LLVM module
        var function = _module.AddFunction(functionName, functionType);

        _visitor.Functions[functionName] = function;
        CobraLogger.Success($"Declared function prototype: {functionName}");
    }


    /// <summary>
    /// PASS 2: Fills in the function body with instructions.
    /// </summary>
    public LLVMValueRef VisitFunctionDeclaration_Pass2(CobraParser.FunctionDeclarationContext context)
    {
        var functionName = context.ID().GetText();
        
        var function = _visitor.Functions[functionName];

        var originalBlock = _builder.InsertBlock;

        var oldFunction = _visitor.CurrentFunction;
        var oldIsGlobal = _visitor.IsGlobalScope;
        _visitor.CurrentFunction = function;
        _visitor.IsGlobalScope = false;

        var entryBlock = function.AppendBasicBlock("entry");
        _builder.PositionAtEnd(entryBlock);

        _visitor.ScopeManagement.EnterScope();
        try
        {
            if (context.parameterList() != null)
            {
                for (int i = 0; i < context.parameterList().parameter().Length; i++)
                {
                    var paramContext = context.parameterList().parameter(i);
                    var paramName = paramContext.ID().GetText();
                    var paramValue = function.GetParam((uint)i);
                    paramValue.Name = paramName;

                    var allocation = _builder.BuildAlloca(paramValue.TypeOf, paramName);
                    _builder.BuildStore(paramValue, allocation);
                    _visitor.ScopeManagement.DeclareVariable(paramName, allocation);
                }
            }

            _visitor.Visit(context.block());

            if (function.TypeOf.ElementType.ReturnType.Kind == LLVMTypeKind.LLVMVoidTypeKind)
            {
                if (_builder.InsertBlock.Terminator == default)
                {
                    _builder.BuildRetVoid();
                }
            }
        }
        finally
        {
            _visitor.ScopeManagement.ExitScope();

            if (originalBlock.Handle != IntPtr.Zero)
            {
                _builder.PositionAtEnd(originalBlock);
            }

            _visitor.CurrentFunction = oldFunction;
            _visitor.IsGlobalScope = oldIsGlobal;
        }

        CobraLogger.Success($"Defined function body: {functionName}");
        return function;
    }

    public LLVMValueRef VisitExternDeclaration(CobraParser.ExternDeclarationContext context)
    {
        var functionName = context.ID().GetText();

        if (_visitor.Functions.ContainsKey(functionName))
        {
            throw new Exception($"Function '{functionName}' is already declared.");
        }

        var returnType = CobraTypeResolver.ResolveType(context.type());

        // For variadic functions like printf, we need to know the fixed parameters
        var paramTypes = new List<LLVMTypeRef>();
        bool isVariadic = false;
        if (context.parameterList() != null)
        {
            foreach (var param in context.parameterList().parameter())
            {
                //TODO: Use the convention for variadic functions
                // A simple convention for variadic: use '...' as the last parameter name
                // The grammar doesn't support this, so we'll treat it as a special case
                // for now. For printf, let's assume it's variadic if it's named 'printf'.
                // A better approach would be to add `...` to the grammar.
                paramTypes.Add(CobraTypeResolver.ResolveType(param.type()));
            }
        }

        if (functionName == "printf")
        {
            isVariadic = true;
        }

        var functionType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray(), isVariadic);
        var function = _module.AddFunction(functionName, functionType);

        _visitor.Functions[functionName] = function;
        CobraLogger.Success($"Declared extern function prototype: {functionName}");
        return function;
    }
}