namespace VortexScript.Lexer.LexerStructs;

public struct CompiledStatement{
    public StatementId id;
    public CompiledToken[] tokens;

    public CompiledStatement(StatementId id,CompiledToken[] tokens){
        this.id = id;
        this.tokens = tokens;
    }
}

public struct CompiledToken{
    public string? leaf;
    public string[]? branch;
    public TokenType type;

    public CompiledToken(TokenType type, string value){
        this.type = type;
        leaf = value;
    }
    public CompiledToken(string[] values){
        branch = values;
        type = TokenType.Args;
    }
    public CompiledToken(string[] values,bool identifer){
        branch = values;
        type = TokenType.Identifier;
    }
}