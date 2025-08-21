using System.Diagnostics;
using Cobra.Compiler;

namespace Cobra;

public class Program
{
    public static void Main(string[] args)
    {
        // Parse command line arguments
        string outputDir = "./out";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-o" && i + 1 < args.Length)
            {
                outputDir = args[i + 1];
                i++; // Skip the next argument since we've used it
            }
        }

        // Create an output directory if it doesn't exist
        Directory.CreateDirectory(outputDir);

        string source = @"
        x = 10;
        y = 20.5;
        ";

        // Update paths to use the output directory
        string objectFile = Path.Combine(outputDir, "output.so");
        string irFile = Path.Combine(outputDir, "output.ll");

        CobraBuilder builder = new CobraBuilder(source);

        try
        {
            builder.Compile();
            builder.GenerateIR(irFile);
            builder.GenerateObjectFile(objectFile);
            builder.Build(outputDir, objectFile);

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Compilation and Linking Error: {ex.Message}");
        }
    }
}