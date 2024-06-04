﻿namespace Vortex{
    public class Program
    {
        public static void Main(string[] args){
            string file;
            if(args.Length == 0){
                file = "script.vort";
            }  
            else{
                file = args[0];
            }
            new VFile(file).InterpretThisFile(true);
        }
    }
}