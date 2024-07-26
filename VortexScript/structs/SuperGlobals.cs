namespace Vortex
{

    public static class SuperGlobals
    {
        public static Dictionary<string, Func<V_Variable>> SuperGlobalVars { get; private set; } = new(){
            {"true", ()=>V_Variable.Construct(DataType.Bool,true)},
            {"false", ()=>V_Variable.Construct(DataType.Bool,false)},
            {"unset",()=>V_Variable.Construct(DataType.Unset,"")},
            {"🌀",()=>V_Variable.Construct(DataType.String,"Vortex script v. "+Interpreter.version)},
            {"this",()=>V_Variable.Construct(DataType.Module,Interpreter.GetCurrentFrame().VFile.GetFileNameUpper())},
            {"main",()=>V_Variable.Construct(DataType.Module,Interpreter.CallStack.First().ScopeStack.First())},
            {"_frame",()=>V_Variable.Construct(DataType.String,Interpreter.GetCurrentFrame().Name)},
            {"_depth",()=>V_Variable.Construct(DataType.Number,Interpreter.GetCurrentContext().Depth)},
            {"_line",()=>V_Variable.Construct(DataType.Number,Interpreter.GetCurrentFrame().currentLine)},
            {"none",()=>V_Variable.Construct(DataType.None,"none")},
            {"any",()=>V_Variable.Construct(DataType.Any,"any")},
            {"first",()=>V_Variable.Construct(DataType.Indexer,"0")},
            {"last",()=>V_Variable.Construct(DataType.Indexer,"-1")},
        };

        //Math
        
    }
    public  class InternalMath : InternalModule{
        public static readonly V_Variable Pi = V_Variable.Construct(DataType.Number,Math.PI);
        public static readonly V_Variable E = V_Variable.Construct(DataType.Number,Math.E);

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
            PreRandom((int)a,(int)b);
            return random.Next((int)b-(int)a)+(int)a;
        }
        [InternalFunc(DataType.Number)]
        public static double RandomRange(double a, double b){
            PreRandom(a,b);
            return random.NextDouble()*(b-a)+a;
        }
        public static void PreRandom(double a, double b){
            if(a>=b||a<0){
                throw new ArgumentError("Lower limit must be higher than 0 and lower than upper limit.");
            }
            random ??= new();
        }

        [InternalFunc(DataType.Unset)]
        public static void SetSeed(double a){
            random = new((int)a);
        }
    }
     public  class InternalStd : InternalModule{
        [InternalFunc(DataType.Unset)]
        public static void Print(string a){
            Console.WriteLine(a);
        }
    }
}