using Cobra.Compiler;
using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cobra.CLI;

public class Runner
{
    public static void run(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options => RunCompiler(options))
            .WithNotParsed(errors => HandleParseError(errors));
    }

    private static void RunCompiler(Options options)
    {
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
                if (options.VerboseCompilation)
                {
                    Console.WriteLine($"Compiling: {file}");
                }

                string source = File.ReadAllText(file);
                CobraBuilder builder = new CobraBuilder(source, options.VerboseRunning);
                builder.Compile();

                string baseFileName = Path.GetFileNameWithoutExtension(file);
                string objectFile = Path.Combine(intermediateDir, baseFileName + ".o");
                string irFile = Path.Combine(intermediateDir, baseFileName + ".ll");

                builder.GenerateIR(irFile);
                builder.GenerateObjectFile(objectFile);
                objectFiles.Add(objectFile);
            }

            // Link all generated object files to create the final executable
            CobraBuilder.Build(outputDir, intermediateDir, objectFiles, finalExecutablePath);

            Console.WriteLine($"\nSuccessfully compiled and linked to '{finalExecutablePath}'");

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
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine("Make sure you have LLVM and g++ installed and in your system's PATH.");
        }
    }

    private static void HandleParseError(IEnumerable<Error> errors)
    {
        Console.WriteLine("\nCommand line parsing failed. Use --help for usage information.");
    }
}