using Antlr4.Runtime;
using Cobra.Environment;
using Cobra.Utils;
using System.Runtime.InteropServices;

namespace Cobra.Interpreter;

public partial class CobraInterpreter
{
    public override object? VisitNamespaceDeclaration(CobraParser.NamespaceDeclarationContext context)
    {
        var parts = context.qualifiedName().ID().Select(id => id.GetText()).ToList();
        var targetEnvironment = _currentEnvironment;

        foreach (var part in parts)
        {
            object? existing;
            try
            {
                existing = targetEnvironment.GetVariable(part);
            }
            catch (Exception)
            {
                existing = null;
            }

            CobraNamespace? currentNamespace;
            if (existing is CobraNamespace nextNamespace)
            {
                currentNamespace = nextNamespace;
            }
            else if (existing == null)
            {
                currentNamespace = new CobraNamespace(part, targetEnvironment);
                targetEnvironment.DefineVariable(part, currentNamespace, isConst: true);
            }
            else
            {
                throw new Exception($"Identifier '{part}' already exists and is not a namespace.");
            }

            targetEnvironment = currentNamespace.Environment;
        }

        var previousEnv = _currentEnvironment;
        _currentEnvironment = targetEnvironment;
        try
        {
            foreach (var decl in context.children)
            {
                if (decl is not Antlr4.Runtime.Tree.ITerminalNode)
                    Visit(decl);
            }
        }
        finally
        {
            _currentEnvironment = previousEnv;
        }

        return null;
    }

    public override object? VisitImportStatement(CobraParser.ImportStatementContext context)
    {
        var importPathRaw = CobraLiteralHelper.UnescapeString(context.STRING_LITERAL().GetText());
        var resolvedPath = ResolveModulePath(importPathRaw, ".cb");

        if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            throw new FileNotFoundException($"Could not find module to import: '{importPathRaw}'");

        ExecuteFile(resolvedPath);
        return null;
    }

    public override object? VisitLinkStatement(CobraParser.LinkStatementContext context)
    {
        var libPathRaw = CobraLiteralHelper.UnescapeString(context.STRING_LITERAL().GetText());
        var resolvedPath = ResolveModulePath(libPathRaw, GetNativeLibExtension());

        if (string.IsNullOrEmpty(resolvedPath) || !File.Exists(resolvedPath))
            throw new FileNotFoundException($"Could not find native library to link: '{libPathRaw}'");

        if (!_loadedLibraries.ContainsKey(resolvedPath))
        {
            var handle = NativeLibrary.Load(resolvedPath);
            _loadedLibraries[resolvedPath] = handle;
        }

        _currentLinkingLibraryPath = resolvedPath;
        return null;
    }

    public override object? VisitExternDeclaration(CobraParser.ExternDeclarationContext context)
    {
        if (_currentLinkingLibraryPath == null)
            throw new InvalidOperationException("external declaration must be preceded by a link statement.");

        var nativeFuncName = context.STRING_LITERAL() != null
            ? CobraLiteralHelper.UnescapeString(context.STRING_LITERAL().GetText())
            : context.ID().GetText();

        var cobraFuncName = context.ID().GetText();
        var returnType = ParseType(context.type());
        var parameters = context.parameterList()?.parameter()
            .Select(p => (ParseType(p.type()), p.ID().GetText()))
            .ToList() ?? [];

        var externalFunc =
            new CobraExternalFunction(nativeFuncName, returnType, parameters, _currentLinkingLibraryPath);

        _currentEnvironment.DefineVariable(cobraFuncName, externalFunc, isConst: true);

        return null;
    }

    private CobraRuntimeTypes ParseType(CobraParser.TypeContext context)
    {
        return context.GetText() switch
        {
            "void" => CobraRuntimeTypes.Void,
            "int" => CobraRuntimeTypes.Int,
            "float" => CobraRuntimeTypes.Float,
            "bool" => CobraRuntimeTypes.Bool,
            "string" => CobraRuntimeTypes.String,
            "handle" => CobraRuntimeTypes.Handle,
            _ => throw new NotSupportedException(
                $"Type '{context.GetText()}' is not supported for external functions.")
        };
    }

    private string GetNativeLibExtension()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return ".dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return ".so";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return ".dylib";
        throw new NotSupportedException("Operating System not supported for native linking.");
    }

    private string? ResolveModulePath(string path, string extension)
    {
        path = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

        if (path.StartsWith("." + Path.DirectorySeparatorChar) ||
            path.StartsWith(".." + Path.DirectorySeparatorChar))
        {
            if (_sourceFileStack.Count == 0)
                throw new InvalidOperationException("Cannot resolve relative path from REPL or an unknown source.");

            var currentDir = Path.GetDirectoryName(_sourceFileStack.Peek());
            return Path.GetFullPath(Path.Combine(currentDir!, path));
        }

        if (!path.EndsWith(extension))
            path += extension;

        var assemblyLocation = AppDomain.CurrentDomain.BaseDirectory;

        var exeRelativePath = Path.GetFullPath(Path.Combine(assemblyLocation, path));
        if (File.Exists(exeRelativePath))
            return exeRelativePath;

        var stdlibPath = Path.GetFullPath(Path.Combine(assemblyLocation, CobraConstants.StdlibDirectory, path));
        if (File.Exists(stdlibPath))
            return stdlibPath;

        if (Path.IsPathRooted(path) && File.Exists(path))
            return path;

        return null;
    }

    private void ExecuteFile(string path)
    {
        var fullPath = Path.GetFullPath(path);

        if (!_alreadyImported.Add(fullPath))
            return;

        _sourceFileStack.Push(fullPath);

        try
        {
            var code = File.ReadAllText(fullPath);
            var inputStream = new AntlrInputStream(code);
            var lexer = new CobraLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new CobraParser(tokenStream);
            var tree = parser.program();
            Visit(tree);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error importing file '{path}': {ex.Message}", ex);
        }
        finally
        {
            _sourceFileStack.Pop();
        }
    }
}