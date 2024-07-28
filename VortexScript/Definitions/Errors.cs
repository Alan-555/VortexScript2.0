namespace VortexScript.Definitions;


public enum ErrorType
{
    Syntax,
    Runtime,
    Critical

}

public class VortexError : Exception
{

    public static Dictionary<string, string> ErrorHints = new(){
        {"Unknown operator: Unset==Unset", "Use 'a??_' operator insted."}
    };
    public string message = "";
    public string info = "";
    public ErrorType type = ErrorType.Runtime;
    public VortexError(string message, ErrorType type = ErrorType.Runtime, params string[] args)
    {
        this.type = type;
        if (args.Length > 0 && args[0].Length > 1 && args[0][0] == '!')
        {
            this.message = args[0][1..];
            return;
        }

        try
        {
            this.message = string.Format(message, args);
        }
        catch
        {
            this.message = args[0];
        }
    }

    public VortexError(string message)
    {
        this.message = message;
    }
    public VortexError SetInfo(string info)
    {
        this.info = info;
        return this;
    }
    public override string ToString()
    {
        return GetType().ToString().Replace("Vortex.", "") + message + "\t" + info;
    }

    public static void ThrowError(VortexError error)
    {
        Console.WriteLine("Error: ");
        Console.Write(error.GetType().ToString().Replace("Vortex.", "") + " has been raised: ");
        Console.WriteLine(error.message + "\n\t" + error.info);
        if (ErrorHints.TryGetValue(error.info, out var val))
        {
            Console.WriteLine("\t" + val);
        }
        Console.WriteLine("In: " + Interpreter.GetCurrentFrame().VFile.Path + ":" + (Interpreter.GetCurrentFrame().currentLine + 1));
        Console.WriteLine("\nListing frames:");
        int lastLine = 0;
        List<string> toP = [];
        foreach (var frame in Interpreter.CallStack.Reverse())
        {
            toP.Add($"{frame.Name} ({frame.VFile.GetFileName()}:{lastLine + 1})");
            lastLine = frame.currentLine;
        }
        toP.Reverse();
        foreach (var p in toP)
        {
            Console.WriteLine(p);
        }

        if (!Interpreter.itm)
            Environment.Exit(1);
    }
}

public class UnexpectedTokenError(params string[] args) : VortexError("Unexpected token '{0}'", ErrorType.Syntax, args);
public class UnexpectedEndOfStatementError(params string[] args) : VortexError("Unexpected end of statement. {0} expected.", ErrorType.Syntax, args);
public class ExpectedTokenError(params string[] args) : VortexError("'{0}' expected", ErrorType.Syntax, args);
public class UnknownStatementError(params string[] args) : VortexError("Unknown statement '{0}'", ErrorType.Syntax, args);
public class ExpressionEvalError : VortexError
{
    public ExpressionEvalError(Evaluator eval, params string[] args) : base("Could not evaluate expression '" + eval.originalExpression + "'", ErrorType.Runtime, args) { SetInfo(args[0]); }
}

public class IdentifierAlreadyUsedError(params string[] args) : VortexError("The identifier '{0}' has already been used in the current context", ErrorType.Runtime, args);
public class DuplicateVariableError(params string[] args) : VortexError("A variable with the identifier '{0}' is a duplicate.", ErrorType.Runtime, args);
public class IlegalStatementContextError(params string[] args) : VortexError("The statement '{0}' is not valid in the current context. ({1})", ErrorType.Syntax, args);
public class ScopeLeakError(params string[] args) : VortexError("The scope that began on line {0} has leaked.", ErrorType.Critical, args);
public class FunctionBodyLeakError(params string[] args) : VortexError("The function body that began on line {0} has leaked.", ErrorType.Critical, args);
public class InvalidIdentifierError(params string[] args) : VortexError("The identifier '{0}' is not valid. Identifiers must begin with a letter or an '_', only letters, numbers and '_' are allowed and it musn't be a reserved keyword. ", ErrorType.Syntax, args);
public class UnmatchingDataTypeError(params string[] args) : VortexError(args.Length == 0 ? "Unmatching data type" : "Unmatching data type. Could not convert from {0} to {1}", ErrorType.Runtime, args);
public class UnknownNameError(params string[] args) : VortexError("The name '{0}' does not exist in the current context, or it might not match the required type, or it might be an internal name", ErrorType.Runtime, args);
public class ReadingUnsetValueError(params string[] args) : VortexError("The variable with indetifier'{0}' is unset (Declare using '$?' to allow for unset values)", ErrorType.Runtime, args);
public class IlegalContextDeclarationError(params string[] args) : VortexError("A new {0} may not be declared in the current context.", ErrorType.Syntax, args);
public class IlegalDeclarationError(params string[] args) : VortexError("{0}", ErrorType.Runtime, args);
public class FuncOverloadNotFoundError(params string[] args) : VortexError("No overload found for function '{0}' that takes {1} argument(s)", ErrorType.Runtime, args);
public class InvalidFormatError(params string[] args) : VortexError("Cannot convert '{0}' to {1}", ErrorType.Runtime, args);
public class StackOverflowError() : VortexError($"The call stack has exceeded the maximum size of {Interpreter.maxDepth} frames", ErrorType.Critical);
public class FileDoesNotExistError(params string[] args) : VortexError("The file '{0}' does not exist", ErrorType.Critical, args);
public class ModuleAlreadyLoadedError(params string[] args) : VortexError("Module '{0}' has already been loaded", ErrorType.Runtime, args);
public class AssigmentToReadonlyVarError(params string[] args) : VortexError("Variable '{0}' is read-only", ErrorType.Runtime, args);
public class UseOfAReservedNameError(params string[] args) : VortexError("Name '{0}' is reserved and cannot be used!", ErrorType.Runtime, args);
public class ArgumentError(params string[] args) : VortexError("{0}", ErrorType.Runtime, args);
public class IlegalOperationError(params string[] args) : VortexError("{0}", ErrorType.Runtime, args);
public class IndexOutOfBoundsError(params string[] args) : VortexError("Index '{0}' was outside of the Indexeble's bounds.", ErrorType.Runtime, args);
public class UnmatchingGroupTypeError(params string[] args) : VortexError("The group type '{0}' does not match the group type '{1}'.", ErrorType.Runtime, args);
public class AssertionFailedError(params string[] args) : VortexError("Assert failed. Expected '{0}' but got '{1}'.", ErrorType.Runtime, args);
