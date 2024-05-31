namespace Vortex
{
    public class VFrame
    {
        public VFile VFile {get; private set;}
        public int LineStart {get; private set;}
        public int currentLine;
        public Stack<VContext> ScopeStack {get; private set;} = new();

        public VFrame(VFile vFile,int lineStart){
            currentLine = lineStart;
            LineStart = lineStart;
            VFile = vFile;
        }
    }
}