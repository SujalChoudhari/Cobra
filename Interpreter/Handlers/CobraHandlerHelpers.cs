using System.Globalization;
using System.Runtime.InteropServices;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Cobra.Environment;

namespace Cobra.Interpreter;

public partial class CobraInterpreter
{
    private CobraLValue EvaluateLValue(CobraParser.LeftHandSideContext context)
    {
        var container = Visit(context.primary());
        if (context.children.Count == 1)
        {
            if (context.primary().ID() != null)
            {
                return new CobraLValue(_currentEnvironment, context.primary().ID().GetText());
            }

            throw new Exception("Invalid LValue target.");
        }

        for (var i = 1; i < context.ChildCount - 2; i++)
        {
            var op = context.GetChild(i);
            if (op.GetText() == "[")
            {
                container = GetIndex(container, Visit(context.assignmentExpression(i / 2)));
                i += 2;
            }
            else if (op.GetText() == ".")
            {
                container = GetMember(container, context.ID(i / 2).GetText());
                i += 1;
            }
        }

        var lastOp = context.GetChild(context.ChildCount - 2);
        if (lastOp.GetText() == "[")
        {
            var indexExpr = context.assignmentExpression().Last();
            return new CobraLValue(container, Visit(indexExpr));
        }

        var idExpr = context.ID().Last();
        return new CobraLValue(container, idExpr.GetText());
    }


    private object? ResolveQualifiedName(CobraParser.QualifiedNameContext context)
    {
        var ids = context.ID();
        var currentObject = _currentEnvironment.GetVariable(ids[0].GetText());

        for (int i = 1; i < ids.Length; i++)
        {
            if (currentObject is CobraNamespace ns)
            {
                currentObject = ns.Environment.GetVariable(ids[i].GetText());
            }
            else
            {
                // If it's not a namespace, we can't go deeper
                throw new Exception($"Identifier '{ids[i - 1].GetText()}' is not a namespace.");
            }
        }

        return currentObject;
    }

    private int GetPostfixOperatorIndex(CobraParser.PostfixExpressionContext context, int opNodeIndex, int opType = -1)
    {
        int count = 0;
        var opText = opType == -1 ? context.GetChild(opNodeIndex).GetText() : new CommonToken(opType).Text;

        for (int i = 1; i < opNodeIndex; i++)
        {
            var child = context.GetChild(i);
            if (child is ITerminalNode terminalNode && (opType == -1 || terminalNode.Symbol.Type == opType))
            {
                if (terminalNode.GetText() == opText)
                    count++;
            }
        }

        return count;
    }

    private string GetLValueName(CobraParser.PrimaryContext context)
    {
        if (context.ID() != null)
        {
            return context.ID().GetText();
        }

        throw new Exception("Invalid target for postfix operator. Must be a simple variable.");
    }

    private object? GetIndex(object? collection, object? key)
    {
        if (collection is List<object?> list && (key is long or int)) return list[Convert.ToInt32(key)];
        if (collection is Dictionary<string, object?> dict && key is string sKey) return dict[sKey];
        throw new Exception("Object is not indexable or key is of wrong type.");
    }

    private object? GetMember(object? obj, string key)
    {
        switch (obj)
        {
            case CobraInstance instance:
                try
                {
                    return instance.Fields.GetVariable(key);
                }
                catch
                {
                    // ignored
                }

                if (instance.ClassDefinition.Methods.TryGetValue(key, out var method))
                    return method;

                throw new Exception($"Member '{key}' not found on instance of '{instance.ClassDefinition.Name}'.");

            case CobraClass classDef:
                return classDef.StaticEnvironment.GetVariable(key);
            
            case CobraEnum enumDef:
                if (enumDef.Members.TryGetValue(key, out var member))
                    return member;
                throw new Exception($"Member '{key}' not found in enum '{enumDef.Name}'.");
            
            case CobraEnumMember enumMember:
                return key switch
                {
                    "name" => enumMember.Name,
                    "value" => enumMember.Value,
                    _ => throw new Exception($"'{enumMember.EnumType.Name}.{enumMember.Name}' has no property '{key}'. Did you mean 'name' or 'value'?")
                };

            case CobraNamespace ns:
                return ns.Environment.GetVariable(key);

            default:
                throw new Exception("Object does not have members access with '.' operator.");
        }
    }

