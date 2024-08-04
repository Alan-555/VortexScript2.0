using VortexScript.Vortex;

namespace VortexScript.Structs;

public struct Token
{
    public TokenType type;
    public string value = "";

    public object? actualValue;
    public bool isDot = false;

    public Token(TokenType type, string value, object? actualValue = null)
    {
        this.type = type;
        this.value = value;
        this.actualValue = actualValue;
        if (type == TokenType.String)
            this.actualValue = value;
    }

    public readonly object GetVal()
    {
        if (actualValue == null)
            return V_Variable.Construct(Evaluator.TokenTypeToDataType(type), value).value;
        return actualValue;
    }
    public readonly V_Variable GetValVar()
    {
        if (actualValue != null)
            return V_Variable.Construct(Evaluator.TokenTypeToDataType(type), actualValue);
        return V_Variable.Construct(Evaluator.TokenTypeToDataType(type), value);
    }
}
public struct Operator
{
    public string Oper { get; }
    public Type LeftType { get; }
    public Type RightType { get; }
    public Func<dynamic, dynamic, dynamic> Func { get; }
    public int Priority { get; }
    public DataType ResultType { get; }
    public bool Unary { get; }
    public bool IsBeofre { get; }

    public Operator(string oper, DataType leftType, DataType rightType, Func<dynamic, dynamic, dynamic> func, int priority, DataType resultType = DataType.String, bool unary = false)
    {
        Oper = oper;
        LeftType = Evaluator.CSharpDataRepresentations[leftType];
        RightType = Evaluator.CSharpDataRepresentations[rightType];
        Func = func;
        Priority = priority;
        ResultType = resultType;
        Unary = unary;
        IsBeofre = leftType == DataType.None;
    }

}
public struct ExpressionScope(int from, int to)
{
    public int From { get; } = from;
    public int To { get; } = to;
}
public struct OperatorTask(int priority, Operator oper)
{
    public int Priority { get; } = priority;
    public Operator Oper { get; } = oper;
}