using CommandLine;

namespace Cobra.Utils;

public abstract class CobraCommandLine
{
    private static readonly CobraLogger Log = CobraLogger.GetLogger<CobraCommandLine>();

    public static void Run(string[] args)
    {
        if (args.Length == 0)
        {
            Log.Info("No arguments provided. Starting REPL mode...");
            StartRepl(new ReplOptions());
            return;
        }

        Parser.Default.ParseArguments<RunOptions, ReplOptions>(args)
            .MapResult(
                (RunOptions opts) => RunScript(opts),
                (ReplOptions opts) => StartRepl(opts),
                _ => 1);
    }

    private static int RunScript(RunOptions opts)
    {
        if (!File.Exists(opts.File))
        {
            Log.Error($"File not found: {opts.File}");
            return 1;
        }

        Log.Info($"Running: {opts.File}");
        foreach (var lib in opts.LibPaths)
            Log.Info($"  {lib}");
        foreach (var def in opts.Defines)
            Log.Info($"  {def}");

        CobraRunner runner = new();
        runner.Run(File.ReadAllText(opts.File), opts.File);

        return 0;
    }

    private static int StartRepl(ReplOptions opts)
    {
        Log.Info("Starting REPL mode...");
        CobraRunner runner = new();
        runner.StartRepl();
        return 0;
    }
}

[Verb("run", HelpText = "Run a script file.")]
internal class RunOptions
{
    [Value(0, MetaName = "file", Required = true, HelpText = "Path to the script file.")]
    public string File { get; set; } = "";

    [Option('l', "lib", Separator = ';', HelpText = "Additional library folders.")]
    public IEnumerable<string> LibPaths { get; set; } = new List<string>();

    [Option('D', "define", HelpText = "Define key=value pairs for interpreter.")]
    public IEnumerable<string> Defines { get; set; } = new List<string>();
}

[Verb("repl", HelpText = "Start interactive REPL mode.")]
internal class ReplOptions
{
    [Option('l', "lib", Separator = ';', HelpText = "Additional library folders.")]
    public IEnumerable<string> LibPaths { get; set; } = new List<string>();
}