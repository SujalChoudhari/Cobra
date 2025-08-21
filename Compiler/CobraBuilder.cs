// This project requires the following NuGet packages:
// - Antlr4.Runtime.Standard
// - LLVMSharp.Interop (v16.0.0 or later)

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using LLVMSharp.Interop;
using Antlr4.Runtime;
using System.Linq; // Added for using .Select and .Join

namespace Cobra.Compiler;

/// <summary>
/// A simple compiler for the Cobra language using LLVMSharp.
/// This version focuses on compiling assignment statements.
/// </summary>
public class CobraBuilder
{
    private LLVMModuleRef _module;
    private LLVMBuilderRef _builder;
    private Dictionary<string, LLVMValueRef> _namedValues;
    private string _sourceCode;
    private bool _verboseWhileRunning = false;

    public CobraBuilder(string sourceCode, bool verboseWhileRunning)
    {
        _sourceCode = sourceCode;
        _namedValues = new Dictionary<string, LLVMValueRef>();
        _verboseWhileRunning = verboseWhileRunning;
        InitializeLLVM();
    }

    /// <summary>
    /// Initializes the LLVM context, module, and IR builder.
    /// Think of the context as the 'workspace' and the module as the 'file' we are writing to.
    /// </summary>
    private void InitializeLLVM()
    {
        LLVMContextRef context = LLVMContextRef.Create();
        _module = context.CreateModuleWithName("CobraModule");
        _builder = context.CreateBuilder();
    }

    /// <summary>
    /// Compiles the Cobra source code into LLVM IR.
    /// This is the main compiler method.
    /// </summary>
    public void Compile()
    {
        // Step 1: Parsing the source code using ANTLR4
        // The lexer breaks the code into tokens, and the parser builds a tree (parse tree).
        var inputStream = new AntlrInputStream(_sourceCode);
        var lexer = new CobraLexer(inputStream);
        var commonTokenStream = new CommonTokenStream(lexer);
        var parser = new CobraParser(commonTokenStream);
        var programContext = parser.program();

        // Step 2: Setting up the main function
        // All our code will live inside a function named "__cobra_main__".
        LLVMTypeRef int32Type = LLVMTypeRef.Int32;
        LLVMTypeRef mainFunctionType = LLVMTypeRef.CreateFunction(int32Type, Array.Empty<LLVMTypeRef>());
        LLVMValueRef mainFunction = _module.AddFunction("__cobra_main__", mainFunctionType);
        
        // A basic block is a sequence of instructions with one entry and one exit point.
        // The 'entry' block is where the function execution begins.
        LLVMBasicBlockRef entryBlock = mainFunction.AppendBasicBlock("entry");
        _builder.PositionAtEnd(entryBlock);
        
        // Step 3: Declaring the C standard library's printf function
        // We declare it so we can call it from our generated code.
        LLVMTypeRef charPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
        LLVMTypeRef printfFunctionType = LLVMTypeRef.CreateFunction(int32Type, new LLVMTypeRef[] { charPtrType }, true);
        LLVMValueRef printfFunction = _module.AddFunction("printf", printfFunctionType);

        // Step 4: Walking the parse tree and generating LLVM IR
        // The visitor pattern allows us to walk the tree and generate code for each node.
        CobraProgramVisitor programVisitor = new CobraProgramVisitor(_module, _builder, _namedValues);
        programVisitor.Visit(programContext);

        // Step 5: Adding a call to printf for output
        // We load the value of 'x' and print it to the console.
        if (_namedValues.TryGetValue("x", out LLVMValueRef xValue))
        {
            LLVMValueRef loadedXValue = _builder.BuildLoad2(xValue.TypeOf.ElementType, xValue, "x");
            LLVMValueRef formatString = _builder.BuildGlobalStringPtr("The value of x is: %d\n", "format_string");
            LLVMValueRef[] printfArgs = new LLVMValueRef[] { formatString, loadedXValue };
            _builder.BuildCall2(printfFunctionType, printfFunction, printfArgs, "calltmp");
        }
        else
        {
            // If 'x' doesn't exist, we can't generate the printf call.
            Console.WriteLine("Warning: Variable 'x' not found. Skipping printf call.");
        }

        // Step 6: Finalizing the main function
        // Every function needs a return instruction. We return 0, which is the standard for success.
        _builder.BuildRet(LLVMValueRef.CreateConstInt(int32Type, 0, false));
    }

    /// <summary>
    /// Generates the human-readable LLVM IR and saves it to a .ll file.
    /// </summary>
    /// <param name="filePath">The path to save the .ll file.</param>
    public void GenerateIR(string filePath)
    {
        if (_module.TryPrintToFile(filePath, out string errorMessage))
        {
            Console.WriteLine($"Generated LLVM IR file at: {filePath}");
        }
        else
        {
            throw new Exception($"Error generating LLVM IR: {errorMessage}");
        }
    }

    /// <summary>
    /// Generates an object file (.o) from the LLVM IR.
    /// This requires a full LLVM installation on the system.
    /// </summary>
    /// <param name="filePath">The path to save the .o file.</param>
    public void GenerateObjectFile(string filePath)
    {
        LLVM.InitializeNativeTarget();
        LLVM.InitializeNativeAsmPrinter();

        string triple = LLVMTargetRef.DefaultTriple;
        LLVMTargetRef target = LLVMTargetRef.GetTargetFromTriple(triple);

        LLVMTargetMachineRef targetMachine = target.CreateTargetMachine(
            triple, "generic", "",
            LLVMCodeGenOptLevel.LLVMCodeGenLevelAggressive,
            LLVMRelocMode.LLVMRelocPIC, // Position-Independent Code for modern systems
            LLVMCodeModel.LLVMCodeModelDefault
        );

        if (targetMachine.TryEmitToFile(_module, filePath, LLVMCodeGenFileType.LLVMObjectFile, out string errorMessage))
        {
            Console.WriteLine($"Generated object file at: {filePath}");
        }
        else
        {
            throw new Exception($"Error generating object file: {errorMessage}");
        }
    }

    /// <summary>
    /// Links the generated object files and a C++ wrapper to create the final executable.
    /// This uses the 'g++' command-line tool.
    /// </summary>
    /// <param name="outputDir">The directory where output files are stored.</param>
    /// <param name="objectFiles">A list of paths to the object files (.o).</param>
    /// <param name="finalExecutablePath">The desired path for the final executable.</param>
    // The new Build method signature
    public static void Build(string outputDir, string intermediateDir, List<string> objectFiles, string finalExecutablePath)
    {
        string wrapperFile = Path.Combine(intermediateDir, "main.cpp");
    
        // Step 1: Create a small C++ wrapper file
        File.WriteAllText(wrapperFile, @"
    #include <stdio.h>

    extern ""C"" int __cobra_main__();

    int main(int argc, char **argv) {
        return __cobra_main__();
    }
    ");
    
        // Step 2: Prepare the arguments for g++
        string objectFilesArg = string.Join(" ", objectFiles.Select(f => $"\"{f}\""));
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "g++",
            Arguments = $"-o \"{finalExecutablePath}\" \"{wrapperFile}\" {objectFilesArg}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(psi))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"\nSuccessfully created executable: {finalExecutablePath}");
                Console.WriteLine("To run it, use: {0}", finalExecutablePath);
            }
            else
            {
                Console.WriteLine("\nLinking failed. Here is the error output:");
                Console.WriteLine(error);
                throw new Exception("Linking failed. See console output for details.");
            }
        }

        // This file will be deleted by the cleanup logic in Runner.cs
    }
}