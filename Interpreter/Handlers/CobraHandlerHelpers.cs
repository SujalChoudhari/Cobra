using System.Runtime.InteropServices;
using Antlr4.Runtime;
using Cobra.Environment;
using Antlr4.Runtime.Tree;

namespace Cobra.Interpreter;

public partial class CobraInterpreter
{
    private CobraLValue EvaluateLValue(CobraParser.LeftHandSideContext context)
    {
        // Handle the simple case: `x = ...`
        if (context.primary() != null && context.children.Count == 1)
        {
            if (context.primary().ID() != null)
            {
                // This is a variable in the current scope. The container is the environment.
                return new CobraLValue(_currentEnvironment, context.primary().ID().GetText());
            }
            throw new Exception("Invalid LValue target.");
        }

        // Handle complex cases: `a.b`, `a[0]`, `a.b[0]`, etc.
        object? currentObject = Visit(context.primary());

        int childIndex = 1;
        int exprParseIndex = 0;
        int idParseIndex = 0;

        // Loop through the chain of accessors (e.g., .b, [0]) until the second-to-last one.
        // This loop resolves the container that holds the final value to be assigned.
        while (childIndex < context.ChildCount - 1)
        {
            var node = context.GetChild(childIndex);
            if (!(node is ITerminalNode termNode))
            {
                childIndex++;
                continue;
            }

            bool isLastAccessor;
            if (termNode.Symbol.Type == CobraLexer.DOT)
            {
                isLastAccessor = childIndex >= context.ChildCount - 2;
            }
            else if (termNode.Symbol.Type == CobraLexer.LBRACKET)
            {
                isLastAccessor = childIndex >= context.ChildCount - 3;
            }
            else
            {
                childIndex++;
                continue;
            }

            if (isLastAccessor)
            {
                break; // Exit loop to handle the final accessor, which gives us the key.
            }

            // If not the last accessor, resolve it and update currentObject.
            if (termNode.Symbol.Type == CobraLexer.DOT)
            {
                var memberName = context.ID(idParseIndex++).GetText();
                currentObject = GetMember(currentObject, memberName);
                childIndex += 2; // move past '.' and ID
            }
            else if (termNode.Symbol.Type == CobraLexer.LBRACKET)
            {
                var index = Visit(context.assignmentExpression(exprParseIndex++));
                currentObject = GetIndex(currentObject, index);
                childIndex += 3; // move past '[', expr, and ']'
            }
        }

        // Now, handle the last accessor to get the key for the LValue.
        var lastOpNode = context.GetChild(childIndex);
        if (lastOpNode is ITerminalNode lastTermNode)
        {
            if (lastTermNode.Symbol.Type == CobraLexer.DOT)
            {
                var key = context.ID(idParseIndex).GetText();
                return new CobraLValue(currentObject, key);
            }
            else if (lastTermNode.Symbol.Type == CobraLexer.LBRACKET)
            {
                var key = Visit(context.assignmentExpression(exprParseIndex));
                return new CobraLValue(currentObject, key);
            }
        }

        throw new Exception("Invalid LValue structure.");
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
        CobraInstance? instanceContext = null, IToken? callSiteToken = null)
    {
        var functionDefinition = funcObject as CobraFunctionDefinition;
        var funcName = functionDefinition?.Name ?? funcNameForError;
        
        if (callSiteToken != null && _sourceFileStack.Count > 0)
        {
            var frame = new CallFrame(funcName, _sourceFileStack.Peek(), callSiteToken);
            _stackTrace.Push(frame);
        }

        try
        {
            object? result = null;
            switch (funcObject)
            {
                case CobraUserDefinedFunction userFunc:
                {
                    if (args.Count != userFunc.Parameters.Count)
                        throw new CobraRuntimeException(
                            $"Function '{userFunc.Name}' expects {userFunc.Parameters.Count} arguments but got {args.Count}.");

                    var previous = _currentEnvironment;
                    _currentEnvironment = new CobraEnvironment(userFunc.Closure);

                    if (instanceContext != null)
                    {
                        _currentEnvironment.DefineVariable("this", instanceContext, isConst: true);
                    }

                    for (var i = 0; i < userFunc.Parameters.Count; i++)
                    {
                        _currentEnvironment.DefineVariable(userFunc.Parameters[i].Name, args[i]);
                    }

                    result = ExecuteBlockStmts(userFunc.Body);
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
                    throw new CobraRuntimeException($"'{funcNameForError}' is not a function.");
            }

            if (result is CobraReturnValue returnValue)
            {
                return returnValue.Value;
            }
            
            return result;
        }
        catch (CobraRuntimeException ex)
        {
            return CreateAndThrowCobraException(ex.Message, ex.StackTraceValue);
        }
        catch (Exception ex)
        {
            // Wrap any unexpected C# exception into a Cobra exception
            return CreateAndThrowCobraException(ex.Message);
        }
        finally
        {
            if (callSiteToken != null && _sourceFileStack.Count > 0)
            {
                _stackTrace.Pop();
            }
        }
    }
    
    private CobraThrowValue CreateAndThrowCobraException(string message, CobraStackTrace? existingStackTrace = null)
    {
        var stackTrace = existingStackTrace ?? new CobraStackTrace(_stackTrace);
        try
        {
            // Attempt to find the user-defined System.Exception class
            var systemNs = _currentEnvironment.GetVariable("System") as CobraNamespace;
            var exceptionClass = systemNs?.Environment.GetVariable("Exception") as CobraClass;

            if (exceptionClass != null)
            {
                var instance = new CobraInstance(exceptionClass);

                // Manually set the 'message' field as the constructor might not be simple
                if (exceptionClass.Fields.ContainsKey("message"))
                    instance.Fields.DefineVariable("message", message);
                
                // If there's a constructor, call it.
                var constructor = exceptionClass.GetConstructor(1);
                if (constructor != null)
                {
                    ExecuteFunctionCall(constructor, new List<object?> { message }, "Exception", instance);
                }
                
                return new CobraThrowValue(instance, stackTrace);
            }
        }
        catch
        {
            // Fallback if we can't create a Cobra Exception instance for any reason
        }

        // Fallback to throwing the raw string if the Exception class isn't available
        return new CobraThrowValue($"Internal Error: {message}", stackTrace);
    }


    private object? CallExternalFunction(CobraExternalFunction func, List<object?> args)
    {
        if (!_loadedLibraries.TryGetValue(func.LibraryPath, out var libHandle))
            throw new InvalidOperationException($"Library '{func.LibraryPath}' not loaded.");

        if (!NativeLibrary.TryGetExport(libHandle, func.Name, out var funcPtr))
            throw new EntryPointNotFoundException(
                $"Function '{func.Name}' not found in library '{func.LibraryPath}'.");

        var returnType = CobraTypeMarshaller.ToDotNetType(func.ReturnType);
        var paramTypes = func.Parameters.Select(p => CobraTypeMarshaller.ToDotNetType(p.Type)).ToArray();
        
        var delegateType = CobraDelegateFactory.Create(returnType, paramTypes);
        var delegateInstance = Marshal.GetDelegateForFunctionPointer(funcPtr, delegateType);

        object?[] marshalledArgs = new object[args.Count];
        for (int i = 0; i < args.Count; i++)
        {
            marshalledArgs[i] = args[i] switch
            {
                CobraHandle handle => handle.Pointer,
                string s when func.Parameters[i].Type == CobraRuntimeTypes.String => s,
                _ => args[i]
            };
        }
        
        if (func.ReturnType == CobraRuntimeTypes.String)
        {
            var stringPtr = (IntPtr)delegateInstance.DynamicInvoke(marshalledArgs)!;
            if (stringPtr == IntPtr.Zero) return "";

            var result = Marshal.PtrToStringAnsi(stringPtr);

            if (NativeLibrary.TryGetExport(libHandle, "str_free_string", out var freeFuncPtr))
            {
                var freeDelegateType = CobraDelegateFactory.Create(typeof(void), typeof(IntPtr));
                var freeDelegate = Marshal.GetDelegateForFunctionPointer(freeFuncPtr, freeDelegateType);
                freeDelegate.DynamicInvoke(stringPtr);
            }
            return result;
        }

        object? resultObj = delegateInstance.DynamicInvoke(marshalledArgs);

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
                return value switch
                {
                    sbyte v => -v,
                    short v => -v,
                    int v => -v,
                    long v => -v,
                    float v => -v,
                    double v => -v,
                    _ => throw new CobraRuntimeException($"Unary '-' not supported for type {value?.GetType().Name}")
                };
            case "!":
                return !CobraLiteralHelper.IsTruthy(value);
            default:
                throw new NotSupportedException($"Unary operator '{op}' not supported yet.");
        }
    }

