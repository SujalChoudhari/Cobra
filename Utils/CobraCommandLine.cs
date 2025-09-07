using CommandLine;

namespace Cobra.Utils;

public static class CobraCommandLine
{
    public static int Run(string[] args)
    {
        return Parser.Default.ParseArguments<RunOptions, ReplOptions>(args)
            .MapResult(
                (RunOptions opts) => RunScript(opts),
                (ReplOptions opts) => StartRepl(opts),
                _ => 1);
    }

    private static int RunScript(RunOptions opts)
    {
        if (!File.Exists(opts.File))
        {
            Console.Error.WriteLine($"File not found: {opts.File}");
            return 1;
        }

        Console.WriteLine($"Running: {opts.File}");
        Console.WriteLine("Library folders:");
        foreach (var lib in opts.LibPaths)
            Console.WriteLine($"  {lib}");
        Console.WriteLine("Definitions:");
        foreach (var def in opts.Defines)
            Console.WriteLine($"  {def}");

        // TODO: Initialize interpreter, set library paths, apply definitions
        // Interpreter.Run(opts.File, opts.LibPaths, opts.Defines);

        return 0;
    }

    private static int StartRepl(ReplOptions opts)
    {
        Console.WriteLine("Starting REPL...");
        
        return 0;
    }
}

[Verb("run", HelpText = "Run a script file.")]
internal abstract class RunOptions
{
    [Value(0, MetaName = "file", Required = true, HelpText = "Path to the script file.")]
    public required string File { get; set; }

    [Option('l', "lib", Separator = ';', HelpText = "Additional library folders.")]
    public IEnumerable<string> LibPaths { get; set; } = new List<string>();

    [Option('D', "define", HelpText = "Define key=value pairs for interpreter.")]
    public IEnumerable<string> Defines { get; set; } = new List<string>();
}

[Verb("repl", HelpText = "Start interactive REPL mode.")]
internal abstract class ReplOptions
{
    [Option('l', "lib", Separator = ';', HelpText = "Additional library folders.")]
    public IEnumerable<string> LibPaths { get; set; } = new List<string>();
}