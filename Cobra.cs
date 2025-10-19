using Cobra.Utils;

namespace Cobra;

public abstract class Cobra
{
    public static int Main(string[] args)
    {
        return CobraCli.Run(args);
    }
}