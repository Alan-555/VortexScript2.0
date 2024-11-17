using System.Globalization;
using System.Reflection;
using System.Text;
using VortexScript.Definitions;
using VortexScript.Lexer;
using VortexScript.Structs;
using VortexScript.Vortex;
namespace VortexScript;

public class Evaluator
{
    public static bool InitDone = false;
    public static int HighestPrecedence = -1;
    public static string[] UnaryOperators = [];

    public string originalExpression = "";

    public static V_Variable Evaluate(string expression, DataType requiredType = DataType.None)
    {
        expression = expression.Trim();
        Evaluator ev = new();
        var w = Utils.StartWatch();
        var result = ev.Evaluate(expression);
        Utils.StopWatch(w, "Expression " + expression);
        if (requiredType != DataType.None && requiredType != DataType.Any)
        {
            if (result.type != requiredType)
            {
                throw new UnmatchingDataTypeError(result.type.ToString(), requiredType.ToString());
            }
        }
        return result;
    }
    public static V_Variable Evaluate(Lexer.LexerStructs.CompiledStatement statement)
    {
        return Evaluate(LexicalAnalyzer.StatementGetExpression(statement));
    }
    public static string[] operators = [];

    public static void Init()
    {
        if (InitDone) return;
        var x = CSharpDataRepresentations.ToDictionary(x => x.Key, x => x.Value);
        InitDone = true;
        Operators.Init();
        foreach (var oper in Operators.Operators_)
        {
            bool lNone = oper.Key.left == TokenType.None;
            bool rNone = oper.Key.right == TokenType.None;
            if (lNone || rNone)
            {
                char before = lNone ? 'r' : 'l';
                UnaryOperators = [.. UnaryOperators, before + oper.Key.syntax];
            }
            if (!operators.Contains(oper.Key.syntax))
            {
                operators = [.. operators, oper.Key.syntax];
            }
            if (oper.Key.precedence > HighestPrecedence)
                HighestPrecedence = oper.Key.precedence;
        }

    }


    public Evaluator()
    {
        Init();
    }

    public const string identifierValidChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_🌀🌋";


    public static readonly Dictionary<DataType, Type> CSharpDataRepresentations = new(){
        {DataType.String,typeof(string)},
        {DataType.Number,typeof(double)},
        {DataType.Bool,typeof(bool)},
        {DataType.None,typeof(object)},
        {DataType.Any,typeof(object)},
        {DataType.Int,typeof(int)},
        {DataType.Array,typeof(Array)},
        {DataType.Module,typeof(object)},
        {DataType.Indexer,typeof(int)},
        {DataType.Type,typeof(string)},
        {DataType.Error,typeof(string)},
        {DataType.GroupType,typeof(string)},
        {DataType.Class,typeof(VClass)},

    };

    public static DataType TokenTypeToDataType(TokenType type)
    {
        return (DataType)Enum.Parse(typeof(DataType), type.ToString());
    }

    public static TokenType DataTypeToTokenType(DataType type)
    {
        return (TokenType)Enum.Parse(typeof(TokenType), type.ToString());
    }


