using VortexScript.Structs;
using VortexScript.Vortex;

namespace VortexScript.Definitions;


public static class SuperGlobals
{
    public static Dictionary<string, Func<V_Variable>> SuperGlobalVars { get; private set; } = new(){
        {"true", ()=>V_Variable.Construct(DataType.Bool,true)}, //-> 1==1
        {"false", ()=>V_Variable.Construct(DataType.Bool,false)},//-> !true
        {"unset",()=>V_Variable.Construct(DataType.Unset,"")},//-> unset
        {"🌀",()=>V_Variable.Construct(DataType.String,"Vortex script v. "+Interpreter.version)},
        {"_version",()=>V_Variable.Construct(DataType.String,"Vortex script v. "+Interpreter.version)},
        {"this",()=>V_Variable.Construct(DataType.Module,Interpreter.GetCurrentFrame().VFile.GetFileNameUpper())},
        {"main",()=>V_Variable.Construct(DataType.Module,Interpreter.CallStack.First().ScopeStack.First())},
        {"_frame",()=>V_Variable.Construct(DataType.String,Interpreter.GetCurrentFrame().Name)},
        {"_depth",()=>V_Variable.Construct(DataType.Number,Interpreter.GetCurrentContext().Depth)},
        {"_line",()=>V_Variable.Construct(DataType.Number,Interpreter.GetCurrentFrame().currentLine)},
        {"first",()=>V_Variable.Construct(DataType.Indexer,"0")},
        {"last",()=>V_Variable.Construct(DataType.Indexer,"-1")},
        {"inf",()=>V_Variable.Construct(DataType.Number,double.PositiveInfinity)},
        {"ninf",()=>V_Variable.Construct(DataType.Number,double.NegativeInfinity)},
        {"NaN",()=>V_Variable.Construct(DataType.Number,double.NaN)},
        {"∞",()=>V_Variable.Construct(DataType.Number,double.PositiveInfinity)},
        {"Tprimitive",()=>V_Variable.Construct(DataType.Array,"Number,String,Bool")},
        {"Tgeneric",()=>V_Variable.Construct(DataType.Array,"Any,None,Unset")},
        {"Tcomplex",()=>V_Variable.Construct(DataType.Array,"Array,Function,GroupType,Module,Type,Error,Indexer")},
        {"_library",()=>V_Variable.Construct(DataType.Array,Utils.ConvertDictToVArray<VContext>(Interpreter.ActiveModules,DataType.Module),new(){readonly_=true})},
        

    };

    //Math

}
public class InternalMath : InternalStandartLibrary
{
    public static readonly V_Variable Pi = V_Variable.Construct(DataType.Number, Math.PI,new(){readonly_=true});
    public static readonly V_Variable E = V_Variable.Construct(DataType.Number, Math.E,new(){readonly_=true});
    public static readonly V_Variable Rad = V_Variable.Construct(DataType.Number, 1d/180d*Math.PI,new(){readonly_=true});
     public static readonly V_Variable Deg = V_Variable.Construct(DataType.Number, 1/Math.PI*180,new(){readonly_=true});

    [InternalFunc(DataType.Number)]
    public static double Max(double a, double b)
    {
        return Math.Max(a, b);
    }
    [InternalFunc(DataType.Number)]
    public static double Min(double a, double b)
    {
        return Math.Min(a, b);
    }
    [InternalFunc(DataType.Number)]
    public static double Sin(double a)
    {
        return Math.Sin(a);
    }
    [InternalFunc(DataType.Number)]
    public static double Sqrt(double a)
    {
        return Math.Sqrt(a);
    }
    [InternalFunc(DataType.Number)]
    public static double Pow(double a, double b)
    {
        return Math.Pow(a, b);
    }
    [InternalFunc(DataType.Number)]
    public static double Root(double a, double b)
    {
        return Math.Pow(a, 1 / b);
    }
}
public class InternalRandom : InternalStandartLibrary
{
    public static Random? random;

    [InternalFunc(DataType.Number)]
    public static double Random(double a, double b)
    {
        PreRandom((int)a, (int)b);
        return random!.Next((int)b - (int)a) + (int)a;
    }
    [InternalFunc(DataType.Number)]
    public static double RandomRange(double a, double b)
    {
        PreRandom(a, b);
        return random!.NextDouble() * (b - a) + a;
    }
    public static void PreRandom(double a, double b)
    {
        if (a >= b || a < 0)
        {
            throw new ArgumentError("Lower limit must be higher than 0 and lower than upper limit.");
        }
        random ??= new();
    }

    [InternalFunc(DataType.None)]
    public static void SetSeed(double a)
    {
        random = new((int)a);
    }
}
public class InternalStdInOut : InternalStandartLibrary
{
    [InternalFunc(DataType.None)]
    public static void Print(string a)
    {
        Console.WriteLine(a);
    }
    [InternalFunc(DataType.String)]
    public static object Read()
    {
        var res = Console.ReadLine()!;
        return res =="" ? SuperGlobals.SuperGlobalVars["unset"].Invoke() : res!; 
    }

    public static Token TokenRead()
    {
        var res = Console.ReadLine();
        return res =="" ? new Token(TokenType.Unset,"") : new Token(TokenType.String,res); 
    }
}

public class InternalUtils : InternalStandartLibrary
{
    [InternalFunc(DataType.Any)]
    public static V_Variable Evaluate(string expression,DataType requiredType = DataType.Any){
        return Evaluator.Evaluate(expression,requiredType);
    }
}

public class InternalTest : InternalUtils
{
    [InternalFunc(DataType.None)]
    public static void Test(){
        Console.WriteLine("test");
    }
}

public class InternalClio : InternalStandartLibrary
{
    public static readonly V_Variable HorsePower = V_Variable.Construct(DataType.Number, -1d);
    public static readonly V_Variable Incidents = V_Variable.Construct(DataType.Array, new VArray());


    [InternalFunc(DataType.Array)]
    public static V_Variable Drive(){
        string[] allInc = ["tree","hole","ditch","granny","police","deep space","death"];
        string inc = allInc[(int)InternalRandom.Random((int)0,(int)allInc.Length)];
        (Incidents.value as VArray).Add(V_Variable.Construct(DataType.String,inc));
        return Incidents;
    }
}