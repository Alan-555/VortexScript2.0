using System.Globalization;
using System.Text;
using Vortex;
namespace Vorteval
{
    public class Evaluator
    {
        public static bool InitDone = false;
        public static int HighestPrecedence = -1;

        public static void Init()
        {
            if (InitDone) return;
            InitDone = true;

            foreach (var oper in OperatorFunctions)
            {
                if (!Operators.Contains(oper.Value.Oper))
                {
                    Operators = [.. Operators, oper.Value.Oper];
                }
                if (oper.Value.Priority > HighestPrecedence)
                    HighestPrecedence = oper.Value.Priority;
            }
        }


        public Evaluator()
        {
            Init();
        }

        const string identifierValidChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_🌀🌋";


        public static readonly Dictionary<DataType, Type> CSharpDataRepresentations = new(){
            {DataType.String,typeof(string)},
            {DataType.Number,typeof(double)},
            {DataType.Bool,typeof(bool)},
            {DataType.None,typeof(object)},
            {DataType.Any,typeof(object)},
            {DataType.Int,typeof(int)},
            {DataType.Array,typeof(Array)},
            {DataType.Module,typeof(object)},

        };
        public static readonly Dictionary<DataType, TokenType> DataTypeToToken = new(){
            {DataType.String,TokenType.String},
            {DataType.Number,TokenType.Number},
            {DataType.Bool,TokenType.Bool},
            {DataType.Unset,TokenType.Unset},
             {DataType.None,TokenType.None},
             {DataType.Any,TokenType.Any},
             {DataType.NaN,TokenType.NaN},
             {DataType.Array,TokenType.String},
             {DataType.Module,TokenType.Module},
             {DataType.Type,TokenType.Type},

        };
        public static readonly Dictionary<TokenType, DataType> TokenToDataType = new(){
            {TokenType.String,DataType.String},
            {TokenType.Number,DataType.Number},
            {TokenType.Bool,DataType.Bool},
            {TokenType.Unset,DataType.Unset},
            {TokenType.Any,DataType.Any},
            {TokenType.NaN,DataType.NaN},
            {TokenType.Array,DataType.Array},
            {TokenType.None,DataType.None},
            {TokenType.Module,DataType.Module},
            {TokenType.Type,DataType.Type},

        };

