using System.Reflection;
using VortexScript.Lexer.LexerStructs;
using VortexScript.Structs;

namespace VortexScript.Vortex;

public class VFunc
{
    public string Identifier { get; private set; }
    public VFile File { get; private set; }
    public VFuncArg[] Args { get; private set; }

    public CompiledStatement[] FunctionBody { get; set; } = [];
    public int StartLine { get; private set; }
    public MethodInfo? CSharpFunc { get; set; } = null;
    public bool ForceUppercase { get; set; } = false;
    public DataType returnType { get; set; } = DataType.Any;
    public bool IsConstructor {get;set;} = false;

    public VFunc(string indetifier, VFile file, VFuncArg[] args, int startLine)
    {
        string signature = args.Select(x=> x.enforcedType.ToString()).Aggregate("",(x,y)=>x+" "+y);
        Identifier = indetifier+signature;
        File = file;
        Args = args;
        StartLine = startLine;
    }
    public string GetFullPath()
    {
        return File.GetFileName() + "." + Identifier + "()";
    }
    public override string ToString()
    {
        var join = "";
        var isInternal = CSharpFunc != null;
        if (!isInternal)
        {
            join = string.Join(",", Args.Select(x => x.name + ":" + x.enforcedType.ToString() + (x.defaultValue.value == null ? "" : "=" + x.defaultValue.value?.ToString())));
        }
        else
        {
            join = string.Join(",", CSharpFunc!.GetParameters().Select(x => x.Name + ":" + Utils.CSharpTypeToVortexType(x.ParameterType)));
        }
        string f = (isInternal ? (ForceUppercase? Identifier : Identifier[0].ToString().ToLower() + Identifier[1..]) : Identifier) + "(" + join + ")";
        f += " " + returnType.ToString() + " :\t" + (isInternal ? "<internal function>" : string.Join(" | ", FunctionBody));
        return f;
    }
}

public class VFuncArg
{
    public DataType enforcedType;
    public Token defaultValue;
    public string name;

    public VFuncArg(string name, Token defaultValue = default, DataType enforcedType = DataType.Any)
    {
        this.name = name;
        this.defaultValue = defaultValue;
        this.enforcedType = enforcedType;
    }
}
