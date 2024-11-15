using VortexScript.Definitions;
using VortexScript.Lexer.LexerStructs;
using VortexScript.Vortex;

namespace VortexScript.Lexer;

public class LexicalAnalyzer
{

    public static List<StatementType> statements;

    public static void Init()
    {
        statements = StatementType.Init();
    }

    public static  List<CompiledStatement> Tokenize(VFile file)
    {
        string[] lines = FileReader.ReadFile(file.Path);
        var ret = new List<CompiledStatement>();
        for (int i = 0; i < lines.Length; i++)
        {
            ret.Add(TokenizeStatement(lines[i]));
        }
        return ret;
    }

    public static CompiledStatement TokenizeStatement(string statement)
    {
        statement = statement.Replace("\t", " ");
        statement = statement.Replace("\r", "");
        statement = Utils.RemoveInlineComments(statement);
        statement = statement.Trim();
        if(statement=="")
            return new CompiledStatement(StatementId.PASS,[]);
        foreach (var item in statements)
        {
            if (item.CharStart == "")
            {
                //TODO: omptimize
                try{
                    return TokenizeThisStatement(item, statement);
                }
                catch{
                    continue;
                }
            }
            else
            {
                if (statement.StartsWith(item.CharStart))
                {
                    return TokenizeThisStatement(item, statement);
                }
                else
                    continue;
            }
        }
        throw new UnknownStatementError(statement);
    }

    public static CompiledStatement TokenizeThisStatement(StatementType type, string statement)
    {
        List<CompiledToken> ret = [];
        string current = statement;
        //iterate through each token in the statement
        foreach (var item in type.statementStruct)
        {
            if (!item.fixed_) current = current.TrimStart();
            int groupCount = 0;
            int lastPresentIndex = -1;
            int i = -1;
            //iterate through each token in the group
            foreach (var token in item.tokens)
            {
                i++;
                if (!item.fixed_) current = current.TrimStart();
                //if there is no more tokens in the group
                if (current == "")
                {
                    //and the token is mandatory-then throw an error
                    if (item.mandatory)
                    {
                        throw new ExpectedTokenError(token.TypeToString());
                    }
                    //else break and proceed to groupCount eval
                    else
                    {
                        break;
                    }
                }
                //find the end of the current token
                int end = GetTokenEnd(token.tokenType, current, token.value);
                //if end exists
                if (end != -1)
                {
                    lastPresentIndex = i;
                    //we found a token in the current group
                    groupCount++;
                    if (token.tokenType == LexerStructs.TokenType.Args)
                    {
                        ret.Add(new(Utils.StringSplit(current[1..(end - 1)], ',')));
                    }
                    else if (token.tokenType == LexerStructs.TokenType.Identifier)
                    {
                        ret.Add(new(Utils.StringSplit(current[0..end], '.'), true));
                    }
                    else
                    {
                        //add the token
                        ret.Add(new(token.tokenType, current[0..end]));
                    }
                    //remove the token
                    current = current[end..];
                }
            }
            //if mandatory
            if (item.mandatory)
            {
                //check if the group rules are followed
                if (item.groupType == GroupRule.GroupAll)
                {
                    if (groupCount != item.tokens.Count)
                    {
                        if (lastPresentIndex == -1)
                        {
                            throw new ExpectedTokenError(item.tokens[0].TypeToString());
                        }
                        var missingToken = item.tokens[lastPresentIndex];
                        throw new ExpectedTokenError(missingToken.TypeToString());
                    }

                }
                else if (item.groupType == GroupRule.GroupExactlyOne)
                {
                    if (groupCount != 1)
                    {
                        if (lastPresentIndex == -1)
                        {
                            throw new UnexpectedTokenError(item.tokens[0].TypeToString());
                        }
                        var missingToken = item.tokens[lastPresentIndex];
                        throw new UnexpectedTokenError(missingToken.TypeToString());
                    }
                }
                if (groupCount == 0)
                {
                    throw new ExpectedTokenError(item.tokens[0].TypeToString());
                }
            }
            else
            {
                //if it is not mandatory and nothing was found - proceed to the end, otherwise check group rules
                if (groupCount != 0)
                {
                    if (item.groupType == GroupRule.GroupAll)
                    {
                        if (groupCount != item.tokens.Count)
                        {
                            if (lastPresentIndex == -1)
                            {
                                throw new ExpectedTokenError(item.tokens[0].TypeToString());
                            }
                            var missingToken = item.tokens[lastPresentIndex];
                            throw new ExpectedTokenError(missingToken.TypeToString());
                        }

                    }
                    else if (item.groupType == GroupRule.GroupExactlyOne)
                    {
                        if (groupCount != 1)
                        {
                            if (lastPresentIndex == -1)
                            {
                                throw new UnexpectedTokenError(item.tokens[0].TypeToString());
                            }
                            var missingToken = item.tokens[lastPresentIndex];
                            throw new UnexpectedTokenError(missingToken.TypeToString());
                        }
                    }
                }
            }
        }
        //if there is still something in statement we are not expecting it
        if (current != "") throw new UnexpectedTokenError(current);
        return new(type.Id,[.. ret]);
    }

