using VortexScript.Vortex;

namespace VortexScript.Lexer.Structs;

public struct Statement
{

}

public class StatementType(string StartsWith, bool StartsNewScope, ScopeTypeEnum scopeType = ScopeTypeEnum.topLevel, bool endsScope = false)
{
    public string StartsWith { get; private set; } = StartsWith;
    public bool StartsNewScope { get; private set; } = StartsNewScope;
    public ScopeTypeEnum ScopeType { get; private set; } = scopeType;
    public bool EndsScope { get; private set; } = endsScope;

    public List<TokenGroup?> statementStruct = new();

    bool groupOpen=false;
    public StatementType StartGroup(bool and = false){
        groupOpen = true;
        statementStruct.Add(new TokenGroup([],and));
        return this;
    }
    public StatementType EndGroup(){
        groupOpen = false;
        statementStruct.Add(null);
        return this;
    }

    public StatementType Expect(TokenType type, bool mandatory = true)
    {
        Add(new(type,""));
        return this;
    }
    public StatementType Expect(string syntax, bool mandatory = true)
    {
        Add(new(TokenType.Syntax,syntax,mandatory));
        return this;
    }
    public StatementType ExpectRightAfter(TokenType type, bool mandatory = true)
    {
        Add(new(type,"",mandatory,true));
        return this;
    }
    public StatementType ExpectRightAfter(string syntax, bool mandatory = true)
    {
        Add(new(TokenType.Syntax,syntax,mandatory));
        return this;
    }
    
    void Add(Token token,bool fixed_ = false,bool mandatory = true){
        if(groupOpen){
            statementStruct.FindLast(x=>x!=null)!.tokens.Add(token);
        }
        else{
            statementStruct.Add(new TokenGroup(new List<Token>(){token}));
        }
    }


    public static StatementType DeclareStatement;

    public static void Init()
    {

        DeclareStatement = new("$", false);
        DeclareStatement.Expect("$").StartGroup().ExpectRightAfter("").ExpectRightAfter();
    }



}
public class TokenGroup
{
    public List<Token> tokens = new();
    public bool and = false;
    public bool fixed_ = true;
    
    public bool mandatory = false;

    public TokenGroup(List<Token> tokens,bool and = false,bool mandatory = true){
        this.tokens = tokens;
        this.and = and;
        this.mandatory = mandatory;
    }

}

public class Token
{
    public TokenType tokenType = new();
    public string value = "";

    public Token(TokenType type, string value)
    {
        tokenType = type;
        this.value = value;
    }

}

public enum TokenType
{
    Identifier,
    Expression,
    StartScope,
    EndScope,
    Syntax,
    Args,
    Group
}