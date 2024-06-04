namespace Vortex
{

    public static class SuperGlobals
    {
        public static Dictionary<string, Func<V_Variable>> SuperGlobalVars { get; private set; } = new(){
            {"true", ()=>new V_Variable(DataType.Bool,true)},
            {"false", ()=>new V_Variable(DataType.Bool,false)},
            {"unset",()=>new V_Variable(DataType.Unset,"")},
            {"🌀",()=>new V_Variable(DataType.String,"Vortex script v. "+Interpreter.version)},
            {"this",()=>new V_Variable(DataType.String,Interpreter.GetCurrentFrame().VFile.GetFileName())},
            {"_frame",()=>new V_Variable(DataType.String,Interpreter.GetCurrentFrame().Name)},
            {"_depth",()=>new V_Variable(DataType.Number,Interpreter.GetCurrentContext().Depth)},
            {"_line",()=>new V_Variable(DataType.Number,Interpreter.GetCurrentFrame().currentLine)},
            {"none",()=>new V_Variable(DataType.None,"None")},
            {"any",()=>new V_Variable(DataType.Any,"Any")},
        };
    }
}