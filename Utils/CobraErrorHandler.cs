using Cobra.Interpreter;
using Cobra.Environment;

namespace Cobra.Utils
{
    public static class CobraErrorHandler
    {
        public static void PrintException(CobraThrowValue thrown, string? initialSourcePath)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Unhandled Exception:");

            string typeName = thrown.ThrownObject?.GetType().Name ?? "Unknown Type";
            object? message = "An unknown error occurred.";

            if (thrown.ThrownObject is CobraInstance instance)
            {
                typeName = instance.ClassDefinition.Name;
                try
                {
                    var msgGetter = instance.Get("getMessage");
                    if (msgGetter is CobraFunctionDefinition)
                    {
                        // We need a minimal interpreter to run the getMessage function if it exists
                         var interpreter = new CobraInterpreter(CobraEnvironment.CreateGlobalEnvironment(Array.Empty<string>()));
                         message = interpreter.ExecuteFunctionCall(msgGetter, new List<object?>(), "getMessage", instance);
                    }
                    else
                    {
                         // Fallback if getMessage isn't a function or doesn't exist, 
                         // try to get a 'message' field directly as a fallback.
                         try { message = instance.Get("message"); } catch { message = instance.ToString(); }
                    }
                }
                catch
                {
                    message = instance.ToString();
                }
            }
            else
            {
                message = thrown.ThrownObject?.ToString() ?? "null";
            }
            
            Console.Error.WriteLine($"  {typeName}: {message}");

            var stackTrace = thrown.StackTrace;
            if (stackTrace == null || stackTrace.Count == 0)
            {
                Console.Error.WriteLine("  at <unknown location>");
                if (initialSourcePath != null)
                    Console.Error.WriteLine($"   in {initialSourcePath}");
            }
            else
            {
                foreach (var frame in stackTrace)
                {
                    PrintFrame(frame);
                }
            }
            
            Console.ResetColor();
        }

        private static void PrintFrame(CallFrame frame)
        {
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Error.WriteLine($"  at {frame.FunctionName}() in {frame.SourcePath}:line {frame.Line}");

            try
            {
                if (File.Exists(frame.SourcePath))
                {
                    var lines = File.ReadAllLines(frame.SourcePath);
                    if (frame.Line > 0 && frame.Line <= lines.Length)
                    {
                        var line = lines[frame.Line - 1];
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Error.WriteLine($"    {line.TrimStart()}");
                        
                        Console.ForegroundColor = ConsoleColor.Red;
                        // Calculate padding based on how much we trimmed from the start
                        var leadingSpaces = line.Length - line.TrimStart().Length;
                        var padding = Math.Max(0, frame.Column - leadingSpaces);
                        var squigglyLength = Math.Max(1, frame.StopIndex - frame.StartIndex + 1);
                        
                        Console.Error.WriteLine($"    {new string(' ', padding)}{new string('^', squigglyLength)}");
                    }
                }
            }
            catch
            {
                // ignored, best effort to print source line
            }
        }
    }
}