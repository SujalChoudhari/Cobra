using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler;

internal class CobraScopeManagement
{
    private readonly Stack<Dictionary<string, LLVMValueRef>> _scopes = new();

    internal void EnterScope() => _scopes.Push(new Dictionary<string, LLVMValueRef>());
    internal void ExitScope() => _scopes.Pop();

    internal void DeclareVariable(string name, LLVMValueRef value)
    {
        if (_scopes.Count == 0) EnterScope();
        if (_scopes.Peek().ContainsKey(name))
        {

            CobraLogger.Error($"Variable '{name}' is already declared in the current scope.");
            throw new Exception($"Cannot declare variable. Variable '{name}' is already declared.");
        }
        _scopes.Peek()[name] = value;
    }

    internal LLVMValueRef FindVariable(string name)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out var value)) return value;
        }

        CobraLogger.Error($"Undeclared variable: '{name}'");
        throw new Exception($"Undeclared variable: '{name}'");
    }

}