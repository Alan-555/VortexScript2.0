namespace Vortex
{
    public class VFrame
    {
        public VFile VFile {get; private set;} //The file that this frame belongs to
        public int LineOffset {get; private set;}
        public int currentLine;
        public Stack<VContext> ScopeStack {get; private set;} = new();
        public string Name {get; private set;}
        public bool StopSignal {get; set;} = false;
        public bool CatchSignal {get; set;} = false;

        public VFrame(VFile vFile,int lineStart, string name){
            currentLine = lineStart;
            LineOffset = lineStart;
            VFile = vFile;
            Name = name;
        }

        public override string ToString() => $"{VFile.GetFileName()}:{LineOffset+1}";
    }
}