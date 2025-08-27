using System.Collections.Generic;
using CommandLine;

namespace Cobra.CLI;

/// <summary>
/// Defines the command-line options for the Cobra compiler.
/// This class is used by the <see cref="CommandLineParser"/> library to map command-line arguments to properties.
/// </summary>
public class Options
{
    /// <summary>
    /// Gets or sets the path(s) to the input source files for compilation.
    /// This is a required positional argument.
    /// </summary>
    [Value(0, Required = true, Min = 1, HelpText = "Input source files to be compiled.")]
    public required IEnumerable<string> InputFiles { get; set; }

    /// <summary>
    /// Gets or sets the path for the output. This can be a filename for a single input file
    /// or a directory for multiple input files.
    /// </summary>
    [Option('o', "output", Required = false, Default = "a.out",
        HelpText = "Output directory or filename for the compiled executable. If multiple inputs are provided, this will be treated as a directory.")]
    public string Output { get; set; } = "a.out";

    /// <summary>
    /// Gets or sets a value indicating whether to keep intermediate files (LLVM IR, object files)
    /// generated during the compilation process.
    /// </summary>
    [Option('k', "keep-intermediate", Required = false,
        HelpText = "Keep intermediate files (.ll, .o) after compilation.")]
    public bool KeepIntermediate { get; set; }

    /// <summary>
    /// Gets or sets the verbosity level for the compiler's logging output.
    /// Accepted values include 'off', 'error', 'warn', and 'info'.
    /// </summary>
    [Option('v', "verbose", Required = false, Default = "info",
        HelpText = "Set the verbosity level for the compiler. Can be: off, error, warn, success or info. Default is 'info'.")]
    public string VerboseLevel { get; set; } = "info";

    /// <summary>
    /// Gets or sets a value indicating whether to include verbose runtime output in the compiled program.
    /// </summary>
    [Option('V', "verbose-runtime", Required = false,
        HelpText = "Enable verbose runtime output in the compiled program.")]
    public bool VerboseRunning { get; set; }
}