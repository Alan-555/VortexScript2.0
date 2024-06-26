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

        public VortexError SetInfo(string info){
            this.info = info;
            return this;
        }

        public static void ThrowError(VortexError error){
            Console.WriteLine("A "+error.GetType().ToString()+" has occured!");
            Console.WriteLine(error.message+"\n\t"+error.info);
            if(ErrorHints.TryGetValue(error.info,out var val)){
                Console.WriteLine("\t"+val);
            }
            Console.WriteLine("In: "+Interpreter.GetCurrentFrame().VFile.Path+":"+(Interpreter.GetCurrentFrame().currentLine+1));
            Console.WriteLine("Stack trace follows:");
            int lastLine = 0;
            List<string> toP = [];
            foreach (var frame in Interpreter.CallStack.Reverse())
            {
                toP.Add($"{frame.Name} ({frame.VFile.GetFileName()}:{lastLine+1})");
                lastLine = frame.currentLine;
            }
            toP.Reverse();
            foreach (var p in toP)
            {
                Console.WriteLine(p);
            }

            
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
    public class DuplicateVariableError(params string[] args): VortexError("A variable with the identifier '{0}' is a duplicate.",args);
    public class IlegalStatementContextError(params string[] args) : VortexError("The statement '{0}' is not valid in the current context. ({1})",args);
    public class ScopeLeakError(params string[] args) : VortexError("The scope that began on line {0} has leaked.",args);
    public class FunctionBodyLeakError(params string[] args) : VortexError("The function body that began on line {0} has leaked.",args);
    public class InvalidIdentifierError(params string[] args) : VortexError("The identifier '{0}' is not valid. Identifiers must begin with a letter or an '_', only letters, numbers and '_' are allowed and it musn't be a reserved keyword. ",args);
    public class UnmatchingDataTypeError(params string[] args) : VortexError("Unmatching data type. Could not convert from {0} to {1}",args);
    public class UnknownNameError(params string[] args) : VortexError("The name '{0}'' does not exist in the current context, or it might not match the required type",args);
    public class ReadingUnsetValueError(params string[] args) : VortexError("The variable with indetifier'{0}' is unset (Declare using '$!' to allow for unset values)",args);
    public class IlegalDeclarationError(params string[] args) : VortexError("A new {0} may not be declared in the current context.",args);
    public class FuncOverloadNotFoundError(params string[] args) : VortexError("No overload found for function '{0}' that takes {1} argument(s)",args);
    public class InvalidFormatError(params string[] args) : VortexError("Cannot convert '{0}' to {1}",args);
    public class StackOverflowError() : VortexError($"The call stack has exceeded the maximum size of {Interpreter.maxDepth} frames");
    public class FileDoesNotExistError(params string[] args) : VortexError("The file '{0}' does not exist",args);
    public class ModuleAlreadyLoadedError(params string[] args) : VortexError("Module '{0}' has already been loaded",args);
    
    
}