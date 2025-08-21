using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using LLVMSharp.Interop;
using System;
using System.IO;
using Cobra.Interpreter;

public class DescriptiveErrorListener : BaseErrorListener
{
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        throw new Exception($"Syntax error at line {line}:{charPositionInLine}: {msg}");
    }
}

class Program
{
    static void Main(string[] args)
    {
        try
        {
            // Sample Cobra source code
            string sourceCode = @"
                int main() {  return 0; }
            ";

            Console.WriteLine("Starting parsing...");
            // Create an input stream from the source code
            var inputStream = new AntlrInputStream(sourceCode);

            // Create a lexer and feed it the input stream
            var lexer = new CobraLexer(inputStream);

            // Create a buffer of tokens pulled from the lexer
            var commonTokenStream = new CommonTokenStream(lexer);

            // Create a parser and feed it the tokens
            var parser = new CobraParser(commonTokenStream);

            // Add custom error listener
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new DescriptiveErrorListener());

            // Begin parsing at the 'program' rule and get the parse tree
            Console.WriteLine("Generating parse tree...");
            IParseTree parseTree = parser.program();

            // Create the Cobra compiler (visitor)
            Console.WriteLine("Creating compiler...");
            var compiler = new CobraCompiler();

            // Use the visitor to traverse the parse tree
            Console.WriteLine("Visiting parse tree...");
            compiler.Visit(parseTree);

            // Get the generated LLVM module
            Console.WriteLine("Retrieving LLVM module...");
            LLVMModuleRef module = compiler.Module;

            // Verify the module
            Console.WriteLine("Verifying module...");
            if (module.TryVerify(LLVMVerifierFailureAction.LLVMPrintMessageAction, out string error))
            {
                Console.WriteLine("Module verification failed:");
                Console.WriteLine(error);
                return;
            }

            // Print the generated LLVM IR
            Console.WriteLine("Generated LLVM IR:");
            Console.WriteLine(module.ToString());

            // Optionally save the IR to a file
            File.WriteAllText("output.ll", module.ToString());

            Console.WriteLine("Parsing and code generation complete.");
        }
        catch (DllNotFoundException ex)
        {
            Console.WriteLine($"DLL Error: {ex.Message}");
            Console.WriteLine("Ensure libLLVM.so is installed and accessible (e.g., in /lib/x86_64-linux-gnu).");
            Console.WriteLine("Run 'ldconfig -p | grep libLLVM' to verify library availability.");
            Console.WriteLine("Check dependencies with 'ldd /lib/x86_64-linux-gnu/libLLVM-19.so'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.WriteLine($"Inner Exception: {ex.InnerException}");
        }
    }
}