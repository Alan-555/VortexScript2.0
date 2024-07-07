using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Vortex
{
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
                    if(c==searchChar){
                        matchIndex = i;
                    }
                }
            }
            

            return matchIndex;
        }

        public static string[] StringSplit(string input, char delimiter)
        {
            List<string> result = new();
            StringBuilder currentString = new();
            bool insideQuotes = false;
            int nestedScope = 0;

            foreach (char c in input)
            {
                if (c == '\"')
                {
                    insideQuotes = !insideQuotes; // Toggle the insideQuotes flag
                    currentString.Append('"');
                }
                else if (c == '('){
                    nestedScope++;
                    currentString.Append('(');
                }
                else if(c == ')'){
                    nestedScope--;
                    currentString.Append(')');
                }
                else if (c == delimiter && !insideQuotes&&nestedScope==0)
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



        public static Dictionary<string, V_Variable> GetAllVars()
        {
            Dictionary<string, V_Variable> vars =[ ];
            foreach (var Context in Interpreter.GetCurrentFrame().ScopeStack)
            {
                foreach (var kv in Context.Variables)
                {
                    vars.TryAdd(kv.Key, kv.Value);
                }
            }
            return vars;
        }
        public static CustomAttributeTypedArgument GetStatementAttribute(MethodInfo statement, StatementAttributes index)
        {
            return statement.CustomAttributes.ToList()[0].ConstructorArguments[(int)index];
        }
        public static CustomAttributeTypedArgument GetStatementAttribute<T>(MethodInfo statement,int index)
        {
            return statement.CustomAttributes.ToList().Find(x=>x.AttributeType==typeof(T)).ConstructorArguments[index];
        }
        public static bool IsIdentifierValid(string identifier)
        {
            if(Interpreter.keywords.Contains(identifier))return false;
            string pattern = @"^[a-zA-Z_🌋][a-zA-Z0-9_🌀🌋]*$";
            Regex regex = new(pattern);
            return regex.IsMatch(identifier);
        }
        public static object CastToCSharpType(DataType type,string value){
            return type switch
            {
                DataType.String => value,
                DataType.Number => double.Parse(value),
                DataType.Bool => value == "True",
                DataType.Unset => null,
                DataType.Int => 0,
                _ => null,
            };
        }
        public static VFuncArg[] ConvertMethodInfoToArgs(MethodInfo method){
            return method.GetParameters().Select(x => new VFuncArg(x.Name)).ToArray();
        }


        public static void Swap<T>(IList<T> list, int indexA, int indexB)
        {
            (list[indexB], list[indexA]) = (list[indexA], list[indexB]);
        }
    }



}