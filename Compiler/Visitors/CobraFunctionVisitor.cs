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

        if (_visitor.Functions.ContainsKey(functionName))
        {
            CobraLogger.Error($"Function '{functionName}' is already declared.");
            throw new Exception($"Function '{functionName}' is already declared.");
        }

        var returnType = CobraTypeResolver.ResolveType(context.type());
        var paramTypes = new List<LLVMTypeRef>();
        if (context.parameterList() != null)
        {
            paramTypes.AddRange(context.parameterList().parameter().Select(param => CobraTypeResolver.ResolveType(param.type())));
        }

        var functionType = LLVMTypeRef.CreateFunction(returnType, paramTypes.ToArray());
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

        // --- CRITICAL FIX: Save the builder's current position ---
        var originalBlock = _builder.InsertBlock;

        var oldFunction = _visitor.CurrentFunction;
        var oldIsGlobal = _visitor.IsGlobalScope;
        _visitor.CurrentFunction = function;
        _visitor.IsGlobalScope = false; // We are inside a function now

        var entryBlock = function.AppendBasicBlock("entry");
        _builder.PositionAtEnd(entryBlock);

        _visitor.ScopeManagement.EnterScope();
        try
        {
            // Process parameters
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

            // Add a return if one doesn't exist for void functions
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
            
            // --- CRITICAL FIX: Restore the builder's position and state ---
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
    
}