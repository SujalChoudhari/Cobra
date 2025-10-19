using Antlr4.Runtime;

namespace Cobra.Interpreter
{
    public class CallFrame
    {
        public string FunctionName { get; }
        public string SourcePath { get; }
        public int Line { get; }
        public int Column { get; }
        public int StartIndex { get; }
        public int StopIndex { get; }

        public CallFrame(string functionName, string sourcePath, IToken callSiteToken)
        {
            FunctionName = functionName;
            SourcePath = sourcePath;
            Line = callSiteToken.Line;
            Column = callSiteToken.Column;
            StartIndex = callSiteToken.StartIndex;
            StopIndex = callSiteToken.StopIndex;
        }
    }
}