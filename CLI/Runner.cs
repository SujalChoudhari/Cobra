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
    private static IEnumerable<string> GetImports(string filePath)
    {
        var source = File.ReadAllText(filePath);
        var inputStream = new Antlr4.Runtime.AntlrInputStream(source);
        var lexer = new CobraLexer(inputStream);
        var commonTokenStream = new Antlr4.Runtime.CommonTokenStream(lexer);
        var parser = new CobraParser(commonTokenStream);
        var programContext = parser.program();

        return programContext.importStatement().Select(import => import.qualifiedName().GetText());
    }

    private static List<string> ResolveDependencies(IEnumerable<string> initialFiles)
    {
        var filesToCompile = new List<string>();
        var processedFiles = new HashSet<string>();
        var initialFilePaths = initialFiles.Select(Path.GetFullPath).ToList();
        var filesToScan = new Queue<string>(initialFilePaths);

        string baseDirectory = Path.GetDirectoryName(initialFilePaths.First()) ?? Directory.GetCurrentDirectory();


        while (filesToScan.Count > 0)
        {
            var currentFile = filesToScan.Dequeue();
            if (processedFiles.Contains(currentFile))
                continue;

            CobraLogger.Info($"Resolving dependencies for: {Path.GetRelativePath(baseDirectory, currentFile)}");
            processedFiles.Add(currentFile);
            filesToCompile.Add(currentFile);

            if (!File.Exists(currentFile))
            {
                throw new FileNotFoundException($"The source file was not found: {currentFile}");
            }

            var imports = GetImports(currentFile);
            foreach (var importPath in imports)
            {
                string relativePath = importPath.Replace('.', Path.DirectorySeparatorChar) + ".cb";
                string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, relativePath));

                if (!processedFiles.Contains(fullPath))
                {
                    CobraLogger.Info($"Found dependency: {Path.GetRelativePath(baseDirectory, fullPath)}");
                    filesToScan.Enqueue(fullPath);
                }
            }
        }

        // We want to compile dependencies first, though order for linking doesn't strictly matter.
        // Reversing provides a more logical compilation order (dependencies first).
        filesToCompile.Reverse();
        return filesToCompile;
    }


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
            var allFilesToCompile = ResolveDependencies(options.InputFiles);
            CobraLogger.Info("Dependency resolution complete.");
            CobraLogger.Info($"Compiling {allFilesToCompile.Count} files...");

            var (finalExecutablePath, outputDir, intermediateDir) = GetOutputPaths(options);

            Directory.CreateDirectory(outputDir);
            Directory.CreateDirectory(intermediateDir);

            var objectFiles = new List<string>();

            // Compile dependencies first
            foreach (var file in allFilesToCompile)
            {
                bool isMainModule = options.InputFiles.Contains(file);
                CobraLogger.Info($"Compiling file: {file} (Main: {isMainModule})");
                CompileSingleFile(file, options, intermediateDir, objectFiles, isMainModule);
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
            "success" => LogLevel.Success,
            _ => LogLevel.Info
        };

        if (CobraLogger.Level == LogLevel.Warn && options.VerboseLevel.ToLower() != "warn")
        {
            CobraLogger.Error("Invalid verbose level specified. Using default level 'warn'.");
        }
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
    /// <param name="isMainModule"></param>
    private static void CompileSingleFile(string filePath, Options options, string intermediateDir,
        List<string> objectFiles, bool isMainModule = true)
    {
        var source = File.ReadAllText(filePath);
        var baseFileName = Path.GetFileNameWithoutExtension(filePath);
        CobraBuilder builder = new(baseFileName, source);
        builder.Compile(options.KeepIntermediate, intermediateDir, baseFileName, isMainModule);


        var objectFile = Path.Combine(intermediateDir, baseFileName + ".o");
        var irFile = Path.Combine(intermediateDir, baseFileName + ".ll");

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
        if (options.KeepIntermediate) return;

        if (Directory.Exists(intermediateDir))
        {
            Directory.Delete(intermediateDir, true);
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