    private object? ApplyBinaryOperator(string op, object? left, object? right)
    {
        if (op == "+" && (left is string || right is string))
            return CobraLiteralHelper.Stringify(left) + CobraLiteralHelper.Stringify(right);
        
        if (op is "==" or "!=" && (left == null || right == null))
        {
            return op == "==" ? left == right : left != right;
        }
        
        if (left is CobraEnumMember lMember) left = lMember.Value;
        if (right is CobraEnumMember rMember) right = rMember.Value;

        if (!CobraTypeHelper.IsNumeric(left) || !CobraTypeHelper.IsNumeric(right))
            throw new CobraRuntimeException($"Operator '{op}' cannot be applied to non-numeric types '{left?.GetType().Name}' and '{right?.GetType().Name}'.");

        var (pLeft, pRight, resultType) = CobraTypeHelper.PromoteNumericsForBinaryOp(left, right);
        
        if (CobraTypeHelper.IsFloatingPoint(pLeft))
        {
            var l = Convert.ToDouble(pLeft);
            var r = Convert.ToDouble(pRight);
            return op switch
            {
                "+" => l + r,
                "-" => l - r,
                "*" => l * r,
                "/" => r == 0 ? throw new DivideByZeroException() : l / r,
                "%" => r == 0 ? throw new DivideByZeroException() : l % r,
                ">" => l > r,
                "<" => l < r,
                ">=" => l >= r,
                "<=" => l <= r,
                "==" => Math.Abs(l - r) < 0.000001,
                "!=" => Math.Abs(l - r) > 0.000001,
                _ => throw new NotSupportedException($"Operator '{op}' not supported for floating-point numbers.")
            };
        }