    public object? ExecuteFunctionCall(object? funcObject, List<object?> args, string funcNameForError,
        CobraInstance? instanceContext = null)
    {
        object? result = null;
        switch (funcObject)
        {
            case CobraUserDefinedFunction userFunc:
            {
                if (args.Count != userFunc.Parameters.Count)
                    throw new Exception(
                        $"Function '{userFunc.Name}' expects {userFunc.Parameters.Count} arguments but got {args.Count}.");

                var previous = _currentEnvironment;
                _currentEnvironment = new CobraEnvironment(userFunc.Closure);

                try
                {
                    if (instanceContext != null)
                    {
                        _currentEnvironment.DefineVariable("this", instanceContext, isConst: true);
                    }

                    for (var i = 0; i < userFunc.Parameters.Count; i++)
                    {
                        _currentEnvironment.DefineVariable(userFunc.Parameters[i].Name, args[i]);
                    }

                    result = ExecuteBlockStmts(userFunc.Body);
                }
                finally
                {
                    _currentEnvironment = previous;
                }

                break;
            }
            case CobraBuiltinFunction builtinFunc:
            {
                result = builtinFunc.Action(args);
                break;
            }
            case CobraExternalFunction externalFunc:
            {
                result = CallExternalFunction(externalFunc, args);
                break;
            }
            default:
                throw new Exception($"'{funcNameForError}' is not a function.");
        }

        if (result is CobraReturnValue returnValue)
        {
            return returnValue.Value;
        }

        if (result is CobraThrowValue)
        {
            return result;
        }

        return null; 
    }

    private object? CallExternalFunction(CobraExternalFunction func, List<object?> args)
    {
        if (!_loadedLibraries.TryGetValue(func.LibraryPath, out var libHandle))
            throw new InvalidOperationException($"Library '{func.LibraryPath}' not loaded.");

        if (!NativeLibrary.TryGetExport(libHandle, func.Name, out var funcPtr))
            throw new EntryPointNotFoundException(
                $"Function '{func.Name}' not found in library '{func.LibraryPath}'.");

        if (func.ReturnType == CobraRuntimeTypes.String)
        {
            var delegateType = CobraDelegateFactory.Create(typeof(IntPtr),
                func.Parameters.Select(p => CobraTypeMarshaller.ToDotNetType(p.Type)).ToArray());
            var delegateInstance = Marshal.GetDelegateForFunctionPointer(funcPtr, delegateType);

            object?[] marshalledArgs = new object[args.Count];
            for (int i = 0; i < args.Count; i++)
            {
                marshalledArgs[i] = args[i] is CobraHandle handle ? handle.Pointer : args[i];
            }

            var stringPtr = (IntPtr)delegateInstance.DynamicInvoke(marshalledArgs)!;
            if (stringPtr == IntPtr.Zero)
            {
                return "";
            }

            var result = Marshal.PtrToStringAnsi(stringPtr);

            if (!NativeLibrary.TryGetExport(libHandle, "str_free_string", out var freeFuncPtr)) return result;
            var freeDelegateType = CobraDelegateFactory.Create(typeof(void), typeof(IntPtr));
            var freeDelegate = Marshal.GetDelegateForFunctionPointer(freeFuncPtr, freeDelegateType);
            freeDelegate.DynamicInvoke(stringPtr);
            return result;
        }

        var returnType = CobraTypeMarshaller.ToDotNetType(func.ReturnType);
        var paramTypes = func.Parameters.Select(p => CobraTypeMarshaller.ToDotNetType(p.Type)).ToArray();

        var defaultDelegateType = CobraDelegateFactory.Create(returnType, paramTypes);
        var defaultDelegateInstance = Marshal.GetDelegateForFunctionPointer(funcPtr, defaultDelegateType);

