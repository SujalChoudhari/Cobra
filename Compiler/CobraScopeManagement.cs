using LLVMSharp.Interop;

namespace Cobra.Compiler;

internal class CobraScopeManagement
{
    private readonly Stack<Dictionary<string, LLVMValueRef>> _scopes = new();
    private readonly Dictionary<string, LLVMValueRef> _globals = new();

    internal void EnterScope() => _scopes.Push(new Dictionary<string, LLVMValueRef>());
    internal void ExitScope() => _scopes.Pop();

    internal void DeclareVariable(string name, LLVMValueRef value, bool isGlobal = false)
    {
        if (isGlobal)
        {
            if (!_globals.TryAdd(name, value))
                throw new Exception($"Global variable '{name}' is already declared.");
            return;
        }

        if (_scopes.Count == 0) EnterScope();
        if (_scopes.Peek().ContainsKey(name))
            throw new Exception($"Variable '{name}' is already declared in the current scope.");
        _scopes.Peek()[name] = value;
    }

    internal LLVMValueRef FindVariable(string name)
    {
        // Search local scopes first
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out var value)) return value;
        }

        // Search globals if not found locally
        if (_globals.TryGetValue(name, out var gvalue)) return gvalue;

        throw new Exception($"Undeclared variable: '{name}'");
    }
}