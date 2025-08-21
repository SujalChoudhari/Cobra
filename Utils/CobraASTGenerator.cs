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
    private readonly CobraParser.ProgramContext _programContext;
    private readonly string _programName;

    public CobraAstGenerator(CobraParser.ProgramContext programContext, string programName = "cobra_program")
    {
        _programContext = programContext ?? throw new ArgumentNullException(nameof(programContext));
        _programName = string.IsNullOrWhiteSpace(programName) ? "cobra_program" : programName;
    }

    /// <summary>
    /// Generates the AST and saves it to a .ast file in the specified directory.
    /// </summary>
    /// <param name="outputDir">Directory to save the .ast file.</param>
    public void GenerateAst(string outputDir)
    {
        string astFilePath = Path.Combine(outputDir, $"{_programName}.ast");
        CobraLogger.Info($"Generating AST at: {astFilePath}");

        try
        {
            // Generate the formatted AST
            string astText = GenerateAstText(_programContext);

            // Save to file
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
    private string GenerateAstText(IParseTree context)
    {
        StringBuilder astBuilder = new StringBuilder();
        AstVisitor visitor = new AstVisitor();
        visitor.Visit(context, astBuilder, new List<bool>());
        return astBuilder.ToString();
    }

    /// <summary>
    /// Visitor class to traverse the parse tree and generate a formatted AST with tree markers.
    /// </summary>
    private class AstVisitor : CobraBaseVisitor<object>
    {
        public void Visit(IParseTree tree, StringBuilder builder, List<bool> isLast)
        {
            if (tree == null)
                return;

            // Determine node name
            string nodeName = tree switch
            {
                ParserRuleContext ruleContext => CobraParser.ruleNames[ruleContext.RuleIndex],
                TerminalNodeImpl terminal => $"{terminal.Symbol.Type switch
                {
                    CobraParser.Eof => "EOF",
                    _ => $"{CobraLexer.DefaultVocabulary.GetSymbolicName(terminal.Symbol.Type) ?? "TOKEN"}: {terminal.Symbol.Text}"
                }}",
                _ => tree.GetType().Name
            };

            // Build indentation with tree markers
            string indent = "";
            for (int i = 0; i < isLast.Count - 1; i++)
            {
                indent += isLast[i] ? "    " : "│   ";
            }

            indent += isLast.Count > 0 ? (isLast[^1] ? "└── " : "├── ") : "";

            // Append the node
            builder.AppendLine($"{indent}{nodeName}");

            // Visit children
            for (int i = 0; i < tree.ChildCount; i++)
            {
                var newIsLast = new List<bool>(isLast) { i == tree.ChildCount - 1 };
                Visit(tree.GetChild(i), builder, newIsLast);
            }
        }

        public override object VisitTerminal(ITerminalNode node)
        {
            // Handled in Visit to include token details
            return null;
        }
    }
}