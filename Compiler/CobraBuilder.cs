using System.Diagnostics;
using Antlr4.Runtime;
using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler;

/// <summary>
/// Represents the core class for compiling Cobra source code into LLVM Intermediate Representation (IR),
/// providing functionality for generating IR, object files, and building the final executable.
///
/// @author: Sujal Choudhari 
/// </summary>
public class CobraBuilder
{
    private LLVMModuleRef _module;
    private LLVMBuilderRef _builder;
    private readonly Dictionary<string, LLVMValueRef> _namedValues;
    private readonly string _sourceCode;

    public CobraBuilder(string sourceCode)
    {
        _sourceCode = sourceCode;
        InitializeLlvm();
    }

    /// <summary>
    /// Initializes the LLVM context, module, and IR builder.
    /// Think of the context as the 'workspace' and the module as the 'file' we are writing to.
    /// </summary>
    private void InitializeLlvm()
    {
        var context = LLVMContextRef.Create();
        _module = context.CreateModuleWithName("CobraModule");
        _builder = context.CreateBuilder();
    }

    /// <summary>
    /// Compiles the Cobra source code into LLVM IR.
    /// This is the main compiler method.
    /// </summary>
    public void Compile(bool makeAst = false, string outputDir = "path/to/output/directory",
        string programName = "my_program")
    {
        var inputStream = new AntlrInputStream(_sourceCode);
        var lexer = new CobraLexer(inputStream);
        var commonTokenStream = new CommonTokenStream(lexer);
        var parser = new CobraParser(commonTokenStream);
        var programContext = parser.program();

        if (makeAst)
        {
            var astGenerator = new CobraAstGenerator(programContext, programName);
            astGenerator.GenerateAst(outputDir);
        }

        var int32Type = LLVMTypeRef.Int32;
        var mainFunctionType = LLVMTypeRef.CreateFunction(int32Type, []);
        var mainFunction = _module.AddFunction("__cobra_main__", mainFunctionType);

        var entryBlock = mainFunction.AppendBasicBlock("entry");
        _builder.PositionAtEnd(entryBlock);

        var charPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
        var printfFunctionType = LLVMTypeRef.CreateFunction(int32Type, new[] { charPtrType }, true);
        var printfFunction = _module.AddFunction("printf", printfFunctionType);
        CobraLogger.PrintfFunction = printfFunction;

        var programVisitor = new CobraProgramVisitor(_module, _builder);
        programVisitor.Visit(programContext);

        _builder.BuildRet(LLVMValueRef.CreateConstInt(int32Type, 0));
    }

    /// <summary>
    /// Generates the human-readable LLVM IR and saves it to a .ll file.
    /// </summary>
    /// <param name="filePath">The path to save the .ll file.</param>
    public void GenerateIr(string filePath)
    {
        CobraLogger.Info($"Generating IR at: {filePath}");
        if (_module.TryPrintToFile(filePath, out var errorMessage))
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

        var triple = LLVMTargetRef.DefaultTriple;
        var target = LLVMTargetRef.GetTargetFromTriple(triple);

        var targetMachine = target.CreateTargetMachine(
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
    public static void Build(string outputDir, string intermediateDir, List<string> objectFiles,
        string finalExecutablePath)
    {
        CobraLogger.Info("Starting final linking process...");
        var wrapperFile = Path.Combine(intermediateDir, "main.cpp");

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
        var objectFilesArg = string.Join(" ", objectFiles.Select(f => $"\"{f}\""));
        var psi = new ProcessStartInfo
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
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

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
                CobraLogger.Error(
                    "No specific error message was captured from stderr. Ensure g++ is installed and configured correctly.");
            }

            throw new Exception($"Linking failed with exit code {process.ExitCode}. See console output for details.");
        }
    }
}