using VortexScript.Structs;

namespace VortexScript.Definitions;

public class InterpreterWarnings
{
    public static void CheckStatement(string statement)
    {
        if (Directives.DIR_suppressWarnings.value) return;
    }
    public static void CheckExpression(List<Token> tokens)
    {
        if (Directives.DIR_suppressWarnings.value) return;
    }
    public static void CheckOperator(TokenType left, OperatorDefinition oper, TokenType right)
    {
        if (Directives.DIR_suppressWarnings.value) return;
        if ((left == TokenType.Unset || right == TokenType.Unset) && oper.syntax == "==")
        {
            PrintWarning("Use '??' operator instead of any==unset");
        }
    }

    public static void PrintWarning(string message)
    {
        Console.WriteLine("Warning at: " + Interpreter.GetCurrentFrame());
        Console.WriteLine(message);
    }
}