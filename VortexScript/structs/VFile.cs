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
        public void InterpretThisFile(bool entrypoint = false){
            if(entrypoint){
                FileFrame = new(this,0,"Entrypoint("+Path+")");
            }
            else{
                FileFrame = new(this,0,"Acquired("+Path+")");
            }
            Interpreter.CallStack.Push(FileFrame);
            TopLevelContext = new VContext([], [], 0, ScopeTypeEnum.topLevel)
            {
                IsMain = entrypoint
            };
            if (!Interpreter.ActiveModules.TryAdd(GetFileName(),TopLevelContext)){
                throw new ModuleAlreadyLoadedError(GetFileName());
            }
            FileFrame.ScopeStack.Push(TopLevelContext);
            new Interpreter(this).ExecuteFile();
        }
        public string[] ReadFile(){
            if(bufferedFile.Length==0){
                bufferedFile = FileReader.ReadFile(Path);
            }
            return bufferedFile;
        }
        public bool Exists(){
            return FileReader.FileExists(Path);
        }

        public string GetFileName(){
            return Path.Split(".")[0];
        }
    }
}