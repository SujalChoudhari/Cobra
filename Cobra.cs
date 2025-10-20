using Cobra.Utils;

namespace Cobra;

/// <summary>
/// Main Class that initiates the interpreter toolchain
/// </summary>
public abstract class Cobra
{
    public static int Main(string[] args)
    {
        return CobraCli.Run(args);
    }
}