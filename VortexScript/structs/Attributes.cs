using VortexScript.Vortex;
using VortexScript.Lexer.LexerStructs;
namespace VortexScript.Structs;

[AttributeUsage(AttributeTargets.Method)]
public class MarkStatement : System.Attribute
{
    public StatementId StatementId;
    public MarkStatement(StatementId id)
    {
        StatementId = id;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class InternalFunc : Attribute
{
    public DataType ReturnType { get; set; }
    public bool ForceUppercase { get; set; } = false;

    public InternalFunc(DataType returnType, bool forceUppercase = false)
    {
        ReturnType = returnType;
        ForceUppercase = forceUppercase;
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