using Cobra.Compiler;
using Cobra.Utils;
using CommandLine;

namespace Cobra.CLI;

/// <summary>
/// The main entry point for the Cobra command-line interface.
/// This class handles argument parsing and orchestrates the compilation process.
/// </summary>
public static class Runner
{
    /// <summary>
    /// Parses command-line arguments and initiates the compilation process.
    /// </summary>
    /// <param name="args">The command-line arguments provided by the user.</param>
    public static void Run(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(RunCompiler)
            .WithNotParsed(HandleParseError);
    }

    /// <summary>
    /// Executes the compiler logic after successfully parsing command-line options.
    /// It handles compilation of single or multiple source files and links them into a final executable.
    /// </summary>
    /// <param name="options">The parsed command-line options.</param>
    private static void RunCompiler(Options options)
    {
        SetLoggingLevels(options);

        try
        {
            var (finalExecutablePath, outputDir, intermediateDir) = GetOutputPaths(options);

            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(intermediateDir);

            var objectFiles = new List<string>();
            foreach (var file in options.InputFiles)
            {
                CobraLogger.Info($"Compiling file: {file}");
                CompileSingleFile(file, options, intermediateDir, objectFiles);
            }

            CobraBuilder.Build(outputDir, intermediateDir, objectFiles, finalExecutablePath);

            CobraLogger.Success($"Successfully compiled and linked to '{finalExecutablePath}'");

            CleanIntermediateFiles(options, intermediateDir);
        }
        catch (Exception ex)
        {
            CobraLogger.Error($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets the static logging levels based on user-provided command-line options.
    /// </summary>
    /// <param name="options">The parsed command-line options.</param>
    private static void SetLoggingLevels(Options options)
    {
        CobraLogger.Level = options.VerboseLevel.ToLower() switch
        {
            "off" => LogLevel.Off,
            "error" => LogLevel.Error,
            "warn" => LogLevel.Warn,
            "info" => LogLevel.Info,
            _ => LogLevel.Warn
        };

        if (CobraLogger.Level == LogLevel.Warn && options.VerboseLevel.ToLower() != "warn")
        {
            CobraLogger.Error("Invalid verbose level specified. Using default level 'warn'.");
        }

        CobraLogger.EnableRuntime = options.VerboseRunning;
    }

    /// <summary>
    /// Determines the paths for the final executable, output directory, and intermediate files.
    /// </summary>
    /// <param name="options">The parsed command-line options.</param>
    /// <returns>A tuple containing the final executable path, output directory, and intermediate directory.</returns>
    private static (string finalExecutablePath, string outputDir, string intermediateDir) GetOutputPaths(
        Options options)
    {
        string finalExecutablePath;
        string outputDir;

        if (options.InputFiles.Count() == 1)
        {
            finalExecutablePath = options.Output;
            outputDir = Path.GetDirectoryName(finalExecutablePath) ?? "./";
        }
        else
        {
            outputDir = options.Output;
            finalExecutablePath = Path.Combine(outputDir, "a.out");
        }

        string intermediateDir = Path.Combine(outputDir, "i");

        return (finalExecutablePath, outputDir, intermediateDir);
    }

    /// <summary>
    /// Compiles a single source file, generates the corresponding IR and object file, and adds it to the list of object files.
    /// </summary>
    /// <param name="filePath">The path to the source file.</param>
    /// <param name="options">The parsed command-line options.</param>
    /// <param name="intermediateDir">The directory for intermediate files.</param>
    /// <param name="objectFiles">The list to which the generated object file path will be added.</param>
    private static void CompileSingleFile(string filePath, Options options, string intermediateDir,
        List<string> objectFiles)
    {
        string source = File.ReadAllText(filePath);
        string baseFileName = Path.GetFileNameWithoutExtension(filePath);
        CobraBuilder builder = new(source);
        builder.Compile(options.KeepIntermediate,intermediateDir, baseFileName);
        

        string objectFile = Path.Combine(intermediateDir, baseFileName + ".o");
        string irFile = Path.Combine(intermediateDir, baseFileName + ".ll");

        builder.GenerateIr(irFile);
        builder.GenerateObjectFile(objectFile);
        objectFiles.Add(objectFile);
    }

    /// <summary>
    /// Cleans up the intermediate directory if the user has not requested to keep the files.
    /// </summary>
    /// <param name="options">The parsed command-line options.</param>
    /// <param name="intermediateDir">The path to the intermediate directory.</param>
    private static void CleanIntermediateFiles(Options options, string intermediateDir)
    {
        if (!options.KeepIntermediate)
        {
            if (Directory.Exists(intermediateDir))
            {
                Directory.Delete(intermediateDir, true);
            }
        }
    }

    /// <summary>
    /// Handles and reports command-line parsing errors to the user.
    /// </summary>
    /// <param name="errors">A collection of errors that occurred during parsing.</param>
    private static void HandleParseError(IEnumerable<Error> errors)
    {
        CobraLogger.Info("Command line parsing failed. Use --help for usage information.");
    }
}