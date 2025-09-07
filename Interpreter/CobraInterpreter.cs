using Cobra.Environment;

namespace Cobra.Interpreter;

public class CobraInterpreter: CobraBaseListener
{
    private CobraEnvironment GlobalEnvironment { get; } = new();
    
    public override void EnterProgram(CobraParser.ProgramContext context)
    {
        GlobalEnvironment.DefineVariable("this", GlobalEnvironment);
    }
}