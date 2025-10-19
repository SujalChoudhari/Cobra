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
            catch (CobraRuntimeException)
            {
                // The error handler in CobraRunner already printed the details.
                // We just need to return the error code.
                return 1;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Internal Interpreter Error: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }

            return 0;
        }
    }
}