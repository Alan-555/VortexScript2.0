using VortexScript.Vortex;

namespace VortexScript.Lexer.Structs;

public struct Statement
{

}

public class StatementType(string StartsWith, bool StartsNewScope, ScopeTypeEnum scopeType = ScopeTypeEnum.topLevel, bool endsScope = false)
{
    public string StartsWith { get; private set; } = StartsWith; //if empty, this statement cannot be determined simply by keywords (e.g. declare function, assigment)
    public bool StartsNewScope { get; private set; } = StartsNewScope;
    public ScopeTypeEnum ScopeType { get; private set; } = scopeType;
    public bool EndsScope { get; private set; } = endsScope;

    public List<TokenGroup?> statementStruct = new();

    bool groupOpen=false;
    public StatementType StartGroup(GroupRule multiple = GroupRule.GroupAny,bool fixed_ = false, bool mandatory = true){
        groupOpen = true;
        statementStruct.Add(new TokenGroup([],multiple,fixed_,mandatory));
        return this;
    }
    public StatementType EndGroup(){
        groupOpen = false;
        statementStruct.Add(null);
        return this;
    }
    
    public StatementType Expect(string val,bool mandatory = true){
        Add(new(TokenType.Syntax,val),GroupRule.GroupAny,false,mandatory);
        return this;
    }
    public StatementType ExpectAfter(string val,bool mandatory = true){
        Add(new(TokenType.Syntax,val),GroupRule.GroupAny,true,mandatory);
        return this;
    }
    public StatementType Expect(TokenType type,bool mandatory = true){
        Add(new(type,""),GroupRule.GroupAny,false,mandatory);
        return this;
    }
    public StatementType ExpectAfter(TokenType type,bool mandatory = true){
        Add(new(type,""),GroupRule.GroupAny,true,mandatory);
        return this;
    }

    void Add(Token token,GroupRule multiple = GroupRule.GroupAny,bool fixed_ = false, bool mandatory = true){
        if(groupOpen){
            statementStruct.FindLast(x=>x!=null)!.tokens.Add(token);
        }
        else{
            statementStruct.Add(new TokenGroup(new List<Token>(){token},multiple,fixed_,mandatory));
        }
    }
    


    public static StatementType DeclareStatement;

    public static void Init()
    {

        DeclareStatement = new("$", false);
        DeclareStatement.Expect("$")
                        .StartGroup(GroupRule.GroupAny,true,false)
                        .Expect("?")
                        .Expect("!")
                        .EndGroup()
                        .ExpectAfter(TokenType.DecleareIdentifier)
                        .StartGroup(GroupRule.GroupAll,false,false)
                        .Expect("=")
                        .Expect(TokenType.Expression);
    }



}
public class TokenGroup
{
    public List<Token> tokens = new();
    public GroupRule groupType = GroupRule.GroupAny; 
    public bool fixed_ = false; //expect right after. No white space is allowed when true
    public bool mandatory = true; //if true- the group may be ommited

    public TokenGroup(List<Token> tokens,GroupRule groupType = GroupRule.GroupAny,bool fixed_ = false, bool mandatory = true){
        this.tokens = tokens;
        this.groupType = groupType;
        this.fixed_ = fixed_;
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
    DecleareIdentifier,
    Expression,
    StartScope,
    EndScope,
    Syntax,
    Args,
    Group
}
public enum GroupRule{
    GroupAny = 0,
    GroupAll = 1,
    GroupExactlyOne = 2
}