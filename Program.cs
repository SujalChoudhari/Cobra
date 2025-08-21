using Cobra.CLI;
using Cobra.Compiler;
using CommandLine;

namespace Cobra;

public class Program
{
    public static void Main(string[] args)
    {
        Runner.run(args);
    }
}