        public static readonly OperatorsDictiononary OperatorFunctions = new()
        {
            //unary
            {"a§_", new("§",DataType.Any,DataType.None,(a,b)=> -b,0,DataType.Bool,true)},
            {"_!b", new("!",DataType.None,DataType.Bool,(a,b)=> !b,0,DataType.Bool,true)},
            {"_-n", new("-",DataType.None,DataType.Number,(a,b)=> -b,0,DataType.Number,true)},
            {"s~_", new("~",DataType.String,DataType.None,(a,b)=> {if(int.TryParse(a,out int res)) return res; else throw new InvalidFormatError(a,"a number");},0,DataType.Number,true)},
            {"b~_", new("~",DataType.Bool,DataType.None,(a,b)=> a ? 1:0,0,DataType.Number,true)},
            {"n~_", new("~",DataType.Number,DataType.None,(a,b)=>a,0,DataType.Number,true)},
            {"a??_", new("??",DataType.Any,DataType.None,(a,b)=> -b,0,DataType.Bool,true)},
            //multiplicative
            { "n*n", new("*",DataType.Number, DataType.Number, (a, b) => a * b, 1,DataType.Number) },
            { "n/n", new("/",DataType.Number, DataType.Number, (a, b) => a / b, 1,DataType.Number) },
            { "n%n", new("%",DataType.Number, DataType.Number, (a, b) => a % b, 1,DataType.Number) },
            { "n°n", new("°",DataType.Number, DataType.Number, (a, b) => Math.Pow(a, b), 1,DataType.Number) },

            //addative
            { "n+n", new("+",DataType.Number, DataType.Number, (a, b) => a + b, 2,DataType.Number) },
            { "n-n", new("-",DataType.Number, DataType.Number, (a, b) => a - b, 2,DataType.Number) },
            { "n+s", new("+",DataType.Number, DataType.String, (a, b) => a.ToString() + b, 2) },
            { "s+n", new("+",DataType.Number, DataType.String, (a, b) => a + b.ToString(), 2) },
            { "s+s", new("+",DataType.String, DataType.String, (a, b) => a + b, 2) },

            //bitshift
            { "n<<n", new("<<",DataType.Number, DataType.Number, (a, b) => (int)a << (int)b, 3,DataType.Number) },
            { "n>>n", new(">>",DataType.Number, DataType.Number, (a, b) => (int)a >> (int)b, 3,DataType.Number) },


            //relative
            {"n>n", new(">",DataType.Number, DataType.Number, (a,b) => a > b, 4,DataType.Bool) },
            {"n<n", new("<",DataType.Number, DataType.Number, (a,b) => a < b, 4,DataType.Bool) },
            {"n>=n", new(">=",DataType.Number, DataType.Number, (a,b) => a >= b, 4,DataType.Bool) },
            {"n<=n", new("<=",DataType.Number, DataType.Number, (a,b) => a <= b, 4,DataType.Bool) },


            //compparative
            {"n==n", new("==",DataType.Number, DataType.Number, (a,b) => a == b, 5,DataType.Bool) },
            {"s==s", new("==",DataType.String, DataType.Number, (a,b) => a == b, 5,DataType.Bool) },
            {"n==s", new("==",DataType.Number, DataType.String, (a,b) => a.ToString() == b, 5,DataType.Bool) },
            {"s==n", new("==",DataType.String, DataType.Number, (a,b) => a == b.ToString(), 5,DataType.Bool) },
            {"b==b", new("==",DataType.Bool, DataType.Number, (a,b) => a.ToString() == b.ToString(), 5,DataType.Bool) },
            {"a==a", new("==",DataType.Any, DataType.Any, (a,b) => a.ToString() == b.ToString(), 5,DataType.Bool) },

            //bitwise
            { "n&n", new("&",DataType.Number, DataType.Number, (a, b) => a & b, 6,DataType.Number) },
            { "n|n", new("|",DataType.Number, DataType.Number, (a, b) => a | b, 6,DataType.Number) },
            { "n^n", new("^",DataType.Number, DataType.Number, (a, b) => a ^ b, 6,DataType.Number) },
            

            //logical
            {"b&&b", new("&&",DataType.Bool, DataType.Bool, (a,b) => a && b, 7,DataType.Bool) },
            {"b||b", new("||",DataType.Bool, DataType.Bool, (a,b) => a || b, 7,DataType.Bool) },
            //ternary TODO:


        };
        public static string[] Operators = [];

        public string originalExpression;

