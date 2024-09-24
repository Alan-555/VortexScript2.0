using System.Reflection;
using System.Runtime.ExceptionServices;
using VortexScript.Structs;
using VortexScript.Definitions;
using VortexScript.Vortex;

namespace VortexScript;

public class Interpreter
{
    //Config
    public static readonly int maxDepth = 256;
    public static readonly string version = "beta 1.0.0";
    public static readonly bool debug = false;

    public static bool itm = false;



    //Internal stuff
    public static MethodInfo[] statements = [];
    public static Dictionary<string, VContext> InternalModules = new();
    public static Dictionary<string, MethodInfo> OperToSpecialAssigment = new(){
        {"+",typeof(V_Variable).GetMethod(nameof(V_Variable.SpecialAdd))!},
        {"-",typeof(V_Variable).GetMethod(nameof(V_Variable.SpecialSub))!},
        {"*",typeof(V_Variable).GetMethod(nameof(V_Variable.SpecialMul))!},
        {"/",typeof(V_Variable).GetMethod(nameof(V_Variable.SpecialDiv))!},
        {".",typeof(V_Variable).GetMethod(nameof(V_Variable.SpecialClear))!},
    };

    //Memory
    public static Dictionary<string, VContext> ActiveModules { get; private set; } = []; //loaded modules in memory (libary)
    public static string[] keywords = []; //all reserved keywords are stored here

    public static Stack<VFrame> CallStack { get; private set; } = []; //the call stack
    string[] ITM_Buffer = [];

    //Instance    
    public VFile File { private set; get; }

    public Interpreter(VFile file)
    {
        File = file;
        if (statements.Length == 0)
            Init();
    }

    public void Init()
    {
        //load all statements
        statements = Assembly.GetAssembly(typeof(Interpreter))!.GetTypes()
                  .SelectMany(t => t.GetMethods())
                  .Where(m => m.GetCustomAttributes(typeof(MarkStatement), false).Length > 0)
                  .ToArray();
        //superglobal variables are subject of reserved keywords
        foreach (var var in SuperGlobals.SuperGlobalVars)
        {
            keywords = [.. keywords, var.Key];
        }
        //load all internal modules
        var listOfBs = (
           from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
           from type in domainAssembly.GetTypes()
           where typeof(InternalStandartLibrary).IsAssignableFrom(type)
           select type).ToArray();
        bool first = true;
        foreach (var type in listOfBs)
        {
            var method = type
            .GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(InternalFunc), false).Length > 0) //get only internal functions
            .Select(mi => new VFunc(mi.Name, null!, Utils.ConvertMethodInfoToArgs(mi), -1) { CSharpFunc = mi, returnType = (DataType)Utils.GetStatementAttribute<InternalFunc>(mi, 0).Value! })
            .ToDictionary(x => { var id = x.Identifier; id = id[0].ToString().ToLower() + id[1..]; return id; }, x => x);
            Dictionary<string, V_Variable> constants = [];
            type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .ToList()
            .ForEach(f =>
            {
                if (f.GetValue(null) != null)
                {
                    var val = (V_Variable)f.GetValue(null)!;
                    constants.Add(f.Name, val);
                }
            });
            foreach (var item in method)
            {
                constants.Add(item.Key, V_Variable.Construct(DataType.Function, item.Value));
            }
            if (first)
            {
                foreach (var item in constants)
                {
                    keywords = [.. keywords, item.Key];
                    SuperGlobals.SuperGlobalVars.Add(item.Key, () => item.Value);
                }
            }

