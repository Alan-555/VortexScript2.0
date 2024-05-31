namespace Vortex
{
    public class VFile{
        public string Path { get; private set;}
        string[] bufferedFile= [];
        public Interpreter FileInterpreter {get;  set;}
        public VFile(string path){
            Path = path;
        }
        public string[] ReadFile(){
            if(bufferedFile.Length==0){
                bufferedFile = FileReader.ReadFile(Path);
            }
            return bufferedFile;
        }
    }
}