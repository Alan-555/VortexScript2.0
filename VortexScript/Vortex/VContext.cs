using VortexScript.Definitions;

namespace VortexScript.Vortex;

public class VContext
{
    public string Name { get; set; } = "";
    public Dictionary<string, V_Variable> Variables { set; get; } //Local variables
    public int Depth { private set; get; } //How deep we are
    public ScopeTypeEnum ScopeType { private set; get; } //Type of the scope
    public bool Ignore { set; get; } //When true, code will not be executed. Scopes won't be ignored
    public int StartLine { private set; get; } //The line that this scope was started at
    public IfState IfState { set; get; } = IfState.passed;
    public bool InTryScope { set; get; } = false;
    public VortexError? ErrorRaised { set; get; } = null;
    public VFunc? FuncBeingRead { set; get; } = null;
    public V_Variable? ReturnValue { set; get; } = null;
    public bool InAFunc { get; set; } = false;
    public bool IsMain { get; set; } = false;
    public VFile? File { get; set; }
    public string LoopCondition { get; set; } = "";
    public VContext(Dictionary<string, V_Variable> vars, VFile? file, int depth = 0, ScopeTypeEnum scopeType = ScopeTypeEnum.genericScope, bool ignore = false, int StartLine = 0)
    {
        Variables = vars;
        Depth = depth;
        ScopeType = scopeType;
        Ignore = ignore;
        this.StartLine = StartLine;
        File = file;
    }

    public void Destroy()
    {
        Name = "Released";
        Variables.Clear();
        Depth = 0;
        FuncBeingRead = null;
        ReturnValue = null;
        if (File != null)
            File.TopLevelContext = null;
        File = null;
        Variables.Clear();
    }

    public static void RenameNew(string name)
    {
        string toRemove = Interpreter.ActiveModules.Last().Key;
        Interpreter.ActiveModules.Remove(toRemove, out var context);
        Interpreter.ActiveModules.Add(name, context!);
        context.File.OverridenName = name;
        context.Name = name;
    }

    public override string ToString()
    {
        return Name;
    }
}

public enum ScopeTypeEnum
{
    topLevel = 0, //top level of a file
    ifScope = 1, //the scope immediately after an if statement 
    elseScope = 2, //the scope immediately after an else statement
    functionScope = 3, //scope inside a function
    genericScope = 4, //scope defined by the user
    tryScope = 5, //try scope of a try-catch statement
    catchScope = 6, //catch scope of a try-catch statement
    loopScope = 7,
    classScope = 8,
    internal_ = -1,
}


public enum IfState
{
    failed,
    deadBranch,
    passed,

}