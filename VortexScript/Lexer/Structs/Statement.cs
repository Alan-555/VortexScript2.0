using VortexScript.Vortex;

namespace VortexScript.Lexer.Structs;

public struct Statement
{

}

public class StatementType(StatementId id, string StartsWith, bool StartsNewScope, ScopeTypeEnum[]? validIn = null, ScopeTypeEnum scopeType = ScopeTypeEnum.topLevel, bool endsScope = false,bool invalidMode = false)
{
    public StatementId Id { get; private set; } = id; 
    public string CharStart { get; private set; } = StartsWith; //if empty, this statement cannot be determined simply by keywords (e.g. declare function, assigment)
    public bool StartsNewScope { get; private set; } = StartsNewScope;
    public ScopeTypeEnum ScopeType { get; private set; } = scopeType;
    public bool EndsScope { get; private set; } = endsScope;
    public ScopeTypeEnum[] ValidIn {get; private set;} = validIn ?? [];
    public bool InvalidInMode { get; private set; } = invalidMode;

    public List<TokenGroup> statementStruct = new();

    bool groupOpen = false;
    public StatementType StartGroup(GroupRule multiple = GroupRule.GroupAny, bool fixed_ = false, bool mandatory = true)
    {
        groupOpen = true;
        statementStruct.Add(new TokenGroup([], multiple, fixed_, mandatory));
        return this;
    }
    public StatementType EndGroup()
    {
        groupOpen = false;
        return this;
    }

    public StatementType Expect(string val, bool mandatory = true)
    {
        Add(new(TokenType.Syntax, val), GroupRule.GroupAny, false, mandatory);
        return this;
    }
    public StatementType ExpectAfter(string val, bool mandatory = true)
    {
        Add(new(TokenType.Syntax, val), GroupRule.GroupAny, true, mandatory);
        return this;
    }
    public StatementType Expect(TokenType type, bool mandatory = true)
    {
        Add(new(type, ""), GroupRule.GroupAny, false, mandatory);
        return this;
    }
    public StatementType ExpectAfter(TokenType type, bool mandatory = true)
    {
        Add(new(type, ""), GroupRule.GroupAny, true, mandatory);
        return this;
    }

    void Add(Token token, GroupRule multiple = GroupRule.GroupAny, bool fixed_ = false, bool mandatory = true)
    {
        if (groupOpen)
        {
            statementStruct.FindLast(x => x != null)!.tokens.Add(token);
        }
        else
        {
            statementStruct.Add(new TokenGroup(new List<Token>() { token }, multiple, fixed_, mandatory));
        }
    }

    public static StatementType DeclareStatement;

