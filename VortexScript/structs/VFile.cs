namespace Vortex
{
    public class VFile{
        public string Path { get; private set;}
        string[] bufferedFile= [];
        public VFrame FileFrame {set; get; }
        public VContext? TopLevelContext {set; get; }
        public VFile(string path){
            Path = path;
            FileFrame = new(this,0,"Entrypoint("+Path+")");
        }
        public void InterpretThisFile(){
            FileFrame = new(this,0,"Entrypoint("+Path+")");
            Interpreter.CallStack.Push(FileFrame);
            TopLevelContext = new VContext([],[],0,ScopeTypeEnum.topLevel);
            FileFrame.ScopeStack.Push(TopLevelContext);
            new Interpreter(this).ExecuteFile();
        }
        public string[] ReadFile(){
            if(bufferedFile.Length==0){
                bufferedFile = FileReader.ReadFile(Path);
            }
            return bufferedFile;
        }

        public string GetFileName(){
            return Path.Split(".")[0];
        }
    }
}