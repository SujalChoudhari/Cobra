// This project requires the following NuGet packages:
// - Antlr4.Runtime.Standard
// - LLVMSharp.Interop (v16.0.0 or later)

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using LLVMSharp.Interop;
using Antlr4.Runtime;
using System.Linq;
using Cobra.Utils; // Added for using .Select and .Join

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

    public CobraBuilder(string sourceCode)
    {
        _sourceCode = sourceCode;
        _namedValues = new Dictionary<string, LLVMValueRef>();
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
    public void Compile(bool makeAST = false, String outputDir = "path/to/output/directory", String programName = "my_program")
    {
        // Step 1: Parsing the source code using ANTLR4
        // The lexer breaks the code into tokens, and the parser builds a tree (parse tree).
        var inputStream = new AntlrInputStream(_sourceCode);
        var lexer = new CobraLexer(inputStream);
        var commonTokenStream = new CommonTokenStream(lexer);
        var parser = new CobraParser(commonTokenStream);
        var programContext = parser.program();

        if (makeAST)
        {
            // Generate AST
            var astGenerator = new CobraAstGenerator(programContext, programName);
            astGenerator.GenerateAst(outputDir);
        }

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
        CobraLogger.PrintfFunction = printfFunction;

        // Step 4: Walking the parse tree and generating LLVM IR
        // The visitor pattern allows us to walk the tree and generate code for each node.
        CobraProgramVisitor programVisitor = new CobraProgramVisitor(_module, _builder, _namedValues);
        programVisitor.Visit(programContext);

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
        CobraLogger.Info($"Generating IR at: {filePath}");
        if (_module.TryPrintToFile(filePath, out string errorMessage))
        {
            CobraLogger.Success($"Generated LLVM IR file at: {filePath}");
        }
        else
        {
            CobraLogger.Error($"Error generating LLVM IR: {errorMessage}");
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
        CobraLogger.Info($"Generating object file at: {filePath}");
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
            CobraLogger.Success($"Generated object file at: {filePath}");
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
    /// <param name="intermediateDir">The directory where intermediate files (like the C++ wrapper) are stored.</param>
    /// <param name="objectFiles">A list of paths to the object files (.o).</param>
    /// <param name="finalExecutablePath">The desired path for the final executable.</param>
    public static void Build(string outputDir, string intermediateDir, List<string> objectFiles, string finalExecutablePath)
    {
        CobraLogger.Info("Starting final linking process...");
        string wrapperFile = Path.Combine(intermediateDir, "main.cpp");

        // Step 1: Create a small C++ wrapper file
        CobraLogger.Info($"Creating C++ wrapper at: {wrapperFile}");
        File.WriteAllText(wrapperFile, @"
#include <stdio.h>

extern ""C"" int __cobra_main__();

int main(int argc, char **argv) {
    return __cobra_main__();
}
");

        // Step 2: Prepare the arguments for the linker (g++)
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

        CobraLogger.Info($"Executing linker command: {psi.FileName} {psi.Arguments}");
        
        using var process = Process.Start(psi);
        if (process == null)
        {
            CobraLogger.Error("Failed to start the linker process. Is g++ installed and in your system's PATH?");
            throw new Exception("Process.Start returned null.");
        }
        
        // Wait for the process to exit and capture all output
        process.WaitForExit();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        
        CobraLogger.Info($"Linker process completed with exit code: {process.ExitCode}");

        if (process.ExitCode == 0)
        {
            CobraLogger.Success($"Successfully created executable: {finalExecutablePath}");
            if (!string.IsNullOrWhiteSpace(output))
            {
                CobraLogger.Info("Linker output (stdout):");
                Console.WriteLine(output);
            }
            CobraLogger.Success($"To run it, use: ./{Path.GetFileName(finalExecutablePath)}");
        }
        else
        {
            CobraLogger.Error("Linking failed. See error details below:");
            
            if (!string.IsNullOrWhiteSpace(output))
            {
                CobraLogger.Warn("Linker output (stdout):");
                CobraLogger.Warn(output);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                CobraLogger.Error("Linker error (stderr):");
                CobraLogger.Error(error);
            }
            else
            {
                CobraLogger.Error("No specific error message was captured from stderr. Ensure g++ is installed and configured correctly.");
            }
            
            throw new Exception($"Linking failed with exit code {process.ExitCode}. See console output for details.");
        }
    }
}