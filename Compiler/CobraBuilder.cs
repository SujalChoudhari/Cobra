// This project requires the following NuGet packages:
// - Antlr4.Runtime.Standard
// - LLVMSharp.Interop (v16.0.0 or later)

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using LLVMSharp.Interop;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

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
    /// </summary>
    private void InitializeLLVM()
    {
        // Create an LLVM context and module
        LLVMContextRef context = LLVMContextRef.Create();
        _module = context.CreateModuleWithName("CobraModule");

        // Create an IR builder
        _builder = context.CreateBuilder();
    }

    /// <summary>
    /// Compiles the Cobra source code.
    /// </summary>
    public void Compile()
    {
        // 1. ANTLR4 Parsing
        var inputStream = new AntlrInputStream(_sourceCode);
        var lexer = new CobraLexer(inputStream);
        var commonTokenStream = new CommonTokenStream(lexer);
        var parser = new CobraParser(commonTokenStream);
        var programContext = parser.program();

        // 2. Set up the main function to hold our compiled code
        LLVMTypeRef int32Type = LLVMTypeRef.Int32;
        LLVMTypeRef mainFunctionType = LLVMTypeRef.CreateFunction(int32Type, Array.Empty<LLVMTypeRef>());
        LLVMValueRef mainFunction = _module.AddFunction("__cobra_main__", mainFunctionType);

        // Create a basic block for the main function
        LLVMBasicBlockRef entryBlock = mainFunction.AppendBasicBlock("entry");
        _builder.PositionAtEnd(entryBlock);
        
        // 3. Declare the C standard library's printf function
        LLVMTypeRef charPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
        LLVMTypeRef printfFunctionType = LLVMTypeRef.CreateFunction(int32Type, new LLVMTypeRef[] { charPtrType }, true);
        LLVMValueRef printfFunction = _module.AddFunction("printf", printfFunctionType);

        // 4. Walk the parse tree and compile statements
        CobraProgramVisitor programVisitor = new CobraProgramVisitor(_module, _builder, _namedValues);
        programVisitor.Visit(programContext);

        // 5. Add a call to printf to see a result
        LLVMValueRef xValue = _namedValues["x"];
        LLVMValueRef loadedXValue = _builder.BuildLoad2(xValue.TypeOf.ElementType, xValue, "x");

        LLVMValueRef formatString = _builder.BuildGlobalStringPtr("The value of x is: %d\n", "format_string");
        LLVMValueRef[] printfArgs = new LLVMValueRef[] { formatString, loadedXValue };
        _builder.BuildCall2(printfFunctionType, printfFunction, printfArgs, "calltmp");

        // 6. Finalize the main function by returning 0
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
            Console.WriteLine($"Error generating LLVM IR: {errorMessage}");
        }
    }

    /// <summary>
    /// Generates a shared object (.so) file.
    /// Note: This functionality requires a full LLVM installation on the system.
    /// For this example, we'll only demonstrate the C# code part.
    /// You will need to link the object file to create an executable.
    /// </summary>
    /// <param name="filePath">The path to save the .so file.</param>
    public void GenerateObjectFile(string filePath)
    {
        // Initialize the native target and assembly printer for the current machine.
        // This is a more robust alternative to initializing all targets.
        LLVM.InitializeNativeTarget();
        LLVM.InitializeNativeAsmPrinter();

        string triple = LLVMTargetRef.DefaultTriple;
        LLVMTargetRef target = LLVMTargetRef.GetTargetFromTriple(triple);

        // Create a target machine for the current architecture
        LLVMTargetMachineRef targetMachine = target.CreateTargetMachine(
            triple, "generic", "",
            LLVMCodeGenOptLevel.LLVMCodeGenLevelAggressive,
            // The fix: use a relocation model that supports Position-Independent Code (PIC)
            LLVMRelocMode.LLVMRelocPIC,
            LLVMCodeModel.LLVMCodeModelDefault
        );

        // Generate the object file
        if (targetMachine.TryEmitToFile(_module, filePath, LLVMCodeGenFileType.LLVMObjectFile, out string errorMessage))
        {
            Console.WriteLine($"Generated object file at: {filePath}");
        }
        else
        {
            Console.WriteLine($"Error generating object file: {errorMessage}");
        }
    }

    public void Build(String outputDir, String objectFile)
    {
        string executableFile = Path.Combine(outputDir, "myprogram");
        string wrapperFile = Path.Combine(outputDir, "main.cpp");
        
        // Step 2: Automatically create the C++ wrapper file
        File.WriteAllText(wrapperFile, @"
                #include <stdio.h>
                extern ""C"" int __cobra_main__();
                int main(int argc, char **argv) {
                    return __cobra_main__();
                }
            ");

        // Step 3: Automatically link the object file with the wrapper to create the final executable
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "g++",
            Arguments = $"-o {executableFile} {wrapperFile} {objectFile}",
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
                Console.WriteLine($"\nSuccessfully created executable: {executableFile}");
                Console.WriteLine("To run it, use: {0}", executableFile);
            }
            else
            {
                Console.WriteLine("\nLinking failed. Here is the error output:");
                Console.WriteLine(error);
            }
        }
    }
}
