using Cobra.Compiler;
using Cobra.Utils;
using CommandLine;

namespace Cobra.CLI;

public static class Runner
{
    public static void Run(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(RunCompiler)
            .WithNotParsed(HandleParseError);
    }

    private static void RunCompiler(Options options)
    {
        // Set the static logging level based on the command-line argument.
        switch (options.VerboseLevel.ToLower())
        {
            case "off":
                CobraLogger.Level = LogLevel.Off;
                break;
            case "error":
                CobraLogger.Level = LogLevel.Error;
                break;
            case "warn":
                CobraLogger.Level = LogLevel.Warn;
                break;
            case "info":
                CobraLogger.Level = LogLevel.Info;
                break;
            default:
                CobraLogger.Error("Invalid verbose level specified. Using default level 'warn'.");
                CobraLogger.Level = LogLevel.Warn;
                break;
        }

        // Set the runtime logging flag.
        CobraLogger.EnableRuntime = options.VerboseRunning;

        try
        {
            // Determine the final output path and directory
            string finalExecutablePath;
            string outputDir;

            if (options.InputFiles.Count() == 1)
            {
                // Case 1: Single input file
                // Use the provided output path directly.
                finalExecutablePath = options.Output;
                outputDir = Path.GetDirectoryName(finalExecutablePath) ?? "./";
            }
            else
            {
                // Case 2: Multiple input files
                // The provided output is a directory.
                outputDir = options.Output;
                finalExecutablePath = Path.Combine(outputDir, "a.out");
            }

            // A dedicated directory for all intermediate files
            string intermediateDir = Path.Combine(outputDir, "i");

            // Ensure all necessary directories exist
            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(intermediateDir);

            var objectFiles = new List<string>();
            foreach (var file in options.InputFiles)
            {
                CobraLogger.Info($"Compiling file: {file}");

                string source = File.ReadAllText(file);
                string baseFileName = Path.GetFileNameWithoutExtension(file);
                CobraBuilder builder = new CobraBuilder(source);
                builder.Compile(options.KeepIntermediate, intermediateDir, baseFileName);

                string objectFile = Path.Combine(intermediateDir, baseFileName + ".o");
                string irFile = Path.Combine(intermediateDir, baseFileName + ".ll");

                builder.GenerateIR(irFile);
                builder.GenerateObjectFile(objectFile);
                objectFiles.Add(objectFile);
            }

            // Link all generated object files to create the final executable
            CobraBuilder.Build(outputDir, intermediateDir, objectFiles, finalExecutablePath);

            CobraLogger.Success($"Successfully compiled and linked to '{finalExecutablePath}'");

            // Clean up intermediate files if requested
            if (!options.KeepIntermediate)
            {
                if (Directory.Exists(intermediateDir))
                {
                    Directory.Delete(intermediateDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            CobraLogger.Error($"\nError: {ex.Message}");
            CobraLogger.Info("Make sure you have LLVM and g++ installed and in your system's PATH.");
        }
    }

    private static void HandleParseError(IEnumerable<Error> errors)
    {
        CobraLogger.Info("Command line parsing failed. Use --help for usage information.");
    }
}