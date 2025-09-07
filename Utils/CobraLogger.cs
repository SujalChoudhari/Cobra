using System.Runtime.CompilerServices;

namespace Cobra.Utils
{
    public class CobraLogger
    {
        private readonly string _className;

        private CobraLogger(string className)
        {
            _className = className;
        }

        public static CobraLogger GetLogger<T>()
        {
            return new CobraLogger(typeof(T).Name);
        }

        private void Log(string level, string message, ConsoleColor color,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string member = "")
        {
            var fileName = Path.GetFileName(file);
            var timestamp = DateTime.Now.ToString("HH:mm:ss");

            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"{timestamp} [{level}] {_className}.{member} ({fileName}:{line}) - {message}");
            Console.ForegroundColor = previousColor;
        }

        public void Info(string message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string member = "")
        {
            Log("INFO", message, ConsoleColor.White, file, line, member);
        }

        public void Warn(string message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string member = "")
        {
            Log("WARN", message, ConsoleColor.Yellow, file, line, member);
        }

        public void Error(string message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string member = "")
        {
            Log("ERROR", message, ConsoleColor.Red, file, line, member);
        }

        public void Debug(string message,
            [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerMemberName] string member = "")
        {
            Log("DEBUG", message, ConsoleColor.Cyan, file, line, member);
        }
    }
}