        public V_Variable Evaluate(string expression)
        {
            originalExpression = expression;
            if (expression == "")
            {
                throw new ExpressionEvalError(this, "Empty expression");
            }
            var tokens = Tokenize(expression);
            return RecursiveEval(tokens);
        }
        V_Variable RecursiveEval(List<Token> tokens)
        {
            //proccess scopes
            if (tokens.Any(x => x.type == TokenType.Scope))
            {
                var scopes = Scopify(tokens);
                int dist = 0;
                foreach (var scope in scopes)
                {
                    int start = scope.From - dist;
                    int end = scope.To - dist;
                    var expression = tokens[(start + 1)..end];
                    if(expression.Count==0)
                        throw new ExpressionEvalError(new(){originalExpression=""},"Empty expression");
                    var result = RecursiveEval(expression);
                    tokens.RemoveRange(start, end - start + 1);
                    tokens.Insert(start, new(DataTypeToToken[result.type], result.ToString()));
                    dist = end - start;
                }
            }
            var scopeTest = tokens.Find(x => x.type == TokenType.Scope);
            if (!scopeTest.Equals(default(Token))){
                if(scopeTest.value=="(")
                    throw new ExpectedTokenError(")");
                else
                    throw new UnexpectedTokenError(")");

            }
            //proccess variables
            tokens = ProccessVariables(tokens);
            tokens.RemoveAll(x => x.type == TokenType.Ignore);
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.type != TokenType.Indexer)
                    continue;
                var prevToken = tokens[i - 1];
                //TODO: finish
            }
            //process operands and operators
            for (int i = 0; i < HighestPrecedence + 1; i++)
            {
                while (true)
                {
                    bool modified = ProccessOperators(tokens, i, out var newTokens);
                    if (modified)
                    {
                        tokens = newTokens;
                    }
                    else
                        break;

                }
            }
            if (tokens.Count != 1)
            {
                throw new ExpressionEvalError(this, "Too many tokens (" + TokensToExpression(tokens,true) + ") apeared after proccessing. Are you missing an operator or an operand?");
            }
            var dataType = TokenToDataType[tokens[0].type];
            var value = tokens[0].value;
            var vVar = V_Variable.Construct(dataType, value, new() { readonly_ = true });
            return vVar;
        }
        public List<Token> ProccessVariables(List<Token> tokens)
        {
            VContext? module = null;
            string moduleName = "";
            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].type == TokenType.Variable)
                {
                    bool good = Interpreter.ReadVar(tokens[i].value, out var variable, module, module != null);
                    if(module!=null)
                        tokens[i-1] = new(TokenType.Ignore, "");
                    if (!good)
                    {
                        throw new UnknownNameError(tokens[i].value);
                    }
                    //TODO: look for more indexers into the future (x[0][0])
                    if(i!=tokens.Count-1&&tokens[i+1].type == TokenType.Indexer){
                        var index = ExpressionEval.Evaluate(tokens[i+1].value,DataType.Number);
                        variable = variable.Index(Convert.ToInt32(index.value)); 
                        tokens[i+1] = new(TokenType.Ignore,"");
                    }
                    tokens[i] = new(DataTypeToToken[variable.type], variable.ToString());
                }
                else if (tokens[i].type == TokenType.Console_in)
                {
                    if (module != null)
                    {
                        throw new ExpressionEvalError(this, $"The module '{moduleName}' doe not contain a definition for ':'");
                    }
                    var in_ = Console.ReadLine();
                    if (in_ == "")
                        tokens[i] = new(TokenType.Unset, "");
                    else
                        tokens[i] = new(TokenType.String, in_);

                }
                else if (tokens[i].type == TokenType.Function)
                {
                    Interpreter.CallFunctionStatement(tokens[i].value, out var res, module);
                    if(module!=null)
                        tokens[i-1] = new(TokenType.Ignore, "");
                    if (res == null)
                    {
                        tokens[i] = new(TokenType.Unset, "unset");
                    }
                    else
                    {
                        tokens[i] = new(DataTypeToToken[res.type], res.value.ToString());
                    }
                    module = null;
                }
                else if (tokens[i].type == TokenType.Module)
                {

                    if (Interpreter.TryGetModule(tokens[i].value, out module))
                    {
                        moduleName = tokens[i].value;
                    }
                    else
                    {
                        throw new UnknownNameError(tokens[i].value);
                    }

                }
            }

            return tokens;

        }
        public bool ProccessOperators(List<Token> tokens, int priority, out List<Token> tokensOut)
        {
            int index = 0;
            bool modified = false;
            bool found = false;
            Operator oper = new();
            int leftI = 0, rightI = 0;
            foreach (var token in tokens)
            {
                if (token.type == TokenType.Operator)
                {
                    found = true;
                    leftI = index - 1;
                    rightI = index + 1;
                    char left = '_', right = '_';
                    try
                    {
                        left = tokens[leftI].type.ToString().ToLower()[0];
                        if (left == 'o')
                            left = '_';
                    }
                    catch { }
                    try
                    {
                        right = tokens[rightI].type.ToString().ToLower()[0];
                        if (right == 'o')
                            right = '_';
                    }
                    catch { }
                    bool good = false;
                    good = OperatorFunctions.TryGet(left + token.value + right, out oper);

                    if (!good)
                    {
                        OperatorFunctions.TryGetAny(token.value, out oper);
                        if (oper.Priority != priority)
                        {
                            index++;
                            continue;
                        }
                        if (left != '_' && right != '_')
                            throw new ExpressionEvalError(this, "Unknown operator: " + tokens[leftI].type.ToString() + token.value + tokens[rightI].type.ToString());
                        else if (left != '_')
                            throw new ExpressionEvalError(this, "Unknown operator: " + tokens[leftI].type.ToString() + token.value);
                        else if (right != '_')
                            throw new ExpressionEvalError(this, "Unknown operator: " + token.value + tokens[rightI].type.ToString());
                        else
                            throw new ExpressionEvalError(this, "No operands found for operator: " + token.value);


                    }
                    if (oper.Priority != priority)
                    {
                        index++;
                        continue;
                    }
                    if (oper.Unary)
                    {
                        if (oper.IsBeofre)
                        {
                            if (right == '_')
                            {
                                throw new ExpressionEvalError(this, "Expected an operand after unary operator '" + oper.Oper + "'");
                            }
                        }
                        else
                        {
                            if (left == '_')
                            {
                                throw new ExpressionEvalError(this, "Expected an operand before unary operator '" + oper.Oper + "'");
                            }
                        }
                    }
                    modified = true;
                    break;

                }

                index++;
            }
            if (!found)
            {
                tokensOut = tokens;
                return false;
            }
            if (modified)
            {
                if (oper.Unary)
                {
                    if (oper.Oper == "??")
                    {
                        var val1 = tokens[leftI];
                        var result = val1.type != TokenType.Unset;
                        tokens.RemoveRange(leftI, 2);
                        tokens.Insert(leftI, new(TokenType.Bool, result.ToString()));
                    }
                    else
                    if (oper.Oper == "§")
                    {
                        var val1 = tokens[leftI];
                        var result = val1.type != TokenType.Unset;
                        tokens.RemoveRange(leftI, 2);
                        if (result)
                            tokens.Insert(leftI, val1);
                        else
                            tokens.Insert(leftI, new(TokenType.String, ""));

                    }
                    else
                    if (oper.IsBeofre)
                    {
                        var val2 = TokenToPrimitive(tokens[rightI]);
                        var result = oper.Func(0, val2);
                        tokens.RemoveRange(leftI + 1, 2);
                        tokens.Insert(leftI + 1, new(DataTypeToToken[oper.ResultType], result.ToString()));
                    }
                    else
                    {
                        var val1 = TokenToPrimitive(tokens[leftI]);
                        var result = oper.Func(val1, 0);
                        tokens.RemoveRange(leftI, 2);
                        tokens.Insert(leftI, new(DataTypeToToken[oper.ResultType], result.ToString()));
                    }
                }
                else
                {
                    var val1 = TokenToPrimitive(tokens[leftI]);
                    var val2 = TokenToPrimitive(tokens[rightI]);
                    var result = oper.Func(val1, val2);
                    tokens.RemoveRange(leftI, rightI - leftI + 1);
                    tokens.Insert(leftI, new(DataTypeToToken[oper.ResultType], result.ToString()));
                }
            }


            tokensOut = tokens;
            return modified;
        }

        public object TokenToPrimitive(Token token)
        {
            return V_Variable.Construct(TokenToDataType[token.type],token.value).ConvertToCSharpType(token.value);
            switch (token.type)
            {
                case TokenType.Number:
                    return V_Variable.Construct(TokenToDataType[token.type],token.value).ConvertToCSharpType(token.value);
                case TokenType.Array:
                    return V_Variable.Construct(TokenToDataType[token.type],token.value).ConvertToCSharpType(token.value);
                case TokenType.String:
                    return token.value;
                case TokenType.Bool:
                    return token.value == "true";
                case TokenType.Unset:
                    return null;
                default:
                    throw new ArgumentException("Token must be a value");
            }
        }

        public string TokensToExpression(List<Token> tokens,bool seperate =false)
        {
            string expression = "";
            foreach (Token token in tokens)
            {
                expression += token.value;
                if(seperate)
                    expression+=";";
            }
            expression = expression[..^1];
            return expression;
        }

        public List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            var currentToken = new StringBuilder();
            bool inString = false;
            var operatorTest = "";
            bool readingVar = false;

            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression[i];

                if (inString)
                {
                    if (c == '"')
                    {
                        inString = false;
                        tokens.Add(new Token(TokenType.String, currentToken.ToString()));
                        currentToken.Clear();
                    }
                    else
                    {
                        currentToken.Append(c);
                    }
                    continue;
                }
                else if (c == ':')
                {
                    tokens.Add(new(TokenType.Console_in, ""));
                }
                else if (readingVar && c == '.')
                {

                    tokens.Add(new(TokenType.Module, currentToken.ToString()));
                    currentToken.Clear();
                    readingVar = false;
                    continue;
                }

                else if (!(!readingVar && char.IsDigit(c)) && identifierValidChars.Contains(c))
                {
                    readingVar = true;
                    currentToken.Append(c);
                }
                else

                if (char.IsDigit(c) || (c == '.' && currentToken.Length > 0 && char.IsDigit(currentToken[currentToken.Length - 1])))
                {
                    currentToken.Append(c);
                }
                else
                {
                    if (currentToken.Length > 0)
                    {
                        if (c == '(' && readingVar)
                        {
                            string trucknated = expression[(i - currentToken.Length)..];
                            int end = Utils.StringLastIndexOf(trucknated, ')');
                            if (end == -1)
                                throw new ExpectedTokenError(")");
                            var callFunctionStatement = trucknated[0..(end + 1)];
                            tokens.Add(new(TokenType.Function, callFunctionStatement));
                            i = i - currentToken.Length + callFunctionStatement.Length - 1;
                            currentToken.Clear();
                            readingVar = false;
                            if (i >= expression.Length)
                            {
                                break;
                            }
                            continue;
                        }
                        else
                        if (readingVar)
                        {
                            tokens.Add(new Token(TokenType.Variable, currentToken.ToString()));
                            readingVar = false;
                        }
                        else
                        {
                            tokens.Add(new Token(TokenType.Number, currentToken.ToString()));

                        }
                        currentToken.Clear();
                    }
                    bool isOperator = false;
                    if (!inString)
                    {
                        operatorTest += c;
                        foreach (var oper in OperatorFunctions)
                        {
                            if (oper.Value.Oper.StartsWith(operatorTest))
                            {
                                isOperator = true;
                                break;
                            }
                        }
                        if (Operators.Contains(operatorTest))
                        {
                            if (i < expression.Length - 1)
                            {
                                if (Operators.Any(x => x.StartsWith(operatorTest + expression[i + 1])))
                                {
                                    continue;
                                }
                            }
                            tokens.Add(new(TokenType.Operator, operatorTest));
                            operatorTest = "";
                        }
                    }
                    if (isOperator)
                    {
                        continue;
                    }
                    else
                    {
                        operatorTest = "";
                    }


                    if (c == '(' || c == ')')
                    {

                        tokens.Add(new Token(TokenType.Scope, c.ToString()));
                    }
                    else if (c == '[')
                    {
                        if (tokens.Count != 0 && tokens[^1].type != TokenType.Operator)
                        {
                            //indexer
                            string trucknated = expression[i..];
                            int end = Utils.StringGetMatchingSquarePer(trucknated);
                            if (end == -1)
                                throw new ExpectedTokenError("]");
                            var index = trucknated[1..end];
                            tokens.Add(new(TokenType.Indexer, index.ToString()));
                            i += end;
                            if (i+1 >= expression.Length)
                            {
                                break;
                            }
                            continue;
                        }
                        else
                        {
                            //array init
                            string arrayInit = expression[i..][1..];
                            int end = Utils.StringGetMatchingSquarePer("["+arrayInit);
                            end--;
                            if (end == -1)
                            {
                                continue;
                            }
                            arrayInit = arrayInit[..end];
                            tokens.Add(new Token(TokenType.Array, arrayInit));
                            i = end + 1;
                            if (i >= expression.Length)
                            {
                                break;
                            }
                            continue;
                        }
                    }

                    else if (c == '"')
                    {
                        inString = true;
                    }
                    else if (c == ' ')
                    {

                    }
                    else
                    {
                        throw new ExpressionEvalError(this, "Unexpected token '" + c + "'");
                    }
                }
            }
            if (operatorTest != "")
            {
                throw new ExpressionEvalError(this, "Unknown operator " + operatorTest);
            }
            if (currentToken.Length > 0)
            {
                if (readingVar)
                {
                    tokens.Add(new Token(TokenType.Variable, currentToken.ToString()));
                }
                else
                {
                    tokens.Add(new Token(TokenType.Number, currentToken.ToString()));

                }
                currentToken.Clear();
            }

            return tokens;
        }

        public List<ExpressionScope> Scopify(List<Token> tokens)
        {
            List<ExpressionScope> scopes = new();
            int depth = 0, start = -1, end = -1;
            int i = 0;
            foreach (var token in tokens)
            {
                if (token.type == TokenType.Scope)
                {
                    if (token.value == "(")
                    {
                        depth++;
                        if (start == -1)
                            start = i;
                    }
                    else
                    if (token.value == ")")
                    {
                        depth--;
                        if (depth == 0)
                        {
                            end = i;
                            scopes.Add(new(start, end));
                            start = -1;
                            end = -1;
                        }
                    }
                }
                i++;
            }

            return scopes;
        }


    }

    public struct Token
    {
        public TokenType type;
        public string value;

        public Token(TokenType type, string value)
        {
            this.type = type;
            this.value = value;
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
    public enum TokenType
    {
        Number = 0, //1,5
        Operator = 1, //+,-,*
        Scope = 2, // ()
        String = 3, // "blah"
        Variable = 4, // i
        Function = 5,
        Bool = 6,
        Unset = 7,
        Console_in = 8,
        Any = 9,
        Module = 10,
        NaN = 11,
        Array = 12,
        Indexer = 13,
        None = 14,
        Type = 15,
        Ignore = 100,

        Unknown = -1 //  some garbage
    }

    public class OperatorsDictiononary : Dictionary<string, Operator>
    {
        public bool TryGet(string key, out Operator result)
        {
            KeyValuePair<string, Operator> result_ = default;
            if (key.Contains("??"))
            {
                result_ = new("a??_", new("??", DataType.Any, DataType.None, (a, b) => a, 0, DataType.Number, true));
            }
            else
            if (key.Contains("§"))
            {
                result_ = new("a§_", new("§", DataType.Any, DataType.None, (a, b) => a, 0, DataType.Number, true));
            }
            else
            if (key.Contains('_'))
            {
                if (key[0] == '_')
                {
                    try
                    {
                        result_ = this.First(x => x.Key.EndsWith(key[1..]));
                        if (!result_.Key.Contains('_'))
                            throw new Exception("Invalid wildcard");
                    }
                    catch
                    {
                        result_ = default;
                    }
                }
                else
                {
                    try
                    {
                        result_ = this.First(x => x.Key.StartsWith(key[..^1]));
                        if (!result_.Key.Contains('_'))
                            throw new Exception("Invalid wildcard");
                    }
                    catch
                    {
                        result_ = default;
                    }
                }
            }

            else
            {
                return base.TryGetValue(key, out result);
            }
            if (result_.Equals(default(KeyValuePair<string, Operator>)))
            {
                result = default;
                return false;
            }
            result = result_.Value;
            return true;
        }

        public bool TryGetAny(string key, out Operator result)
        {
            KeyValuePair<string, Operator> result_ = default;

            try
            {
                result_ = this.First(x => x.Key.Contains(key));
            }
            catch
            {
                result_ = default;
            }

            if (result_.Equals(default(KeyValuePair<string, Operator>)))
            {
                result = default;
                return false;
            }
            result = result_.Value;
            return true;
        }
    }

}