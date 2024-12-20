using System.Globalization;
using System.Runtime.CompilerServices;
using VortexScript.Definitions;

namespace VortexScript.Vortex;

public struct V_VarFlags
{
    public bool unsetable = false;
    public bool readonly_ = false;

    public V_VarFlags()
    {
    }
}
public abstract class V_Variable
{
    public static Dictionary<DataType, Type> DataTypeToType = new(){
            { DataType.Number, typeof(VType_Number)},
            { DataType.Bool, typeof(VType_Bool)},
            { DataType.String, typeof(VType_String)},
            { DataType.Array, typeof(VType_Array)},
            { DataType.Unset, typeof(VType_Unset)},
            { DataType.None, typeof(VType_None)},
            { DataType.Any, typeof(VType_Any)},
            { DataType.Module, typeof(VType_Module)},
            { DataType.Type, typeof(VType_Type)},
            { DataType.Indexer, typeof(VType_Indexer)},
            { DataType.Error, typeof(VType_Error)},
            { DataType.GroupType, typeof(VType_Tag)},
            { DataType.Int, typeof(VType_Int)},
            { DataType.Function, typeof(VType_Function)},
            { DataType.Class, typeof(VType_Class)},
            { DataType.Object, typeof(VType_Object)},
        };

    public DataType type;
    public object value;

    //flags
    public V_VarFlags flags;
    public static V_Variable Construct(DataType type, object? value, V_VarFlags flags = default)
    {
        var type_ = DataTypeToType[type];
        return (V_Variable)Activator.CreateInstance(type_, type, value, flags)!;
    }
    public static V_Variable Construct(DataType type, string value, V_VarFlags flags = default)
    {
        var type_ = DataTypeToType[type];
        var instance = (V_Variable)Activator.CreateInstance(type_, type, null, flags)!;
        instance.value = instance.ConvertToCSharpType(value);
        return instance;
    }
    public static object ConstructValue(DataType type, string value, V_VarFlags flags = default)
    {
        return Construct(type, value, flags).value;
    }
    public void Assign(V_Variable new_,string ident = ""){
        if(flags.readonly_) throw new AssigmentToReadonlyVarError(ident);
        type = new_.type;
        value = new_.value;
    }
    protected V_Variable(DataType type, object? value, V_VarFlags flags)
    {

        this.type = type;
        this.value = value!;

        this.flags = flags;
        if (type == DataType.Unset)
            this.value = "unset";
        if (value == null)
            this.value = "unset";
    }

    public abstract object ConvertToCSharpType(string v);
    public abstract override string ToString();
    public virtual VFunc? GetCallableFunc(params object[] args)
    {
        return null;
    }
    public virtual V_Variable GetField(string identifier)
    {
        throw new IlegalOperationError($"The type '{type}' cannot be accessed with the dot notation.");
    }


    public virtual V_Variable Index(IndexerRange index)
    {
        throw new IlegalOperationError($"The type '{type}' is not indexable");
    }
    public virtual V_Variable SpecialAdd(V_Variable second)
    {
        throw new IlegalOperationError($"The type '{type}' has not add operator");
    }
    public virtual V_Variable SpecialSub(V_Variable second)
    {
        throw new IlegalOperationError($"The type '{type}' has no sub operator");
    }
    public virtual V_Variable SpecialMul(V_Variable second)
    {
        throw new IlegalOperationError($"The type '{type}' has no mul operator");
    }
    public virtual V_Variable SpecialDiv(V_Variable second)
    {
        throw new IlegalOperationError($"The type '{type}' has no div operator");
    }
    public virtual V_Variable SpecialClear(object dummy)
    {
        throw new IlegalOperationError($"The type '{type}' has no clear operator");
    }
    public T GetCorrectType<T>()
    {
        return (T)value;
    }
}

public class VType_Number : V_Variable
{
    public VType_Number(DataType type, object value, V_VarFlags flags) : base(type, value, flags)
    {
        if (value != null)
        {
            if ((double)value == 0)
                this.value = 0d;
        }

    }
    public override object ConvertToCSharpType(string v)
    {
        return double.Parse(v, CultureInfo.InvariantCulture);
    }
    public override string ToString()
    {
        if (double.IsPositiveInfinity((double)value))
            return "∞";
        if (double.IsNegativeInfinity((double)value))
            return "-∞";
        return ((double)value).ToString(CultureInfo.InvariantCulture)!;
    }