    public V_Variable Evaluate(string expression)
    {
        originalExpression = expression;
        if (expression == "")
        {
            throw new ExpressionEvalError(this, "Empty expression");
        }
        var tokens = Tokenize(expression);
        InterpreterWarnings.CheckExpression(tokens);
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
                if (expression.Count == 0)
                    throw new ExpressionEvalError(new() { originalExpression = "" }, "Empty expression");
                var result = RecursiveEval(expression);
                tokens.RemoveRange(start, end - start + 1);
                tokens.Insert(start, new(DataTypeToTokenType(result.type), result.ToString(), result.value));
                dist = end - start;
            }
        }
        var scopeTest = tokens.Find(x => x.type == TokenType.Scope);
        if (!scopeTest.Equals(default(Token)))
        {
            if (scopeTest.value == "(")
                throw new ExpectedTokenError(")");
            else
                throw new UnexpectedTokenError(")");

        }
        //proccess variables
        tokens = ProccessVariables(tokens);
        tokens.RemoveAll(x => x.type == TokenType.Ignore);//TODO: use the new dot notation. Same with indexers
        for (int i = 0; i < tokens.Count; i++)
        {
            if (i != tokens.Count - 1 && tokens[i + 1].type == TokenType.Indexer)
            {
                IndexerRange index;
                string expr = tokens[i + 1].value;
                var res = Evaluator.Evaluate(expr);
                if (res.value is double)
                {
                    index = new(Convert.ToInt32(res.value));
                }
                else if (res.value is IndexerRange range)
                {
                    index = range;
                }
                else
                {
                    throw new InvalidFormatError(res.ToString(), "number | range");
                }
                var variable = tokens[i].GetValVar();
                try
                {
                    variable = variable.Index(index);
                }
                catch (OverflowException)
                {
                    throw new IndexOutOfBoundsError(index.ToString());
                }
                tokens[i] = new(TokenType.Ignore, "");
                tokens[i + 1] = new(DataTypeToTokenType(variable.type), variable.ToString(), variable.value);
            }
        }
        tokens.RemoveAll(x => x.type == TokenType.Ignore);
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
            throw new ExpressionEvalError(this, "Too many tokens (" + TokensToExpression(tokens, true) + ") apeared after proccessing. Are you missing an operator or an operand?");
        }
        if (tokens[0].type == TokenType.Operator)
            throw new ExpressionEvalError(this, "Operator cannot be a result of an expression. Are you missing operands?");
        var dataType = TokenTypeToDataType(tokens[0].type);
        if (tokens[0].actualValue != null)
            return V_Variable.Construct(dataType, tokens[0].actualValue!);
        var value = tokens[0].value;
        var vVar = V_Variable.Construct(dataType, value, new() { readonly_ = true });
        return vVar;
    }
    public List<Token> ProccessVariables(List<Token> tokens)
    {
        VContext? module = null;
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].type == TokenType.Variable)
            {
                //if module is true do not read var
                if (module != null && tokens[i].value == "this")
                    throw new IlegalOperationError("'this' can only be accessed via top-level context.");
                bool good = Interpreter.ReadVar(tokens[i].value, out var variable, module);

                if (module != null)
                    tokens[i - 1] = new(TokenType.Ignore, "");
                if (!good)
                {
                    throw new UnknownNameError(tokens[i].value);
                }
                tokens[i] = new(DataTypeToTokenType(variable.type), variable.ToString(), variable.value);
            }
            else if (tokens[i].type == TokenType.Console_in)
            {
                if (module != null)
                {
                    throw new ExpressionEvalError(this, $"'%' is a language construct that can only be used in the top-level");
                }
                if (Interpreter.itm)
                    Console.Write("% ");
                tokens[i] = InternalStdInOut.TokenRead();


            }
            else if (tokens[i].type == TokenType.Function)
            {
                var statement = LexicalAnalyzer.TokenizeStatement(tokens[i].value);
                var result = Interpreter.CallFunctionStatement(statement);
                tokens[i] = new(DataTypeToTokenType(result.type),"",result.value);
            }
            else if (tokens[i].type == TokenType.Module && tokens[i].isDot)
            {
                if (i == tokens.Count - 1)
                {
                    throw new ExpectedTokenError("identifier");
                }
                var dotable = tokens[i].actualValue == null ? Evaluator.Evaluate(tokens[i].value) : tokens[i].GetValVar();
                var other = dotable.GetField(tokens[i + 1].value);
                tokens[i] = new(TokenType.Ignore, "");
                tokens[i + 1] = new(DataTypeToTokenType(other.type), other.ToString(), other.value) { isDot = tokens[i + 1].isDot };

            }
            else if (tokens[i].type == TokenType.Array)
            {
                var t = tokens[i];
                if (t.actualValue == null)
                    tokens[i] = new(t.type, t.value, InternalStandartLibrary.Array(t.value).value);

            }
        }

        return tokens;

    }
    public bool ProccessOperators(List<Token> tokens, int priority, out List<Token> tokensOut)
    {
        int index = 0;
        bool modified = false;
        bool found = false;
        int unaryT = 0;
        int leftI = 0, rightI = 0;
        object? value = null;

        DataType resultType = DataType.None;
        foreach (var token in tokens)
        {
            if (token.type == TokenType.Operator)
            {
                Token? left = null, right = null;
                leftI = index - 1;
                rightI = index + 1;
                if (index == 0)
                {
                    left = null;
                }
                else
                {
                    left = tokens[leftI];
                }
                if (index == tokens.Count - 1)
                {
                    right = null;
                }
                else
                {
                    right = tokens[rightI];
                }
                value = Operators.RunOperation(left, right, token.value, priority, this, out resultType, out unaryT);
                if (value == null)
                {
                    index++;
                    continue;
                }
                found = true;
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
            if (unaryT == 1)
            {
                //right irelevant
                tokens.RemoveAt(leftI);
            }
            else if (unaryT == 2)
            {
                //left irelevant
                tokens.RemoveAt(rightI);
                leftI++;
            }
            else
            {
                tokens.RemoveRange(leftI, 2);
            }

            var val = V_Variable.Construct(resultType, value);
            tokens[leftI] = new(DataTypeToTokenType(resultType), val.ToString(), val.value);
        }


        tokensOut = tokens;
        return modified;
    }

    public object TokenToPrimitive(Token token)
    {
        if (token.actualValue != null)
            return token.actualValue;
        return V_Variable.Construct(TokenTypeToDataType(token.type), token.value).ConvertToCSharpType(token.value);
    }

    public string TokensToExpression(List<Token> tokens, bool seperate = false)
    {
        string expression = "";
        foreach (Token token in tokens)
        {
            expression += token.value;
            if (seperate)
                expression += ";";
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
        bool forceVar = false;

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
            else if (c == '%' && !readingVar)
            {
                tokens.Add(new(TokenType.Console_in, ""));
            }
            else if (readingVar && c == '.')
            {
                tokens.Add(new(TokenType.Module, currentToken.ToString()) { isDot = true });

                currentToken.Clear();
                readingVar = false;
                continue;
            }

            else if ((readingVar || !char.IsDigit(c)) //if reading a variable or if it's not a number
                    && identifierValidChars.Contains(c) //if the current character is a valid identifier character
                    && operatorTest == "" //if not reading an operator
                    && (readingVar //if reading a variable
                    || forceVar //or it is forced
                    || tokens.Count == 0 //or if it is the first token
                    || CanVarFollow(tokens[^1]) //or if the previous token can follow a variable
                    ))
            {
                forceVar = false;
                readingVar = true;
                currentToken.Append(c);
            }
            else

            if (char.IsDigit(c) || c == '.' && currentToken.Length > 0 && char.IsDigit(currentToken[currentToken.Length - 1]))
            {
                currentToken.Append(c);
            }
            else if (c == '∞')
            {
                tokens.Add(new(TokenType.Number, "∞", double.PositiveInfinity));
                continue;
            }
            else
            {
                if (currentToken.Length > 0)
                {
                    if (c == '(' && readingVar)
                    {
                        string trucknated = expression[(i - currentToken.Length)..];
                        int end = Utils.StringGetMatchingPer(trucknated);
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
                        if (Operators.Operators_.Any(x => x.Key.syntax == currentToken.ToString()))
                            tokens.Add(new Token(TokenType.Operator, currentToken.ToString()));
                        else
                            tokens.Add(new Token(TokenType.Variable, currentToken.ToString()));
                        readingVar = false;
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.Number, currentToken.ToString()));

                    }
                    currentToken.Clear();
                }
                if (c == '"')
                {
                    inString = true;
                    continue;
                }
                bool isOperator = false;
                if (!inString)
                {
                    operatorTest += c;
                    foreach (var oper in Operators.Operators_)
                    {
                        if (oper.Key.syntax.StartsWith(operatorTest))
                        {
                            isOperator = true;
                            break;
                        }
                    }
                    if (Operators.Operators_.Any(x => x.Key.syntax == operatorTest))
                    {
                        if (i < expression.Length - 1)
                        {
                            if (Operators.Operators_.Any(x => x.Key.syntax.StartsWith(operatorTest + expression[i + 1])))
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
                        if (i + 1 >= expression.Length)
                        {
                            break;
                        }
                        continue;
                    }
                    else
                    {
                        //array init
                        string arrayInit = expression[i..][1..];
                        int end = Utils.StringGetMatchingSquarePer("[" + arrayInit);
                        end--;
                        if (end == -1)
                        {
                            continue;
                        }
                        arrayInit = arrayInit[..end];
                        tokens.Add(new Token(TokenType.Array, arrayInit));
                        i += 1 + arrayInit.Length;
                        if (i >= expression.Length)
                        {
                            break;
                        }
                        continue;
                    }
                }
                else if (c == ' ')
                {
                    forceVar = true;
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
        if (inString)
            throw new ExpectedTokenError("\"");
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
        int depth = 0, start = -1, end;
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

    public static bool CanVarFollow(Token prevToken)
    {
        return prevToken.type == TokenType.Operator
        || (prevToken.type == TokenType.Scope && prevToken.value == "(")
        || (prevToken.type == TokenType.Module && prevToken.isDot);
    }

}

public class OperatorDict : Dictionary<OperatorDefinition, MethodInfo>
{
    public new bool TryGetValue(OperatorDefinition def, out MethodInfo? out_, out bool precedenceCorrect)
    {
        try
        {
            bool r = false;
            out_ = this.First(x =>
            {
                var k = x.Key;
                r = k.precedence == def.precedence;
                bool leftGood, rightGood;
                leftGood = k.left == TokenType.Any || k.left == def.left;
                rightGood = k.right == TokenType.Any || k.right == def.right;
                return leftGood && rightGood && def.syntax == k.syntax;
            }).Value;
            precedenceCorrect = r;
            return true;
        }
        catch
        {
            out_ = null;
            precedenceCorrect = false;
            return false;
        }

    }
    public OperatorDict(Dictionary<OperatorDefinition, MethodInfo>? dict)
    {
        if (dict == null) return;
        foreach (var item in dict)
        {
            this[item.Key] = item.Value;
        }
    }
}

public class Operators
{
    public static OperatorDict Operators_ = new(null);
    public static void Init()
    {
        Operators_ = new(typeof(Operators)
           .GetMethods()
           .Where(m => m.GetCustomAttributes(typeof(OperatorDefinition), false).Length > 0)
           .ToDictionary(m => m.GetCustomAttribute<OperatorDefinition>(), m => m));
    }
    //TODO: find findind. WHen not find, it says "wrong prcedence" and never says "unknown operator"
    public static object? RunOperation(Token? left, Token? right, string syntax, int currentPrecedence, Evaluator eval, out DataType returnType, out int unaryN)
    {

        string? element = Evaluator.UnaryOperators.FirstOrDefault(x => x[1..] == syntax);
        bool unary = false;
        unaryN = 0;
        if (element != null)
        {
            //unary
            if (element[0] == 'l')
            {
                unaryN = 1;
                if (!right.HasValue || right.Value.type == TokenType.Operator)
                {
                    right = null;
                    unary = true;
                }
            }
            else if (!left.HasValue || left.Value.type == TokenType.Operator)
            {
                unaryN = 2;
                left = null;
                unary = true;
            }
        }
        else
        {
            unaryN = 0;
            //binary - search for a binary operator

        }
        //find the operator for current precedence
        TokenType left_ = left.HasValue ? left.Value.type : TokenType.None;
        TokenType right_ = right.HasValue ? right.Value.type : TokenType.None;
        OperatorDefinition def = new(left_, right_, syntax, currentPrecedence);

        //check if the operator exists
        if (!Operators_.TryGetValue(def, out var oper, out bool precedenceCorrect))
        {
            (left_, right_) = (right_, left_);
            //check if the operator with the other order exists
            if (unary || !Operators_.TryGetValue(def, out oper, out precedenceCorrect))
            {
                var oldTypeLeft = left_;
                var oldTypeRight = right_;
                if (unary)
                {
                    (left_, right_) = (right_, left_);
                    if (left_ == TokenType.None)
                        right_ = TokenType.Any;
                    else
                        left_ = TokenType.Any;
                }
                else
                {
                    left_ = TokenType.Any;
                    right_ = TokenType.Any;
                }
                def = new(left_, right_, syntax, currentPrecedence);
                if (!Operators_.TryGetValue(def, out oper, out precedenceCorrect) && precedenceCorrect)
                    throw new ExpressionEvalError(eval, "Unknown operator: " + (oldTypeRight == TokenType.None ? "" : oldTypeRight.ToString()) + syntax + (oldTypeLeft == TokenType.None ? "" : oldTypeLeft.ToString()));
            }
            else
            {
                //adjust the operands back
                (right_, left_) = (left_, right_);

            }
        }
        if (!precedenceCorrect)
        {
            returnType = DataType.None;
            return null;
        }
        InterpreterWarnings.CheckOperator(left_, def, right_);
        if (unary && oper.GetParameters().Length != 1)
        {
            throw new ExpressionEvalError(eval, "An unary operator takes only one operand: " + syntax);
        }
        object? result = null;
        if (unary)
        {
            try
            {

                if (right == null)
                {
                    result = oper.Invoke(null, parameters: [left]);
                }
                else
                {
                    result = oper.Invoke(null, parameters: [right]);
                }
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException!;
            }
        }
        else
        {
            //binary
            if (!left.HasValue || !right.HasValue)
                throw new ExpressionEvalError(eval, "A binary operator '" + syntax + "'  must take two operands.");
            try
            {
                result = oper.Invoke(null, parameters: [left, right]);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException!;
            }
        }
        if (result == null)
        {
            throw new ExpressionEvalError(eval, "An operator must return a value: " + syntax);
        }
        returnType = oper.GetCustomAttribute<OperatorDefinition>()!.returnType;
        return result;
    }
    //unary
    [OperatorDefinition(TokenType.None, TokenType.Number, "-", 0, DataType.Number)]
    public static double Negate(Token right)
    {
        return -(double)right.GetVal();
    }
    [OperatorDefinition(TokenType.None, TokenType.Bool, "!", 0, DataType.Bool)]
    public static bool BoolNegate(Token left)
    {
        return !(bool)left.GetVal();
    }
    [OperatorDefinition(TokenType.String, TokenType.None, "s", 0, DataType.String)]
    public static string StringUnset(Token left)
    {
        var value = left.GetVal().ToString()!;
        return value;
    }
    [OperatorDefinition(TokenType.Unset, TokenType.None, "s", 0, DataType.String)]
    public static string StringUnset_(Token left)
    {
        return "";
    }
    [OperatorDefinition(TokenType.String, TokenType.None, "n", 0, DataType.Number)]
    public static double CastToInt(Token left)
    {
        var value = left.GetVal().ToString()!;
        if (double.TryParse(value, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }
        throw new InvalidFormatError(left.value, "a number");
    }
    [OperatorDefinition(TokenType.Bool, TokenType.None, "n", 0, DataType.Number)]
    public static double CastBoolToInt(Token left)
    {
        var value = (bool)left.GetVal();
        return value ? 1 : 0;
    }
    [OperatorDefinition(TokenType.Number, TokenType.None, "n", 0, DataType.Number)]
    public static double CastNumberToInt(Token left)
    {
        return (double)left.GetVal();
    }
    [OperatorDefinition(TokenType.Any, TokenType.None, "??", 0, DataType.Bool)]
    public static bool IsNotUnset(Token left)
    {
        return left.type != TokenType.Unset;
    }
    [OperatorDefinition(TokenType.None, TokenType.Any, "typeof", 0, DataType.Type)]
    public static DataType TypeOfOperator(Token right)
    {
        return right.GetValVar().type;
    }

    [OperatorDefinition(TokenType.Number, TokenType.Number, "->", 1, DataType.Indexer)]
    public static IndexerRange InexerRange(Token left, Token right)
    {
        return new IndexerRange(Convert.ToInt32((double)left.GetVal()), Convert.ToInt32((double)right.GetVal()));
    }

    //mult
    [OperatorDefinition(TokenType.Number, TokenType.Number, "*", 1, DataType.Number)]
    public static double MulNumbers(Token left, Token right)
    {
        return (double)left.GetVal() * (double)right.GetVal();
    }
    [OperatorDefinition(TokenType.Number, TokenType.Number, "/", 1, DataType.Number)]
    public static double DivNumbers(Token left, Token right)
    {
        return (double)left.GetVal() / (double)right.GetVal();
    }
    [OperatorDefinition(TokenType.Number, TokenType.Number, "%", 1, DataType.Number)]
    public static double ModNumbers(Token left, Token right)
    {
        return (double)left.GetVal() % (double)right.GetVal();
    }
    [OperatorDefinition(TokenType.Number, TokenType.Number, "°", 1, DataType.Number)]
    public static double ExpNumbers(Token left, Token right)
    {
        return Math.Pow((double)left.GetVal(), (double)right.GetVal());
    }
    [OperatorDefinition(TokenType.Number, TokenType.Number, "°°", 1, DataType.Number)]
    public static double Root(Token left, Token right)
    {
        return Math.Pow((double)right.GetVal(), 1 / (double)left.GetVal());
    }
    //addative
    [OperatorDefinition(TokenType.Number, TokenType.Number, "+", 2, DataType.Number)]
    public static double AddNumbers(Token left, Token right)
    {
        return (double)left.GetVal() + (double)right.GetVal();
    }
    [OperatorDefinition(TokenType.Int, TokenType.Int, "+", 2, DataType.Int)]
    public static double AddNumbersInt(Token left, Token right)
    {
        return (double)left.GetVal() + (double)right.GetVal();
    }
    [OperatorDefinition(TokenType.Array, TokenType.Any, "+", 2, DataType.Array)]
    public static VArray ArrayAdd(Token left, Token right)
    {
        var arr = ((VArray)left.GetVal()).ToArray();
        var val = right.GetValVar();
        return [.. arr, val];
    }
    [OperatorDefinition(TokenType.Array, TokenType.Array, "+", 2, DataType.Array)]
    public static VArray ArrayAddArray(Token left, Token right)
    {
        var arr = ((VArray)left.GetVal()).ToArray();
        var val = ((VArray)right.GetValVar().value).ToArray();
        return [.. arr, .. val];
    }
    [OperatorDefinition(TokenType.Any, TokenType.Any, "+", 2, DataType.String)]
    public static string Add(Token left, Token right)
    {
        return left.GetVal().ToString() + right.GetVal().ToString();
    }


    [OperatorDefinition(TokenType.Number, TokenType.Number, "-", 2, DataType.Number)]
    public static double Sub(Token left, Token right)
    {
        return (double)left.GetVal() - (double)right.GetVal();
    }


    //Bitshift
    [OperatorDefinition(TokenType.Number, TokenType.Number, "<<", 3, DataType.Number)]
    public static double ShiftLeft(Token left, Token right)
    {
        return (int)Math.Floor((double)left.GetVal()) << (int)Math.Floor((double)right.GetVal());
    }
    [OperatorDefinition(TokenType.Number, TokenType.Number, ">>", 3, DataType.Number)]
    public static double ShiftRight(Token left, Token right)
    {
        return (int)Math.Floor((double)left.GetVal()) >> (int)Math.Floor((double)right.GetVal());
    }

    //relative
    [OperatorDefinition(TokenType.Number, TokenType.Number, ">", 4, DataType.Bool)]
    public static bool Greater(Token left, Token right)
    {
        return (double)left.GetVal() > (double)right.GetVal();
    }
    [OperatorDefinition(TokenType.Number, TokenType.Number, "<", 4, DataType.Bool)]
    public static bool Lesser(Token left, Token right)
    {
        return (double)left.GetVal() < (double)right.GetVal();
    }
    [OperatorDefinition(TokenType.Number, TokenType.Number, ">=", 4, DataType.Bool)]
    public static bool GreaterOrEqual(Token left, Token right)
    {
        return (double)left.GetVal() >= (double)right.GetVal();
    }
    [OperatorDefinition(TokenType.Number, TokenType.Number, "<=", 4, DataType.Bool)]
    public static bool LesserOrEqual(Token left, Token right)
    {
        return (double)left.GetVal() <= (double)right.GetVal();
    }
    [OperatorDefinition(TokenType.Any, TokenType.Type, "is", 5, DataType.Bool)]
    public static bool IsOperator(Token left, Token right)
    {
        return left.GetValVar().type == (DataType)right.GetVal();
    }

    [OperatorDefinition(TokenType.Any, TokenType.Array, "is", 5, DataType.Bool)]
    public static bool IsOperatorArray(Token left, Token right)
    {
        var values = (VArray)right.GetValVar().value;
        var vals_ = values.Select(x => (DataType)x.value);
        return vals_.Any(x => x == left.GetValVar().type);
    }
    [OperatorDefinition(TokenType.Any, TokenType.Any, "==", 5, DataType.Bool)]
    public static bool Equals(Token left, Token right)
    {
        dynamic val1 = left.GetVal(), val2 = right.GetVal();
        return val1.ToString() == val2.ToString();
    }
    [OperatorDefinition(TokenType.Any, TokenType.Any, "!=", 5, DataType.Bool)]
    public static bool NotEquals(Token left, Token right)
    {
        dynamic val1 = left.GetVal(), val2 = right.GetVal();
        return val1.ToString() != val2.ToString();
    }
    [OperatorDefinition(TokenType.Any, TokenType.Any, "===", 5, DataType.Bool)]
    public static bool EqualsStrict(Token left, Token right)
    {
        var lType = left.type;
        var rType = right.type;
        dynamic val1 = left.GetValVar(), val2 = right.GetValVar();
        return val1.value.ToString() == val2.value.ToString() && val1.type == val2.type;
    }
    [OperatorDefinition(TokenType.Any, TokenType.Any, "!==", 5, DataType.Bool)]
    public static bool NotEqualsStrict(Token left, Token right)
    {
        dynamic val1 = left.GetValVar(), val2 = right.GetValVar();
        return val1.value.ToString() != val2.value.ToString() || val1.type != val2.type;
    }
    [OperatorDefinition(TokenType.Bool, TokenType.Bool, "||", 8, DataType.Bool)]
    public static bool Or(Token left, Token right)
    {
        return (bool)left.GetVal() || (bool)right.GetVal();
    }
    [OperatorDefinition(TokenType.Bool, TokenType.Bool, "&&", 7, DataType.Bool)]
    public static bool And(Token left, Token right)
    {
        return (bool)left.GetVal() && (bool)right.GetVal();
    }

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
    Error = 16,
    GroupType = 17,
    Int = 18,
    Object = 19,
    Class = 20,

    Ignore = 100,

    Unknown = -1 //  some garbage
}

