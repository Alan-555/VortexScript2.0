using Vorteval;

namespace Vortex{

    public class VortexError : Exception{

        public static Dictionary<string,string> ErrorHints = new(){
            {"Unknown operator: Unset==Unset", "Use 'a??_' operator insted."}
        };
        public string message = "";
        public string info = "";
        public VortexError(string message,params string[] args) {
            this.message = string.Format(message,args);
        }

        public static void ThrowError(VortexError error){
            Console.WriteLine("A "+error.GetType().ToString()+" has occured!");
            Console.WriteLine(error.message+"\n\t"+error.info);
            if(ErrorHints.TryGetValue(error.info,out var val)){
                Console.WriteLine("\t"+val);
            }
            Console.WriteLine("In: "+Interpreter.Instance.File+":"+(Interpreter.Instance.Line+1));
            Console.WriteLine("Stack trace follows:");
            //TODO: stack trace

            
            Environment.Exit(1);
        }
    }

    public class UnexpectedTokenError(params string[] args) : VortexError("Unexpected token '{0}'",args);
    public class UnexpectedEndOfStatementError(params string[] args) : VortexError("Unexpected end of statement. {0} expected.",args);
    public class ExpectedTokenError(params string[] args) : VortexError("'{0}' expected",args);
    public class UnknownStatementError(params string[] args): VortexError("Unknown statement '{0}'",args);
    public class ExpressionEvalError : VortexError
    {
        public ExpressionEvalError(Evaluator eval, params string[] args) : base("Could not evaluate expression '" + eval.originalExpression + "'", args)
        {
            info = args[0];
        }
    }

    public class VariableAlreadyDeclaredError(params string[] args): VortexError("A variable with the identifier '{0}' has already been declared in the current scope",args);
    public class IlegalStatementContextError(params string[] args) : VortexError("The statement '{0}' is not valid in the current context. ({1})",args);
    public class ScopeLeakError(params string[] args) : VortexError("The scope that began on line {0} has leaked.",args);
    public class InvalidIdentifierError(params string[] args) : VortexError("The identifier '{0}' is not valid. Identifiers must begin with a letter or an '_', only letters, numbers and '_' are allowed and it musn't be a reserved keyword. ",args);
    public class UnmatchingDataType(params string[] args) : VortexError("Unmatching data type. Could not convert from {0} to {1}",args);
    public class UnknownName(params string[] args) : VortexError("The name {0} does not exist in the current context.",args);
    public class ReadingUnsetValue(params string[] args) : VortexError("The variable with indetifier'{0}' is unset (Declare using '$!' to allow for unset values)",args);
}