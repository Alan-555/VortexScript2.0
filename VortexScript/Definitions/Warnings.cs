using VortexScript.Structs;

namespace VortexScript.Definitions;

public class InterpreterWarnings
{
    public static void CheckStatement(string statement){

    }
    public static void CheckExpression(Token[] tokens){

    }
    public static void CheckOperator(TokenType left, OperatorDefinition oper, TokenType right){
        if(left==TokenType.Unset||right==TokenType.Unset&&oper.syntax=="=="){
            PrintWarning("Use '??' operator instead of any==unset");
        }   
    }

    public static void PrintWarning(string message){
        Console.WriteLine("Warning at: "+ Interpreter.GetCurrentFrame());
        Console.WriteLine(message);
    }
}