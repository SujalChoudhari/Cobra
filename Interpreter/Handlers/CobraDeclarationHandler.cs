using Cobra.Environment;

namespace Cobra.Interpreter;

public partial class CobraInterpreter
{
    public override object? VisitVarDeclaration(CobraParser.VarDeclarationContext context)
    {
        var varName = context.ID().GetText();
        object? value = null;
        if (context.assignmentExpression() != null)
        {
            value = Visit(context.assignmentExpression());
        }

        _currentEnvironment.DefineVariable(varName, value);
        return null;
    }

    public override object? VisitConstDeclaration(CobraParser.ConstDeclarationContext context)
    {
        var constName = context.ID().GetText();
        var value = Visit(context.assignmentExpression());
        _currentEnvironment.DefineVariable(constName, value, isConst: true);
        return null;
    }

    public override object? VisitFunctionDeclaration(CobraParser.FunctionDeclarationContext context)
    {
        var funcName = context.ID().GetText();
        var parameters = context.parameterList()?.parameter()
                             .Select(p => (CobraRuntimeTypes.Void, p.ID().GetText())).ToList() ??
                         [];
        var function = new CobraUserDefinedFunction(funcName, parameters, context.block(), _currentEnvironment);
        _currentEnvironment.DefineVariable(funcName, function, isConst: true);
        return null;
    }

    public override object? VisitClassDeclaration(CobraParser.ClassDeclarationContext context)
    {
        var className = context.ID().GetText();
        var methods = new Dictionary<string, CobraFunctionDefinition>();
        var fields = new Dictionary<string, (object? InitialValue, bool IsPublic)>();
        var staticEnv = new CobraEnvironment();
        CobraUserDefinedFunction? constructor = null;
        CobraUserDefinedFunction? destructor = null;

        foreach (var member in context.memberDeclaration())
        {
            var isStatic = member.STATIC() != null;
            var isPublic = member.accessModifier()?.PRIVATE() == null;

            if (member.constructorDeclaration() != null)
            {
                var ctorCtx = member.constructorDeclaration();
                var parameters = ctorCtx.parameterList()?.parameter()
                                     .Select(p => (CobraRuntimeTypes.Void, p.ID().GetText())).ToList() ??
                                 [];
                constructor = new CobraUserDefinedFunction(className, parameters, ctorCtx.block(), _currentEnvironment);
            }
            else if (member.destructorDeclaration() != null)
            {
                var destructorDeclaration = member.destructorDeclaration();
                destructor = new CobraUserDefinedFunction("~" + className, new List<(CobraRuntimeTypes, string)>(),
                    destructorDeclaration.block(), _currentEnvironment);
            }
            else if (member.fieldDeclaration() != null)
            {
                var fieldCtx = member.fieldDeclaration().varDeclaration();
                var fieldName = fieldCtx.ID().GetText();
                var initialValue = fieldCtx.assignmentExpression() != null
                    ? Visit(fieldCtx.assignmentExpression())
                    : null;

                if (isStatic)
                    staticEnv.DefineVariable(fieldName, initialValue);
                else
                    fields[fieldName] = (initialValue, isPublic);
            }
            else if (member.methodDeclaration() != null)
            {
                var methodCtx = member.methodDeclaration().functionDeclaration();
                var methodName = methodCtx.ID().GetText();
                var parameters = methodCtx.parameterList()?.parameter()
                                     .Select(p => (CobraRuntimeTypes.Void, p.ID().GetText())).ToList() ??
                                 [];

                var method =
                    new CobraUserDefinedFunction(methodName, parameters, methodCtx.block(), _currentEnvironment);

                if (isStatic)
                    staticEnv.DefineVariable(methodName, method, isConst: true);
                else
                    methods[methodName] = method;
            }
        }

        var classDefinition = new CobraClass(className, constructor, destructor, methods, fields, staticEnv);
        _currentEnvironment.DefineVariable(className, classDefinition, isConst: true);

        return null;
    }
}