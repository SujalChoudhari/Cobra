using Cobra.Interpreter;

namespace Cobra.Utils
{
    public static class CobraCli
    {
        public static int Run(string[] args)
        {
            var runner = new CobraRunner();

            if (args.Length == 0)
            {
                Console.WriteLine("Cobra REPL. Type 'exit' to quit.");
                runner.StartRepl();
                return 0;
            }

            var scriptPath = args[0];
            if (!File.Exists(scriptPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: File not found '{scriptPath}'");
                Console.ResetColor();
                return 1;
            }
            
            var scriptArgs = args.Skip(1).ToArray();

            try
            {
                var code = File.ReadAllText(scriptPath);
                runner.Run(code, scriptPath, scriptArgs);
            }
            catch (CobraRuntimeException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(ex.Message);
                Console.ResetColor();
                return 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Internal Interpreter Error: {ex.Message}");
                Console.ResetColor();
                return 1;
            }

            return 0;
        }
    }
}