    public override V_Variable SpecialAdd(V_Variable second)
    {
        if (second.type != type)
            throw new InvalidFormatError(second.type.ToString(), type.ToString());
        value = (double)value + (double)second.value;
        return this;
    }
    public override V_Variable SpecialSub(V_Variable second)
    {
        if (second.type != type)
            throw new InvalidFormatError(second.type.ToString(), type.ToString());
        value = (double)value - (double)second.value;
        return this;
    }
    public override V_Variable SpecialMul(V_Variable second)
    {
        if (second.type != type)
            throw new InvalidFormatError(second.type.ToString(), type.ToString());
        value = (double)value * (double)second.value;
        return this;
    }
    public override V_Variable SpecialDiv(V_Variable second)
    {
        if (second.type != type)
            throw new InvalidFormatError(second.type.ToString(), type.ToString());
        value = (double)value / (double)second.value;
        return this;
    }
}

public class VType_Int : V_Variable
{
    public VType_Int(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        if (v == "∞" || v == "-∞" || v == "NaN")
        {
            throw new ArgumentError("Int may not be infinite or NaN");
        }
        return Math.Floor(double.Parse(v, CultureInfo.InvariantCulture));
    }
    public override string ToString()
    {
        return value.ToString()!;
    }

}

public class VType_String : V_Variable
{
    public VType_String(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        return v;
    }
    public override string ToString()
    {
        return value.ToString()!;
    }
    public override V_Variable Index(IndexerRange index)
    {
        try
        {
            index = index.UseLength(((string)value).Length);
            return Construct(type, value.ToString()![index.start..index.end], new() { readonly_ = true });
        }
        catch (FormatException)
        {
            throw new IndexOutOfBoundsError(index.ToString());
        }
    }
    public override V_Variable SpecialAdd(V_Variable second)
    {
        value = (string)value + second.value.ToString();
        return this;
    }
    public override V_Variable SpecialSub(V_Variable second)
    {
        value = ((string)value).Replace(second.ToString(), "")!;
        return this;
    }
}
public class VType_Bool : V_Variable
{
    public VType_Bool(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        return v == "true" || v == "True" || v == "1";
    }
    public override string ToString()
    {
        return (bool)value ? "true" : "false";
    }
}
public class VType_Array : V_Variable
{
    public VType_Array(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        return Utils.ArgsEval(v, ',')!;
    }
    public override string ToString()
    {
        var arr = (VArray)value;
        if (arr.Count == 0)
            return "[]";
        string ret = "[";
        arr.ForEach(x => ret += x.value.ToString() + ",");
        ret = ret[..^1] + "]";
        return ret;
    }
    public override V_Variable Index(IndexerRange index)
    {
        var arr = (VArray)value;
        try
        {
            index = index.UseLength(arr.Count);
            if (index.IsRange)
            {
                return Construct(DataType.Array, new VArray(arr[index.start..index.end].ToList()));
            }
            else{
                return arr[index.start];
            }
        }
        catch (ArgumentException)
        {
            throw new IndexOutOfBoundsError(index.ToString());
        }
    }
    public override V_Variable SpecialAdd(V_Variable second)
    {
        ((VArray)value).Add(second);
        return this;
    }
    public override V_Variable SpecialSub(V_Variable second)
    {
        if (second.type == DataType.Indexer)
        {
            int i = Convert.ToInt32(second.value);
            try
            {
                if (i < 0) i = ((VArray)value).Count + i;
                ((VArray)value).RemoveAt(i);
                return this;
            }
            catch (IndexOutOfRangeException)
            {
                throw new IndexOutOfBoundsError(i + "");
            }
        }
        ((VArray)value).RemoveAll(x => x.value.Equals(second.value));
        return this;
    }
    public override V_Variable SpecialClear(object dummy)
    {
        ((VArray)value).Clear();
        return this;
    }
}

public class VArray : List<V_Variable>
{
    public override string ToString()
    {
        var arr = this;
        if (arr.Count == 0)
            return "[]";
        string ret = "[";
        arr.ForEach(x => ret += x.value.ToString() + ",");
        ret = ret[..^1] + "]";
        return ret;
    }
    public VArray() { }

    public VArray(List<V_Variable> l) {
        AddRange(l);
    }
    public VArray(params dynamic[] vals)
    {
        foreach (var val in vals)
        {
            this.Add(V_Variable.Construct(Utils.CSharpTypeToVortexType(val.GetType()), val));
        }
    }
}

public class VType_Class : V_Variable
{
    public VClass GetClass(){
        return (VClass)this.value;
    }
    public VType_Class(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }

