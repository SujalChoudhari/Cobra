using Cobra.Utils;

namespace Cobra;

public abstract class Cobra
{
    public static void Main(string[] args)
    {
        CobraCommandLine.Run(args);
    }
}