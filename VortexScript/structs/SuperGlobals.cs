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
        [InternalFunc(DataType.Number)]
        public static double Min(double a, double b){
            return Math.Min(a,b);
        }
        [InternalFunc(DataType.Number)]
        public static double Sin(double a){
            return Math.Sin(a);
        }
        [InternalFunc(DataType.Number)]
        public static double Sqrt(double a){
            return Math.Sqrt(a);
        }
        [InternalFunc(DataType.Number)]
        public static double Pow(double a,double b){
            return Math.Pow(a,b);
        }
        [InternalFunc(DataType.Number)]
        public static double Root(double a,double b){
            return Math.Pow(a,1/b);
        }
    }
    public  class InternalRandom : InternalModule{
        public static Random random;

        [InternalFunc(DataType.Number)]
        public static double Random(double a, double b){
            return new Random().Next((int)b)+(int)a;
        }

        [InternalFunc(DataType.Unset)]
        public static void SetSeed(int a){
            random = new(a);
        }
    }
     public  class InternalStd : InternalModule{
        [InternalFunc(DataType.Unset)]
        public static void Print(string a){
            Console.WriteLine(a);
        }
    }
}