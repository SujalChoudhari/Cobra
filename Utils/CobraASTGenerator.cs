using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace Cobra.Utils;

/// <summary>
/// Generates a human-readable Abstract Syntax Tree (AST) from the Cobra parse tree
/// with tree-like formatting and saves it to a .ast file.
/// </summary>
public class CobraAstGenerator
{
    private readonly CobraParser.ProgramContext? _programContext;
    private readonly string _programName;

    /// <summary>
    /// Initializes a new instance of the <see cref="CobraAstGenerator"/> class.
    /// </summary>
    /// <param name="programContext">The root context of the parsed program's tree.</param>
    /// <param name="programName">The base name for the output file. Defaults to "cobra_program".</param>
    /// <exception cref="ArgumentNullException">Thrown if the program context is null.</exception>
    public CobraAstGenerator(CobraParser.ProgramContext? programContext, string programName = "cobra_program")
    {
        _programContext = programContext ?? throw new ArgumentNullException(nameof(programContext));
        _programName = string.IsNullOrWhiteSpace(programName) ? "cobra_program" : programName;
    }

    /// <summary>
    /// Generates the AST and saves it to a .ast file in the specified directory.
    /// </summary>
    /// <param name="outputDir">The directory where the .ast file will be saved.</param>
    /// <exception cref="Exception">Thrown if an error occurs during AST generation or file writing.</exception>
    public void GenerateAst(string outputDir)
    {
        var astFilePath = Path.Combine(outputDir, $"{_programName}.ast");
        CobraLogger.Info($"Generating AST at: {astFilePath}");

        try
        {
            var astText = GenerateAstText(_programContext);
            File.WriteAllText(astFilePath, astText);
            CobraLogger.Success($"Generated AST file at: {astFilePath}");
        }
        catch (Exception ex)
        {
            CobraLogger.Error($"Error generating AST: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Generates a human-readable AST string with tree-like formatting.
    /// </summary>
    /// <param name="context">The root parse tree node (program context).</param>
    /// <returns>A formatted string representing the AST.</returns>
    private static string GenerateAstText(IParseTree? context)
    {
        var astBuilder = new StringBuilder();
        var visitor = new AstVisitor();
        AstVisitor.Visit(context, astBuilder, new List<bool>());
        return astBuilder.ToString();
    }

    /// <summary>
    /// A nested visitor class to traverse the parse tree and build the formatted AST string.
    /// It uses a list of booleans to track branch status for proper tree-like indentation.
    /// </summary>
    private class AstVisitor : CobraBaseVisitor<object>
    {
        /// <summary>
        /// Recursively visits a parse tree node and its children, appending a formatted
        /// representation to a StringBuilder.
        /// </summary>
        /// <param name="tree">The parse tree node to visit.</param>
        /// <param name="builder">The StringBuilder to append the formatted AST text to.</param>
        /// <param name="isLast">A list of booleans indicating if each ancestor is the last child.</param>
        public static void Visit(IParseTree? tree, StringBuilder builder, List<bool> isLast)
        {
            if (tree == null)
            {
                return;
            }

            string? nodeName = GetNodeName(tree);
            string indent = GetIndent(isLast);
            
            builder.AppendLine($"{indent}{nodeName}");

            for (int i = 0; i < tree.ChildCount; i++)
            {
                var newIsLast = new List<bool>(isLast) { i == tree.ChildCount - 1 };
                Visit(tree.GetChild(i), builder, newIsLast);
            }
        }

        /// <summary>
        /// Determines the display name for a given parse tree node.
        /// </summary>
        /// <param name="tree">The parse tree node.</param>
        /// <returns>The formatted name of the node.</returns>
        private static string? GetNodeName(IParseTree? tree)
        {
            return tree switch
            {
                ParserRuleContext ruleContext => CobraParser.ruleNames[ruleContext.RuleIndex],
                ITerminalNode terminal => terminal.Symbol.Type switch
                {
                    CobraParser.Eof => "EOF",
                    _ => $"{CobraLexer.DefaultVocabulary.GetSymbolicName(terminal.Symbol.Type) ?? "TOKEN"}: {terminal.GetText()}"
                },
                _ => tree?.GetType().Name
            };
        }

        /// <summary>
        /// Generates the indentation string with tree markers based on the `isLast` list.
        /// </summary>
        /// <param name="isLast">A list of booleans indicating if each ancestor is the last child.</param>
        /// <returns>The formatted indentation string.</returns>
        private static string GetIndent(IReadOnlyList<bool> isLast)
        {
            if (isLast.Count == 0)
            {
                return string.Empty;
            }
            
            var indentBuilder = new StringBuilder();
            for (int i = 0; i < isLast.Count - 1; i++)
            {
                indentBuilder.Append(isLast[i] ? "    " : "│   ");
            }

            indentBuilder.Append(isLast[^1] ? "└── " : "├── ");
            return indentBuilder.ToString();
        }

        /// <summary>
        /// Overrides the default VisitTerminal to prevent double-processing, as terminals are handled in the main Visit method.
        /// </summary>
        /// <param name="node">The terminal node.</param>
        /// <returns>Null, as this method does not produce a value.</returns>
        public override object VisitTerminal(ITerminalNode node)
        {
            return null!;
        }
    }
}