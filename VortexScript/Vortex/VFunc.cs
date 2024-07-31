using System.Reflection;
using VortexScript.Structs;

namespace VortexScript.Vortex;

public class VFunc
{
    public string Identifier { get; private set; }
    public VFile File { get; private set; }
    public VFuncArg[] Args { get; private set; }

    public string[] FunctionBody { get; set; } = [];
    public int StartLine { get; private set; }
    public MethodInfo? CSharpFunc { get; set; } = null;
    public DataType returnType { get; set; } = DataType.None;

    public VFunc(string indetifier, VFile file, VFuncArg[] args, int startLine)
    {
        Identifier = indetifier;
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
            join = string.Join(",", CSharpFunc!.GetParameters().Select(x => x.Name + ":" + x.ParameterType.ToString()));
        }
        string f = (isInternal ? Identifier[0].ToString().ToLower() + Identifier[1..] : Identifier) + "(" + join + ")";
        f += " " + returnType.ToString() + " :\t" + (isInternal ? "<internal function>" : string.Join(';', FunctionBody));
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