            InternalModules.Add(type.Name[8..], new(constants, null, scopeType: ScopeTypeEnum.internal_) { Name = type.Name[8..] });
            first = false;
        }
        //add types
        var types = Enum.GetNames(typeof(DataType)).ToList();
        foreach (var type in types)
        {
            SuperGlobals.SuperGlobalVars.Add(type, () => V_Variable.Construct(DataType.Type, type));
        }
        //add errors
        var errors = GetType().Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(VortexError)));
        foreach (var error in errors)
        {
            SuperGlobals.SuperGlobalVars.Add(error.Name, () => { var x = (VType_Tag)V_Variable.Construct(DataType.GroupType, new GroupType("error", error)); return x; });
        }

    }

    //
    
    public void ExecuteFile()
    {
        if (File.Path == Program.InteractiveTermMode)
        {
            itm = true;
            while (true)
            {
                if (GetCurrentContext().Depth != 0)
                {
                    for (int i = 0; i < GetCurrentContext().Depth; i++)
                    {
                        Console.Write("｜");
                        Console.Write("\t");
                    }
                    if (DirectiveDefinition<int>.DIR_BufferMode.value)
                    {
                        List<string> list = [];
                        while (true)
                        {
                            var input = Console.ReadLine()!;
                            if (input == "endbuffer")
                                break;
                            list.Add(input);
                        }
                        ExecuteLines([.. list]);
                    }
                }
                ITM_Buffer = [..ITM_Buffer, Console.ReadLine()!];
                ExecuteLines(ITM_Buffer);
            }
        }
        else
        {
            ExecuteLines(File.ReadFile());
        }
    }

    V_Variable? ExecuteLines(string[] lines)
    {
        while(GetCurrentFrame().currentLine < lines.Length)
        {

            string line;
            line = lines[GetCurrentFrame().currentLine];
            try
            {
                ExecuteLine(line);
            }
            catch (VortexError e)
            {
                if (GetCurrentContext().InTryScope && e.type == ErrorType.Runtime)
                {
                    GetCurrentContext().ErrorRaised = e;
                    GetCurrentContext().Ignore = true;
                    GetCurrentContext().SubsequentFramesIgnore = true;
                }
                else
                    VortexError.ThrowError(e);
            }
            catch (Exception)
            {
                Console.Error.WriteLine("---------------Execution halted on internal exception---------------");
                Console.Error.WriteLine("An Interpreter error has occured, this event is extraordinary. Please report this error.");
                Console.Error.WriteLine("C# exception: ");
                throw;
            }
            if (GetCurrentFrame().StopSignal)
            {
                if (CallStack.Count == 1 && GetCurrentContext().ErrorRaised != null)
                    throw GetCurrentContext().ErrorRaised!;
                CallStack.Pop();
                return null;

            }
            GetCurrentFrame().currentLine++;

            if (GetCurrentContext().ReturnValue != null)
            {
                return GetCurrentContext().ReturnValue;
            }

            if (GetCurrentContext().ErrorRaised != null)
                VortexError.ThrowError(GetCurrentContext().ErrorRaised!);
        }
        if (GetCurrentContext().Depth != 0 && !itm)
            {
                if (GetCurrentContext().FuncBeingRead != null)
                    VortexError.ThrowError(new FunctionBodyLeakError((GetCurrentContext().StartLine + 1).ToString()));
                else
                    VortexError.ThrowError(new ScopeLeakError((GetCurrentContext().StartLine + 1).ToString()));
        }
        return null;
    }

    public void ExecuteLine(string line)
    {
        line = Utils.RemoveInlineComments(line);
        line = line.Trim();
        var func = GetCurrentContext().FuncBeingRead;
        if (func != null)
            func.FunctionBody = [.. func.FunctionBody, line];
        if (line == "endbuffer")
        {
            throw new IlegalOperationError("No buffer opened or not in ITM");
        }
        ExecuteStatement(line);

    }
    public void ExecuteStatement(string statement)
    {
        if (statement == "") return;
        MethodInfo? statementToExec = null;
        foreach (var statement_ in statements)
        {
            if (statement.StartsWith(Utils.GetStatementAttribute(statement_, StatementAttributes.beginsWith).ToString().Replace("\"", "")))
            {
                statementToExec = statement_;
                break;
            }
        }
        try
        {
            if (statementToExec == null)
            {
                if (GetCurrentContext().Ignore)
                {
                    return;
                }
                //check for assignment
                if (AssignStatement(statement))
                    return;
                else
                if (FuncDeclaration(statement))
                    return;
                else
                if (CallFunctionStatement(statement, out _))
                {
                    return;
                }
                else
                {
                    if (itm)
                    {
                        try
                        {
                            Console.WriteLine("> " + Evaluator.Evaluate(statement));
                        }
                        catch
                        {
                            throw new UnknownStatementError(statement);
                        }
                        return;
                    }
                    else
                        throw new UnknownStatementError(statement);
                }
            }
            bool endsScope = (bool)Utils.GetStatementAttribute(statementToExec, StatementAttributes.endScope).Value!;
            bool startsScope = (bool)Utils.GetStatementAttribute(statementToExec, StatementAttributes.mewScope).Value!;
            //border statement
            if (endsScope && startsScope)
            {
                if (statementToExec.Name == "ElseScopeStatement")
                {
                    var context = GetCurrentContext();
                    if (context.ScopeType != ScopeTypeEnum.ifScope)
                    {
                        throw new IlegalStatementContextError("else", context.ScopeType.ToString());
                    }
                    CloseContext();
                    OpenNewContext(ScopeTypeEnum.elseScope);
                    GetCurrentContext().FuncBeingRead = context.FuncBeingRead;
                    GetCurrentContext().InTryScope = context.InTryScope;
                    if (context.SubsequentFramesIgnore)
                    {
                        GetCurrentContext().Ignore = true;
                        GetCurrentContext().SubsequentFramesIgnore = true;
                    }
                    else
                        GetCurrentContext().Ignore = !context.Ignore;
                }
                else if (statementToExec.Name == "ElseIfScopeStatement")
                {
                    var context = GetCurrentContext();
                    if (context.ScopeType != ScopeTypeEnum.ifScope)
                    {
                        throw new IlegalStatementContextError("else if", context.ScopeType.ToString());
                    }
                    CloseContext();
                    OpenNewContext(ScopeTypeEnum.ifScope);
                    GetCurrentContext().FuncBeingRead = context.FuncBeingRead;
                    GetCurrentContext().InTryScope = context.InTryScope;
                    GetCurrentContext().InAFunc = context.InAFunc;
                    if (context.SubsequentFramesIgnore)
                    {
                        GetCurrentContext().Ignore = true;
                        GetCurrentContext().SubsequentFramesIgnore = true;
                    }
                    else
                        GetCurrentContext().Ignore = !context.Ignore;
                }
                else if (statementToExec.Name == "CatchScopeStartStement")
                {
                    var tryContext = GetCurrentContext();
                    if (tryContext.ScopeType != ScopeTypeEnum.tryScope && tryContext.ScopeType != ScopeTypeEnum.catchScope)
                    {
                        throw new IlegalStatementContextError("catch", tryContext.ScopeType.ToString());
                    }
                    CloseContext();
                    if (tryContext.ErrorRaised == null)
                    {
                        OpenNewContext(ScopeTypeEnum.catchScope);
                        GetCurrentContext().FuncBeingRead = tryContext.FuncBeingRead;
                        GetCurrentContext().InAFunc = tryContext.InAFunc;
                        GetCurrentContext().SubsequentFramesIgnore = true;
                        GetCurrentContext().Ignore = true;
                    }
                    else
                    {
                        Type[] errorType = [];
                        bool any = false;
                        if (statement == "catch :")
                            any = true;
                        else
                            try
                            {
                                var cases = string.Join(' ', Utils.StringSplit(statement, ' ')[1..]);
                                cases = cases.Replace(" :", string.Empty);

                                var casesArray = Utils.ArgsEval(cases, ',', oneType: DataType.GroupType);

                                errorType = casesArray!.Select(x => (Type)((GroupType)x.value).value).ToArray();
                            }
                            catch (VortexError e)
                            {
                                VortexError.ThrowError(e);
                                return;
                            }
                        if (tryContext.ErrorRaised.type != ErrorType.Runtime)
                        {
                            throw new IlegalOperationError("Only runtime errors can be caught");
                        }
                        if (any || errorType.Contains(tryContext.ErrorRaised.GetType()))
                        {
                            OpenNewContext(ScopeTypeEnum.catchScope);
                            GetCurrentContext().FuncBeingRead = tryContext.FuncBeingRead;
                            GetCurrentContext().InAFunc = tryContext.InAFunc;
                        }
                        else
                        {
                            GetCurrentContext().ErrorRaised = tryContext.ErrorRaised;
                            OpenNewContext(ScopeTypeEnum.catchScope);
                            GetCurrentContext().FuncBeingRead = tryContext.FuncBeingRead;
                            GetCurrentContext().InAFunc = tryContext.InAFunc;
                            GetCurrentContext().SubsequentFramesIgnore = true;
                            GetCurrentContext().Ignore = true;
                            GetCurrentContext().ErrorRaised = tryContext.ErrorRaised;
                        }


                    }
                }
            }
            else
            if (endsScope)
            {
                var context = GetCurrentContext();
                CloseContext();
                if (context.ErrorRaised != null)
                {
                    GetCurrentContext().ErrorRaised = context.ErrorRaised;
                    GetCurrentContext().Ignore = true;
                    GetCurrentContext().SubsequentFramesIgnore = true;
                }
                if (context.ScopeType == ScopeTypeEnum.catchScope && !context.Ignore)
                {
                    GetCurrentContext().Ignore = true;
                    GetCurrentContext().SubsequentFramesIgnore = true;
                    GetCurrentContext().ErrorRaised = null;
                }
                if(context.ScopeType == ScopeTypeEnum.loopScope&&!context.Ignore){
                    if((bool)Evaluator.Evaluate(context.LoopCondition, DataType.Bool).value){
                        GetCurrentFrame().currentLine = context.StartLine-1;
                    }
                }
            }
            else
            if (startsScope)
            {
                var prevContext = GetCurrentContext();
                OpenNewContext((ScopeTypeEnum)Utils.GetStatementAttribute(statementToExec, StatementAttributes.scopeType).Value!);
                //inhirit igonre flag
                GetCurrentContext().Ignore = prevContext.Ignore;
                //inhirit function
                GetCurrentContext().FuncBeingRead = prevContext.FuncBeingRead;
                GetCurrentContext().InAFunc = prevContext.InAFunc;
                GetCurrentContext().InTryScope = prevContext.InTryScope;
                if (prevContext.Ignore)
                {
                    GetCurrentContext().SubsequentFramesIgnore = true;
                }
            }
            if (!GetCurrentContext().Ignore)
                statementToExec.Invoke(this, [statement]);
        }
        catch (VortexError)
        {
            throw;
        }
        catch (Exception e)
        {
            if (e.InnerException != null)
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            else
                ExceptionDispatchInfo.Capture(e).Throw();
        }
    }

    public VContext OpenNewContext(ScopeTypeEnum type)
    {

        var newC = new VContext([], null, GetCurrentFrame().ScopeStack.Count, type, StartLine: GetCurrentFrame().currentLine);
        GetCurrentFrame().ScopeStack.Push(newC);
        return newC;
    }
    public void CloseContext()
    {
        if (GetCurrentContext().FuncBeingRead != null && GetCurrentContext().FuncTopLevel)
        {
            FinishFuncDeclaration();
        }
        else
        {
            if (GetCurrentContext().ScopeType == ScopeTypeEnum.topLevel)
            {
                throw new UnexpectedTokenError(";").SetInfo("Top level statement may not be closed. Use exit instead");
            }
            GetCurrentFrame().ScopeStack.Pop();

        }
    }


    public static VFrame GetCurrentFrame()
    {
        return CallStack.First();
    }
    public static VContext GetCurrentContext()
    {
        if (!GetCurrentFrame().ScopeStack.TryPeek(out _))
        {
            return new([], null);
        }
        return GetCurrentFrame().ScopeStack.First();
    }
    public bool AssignStatement(string statement)
    {
        if (Utils.StringContains(statement, "="))
        {
            if (statement.Length == 1)
            {
                throw new ExpectedTokenError("identifier");
            }
            var middle = Utils.StringIndexOf(statement, "=");
            if (middle < 1)
            {
                throw new UnexpectedTokenError("=").SetInfo("Identifier expected prior");

            }
            if (middle + 1 == statement.Length)
            {
                throw new UnexpectedEndOfStatementError("Expression");
            }
            string identifier="";
            int i =0;
            while(Evaluator.identifierValidChars.Contains(statement[i])){
                identifier+=statement[i];
                i++;
            }
            string special = statement[i..middle].Trim();
            string expression = statement[(middle + 1)..];
            if (Utils.IsIdentifierValid(identifier))
            {
                if(OperToSpecialAssigment.TryGetValue(special,out var spec)){
                    if(!ReadVar(identifier,out var var)){
                        throw new UnknownNameError(identifier);
                    }
                    
                    spec.Invoke(var,[Evaluator.Evaluate(expression)]);
                    return true;
                }


                if (SetVar(identifier, Evaluator.Evaluate(expression)))
                {
                    return true;
                }
                else
                {
                    throw new UnknownNameError(identifier);
                }
            }
        }
        return false;
    }
    public bool FuncDeclaration(string statement)
    {

        if (Utils.StringContains(statement, "(") && Utils.StringContains(statement, ")") && statement.EndsWith(" :"))
        {
            if (GetCurrentContext().FuncBeingRead != null || GetCurrentContext().ScopeType != ScopeTypeEnum.topLevel)
            {
                throw new IlegalContextDeclarationError("function").SetInfo("Functions may only be declared at the top level");
            }
            int argsStart = Utils.StringIndexOf(statement, "(");
            int argsEnd = Utils.StringIndexOf(statement, ")");
            if (argsStart == 0)
            {
                throw new UnexpectedTokenError("(").SetInfo("Identifier expected prior");
            }
            else if (argsStart == -1)
            {
                throw new ExpectedTokenError("(");
            }
            if (argsEnd == -1)
            {
                throw new ExpectedTokenError(")");
            }
            string identifier = statement[0..statement.IndexOf('(')];
            if (!Utils.IsIdentifierValid(identifier,true))
            {
                throw new InvalidIdentifierError(identifier);
            }
            string args = statement[(argsStart + 1)..argsEnd];
            var argsArray = args.Split(',');
            if (argsArray.Length == 1 && argsArray[0] == "")
            {
                argsArray = [];
            }
            List<VFuncArg> argsList = new List<VFuncArg>();
            foreach (var arg_ in argsArray)
            {
                var ident = arg_.Trim();
                var index = Utils.StringIndexOf(ident," ");
                DataType? type = null;
                if(index!=-1){
                    type = (DataType)Evaluator.Evaluate(ident[..index], DataType.Type).value;
                    ident = ident[(index+1)..];
                }
                if (!Utils.IsIdentifierValid(ident,true))
                {
                    throw new InvalidIdentifierError(ident);
                }
                if (argsList.Any(x => x.name == ident))
                {
                    throw new DuplicateVariableError(ident);
                }
                if(type.HasValue){
                    argsList.Add(new(ident){enforcedType=(DataType)type});                
                }
                else{
                    argsList.Add(new(ident));                
                }
            }
            VFunc func = new(identifier, File, [.. argsList], GetCurrentFrame().currentLine);
            var c = OpenNewContext(ScopeTypeEnum.functionScope);
            c.Ignore = true;
            c.SubsequentFramesIgnore = true;
            c.FuncBeingRead = func;
            c.FuncTopLevel = true;
            return true;
        }
        return false;
    }
    public void FinishFuncDeclaration()
    {
        var func = GetCurrentContext().FuncBeingRead;
        func!.FunctionBody = func.FunctionBody[..^1];
        GetCurrentFrame().ScopeStack.Pop();
        if (!DefineFunc(func.Identifier, func))
        {
            throw new IdentifierAlreadyUsedError(func.Identifier);
        }

    }
    public static bool CallFunctionStatement(string statement, out V_Variable? val, VContext? context = null)
    {
        if (Utils.StringContains(statement, "(") && Utils.StringContains(statement, ")") && !statement.EndsWith(" :"))
        {
            int argsStart = Utils.StringIndexOf(statement, "(");
            int argsEnd = Utils.StringLastIndexOf(statement, ')');
            if (argsEnd != statement.Length - 1)
            {
                throw new UnexpectedTokenError(statement[(argsEnd + 1)..]);
            }
            if (argsStart == 0)
            {
                throw new UnexpectedTokenError("=").SetInfo("Function identifier expected prior");
            }
            else if (argsStart == -1)
            {
                throw new ExpectedTokenError("(");
            }
            if (argsEnd == -1)
            {
                throw new ExpectedTokenError(")");
            }
            string identifier = statement[0..statement.IndexOf('(')];
            int i = Utils.StringIndexOf(identifier, ".");
            var module = "";
            if (i != -1 && i < argsStart)
            {
                module = identifier[..i];
                identifier = identifier[(i + 1)..];
            }
            if (!Utils.IsIdentifierValid(identifier))
            {
                val = null;
                return false;
            }
            if (module != "")
            {
                if (!TryGetModule(module, out context))
                {
                    throw new UnknownNameError(module);
                }
            }
            if (!ReadVar(identifier, out var func_, type: DataType.Function))
            {
                throw new UnknownNameError(identifier);
            }
            if (func_!.value is not VFunc func)
                throw new InvalidCastException("Function is not a VFunc");
            string args = statement[(argsStart + 1)..argsEnd];
            var argsArray = Utils.ArgsEval(args, ',', func.Args.Select(t => t.enforcedType).ToArray()) ?? throw new FuncOverloadNotFoundError(func.Identifier, Utils.StringSplit(args, ',').Length.ToString());
            if (argsArray.Count != func.Args.Length)
            {//TODO: defualt params
                throw new FuncOverloadNotFoundError(func.Identifier, argsArray.Count.ToString());
            }
            Dictionary<string, V_Variable> argsList = [];
            i = 0;
            foreach (var arg in argsArray)
            {
                argsList.Add(func.Args[i].name, arg);
                i++;
            }
            val = CallFunction(func, argsList);
            if (itm)
            {
                var val_ = val == null || val.value == null ? "none" : val.value;
                Console.WriteLine("< " + val_.ToString());

            }
            return true;
        }
        val = null;
        return false;
    }

    public static V_Variable? CallFunction(VFunc func, Dictionary<string, V_Variable> args)
    {
        if (func.CSharpFunc != null)
        {
            var args_ = args.Select(x => x.Value.value).ToArray();
            int i = 0;
            foreach (var arg in func.CSharpFunc.GetParameters())
            {
                if (arg.ParameterType == typeof(string))
                {
                    args_[i] = args_[i].ToString()!;
                }
                else if (arg.ParameterType == typeof(V_Variable))
                {
                    args_[i] = args.ElementAt(i).Value;
                }
                else if (arg.ParameterType == typeof(bool))
                {
                    args_[i] = (bool)args.ElementAt(i).Value.value == true;
                }
                else if (arg.ParameterType == typeof(int))
                {
                    args_[i] = (int)Math.Floor((double)args.ElementAt(i).Value.value);
                }
                /*else if(((Type)args_[i]).IsSubclassOf(typeof(VortexError))){
                     args_[i] = V_Variable.Construct(DataType.Error, args_[i]);
                }*/
                else
                {
                    args_[i] = args_[i];
                }
                i++;
            }
            object? val_ = null;
            try
            {
                val_ = func.CSharpFunc.Invoke(null, args_);
            }
            catch (ArgumentException)
            {
                throw new FuncOverloadNotFoundError(func.Identifier, "-");
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException != null)
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                else
                    ExceptionDispatchInfo.Capture(e).Throw();
            }
            catch (VortexError)
            {
                throw;
            }
            if (val_ is V_Variable v)
            {
                return v;
            }
            return V_Variable.Construct(func.returnType, val_);
        }
        NewFrame(func.File, ScopeTypeEnum.functionScope, func.StartLine + 1, func.GetFullPath()).currentLine = 0;
        Interpreter funcInterpreter = new(func.File);
        foreach (var arg in args)
        {
            DeclareVar(arg.Key, arg.Value);
        }
        GetCurrentContext().InAFunc = true;
        var val = funcInterpreter.ExecuteLines(func.FunctionBody);
        CallStack.Pop();
        return val;
    }
    //
    [MarkStatement("$", false)]
    public void DeclareStatement(string statement)
    {
        //statement = statement.Replace(":connect","=");
        statement = Utils.StringRemoveSpaces(statement);
        if (statement.Length < 2)
        {
            throw new ExpectedTokenError("Var modifier or an identifier");
        }
        bool unsetable = statement[1] == '?';
        bool readonly_ = statement[1] == '!';
        bool unlink = statement[1] == '$';
        if (unsetable || readonly_)
        {
            statement = statement.Remove(1, 1);
        }
        if (unlink)
        {
            string identifer_ = statement[2..];
            bool removed = false;
            foreach (var context in GetCurrentFrame().ScopeStack)
            {
                removed = context.Variables.Remove(identifer_);
                if (removed) return;
            }
            if (!removed)
            {
                throw new UnknownNameError(identifer_);
            }
        }
        bool init = Utils.StringContains(statement, "=");
        string identifier = init ? statement[1..statement.IndexOf('=')] : statement[1..];
        if (!Utils.IsIdentifierValid(identifier,true))
        {
            throw new InvalidIdentifierError(identifier);
        }
        if (init)
        {
            if (statement.EndsWith('='))
            {
                throw new UnexpectedEndOfStatementError("Expression");
            }
            var initVal = Evaluator.Evaluate(statement[(statement.IndexOf('=') + 1)..]);
            initVal.flags.unsetable = unsetable;
            initVal.flags.readonly_ = readonly_;
            bool failed = false;

            if (!DeclareVar(identifier, initVal))
                failed = true;

            if (failed)
            {
                throw new IdentifierAlreadyUsedError(identifier);
            }
        }
        else
        {
            if (readonly_)
            {
                throw new IlegalDeclarationError("A read-only variable has to be initialized");
            }
            if (!DeclareVar(identifier, V_Variable.Construct(DataType.Unset, "unset", new() { unsetable = unsetable })))
            {
                throw new IdentifierAlreadyUsedError(identifier);
            }
        }

    }

    [MarkStatement("#FAIL#", false)]
    public void DebugFail(string statement)
    {
        throw new Exception("MANUAL FAIL TRIGGERED");
    }
    [MarkStatement(">", false)]
    public void OutputStatement(string statement)
    {
        Console.WriteLine(Evaluator.Evaluate(statement[1..]).ToString());
    }

    [MarkStatement("<", false)]
    public void FuncReturnStatement(string statement)
    {
        if (GetCurrentContext().InAFunc)
        {
            GetCurrentContext().ReturnValue = Evaluator.Evaluate(statement[1..]);
        }
        else
        {
            throw new UnexpectedTokenError("<").SetInfo("Return token may not be used outside of a function");
        }
    }

    [MarkStatement("if", true, ScopeTypeEnum.ifScope)]
    public void IfStatement(string statement)
    {
        if (statement.Length < 4)
        {
            throw new UnexpectedEndOfStatementError("Expression");
        }
        if (statement[2] != ' ')
        {
            throw new ExpectedTokenError(" ");
        }
        if (!statement.EndsWith(" :"))
        {
            throw new ExpectedTokenError(" :");
        }
        int end = Utils.StringIndexOf(statement, " :");
        string expression = statement[3..end];
        bool result = (bool)Evaluator.Evaluate(expression, DataType.Bool).value;
        GetCurrentContext().Ignore = !result;
        if (result)
        {
            GetCurrentContext().SubsequentFramesIgnore = true;
        }
    }
    [MarkStatement(":", true, scopeType: ScopeTypeEnum.genericScope)]
    public void GenericStatementStart(string statement)
    {

    }
    [MarkStatement("try :", true, scopeType: ScopeTypeEnum.tryScope)]
    public void TryScopeStartStement(string statement)
    {
        GetCurrentContext().InTryScope = true;
    }
    [MarkStatement("catch ", true, scopeType: ScopeTypeEnum.catchScope, true)]
    public void CatchScopeStartStement(string statement)
    {

    }
    [MarkStatement(";", false, endsScope: true)]
    public void EndScopeStatement(string statement)
    {

    }
    [MarkStatement("exit", false)]
    public void ExitProgrammStatement(string statement)
    {
        Environment.Exit(0);
    }
    [MarkStatement("else :", true, ScopeTypeEnum.elseScope, true)]
    public void ElseScopeStatement(string statement)
    {

    }
    [MarkStatement("else if", true, ScopeTypeEnum.elseScope, true)]
    public void ElseIfScopeStatement(string statement)
    {
        if (statement[7] != ' ')
        {
            throw new ExpectedTokenError(" ");
        }
        if (!statement.EndsWith(" :"))
        {
            throw new ExpectedTokenError(" :");
        }
        int end = Utils.StringIndexOf(statement, " :");
        string expression = statement[7..end];
        bool result = (bool)Evaluator.Evaluate(expression, DataType.Bool).value;
        GetCurrentContext().Ignore = !result;
        if (result)
        {
            GetCurrentContext().SubsequentFramesIgnore = true;
        }
    }

    [MarkStatement("acquire ", false)]
    public void AccessStatement(string statement)
    {
        if (GetCurrentContext().ScopeType != ScopeTypeEnum.topLevel)
        {
            throw new IlegalStatementContextError("Acquire", GetCurrentContext().ScopeType.ToString()).SetInfo("Acquire statement may only be used at the top level");
        }
        var file = statement[8..] + ".vort";
        VFile file_ = new(file);

        if (!file_.Exists())
        {
            throw new FileDoesNotExistError(file);
        }
        if (ActiveModules.ContainsKey(file_.GetFileName()[0].ToString().ToUpper() + file_.GetFileName()[1..]))
        {
            throw new ModuleAlreadyLoadedError(file_.GetFileName());
        }
        file_.InterpretThisFile();
        CallStack.Pop();

    }
    [MarkStatement("safeacquire ", false)]
    public void AccessOnceStatement(string statement)
    {
        if (GetCurrentContext().ScopeType != ScopeTypeEnum.topLevel)
        {
            throw new IlegalStatementContextError("Safeacquire", GetCurrentContext().ScopeType.ToString()).SetInfo("Safeacquire statement may only be used at the top level");
        }
        var file = statement[12..] + ".vort";
        VFile file_ = new(file);
        if (ActiveModules.ContainsKey(file_.GetFileName()[0].ToString().ToUpper() + file_.GetFileName()[1..]))
        {
            return;
        }

        if (!file_.Exists())
        {
            throw new FileDoesNotExistError(file);
        }
        file_.InterpretThisFile();
        CallStack.Pop();

    }
    [MarkStatement("release ", false)]
    public void UnloadStatement(string statement)
    {
        var file = statement[8..];
        VContext? value = null;
        try
        {
            value = (VContext)Evaluator.Evaluate(file, DataType.Module).value;
        }
        catch { }
        if (value == null && !ActiveModules.TryGetValue(file, out value))
        {
            throw new UnknownNameError(file);
        }
        if (GetCurrentFrame().VFile.TopLevelContext == value)
        {
            throw new IlegalStatementContextError("Release", GetCurrentContext().ScopeType.ToString()).SetInfo("Cannot release the currently executed module");
        }
        if (value.IsMain)
        {
            throw new IlegalStatementContextError("Release", GetCurrentContext().ScopeType.ToString()).SetInfo("Cannot release the entrypoint module");
        }
        ActiveModules.Remove(value.Name);
        value.Destroy();
    }

    [MarkStatement("#", false)]
    public void SetDirectiveStatement(string statement)
    {
        var directive = statement[1..].Split(' ');
        if (directive.Length < 2)
        {
            throw new ExpectedTokenError("directive and value");
        }
        else if (directive.Length > 2)
        {
            throw new UnexpectedTokenError(directive[2]);
        }
        string directiveName = directive[0];
        string value = directive[1];
        var thing = DirectiveDefinition<int>.GetDirectiveField("DIR_" + directiveName[0].ToString().ToUpper() + directiveName[1..], out var type);
        var value_ = Evaluator.Evaluate(value, type);
        var fieldValue = thing.GetValue(null);
        var valueField = fieldValue.GetType().GetField("value", BindingFlags.Instance | BindingFlags.Public);
        if (valueField != null && valueField.FieldType.IsAssignableFrom(value_.value.GetType()))
        {
            valueField.SetValue(fieldValue, value_.value);
        }
        else
        {
            throw new ArgumentException($"Value type mismatch: Expected {valueField.FieldType}, got {value.GetType()}");
        }


    }

    [MarkStatement("raise ", false)]
    public void ThrowStatement(string statement)
    {
        var args = Utils.StringSplit(statement, ' ')[1..];
        if (args.Length == 2)
        {
            var type = Evaluator.Evaluate(args[0], DataType.GroupType);
            var message = Evaluator.Evaluate(args[1], DataType.String);
            InternalStandartLibrary.ThrowError(type, message.value.ToString());
        }
        else if (args.Length == 1)
        {
            var error = Evaluator.Evaluate(args[0], DataType.Error);
            throw (VortexError)error.value;
        }
        else
        {
            throw new UnexpectedTokenError(args[2]);
        }
    }

    [MarkStatement("assert ", false)]//assert 5==5   assert 5 8
    public void AssertStatement(string statement)
    {
        var args = Utils.StringSplit(statement, ' ')[1..];
        if (args.Length == 2)
        {
            var val1 = Evaluator.Evaluate(args[0], DataType.Any);
            var val2 = Evaluator.Evaluate(args[1], DataType.Any);
            var value = val1.ToString() == val2.ToString();
            if (!value)
            {
                throw new AssertionFailedError(val1.ToString(), val2.ToString());
            }
        }
        else if (args.Length == 1)
        {
            var value = (bool)Evaluator.Evaluate(args[0], DataType.Bool).value;
            if (!value)
            {
                throw new AssertionFailedError("true", "false");
            }

        }
        else
        {
            throw new UnexpectedTokenError(args[2]);
        }
    }

    [MarkStatement("while ", true, ScopeTypeEnum.loopScope)]
    public void WhileStatement(string statement)
    {
        var condition = Utils.StringSplit(statement, ' ')[1];
        GetCurrentContext().LoopCondition = condition;
        if((bool)Evaluator.Evaluate(condition,DataType.Bool).value==false){
            GetCurrentContext().SubsequentFramesIgnore = true;
            GetCurrentContext().Ignore = true;
        }
        
    }
    [MarkStatement("break", false)]
    public void BreakStatement(string statement)
    {
        bool found = false;
        foreach (var context in GetCurrentFrame().ScopeStack)
        {
            context.Ignore = true;
            context.SubsequentFramesIgnore = true;
            if(context.ScopeType==ScopeTypeEnum.loopScope){
                found = true;
            }
        }
        if(!found){
            throw new IlegalOperationError("Could not find a loop to break from");
        }
    }


    public static T ConvertToGeneric<T>(object obj)
    {
        return (T)obj;
    }
    public static bool ReadVar(string identifier, out V_Variable? val, VContext? context = null, DataType type = DataType.Any)
    {
        Dictionary<string, V_Variable> vars;
        if (context == null)
        {
            //gets all variables in the current frame
            vars = Utils.GetAllVars();
        }
        else
        {
            //use the context variables
            vars = context.Variables;
        }
        //try to read a module
        if(context==null && (ActiveModules.TryGetValue(identifier, out var module)||InternalModules.TryGetValue(identifier, out module))){
            val = V_Variable.Construct(DataType.Module,module,new(){readonly_=true});
            return true;
        }
        //if no context is present try to read a superglobal
        if (context == null)
        {
            if (TryGetSuperGlobal(identifier, out val, type))
                return true;
        }

        //try to read a variable from chosen context
        var res = vars.TryGetValue(identifier, out val);
        if (!res)
        {
            //if we are in an internal function or the context is set, skip reading top level context of the current frame
            if (context?.ScopeType == ScopeTypeEnum.internal_ || context==null)
            {
                return false;
            }
            //try to read a variable from the top level context
            if (!GetCurrentFrame().VFile.TopLevelContext.Variables.TryGetValue(identifier, out val))
            {
                return false;
            }
            else
            {
                res = true;
            }
        }

        if (!val!.flags.unsetable && val.type == DataType.Unset)
            throw new ReadingUnsetValueError(identifier);
        if (type != DataType.Any && val.type != type)
            throw new UnmatchingDataTypeError(identifier, type.ToString());
        return res;
    }
    public static bool TryGetSuperGlobal(string identifier, out V_Variable? val, DataType type = DataType.Any)
    {
        if (SuperGlobals.SuperGlobalVars.TryGetValue(identifier, out var l))
        {
            val = l();
            if (type != DataType.Any && val.type != type)
                throw new UnmatchingDataTypeError(val.type.ToString(), type.ToString());
            return true;
        }
        val = null;
        return false;
    }
    public static bool TryGetModule(string name, out VContext module)
    {
        return ActiveModules.TryGetValue(name, out module) || InternalModules.TryGetValue(name, out module);
    }
    public bool SetVar(string identifier, V_Variable value, VContext? scope = null)
    {
        scope ??= GetCurrentContext();
        if (scope.Variables.ContainsKey(identifier))
        {
            scope.Variables[identifier] = value;
            return true;
        }
        return false;
    }
    public static bool SetVar(string identifier, V_Variable v)
    {
        foreach (var Context in GetCurrentFrame().ScopeStack)
        {
            if (Context.Variables.TryGetValue(identifier, out V_Variable old))
            {
                if (old.flags.readonly_)
                    throw new AssigmentToReadonlyVarError(identifier);
                v.flags.unsetable = old.flags.unsetable;
                v.flags.readonly_ = old.flags.readonly_;
                Context.Variables[identifier] = v;
                return true;
            }
        }
        return false;
    }
    public static bool ArrayAdd(string identifier, V_Variable value)
    {
        foreach (var Context in GetCurrentFrame().ScopeStack)
        {
            if (Context.Variables.TryGetValue(identifier, out V_Variable old))
            {
                if (old.flags.readonly_)
                    throw new AssigmentToReadonlyVarError(identifier);
                try
                {
                    (Context.Variables[identifier].value as VArray).Add(value);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
        return false;
    }
    public static bool ArrayRemove(string identifier, V_Variable value)
    {
        foreach (var Context in GetCurrentFrame().ScopeStack)
        {
            if (Context.Variables.TryGetValue(identifier, out V_Variable old))
            {
                if (old.flags.readonly_)
                    throw new AssigmentToReadonlyVarError(identifier);
                (old.value as VArray)!.RemoveAll(x => x.value.Equals(value.value));
            }
        }
        return false;
    }
    public static bool SetSpecial(string identifier, V_Variable v, SpecialAssigment sa)//TODO: refactor
    {
        V_Variable old = null;
        foreach (var Context in GetCurrentFrame().ScopeStack)
        {
            if (Context.Variables.TryGetValue(identifier, out old))
            {
                if (old.flags.readonly_)
                    throw new AssigmentToReadonlyVarError(identifier);
            }
        }
        if (old.Equals(default(V_Variable)))
        {
            return false;
        }
        string oper = "";
        switch (sa)
        {
            case SpecialAssigment.Add:
                oper = "+";
                break;
            case SpecialAssigment.Remove:
                oper = "-";
                break;
            case SpecialAssigment.Clear:
                break;
            case SpecialAssigment.Mult:
                oper = "*";
                break;
            case SpecialAssigment.Div:
                oper = "/";
                break;
        }
        if (old.type == DataType.Array)
        {
            if (oper == "+")
            {
                return ArrayAdd(identifier, v);
            }
            else if (oper == "-")
            {
                ArrayRemove(identifier, v);
                return true;
            }
            else
            {
                throw new IlegalOperationError("Operator " + oper + " is not a valid array operator for compound assigment");
            }
        }
        else
        {
            string expression = (old.type == DataType.String ? "\"" + old.value + "\"" : old.value) + oper + (v.type == DataType.String ? "\"" + v.value + "\"" : v.value);
            var result = Evaluator.Evaluate(expression);
            return SetVar(identifier, result);
        }
    }

    public static bool DeclareVar(string identifier, V_Variable var)
    {
        return GetCurrentContext().Variables.TryAdd(identifier, var);
    }
    public static bool DefineFunc(string identifier, VFunc var)
    {
        return DeclareVar(identifier, V_Variable.Construct(DataType.Function, var));
    }
    public static VFrame NewFrame(VFile file, ScopeTypeEnum scopeType, int lineOffset, string name)
    {
        if (CallStack.Count == maxDepth)
        {
            throw new StackOverflowError();
        }
        VFrame frame = new(file, lineOffset, name);
        frame.ScopeStack.Push(new([], file, 0, scopeType, StartLine: GetCurrentFrame().currentLine) { InTryScope = GetCurrentContext().InTryScope });
        CallStack.Push(frame);
        return frame;
    }
}

enum StatementAttributes
{
    beginsWith = 0,
    mewScope = 1,
    scopeType = 2,
    endScope = 3
}

public enum SpecialAssigment
{
    Add,
    Remove,
    Clear,
    Mult,
    Div,

}