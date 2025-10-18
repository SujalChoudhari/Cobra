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
            // Use Console.Error for file not found
            Console.Error.WriteLine($"File not found: {opts.File}");
            return 1;
        }

        Log.Info($"Running: {opts.File}");

        try
        {
            CobraRunner runner = new();
            runner.Run(File.ReadAllText(opts.File), opts.File);
        }
        catch (Exception ex)
        {
            // This now catches both internal errors and unhandled Cobra exceptions
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex.Message);
            Console.ForegroundColor = previousColor;
            return 1; // Indicate failure
        }

        return 0; // Indicate success
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