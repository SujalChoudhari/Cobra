using System.Diagnostics;
using Antlr4.Runtime;
using Cobra.Compiler.Visitors;
using Cobra.Utils;
using LLVMSharp.Interop;

namespace Cobra.Compiler
{
    public class CobraBuilder
    {
        private LLVMModuleRef _module;
        private LLVMBuilderRef _builder;
        private LLVMContextRef _context;
        private readonly string _sourceCode;

        public CobraBuilder(string sourceCode)
        {
            _sourceCode = sourceCode;
            InitializeLlvm();
        }

        /// <summary>
        /// Initialize LLVM context, module, and builder
        /// </summary>
        private void InitializeLlvm()
        {
            _context = LLVMContextRef.Create();
            _module = _context.CreateModuleWithName("CobraModule");
            _builder = _context.CreateBuilder();
        }

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

            // Use the single context for all types
            var int32Type = LLVMTypeRef.Int32; // DO NOT create with context manually
            var mainFunctionType = LLVMTypeRef.CreateFunction(int32Type, []);
            var mainFunction = _module.AddFunction("__cobra_main__", mainFunctionType);

            var entryBlock = mainFunction.AppendBasicBlock("entry");
            _builder.PositionAtEnd(entryBlock);

            // printf declaration
            var charPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            var printfFunctionType = LLVMTypeRef.CreateFunction(int32Type, new[] { charPtrType }, true);
            var printfFunction = _module.AddFunction("printf", printfFunctionType);
            CobraLogger.PrintfFunction = printfFunction;

            // Visit program
            var programVisitor = new CobraProgramVisitor(_module, _builder);
            programVisitor.Visit(programContext);
            _builder.BuildRet(LLVMValueRef.CreateConstInt(int32Type, 0));
        }

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
                LLVMRelocMode.LLVMRelocPIC,
                LLVMCodeModel.LLVMCodeModelDefault
            );

            if (targetMachine.TryEmitToFile(_module, filePath, LLVMCodeGenFileType.LLVMObjectFile,
                    out string errorMessage))
            {
                CobraLogger.Success($"Generated object file at: {filePath}");
            }
            else
            {
                throw new Exception($"Error generating object file: {errorMessage}");
            }
        }

        public static void Build(string outputDir, string intermediateDir, List<string> objectFiles,
            string finalExecutablePath)
        {
            CobraLogger.Info("Starting final linking process...");
            var wrapperFile = Path.Combine(intermediateDir, "main.cpp");

            File.WriteAllText(wrapperFile, @"
#include <stdio.h>

extern ""C"" int __cobra_main__();

int main(int argc, char **argv) {
    return __cobra_main__();
}
");

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

                throw new Exception(
                    $"Linking failed with exit code {process.ExitCode}. See console output for details.");
            }
        }
    }
}