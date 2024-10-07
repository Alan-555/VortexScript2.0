using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using VortexScript.Vortex;

namespace VortexScript;

class Utils
{
    public static string RemoveInlineComments(string code)
    {
        string pattern = @"//.*";
        return Regex.Replace(code, pattern, string.Empty);
    }
    public static string StringRemoveSpaces(string input)
    {
        StringBuilder result = new StringBuilder();
        bool insideQuotes = false;

        foreach (char c in input)
        {
            if (c == '\"')
            {
                insideQuotes = !insideQuotes; // Toggle the insideQuotes flag
                result.Append(c);
            }
            else if (c != ' ' || insideQuotes)
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
    public static bool StringContains(string input, string searchString)
    {
        bool insideQuotes = false;
        int searchLength = searchString.Length;
        int inputLength = input.Length;
        int matchIndex = 0;

        for (int i = 0; i < inputLength; i++)
        {
            char c = input[i];

            if (c == '\"')
            {
                insideQuotes = !insideQuotes; // Toggle the insideQuotes flag
                continue;
            }

            if (!insideQuotes)
            {
                if (c == searchString[matchIndex])
                {
                    matchIndex++;
                    if (matchIndex == searchLength)
                    {
                        return true;
                    }
                }
                else
                {
                    matchIndex = 0; // Reset match index if the current character doesn't match
                }
            }
        }

        return false;
    }

    public static int StringIndexOf(string input, string searchString)
    {
        bool insideQuotes = false;
        int searchLength = searchString.Length;
        int inputLength = input.Length;
        int matchIndex = 0;

        for (int i = 0; i < inputLength; i++)
        {
            char c = input[i];

            if (c == '\"')
            {
                insideQuotes = !insideQuotes; // Toggle the insideQuotes flag
                continue;
            }

            if (!insideQuotes)
            {
                if (c == searchString[matchIndex])
                {
                    matchIndex++;
                    if (matchIndex == searchLength)
                    {
                        return i - searchLength + 1; // Return the start index of the match
                    }
                }
                else
                {
                    i -= matchIndex; // Backtrack to re-check characters after a mismatch
                    matchIndex = 0; // Reset match index if the current character doesn't match
                }
            }
        }

        return -1; // Return -1 if the search string is not found
    }

    public static int StringLastIndexOf(string input, char searchChar)
    {
        bool insideQuotes = false;
        int inputLength = input.Length;
        int matchIndex = -1;

        for (int i = 0; i < inputLength; i++)
        {
            char c = input[i];

            if (c == '\"')
            {
                insideQuotes = !insideQuotes; // Toggle the insideQuotes flag
                continue;
            }

            if (!insideQuotes)
            {
                if (c == searchChar)
                {
                    matchIndex = i;
                }
            }
        }


        return matchIndex;
    }

    public static int StringGetMatchingSquarePer(string input)
    {
        bool insideQuotes = false;
        int inputLength = input.Length;
        int nested = 0;

        for (int i = 0; i < inputLength; i++)
        {
            char c = input[i];

            if (c == '\"')
            {
                insideQuotes = !insideQuotes; // Toggle the insideQuotes flag
                continue;
            }

            if (!insideQuotes)
            {
                if (c == '[')
                    nested++;
                else
                if (c == ']')
                    nested--;

                if (c == ']' && nested == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }
    public static int StringGetMatchingPer(string input)
    {
        bool insideQuotes = false;
        int inputLength = input.Length;
        int nested = 0;

        for (int i = 0; i < inputLength; i++)
        {
            char c = input[i];

            if (c == '\"')
            {
                insideQuotes = !insideQuotes; // Toggle the insideQuotes flag
                continue;
            }

            if (!insideQuotes)
            {
                if (c == '(')
                    nested++;
                else
                if (c == ')')
                    nested--;

                if (c == ')' && nested == 0)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    public static string[] StringSplit(string input, char delimiter)
    {
        List<string> result = new();
        StringBuilder currentString = new();
        bool insideQuotes = false;
        int nestedScope = 0;
        int nestedScopeSquare = 0;
        foreach (char c in input)
        {
            if (c == '\"')
            {
                insideQuotes = !insideQuotes; // Toggle the insideQuotes flag
                currentString.Append('"');
            }
            else if (c == '(')
            {
                nestedScope++;
                currentString.Append('(');
            }
            else if (c == ')')
            {
                nestedScope--;
                currentString.Append(')');
            }
            else if (c == '[')
            {
                nestedScopeSquare++;

                currentString.Append('[');
            }
            else if (c == ']')
            {
                nestedScopeSquare--;
                currentString.Append(']');
            }
            else if (c == delimiter && !insideQuotes && nestedScope == 0 && nestedScopeSquare == 0)
            {
                result.Add(currentString.ToString());
                currentString.Clear();
            }
            else
            {
                currentString.Append(c);
            }
        }

        result.Add(currentString.ToString());
        return result.ToArray();
    }

    public static VArray? ArgsEval(string input, char delimiter, DataType[]? dataTypes = null, DataType? oneType = null)
    {
        var argsArray = StringSplit(input, delimiter);
        if (argsArray.Length == 1 && argsArray[0] == "")
        {
            argsArray = [];
        }
        if (oneType != null)
        {
            dataTypes = new DataType[argsArray.Length];
            for (int i_ = 0; i_ < dataTypes.Length; i_++)
            {
                dataTypes[i_] = (DataType)oneType;
            }
        }
        var ret = new VArray();
        bool notEnfored = dataTypes == null;
        int i = 0;
        if (!notEnfored && argsArray.Length != dataTypes!.Length)
        {
            return null;
        }
        foreach (var arg in argsArray)
        {
            if (notEnfored)
                ret.Add(Evaluator.Evaluate(arg));
            else
                ret.Add(Evaluator.Evaluate(arg, dataTypes![i]));
            i++;
        }
        return ret;
    }

    public static Dictionary<string, V_Variable> GetAllVars()
    {
        Dictionary<string, V_Variable> vars = [];
        foreach (var Context in Interpreter.GetCurrentFrame().ScopeStack)
        {
            foreach (var kv in Context.Variables)
            {
                vars.TryAdd(kv.Key, kv.Value);
            }
        }
        foreach(var (k,v) in Interpreter.GetCurrentFrame().VFile.TopLevelContext!.Variables){
            vars.TryAdd(k,v);
        }
        return vars;
    }

    public static CustomAttributeTypedArgument GetStatementAttribute(MethodInfo statement, StatementAttributes index)
    {
        return statement.CustomAttributes.ToList()[0].ConstructorArguments[(int)index];
    }
    public static CustomAttributeTypedArgument GetStatementAttribute<T>(MethodInfo statement, int index)
    {
        return statement.CustomAttributes.ToList().Find(x => x.AttributeType == typeof(T)).ConstructorArguments[index];
    }
    public static bool IsIdentifierValid(string identifier,bool declaring = false)
    {
        if (declaring&&Interpreter.keywords.Contains(identifier)) return false;
        string pattern = @"^[a-zA-Z_🌋][a-zA-Z0-9_🌀🌋]*$";
        Regex regex = new(pattern);
        return regex.IsMatch(identifier);
    }

    public static VFuncArg[] ConvertMethodInfoToArgs(MethodInfo method)
    {
        return method.GetParameters().Select(x => new VFuncArg(x.Name!)).ToArray();
    }


    public static void Swap<T>(IList<T> list, int indexA, int indexB)
    {
        (list[indexB], list[indexA]) = (list[indexA], list[indexB]);
    }

    public static Stopwatch? StartWatch()
    {
        if (!Interpreter.debug) return null;
        Stopwatch sw = new();
        sw.Start();
        return sw;
    }
    public static void StopWatch(Stopwatch? sw, string text)
    {
        if (sw == null) return;
        sw.Stop();
        TimeSpan ts = sw.Elapsed;
        string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
        ts.Hours, ts.Minutes, ts.Seconds,
        ts.Milliseconds / 10);
        Console.WriteLine($"Proccess '{text}' took " + elapsedTime);
    }

    public static VArray ConvertDictToVArray<T>(Dictionary<string,T> list, DataType type){
        VArray ret = new();
        foreach(var item in list){
            ret.Add(V_Variable.Construct(type,item.Key));
        }
        return ret;
    }
    public static VArray ConvertDictToVArray(Dictionary<string,V_Variable> list){
        VArray ret = new();
        foreach(var item in list){
            var key = V_Variable.Construct(DataType.String,item.Key);
            var val = V_Variable.Construct(item.Value.type,item.Value.value);
            var arr = new VArray(){key,val};
            ret.Add(V_Variable.Construct(DataType.Array,arr));
        }
        return ret;
    }
    public static DataType CSharpTypeToVortexType(Type type){//TODO: finish
        if(type == typeof(int)) return DataType.Number;
        if(type == typeof(double)) return DataType.Number;
        if(type == typeof(string)) return DataType.String;
        if(type == typeof(bool)) return DataType.Bool;
        if(type == typeof(DataType)) return DataType.Type;
        if(type == typeof(VArray)) return DataType.Array;
        if(type == typeof(VFunc)) return DataType.Function;
        if(type == typeof(V_Variable)) return DataType.Any;
        if(type == typeof(VContext)) return DataType.Module;

        return DataType.None;
    }
}