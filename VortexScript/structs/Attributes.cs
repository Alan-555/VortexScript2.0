using VortexScript.Vortex;

namespace VortexScript.Structs;

[AttributeUsage(AttributeTargets.Method)]
public class MarkStatement : System.Attribute
{
    public string StartsWith { get; private set; }
    public bool StartsNewScope { get; private set; }
    public ScopeTypeEnum ScopeType{ get; private set; }
    public bool EndsScope { get; private set; }
    public MarkStatement(string StartsWith, bool StartsNewScope, ScopeTypeEnum scopeType = ScopeTypeEnum.topLevel,bool endsScope = false)
    {
        this.StartsWith = StartsWith;
        this.StartsNewScope = StartsNewScope;
        ScopeType = scopeType;
        EndsScope = endsScope;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class InternalFunc : Attribute
{
    public DataType ReturnType { get; set; }

    public InternalFunc(DataType returnType)
    {
        ReturnType = returnType;
    }

}

[AttributeUsage(AttributeTargets.Method)]
public class OperatorDefinition : Attribute
{
    public TokenType left { get; set; }
    public TokenType right { get; set; }
    public string syntax {get; set;}
    public int precedence { get; set; }
    public DataType returnType { get; set; }

    public OperatorDefinition(TokenType left, TokenType right, string syntax, int precedence,DataType dt=DataType.None)
    {
        this.left = left;
        this.right = right;
        this.syntax = syntax;
        this.precedence = precedence;
        this.returnType = dt;
    }
}