    public override object ConvertToCSharpType(string v)
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return GetClass().Identifier;
    }

    public override VFunc? GetCallableFunc(params object[] args)
    {
        return GetClass().Constructor;
    }
}
public class VType_Object : V_Variable
{
    public VClassInstance GetClass(){
        return (VClassInstance)this.value;
    }
    public VType_Object(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }

    public override object ConvertToCSharpType(string v)
    {
        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return GetClass().Type.Identifier+"_instace("+RuntimeHelpers.GetHashCode(GetClass())+")";
    }
    public override V_Variable GetField(string identifier)
    {
        if(!GetClass().InstanceVars.TryGetValue(identifier, out var ret)){//TODO: use read var to support function signatures
            throw new UnknownNameError(identifier);
        }
        return ret;
    }

}


public class VType_Unset : V_Variable
{
    public VType_Unset(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        return "unset";
    }
    public override string ToString()
    {
        return "unset";
    }
}
public class VType_Any : V_Variable
{
    public VType_Any(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        return "any";
    }
    public override string ToString()
    {
        return "any";
    }
}

public class VType_None : V_Variable
{
    public VType_None(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        return SuperGlobals.SuperGlobalVars["unset"];
    }
    public override string ToString()
    {
        return "unset";
    }
}

public class VType_Module : V_Variable
{
    VContext? moduleRef;
    public VType_Module(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        if (moduleRef == null)
        {
            if (Interpreter.TryGetModule(v, out var module))
            {
                return module;
            }
            else
            {
                throw new UnknownNameError(v);
            }
        }
        else
        {
            return moduleRef;
        }
    }
    public override string ToString()
    {
        moduleRef ??= (VContext)value;
        return moduleRef.Name;
    }

    public override V_Variable GetField(string identifier)
    {
        if(!Interpreter.ReadVar(identifier,out var ret,(VContext)value)){
            throw new UnknownNameError(identifier);
        }
        return ret!;
    }
}

public class VType_Type : V_Variable
{
    public VType_Type(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        var values = Enum.GetNames(typeof(DataType));
        if (!values.Contains(v))
            throw new ArgumentError("Type must be one of " + string.Join(", ", values));
        return (DataType)values.ToList().IndexOf(v)!;
    }

    public override string ToString()
    {
        return value.ToString()!;
    }
}
public class VType_Indexer : V_Variable
{

    public VType_Indexer(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        return new IndexerRange();
    }

    public override string ToString()
    {
        return value.ToString()!;
    }
}
public struct IndexerRange(int start, int? end = null)
{
    public int start = start, end = end ?? start+1;
    public IndexerRange UseLength(int len)
    {
        var start_ = start;
        var end_ = end;
        if (start<0){
            start_ = len + start;
            if(!IsRange)
                end_ = start_+1;
                
        }
        if (end < 0)
            end_ = len + end;
        if(end_<start_){
            throw new ArgumentError("The start value has to be smaller or equal to the end value");
        }
        return new(start_, end_);
    }
    public override string ToString()
    {
        if(IsRange)
            return start+"->"+end;
        return start+"";
    }
    public readonly bool IsRange { private set { } get => start+1 != end; }
}

public class VType_Error : V_Variable
{
    public VType_Error(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        return new VortexError(v);
    }

    public override string ToString()
    {
        return ((VortexError)value).message;
    }
}
public class VType_Tag : V_Variable
{
    public VType_Tag(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        return new GroupType("tag", v);
    }

    public override string ToString()
    {
        return value.ToString()!;
    }
}
public struct GroupType(string groupName, object value)
{
    public string groupName = groupName;
    public object value = value;

    public override string ToString()
    {
        return value.ToString()!;
    }
}

public class VType_Function : V_Variable
{

    public VType_Function(DataType type, object value, V_VarFlags flags) : base(type, value, flags) { }
    public override object ConvertToCSharpType(string v)
    {
        throw new IlegalOperationError("Functions cannot be initialized this way");
    }

    public override string ToString()
    {
        return value.ToString()!;
    }
    override public VFunc GetCallableFunc(params object[] args){
        return (value as VFunc)!;
    }
}

public enum DataType
{
    String = 0,
    Number = 1,
    Bool = 2,
    Unset = 3,
    Any = 4,
    Int = 5,
    Array = 6,
    Module = 7,
    Type = 8,
    Indexer = 9,
    Error = 10,
    GroupType = 11,
    Function = 12,
    None = 13,
    Class = 14,
    Object = 15
}
