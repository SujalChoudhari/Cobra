using CommandLine;
using System.Collections.Generic;

namespace Cobra.CLI;

/// <summary>
/// Defines the command-line options for the Cobra compiler.
/// </summary>
public class Options
{
    // The main input files for compilation.
    // 'Value(0)' means this is the first positional argument.
    [Value(0, Required = true, Min = 1, HelpText = "Input source files to be compiled.")]
    public IEnumerable<string> InputFiles { get; set; }

    // Defines the output file or directory.
    // 'Option' defines a named flag like '-o' or '--output'.
    [Option('o', "output", Required = false, Default = "a.out",
        HelpText = "Output directory or filename for the compiled executable. If multiple inputs are provided, this will be treated as a directory.")]
    public string Output { get; set; }

    // Option to keep intermediate files like LLVM IR (.ll) and object files (.o).
    [Option('k', "keep-intermediate", Required = false, HelpText = "Keep intermediate files (.ll, .o) after compilation.")]
    public bool KeepIntermediate { get; set; }

    // Option to enable verbose output during the compilation process itself.
    [Option('v', "verbose", Required = false, HelpText = "Enable verbose output during compilation.")]
    public bool VerboseCompilation { get; set; }

    // Option to enable verbose output in the compiled executable when it's run.
    [Option('V', "verbose-runtime", Required = false,
        HelpText = "Enable verbose runtime output in the compiled program.")]
    public bool VerboseRunning { get; set; }
}