        object?[] defaultMarshalledArgs = new object[args.Count];
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] is CobraHandle handle)
                defaultMarshalledArgs[i] = handle.Pointer;
            else
                defaultMarshalledArgs[i] = args[i]!;
        }

        object? resultObj = defaultDelegateInstance.DynamicInvoke(defaultMarshalledArgs);

        if (func.ReturnType == CobraRuntimeTypes.Handle && resultObj is IntPtr ptr)
            return new CobraHandle(ptr);

        return resultObj;
    }

    private object? EvaluateUnaryThenPostfix(CobraParser.BinaryExpressionContext context, ref int i)
    {
        var prefixOps = new List<string>();
        while (i < context.ChildCount && context.GetChild(i) is CobraParser.UnaryOpContext)
        {
            prefixOps.Add(context.GetChild(i).GetText());
            i++;
        }

        var value = Visit(context.GetChild(i));
        i++;
        for (var j = prefixOps.Count - 1; j >= 0; j--)
        {
            value = ApplyPrefixUnaryOperator(prefixOps[j], value);
        }

        return value;
    }

    private object? ApplyPrefixUnaryOperator(string op, object? value)
    {
        switch (op)
        {
            case "+": return value;
            case "-":
                if (value is long lv) return -lv;
                if (value is double dv) return -dv;
                throw new Exception($"Unary '-' not supported for {value?.GetType().Name}");
            case "!":
                return !CobraLiteralHelper.IsTruthy(value);
            default:
                throw new NotSupportedException($"Unary operator '{op}' not supported yet.");
        }
    }

    private object? ApplyBinaryOperator(string op, object? left, object? right)
    {
        if (op == "+" && (left is string || right is string))
            return (left?.ToString() ?? "") + (right?.ToString() ?? "");
        if (op is "==" or "!=" or "<" or ">" or "<=" or ">=")
        {
            if (left is CobraEnumMember lMember && right is CobraEnumMember rMember)
            {
                left = lMember.Value;
                right = rMember.Value;
            }
            
            if (left == null || right == null)
            {
                return op switch
                {
                    "==" => left == right, "!=" => left != right,
                    _ => throw new Exception("Cannot compare null with '<', '>', '<=', or '>='.")
                };
            }

            if (left is string lStr && right is string rStr)
            {
                var comparison = string.Compare(lStr, rStr, StringComparison.Ordinal);
                return op switch
                {
                    "==" => comparison == 0, "!=" => comparison != 0, "<" => comparison < 0, ">" => comparison > 0,
                    "<=" => comparison <= 0, ">=" => comparison >= 0,
                    _ => throw new InvalidOperationException()
                };
            }

            if (CobraLiteralHelper.IsNumeric(left) && CobraLiteralHelper.IsNumeric(right))
            {
            }
            else if (left.GetType() != right.GetType())
            {
                if (!CobraLiteralHelper.IsNumeric(left) || !CobraLiteralHelper.IsNumeric(right))
                    throw new Exception(
                        $"Cannot compare values of different types: {left.GetType().Name} and {right.GetType().Name}");
            }
        }

        if (!CobraLiteralHelper.IsNumeric(left) || !CobraLiteralHelper.IsNumeric(right))
            throw new Exception($"Operator '{op}' requires numeric operands for operands '{left}' and '{right}'.");
        if (left is double || right is double || op == "/")
        {
            double l = Convert.ToDouble(left, CultureInfo.InvariantCulture);
            double r = Convert.ToDouble(right, CultureInfo.InvariantCulture);
            return op switch
            {
                "+" => l + r, "-" => l - r, "*" => l * r,
                "/" => r == 0 ? throw new DivideByZeroException() : l / r,
                "%" => r == 0 ? throw new DivideByZeroException() : l % r,
                ">" => l > r, "<" => l < r, ">=" => l >= r, "<=" => l <= r, "==" => Math.Abs(l - r) < 0.000001,
                "!=" => Math.Abs(l - r) > 0.000001,
                _ => throw new NotSupportedException($"Operator '{op}' not supported.")
            };
        }
        else
        {
            long l = Convert.ToInt64(left, CultureInfo.InvariantCulture);
            long r = Convert.ToInt64(right, CultureInfo.InvariantCulture);
            return op switch
            {
                "+" => l + r, "-" => l - r, "*" => l * r,
                "/" => r == 0 ? throw new DivideByZeroException() : (double)l / r,
                "%" => r == 0 ? throw new DivideByZeroException() : l % r,
                ">" => l > r, "<" => l < r, ">=" => l >= r, "<=" => l <= r, "==" => l == r, "!=" => l != r,
                _ => throw new NotSupportedException($"Operator '{op}' not supported.")
            };
        }
    }
}