    public static List<StatementType> Init()
    {
        var list = new List<StatementType>
        {
            //declare
            new StatementType(StatementId.Declare, "$", false)
            .Expect("$")
            .StartGroup(GroupRule.GroupAny, true, false)
            .Expect("?")
            .Expect("!")
            .EndGroup()
            .ExpectAfter(TokenType.DecleareIdentifier)
            .StartGroup(GroupRule.GroupAll, false, false)
            .Expect("=")
            .Expect(TokenType.Expression)
            .EndGroup(),

            //call function
            new StatementType(StatementId.Call, "", false)
            .Expect(TokenType.Identifier)
            .ExpectAfter(TokenType.Args),

            //declare function
            new StatementType(StatementId.DeclareFunction, "", false)
            .Expect(TokenType.DecleareIdentifier)
            .ExpectAfter(TokenType.Args)
            .Expect(TokenType.StartScope),

            //assignment
            new StatementType(StatementId.Assignment, "", false)
            .Expect(TokenType.Identifier)
            .Expect("=")
            .Expect(TokenType.Expression),

            //output
            new StatementType(StatementId.Output, ">", false)
            .Expect(">")
            .Expect(TokenType.Expression),

            //return
            new StatementType(StatementId.Return, "<", false, [ScopeTypeEnum.functionScope])
            .Expect("<")
            .Expect(TokenType.Expression),

            //if
            new StatementType(StatementId.If, "if", true, [], ScopeTypeEnum.ifScope)
            .Expect("if")
            .Expect(TokenType.Expression)
            .Expect(TokenType.StartScope),

            //else
            new StatementType(StatementId.Else, "else", true, [ScopeTypeEnum.ifScope], ScopeTypeEnum.elseScope, true)
            .Expect("else")
            .Expect(TokenType.Expression)
            .Expect(TokenType.StartScope),

            //else if
            new StatementType(StatementId.ElseIf, "else if", true, [ScopeTypeEnum.ifScope], ScopeTypeEnum.ifScope, true)
            .Expect("else if")
            .Expect(TokenType.Expression)
            .Expect(TokenType.StartScope),

            //end scope
            new StatementType(StatementId.EndScope, ";", false, [ScopeTypeEnum.topLevel], endsScope: true, invalidMode: true)
            .Expect(";"),

            //start scope
            new StatementType(StatementId.StartScope, ":", false, [], ScopeTypeEnum.genericScope)
            .Expect(":"),

            //exit
            new StatementType(StatementId.Exit, "exit", false)
            .Expect("exit"),

            //acquire
            new StatementType(StatementId.Acquire, "acquire", false, [ScopeTypeEnum.topLevel])
            .Expect("acquire")
            .Expect(TokenType.Identifier),

            //acquires
            new StatementType(StatementId.Acquires, "acquires", false, [ScopeTypeEnum.topLevel])
            .Expect("acquires")
            .Expect(TokenType.Identifier),

            //release
            new StatementType(StatementId.Release, "release", false)
            .Expect("release")
            .Expect(TokenType.Expression),

            //set directive
            new StatementType(StatementId.SetDirective, "#", false)
            .Expect("#")
            .ExpectAfter(TokenType.Identifier)
            .Expect(TokenType.Expression),

            //raise
            new StatementType(StatementId.Raise, "raise", false)
            .Expect("raise")
            .Expect(TokenType.Expression)
            .Expect(TokenType.Expression, false),

            //assert
            new StatementType(StatementId.Assert, "assert", false)
            .Expect("assert")
            .Expect(TokenType.Expression)
            .Expect(TokenType.Expression, false),

            //class
            new StatementType(StatementId.Class, "class", true, [ScopeTypeEnum.classScope], ScopeTypeEnum.classScope)
            .Expect("class")
            .Expect(TokenType.DecleareIdentifier)
            .Expect(TokenType.StartScope),

            //while
            new StatementType(StatementId.While, "while", true, [], ScopeTypeEnum.loopScope)
            .Expect("while")
            .Expect(TokenType.Expression)
            .Expect(TokenType.StartScope),

            //break
            new StatementType(StatementId.Break, "break", false, [ScopeTypeEnum.loopScope])
            .Expect("break"),


        };


        return list;
    }



}


public enum StatementId{
    Declare,
    Call,
    DeclareFunction,
    Assignment,
    Output,
    Return,
    If,
    Else,
    ElseIf,
    EndScope,
    StartScope,
    Exit,
    Acquire,
    Acquires,
    Release,
    SetDirective,
    Raise,
    Assert,
    Class,
    While,
    Break,
}

public class TokenGroup
{
    public List<Token> tokens = new();
    public GroupRule groupType = GroupRule.GroupAny;
    public bool fixed_ = false; //expect right after. No white space is allowed when true
    public bool mandatory = true; //if true- the group may be ommited

    public TokenGroup(List<Token> tokens, GroupRule groupType = GroupRule.GroupAny, bool fixed_ = false, bool mandatory = true)
    {
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

    public string TypeToString(){
        switch (tokenType)
        {
            case TokenType.Identifier:
                return "identifier";
            case TokenType.DecleareIdentifier:
                return "decleration Identifier";
            case TokenType.Expression:
                return "expression";
            case TokenType.StartScope:
                return "':'";
            case TokenType.EndScope:
                return "';'";
            case TokenType.Syntax:
                return "'"+value+"'";
            case TokenType.Args:
                return "arguments";
        }
        return "unknown";
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
}
public enum GroupRule
{
    GroupAny = 0,
    GroupAll = 1,
    GroupExactlyOne = 2
}