        if (pLeft is ulong) // Special case for ulong
        {
            var l = Convert.ToUInt64(pLeft);
            var r = Convert.ToUInt64(pRight);
            return op switch
            {
                "+" => l + r,
                "-" => l - r,
                "*" => l * r,
                "/" => r == 0 ? throw new DivideByZeroException() : l / r,
                "%" => r == 0 ? throw new DivideByZeroException() : l % r,
                ">" => l > r, "<" => l < r, ">=" => l >= r, "<=" => l <= r,
                "==" => l == r, "!=" => l != r,
                _ => throw new NotSupportedException($"Operator '{op}' not supported for ulong.")
            };
        }

        // Standard signed long operations
        var sl = Convert.ToInt64(pLeft);
        var sr = Convert.ToInt64(pRight);
        return op switch
        {
            "+" => sl + sr,
            "-" => sl - sr,
            "*" => sl * sr,
            "/" => sr == 0 ? throw new DivideByZeroException() : sl / sr,
            "%" => sr == 0 ? throw new DivideByZeroException() : sl / sr,
            ">" => sl > sr, "<" => sl < sr, ">=" => sl >= sr, "<=" => sl <= sr,
            "==" => sl == sr, "!=" => sl != sr,
            _ => throw new NotSupportedException($"Operator '{op}' not supported for integers.")
        };
    }
}