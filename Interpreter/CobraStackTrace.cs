using System.Collections;

namespace Cobra.Interpreter
{
    public class CobraStackTrace : IEnumerable<CallFrame>
    {
        private readonly Stack<CallFrame> _frames;

        public CobraStackTrace()
        {
            _frames = new Stack<CallFrame>();
        }
        
        // Copy constructor
        public CobraStackTrace(CobraStackTrace other)
        {
            // Reverse to maintain correct stack order when copying
            _frames = new Stack<CallFrame>(other._frames.Reverse());
        }
        
        public CobraStackTrace(IEnumerable<CallFrame> frames)
        {
            _frames = new Stack<CallFrame>(frames.Reverse());
        }

        public void Push(CallFrame frame)
        {
            _frames.Push(frame);
        }

        public CallFrame Pop()
        {
            return _frames.Pop();
        }

        public CallFrame? Peek()
        {
            return _frames.Count > 0 ? _frames.Peek() : null;
        }

        public int Count => _frames.Count;

        public IEnumerator<CallFrame> GetEnumerator()
        {
            return _frames.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}