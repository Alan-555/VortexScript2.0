namespace Vortex{
    public class Program
    {
        public static readonly string InteractiveTermMode = "interactive_terminal_mode";
        public static void Main(string[] args){
            string file;
            if(args.Length == 0){
                file = InteractiveTermMode;
                file = "script.vort";
            }  
            else{
                file = args[0];
            }
            new VFile(file).InterpretThisFile(true);
        }
    }
}