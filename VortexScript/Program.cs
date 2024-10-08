﻿using VortexScript.Vortex;

namespace VortexScript;


public class Program
{
    public static readonly string InteractiveTermMode = "interactive_terminal";
    public static void Main(string[] args)
    {
        string file;
        if (args.Length == 0)
        {
            file = InteractiveTermMode;
            //file = "script.vort";
        }
        else
        {
            file = args[0];
        }
        new VFile(file).InterpretThisFile(true);
    }
}
