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

        //Math
        
    }
    public  class InternalMath : InternalModule{
        public static readonly V_Variable Pi = new(DataType.Number,Math.PI);
        public static readonly V_Variable E = new(DataType.Number,Math.E);

        [InternalFunc(DataType.Number)]
        public static double Max(double a, double b){
            return Math.Max(a,b);
        }
    }
    public  class InternalRandom : InternalModule{
        [InternalFunc(DataType.Number)]
        public static double Random(double a, double b){
            return new Random().Next((int)b)+(int)a;
        }
    }
}