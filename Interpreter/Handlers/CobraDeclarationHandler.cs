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
                         new List<(CobraRuntimeTypes, string)>();
        var function = new CobraUserDefinedFunction(funcName, parameters, context.block(), _currentEnvironment);
        _currentEnvironment.DefineVariable(funcName, function, isConst: true);
        return null;
    }
}