    public static int GetTokenEnd(LexerStructs.TokenType type, string value, string expectedSyntax = "")
    {
        int i = -1;
        string build = "";
        bool satisfied = false;
        bool wasDot = false;
        bool inBrackedExpression = false;
        bool inString = false;
        int lastDot = 0;
        while (i < value.Length - 1)
        {
            i++;
            build += value[i];
            switch (type)
            {
                case LexerStructs.TokenType.Identifier:
                    if (!satisfied && Utils.IsIdentifierValid(build)) satisfied = true;
                    if (build[^1] == '.')
                    {
                        if (wasDot) return -1; //THROW ERROR
                        wasDot = true;
                        lastDot = i + 1;
                    }
                    else wasDot = false;
                    if (!Utils.IsIdentifierValid(build[lastDot..]) && build[^1] != '.') goto ret;
                    break;
                case LexerStructs.TokenType.DecleareIdentifier:
                    if (!satisfied && Utils.IsIdentifierValid(build)) satisfied = true;
                    if (!Utils.IsIdentifierValid(build)) goto ret; //TODO: check if not already used
                    break;
                case LexerStructs.TokenType.Expression:
                    satisfied = true;
                    if (build[^1] == '\"'){
                        if(inString) goto ret;
                        else inString = true;
                    }
                    if (!inBrackedExpression && build == "(") inBrackedExpression = true;
                    if (inBrackedExpression && build[^1] == ')') goto ret;
                    if(!inBrackedExpression && build[^1]==' ' && !inString) goto ret;
                    break;
                case LexerStructs.TokenType.StartScope:
                    if (build == ":") { satisfied = true; i++;goto ret; }
                    break;
                case LexerStructs.TokenType.EndScope:
                    if (build == ";") { satisfied = true;i++; goto ret; }
                    break;
                case LexerStructs.TokenType.Syntax:
                    if (!expectedSyntax.StartsWith(build))
                    {
                        if (expectedSyntax == build[..^1]) satisfied = true;
                        goto ret;
                    }
                    break;
                case LexerStructs.TokenType.Args:
                    if (value[0] != '(') return -1;
                    return Utils.StringGetMatchingPer(value) + 1;
            }
        }
        if (type == LexerStructs.TokenType.Syntax)
        {
            if (expectedSyntax == build) satisfied = true;
        }
        if (type != LexerStructs.TokenType.Expression)
            i++;
        ret:
        if (type == LexerStructs.TokenType.Expression) return i + 1;
        if (!satisfied) return -1;
        return i;
    }


    public static bool StatementContainsSyntax(CompiledStatement statement,string syntax){
        foreach (var item in statement.tokens)
        {
            if(item.type == LexerStructs.TokenType.Syntax&& item.leaf == syntax) return true;
        }
        return false;
    }
    public static string StatementGetIdentifier(CompiledStatement statement){
        return StatementGetFirst(statement, LexerStructs.TokenType.Identifier);
    }
    public static string StatementGetExpression(CompiledStatement statement){
        return StatementGetFirst(statement, LexerStructs.TokenType.Expression);
    }
    public static string StatementGetFirst(CompiledStatement statement, LexerStructs.TokenType type){
        foreach (var item in statement.tokens)
        {
            if(item.type == type) return item.leaf!;
        }
        throw new Exception("Lexical analysis error: "+type.ToString()+" not found in statement");
    }
    
    public static StatementType GetStatementType(StatementId id){//TODO: make it a dict
        foreach(StatementType statementType in statements){
            if(statementType.Id == id) return statementType;
        }
        return null!;
    }
}