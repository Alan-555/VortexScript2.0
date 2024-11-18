using System.Reflection;
using System.Runtime.ExceptionServices;
using VortexScript.Structs;
using VortexScript.Definitions;
using VortexScript.Vortex;
using System.Diagnostics;
using VortexScript.Lexer.LexerStructs;
using VortexScript.Lexer;

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
    CompiledStatement[] ITM_Buffer = [];
    public static int BufferDepth = 0;

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
            MethodInfo mi_;
            var method = type
            .GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(InternalFunc), false).Length > 0) //get only internal functions
            .Select(mi => new VFunc(mi.Name, null!, Utils.ConvertMethodInfoToArgs(mi), -1) { ForceUppercase = (bool)Utils.GetStatementAttribute<InternalFunc>(mi, 1).Value!, CSharpFunc = mi, returnType = (DataType)Utils.GetStatementAttribute<InternalFunc>(mi, 0).Value! })
            .ToDictionary(x =>
            {
                var id = x.Identifier;
                id = x.ForceUppercase ? id : id[0].ToString().ToLower() + id[1..];
                return id;
            }, x => x);
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
                constants.Add(item.Key, V_Variable.Construct(DataType.Function, item.Value, new() { readonly_ = true }));
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
            keywords = keywords.Append(type.Name[8..]).ToArray();
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
                    if (BufferDepth == 0) BufferDepth++;
                    while (BufferDepth != 0)
                    {
                        for (int i = 0; i < BufferDepth; i++)
                        {
                            Console.Write("｜");
                            Console.Write("\t");
                        }
                        try{
                            ItmRead();
                        }
                        catch(VortexError e){
                            if(e.type == ErrorType.Syntax)
                                continue;
                            throw;
                        }
                        if (LexicalAnalyzer.GetStatementType(ITM_Buffer.Last().id).StartsNewScope)
                        {
                            BufferDepth++;
                        }
                        if (LexicalAnalyzer.GetStatementType(ITM_Buffer.Last().id).EndsScope)
                        {
                            BufferDepth--;
                        }
                    }
                    ExecuteStatements(ITM_Buffer);
                }
                else
                {
                    try{
                        ItmRead();
                    }catch(VortexError e){
                        if(e.type == ErrorType.Syntax)continue;
                        throw;
                    }
                    ExecuteStatements(ITM_Buffer);

                }
            }
        }
        else
        {
            var file = File.ReadFile();
            ExecuteStatements([.. file]);
        }
    }

    public void ItmRead()
    {
        var line = Console.ReadLine()!;
        try
        {
            ITM_Buffer = [.. ITM_Buffer, LexicalAnalyzer.TokenizeStatement(line)];
        }
        catch (UnknownStatementError)
        {
            Console.WriteLine("> " + Evaluator.Evaluate(line).value.ToString());
            ITM_Buffer = [.. ITM_Buffer, new(StatementId.PASS, [])];
        }
        catch(VortexError){
            throw;
        }
    }

    V_Variable? ExecuteStatements(CompiledStatement[] statements)
    {
        while (GetCurrentFrame().currentLine < statements.Length)
        {

            CompiledStatement statement;
            statement = statements[GetCurrentFrame().currentLine];
            try
            {
                ExecuteStatement(statement);
            }
            catch (VortexError e)
            {
                if (GetCurrentContext().InTryScope && e.type == ErrorType.Runtime)
                {
                    GetCurrentContext().ErrorRaised = e;
                    GetCurrentContext().Ignore = true;
                }
                else
                {
                    VortexError.ThrowError(e);//TODO: fix itm error handeling
                    if (itm && GetCurrentFrame() != CallStack.First())
                    {
                        CallStack.Pop();
                        return null;
                    }
                }
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
    public void ExecuteStatement(CompiledStatement statement)
    {
        var func = GetCurrentContext().FuncBeingRead;
        if (func != null)
            func.FunctionBody = [.. func.FunctionBody, statement];
        if (statement.id == StatementId.PASS) return;
        MethodInfo? statementToExec = null;
        foreach (var statement_ in statements)//TODO: convert to dict
        {
            if (statement.id == ((StatementId)Utils.GetStatementAttribute(statement_, 0).Value!))
            {
                statementToExec = statement_;
                break;
            }
        }
        try
        {
            var statementType = LexicalAnalyzer.GetStatementType(statement.id);
            bool endsScope = statementType.EndsScope;
            bool startsScope = statementType.StartsNewScope;
            //border statement
            if (endsScope && startsScope)
            {
                if (statement.id == StatementId.Else)
                {
                    var context = CloseContext();
                    OpenNewContext(ScopeTypeEnum.elseScope, context);
                    if (context.Ignore && context.IfState == IfState.failed)
                    {
                        GetCurrentContext().IfState = IfState.passed;
                        GetCurrentContext().Ignore = false;
                    }
                    else
                    {
                        GetCurrentContext().Ignore = true;
                    }
                }
                else if (statement.id == StatementId.ElseIf)
                {
                    var context = CloseContext();
                    OpenNewContext(ScopeTypeEnum.ifScope, context);
                    if (context.Ignore && context.IfState == IfState.failed)
                    {
                        GetCurrentContext().Ignore = false;
                    }
                    else
                    {
                        GetCurrentContext().Ignore = true;
                    }

                }//TODO: catch
                /*else if (statement.id == StatementId.Catch)
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
                }*/
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
                }
                if (context.ScopeType == ScopeTypeEnum.catchScope && !context.Ignore)
                {
                    GetCurrentContext().Ignore = true;
                    GetCurrentContext().ErrorRaised = null;
                }
                if (context.ScopeType == ScopeTypeEnum.loopScope && !context.Ignore)
                {
                    if ((bool)Evaluator.Evaluate(context.LoopCondition, DataType.Bool).value)
                    {
                        GetCurrentFrame().currentLine = context.StartLine - 1;
                    }
                }
                if (context.ScopeType == ScopeTypeEnum.classScope)
                {
                    var class_ = new VClass(context.Name, context.File!, context.Variables);
                    var var = V_Variable.Construct(DataType.Class, class_);
                    if (!DeclareVar(class_.Identifier, var))
                    {
                        throw new IdentifierAlreadyUsedError(class_.Identifier);
                    }
                }
                if (context.ScopeType == ScopeTypeEnum.functionScope)
                {
                    func!.FunctionBody = func.FunctionBody[..^1];
                    if (!DefineFunc(func.Identifier, func))
                    {
                        throw new IdentifierAlreadyUsedError(func.Identifier);
                    }
                }
            }
            else
            if (startsScope)
            {
                var prevContext = GetCurrentContext();
                OpenNewContext(statementType.ScopeType, prevContext);
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

    public static VContext OpenNewContext(ScopeTypeEnum type)
    {
        var newC = new VContext([], null, GetCurrentFrame().ScopeStack.Count, type, StartLine: GetCurrentFrame().currentLine);
        GetCurrentFrame().ScopeStack.Push(newC);
        return newC;
    }
    public static VContext OpenNewContext(ScopeTypeEnum type, VContext old)
    {
        var enclosingContext = GetCurrentContext();
        var newC = OpenNewContext(type);
        newC.Ignore = old.Ignore;
        newC.InTryScope = old.InTryScope;
        newC.IfState = old.IfState;
        newC.InAFunc = old.InAFunc;
        if (enclosingContext.IfState == IfState.failed)
        {
            newC.IfState = IfState.deadBranch;
        }
        return newC;
    }
    public VContext CloseContext()
    {
        if (GetCurrentContext().ScopeType == ScopeTypeEnum.topLevel)
        {
            throw new UnexpectedTokenError(";").SetInfo("Top level statement may not be closed. Use exit instead");
        }
        return GetCurrentFrame().ScopeStack.Pop();
    }


    public static VFrame GetCurrentFrame()
    {
        return CallStack.First();
    }
    public static VContext GetCurrentTopLevelContext()
    {
        return CallStack.First().VFile.TopLevelContext!;
    }
    public static VContext GetCurrentContext()
    {
        if (!GetCurrentFrame().ScopeStack.TryPeek(out _))
        {
            return new([], null);
        }
        return GetCurrentFrame().ScopeStack.First();
    }

    [MarkStatement(StatementId.Assignment)]
    public void AssignStatement(CompiledStatement statement)
    {
        string identifier = LexicalAnalyzer.StatementGetIdentifier(statement);
        if (!ReadVar(identifier, out var theVar)) throw new UnknownNameError(identifier);
        string special = LexicalAnalyzer.StatementGetFirst(statement, Lexer.LexerStructs.TokenType.Syntax);
        var eval = Evaluator.Evaluate(statement);
        if (special != "=")
        {
            OperToSpecialAssigment[special].Invoke(theVar, [eval]);
        }
        else
            theVar!.Assign(eval, identifier);
    }
    [MarkStatement(StatementId.DeclareFunction)]
    public void FuncDeclaration(CompiledStatement statement)
    {
        if (GetCurrentContext().FuncBeingRead != null || GetCurrentContext().ScopeType != ScopeTypeEnum.topLevel && GetCurrentContext().ScopeType != ScopeTypeEnum.classScope)
        {
            throw new IlegalContextDeclarationError("function").SetInfo("Functions may only be declared at the top level");
        }
        string identifier = LexicalAnalyzer.StatementGetDeclareIdentifier(statement);
        Dictionary<string, string> argsArray = LexicalAnalyzer.StatementGetFuncDeclareArgs(statement);
        List<VFuncArg> argsList = [];
        foreach (var arg_ in argsArray)
        {
            var ident = arg_.Key;
            DataType type = (DataType)Evaluator.Evaluate(arg_.Value, DataType.Type).value;
            argsList.Add(new(ident) { enforcedType = type });

        }
        VFunc func = new(identifier, File, [.. argsList], GetCurrentFrame().currentLine);
        if (GetCurrentContext().ScopeType == ScopeTypeEnum.classScope)
        {
            if (GetCurrentContext().Name == identifier)
            { //constructor
                func.IsConstructor = true;
                func.returnType = DataType.Object;
            }
        }
        var c = OpenNewContext(ScopeTypeEnum.functionScope);
        c.Ignore = true;
        c.FuncBeingRead = func;
    }
    [MarkStatement(StatementId.Call)]
    public static V_Variable CallFunctionStatement(CompiledStatement statement)
    {
        string identifier = LexicalAnalyzer.StatementGetIdentifier(statement);
        string[] args = LexicalAnalyzer.StatementGetArgs(statement);
        var argsArray = Utils.ArgsEval(args);
        argsArray ??= [];
        V_Variable? callable = null;

        string signature = identifier + argsArray.Select(x => x.type).Aggregate("", (x, y) => x + " " + y);
        if (argsArray.Count != 0)
        {
            ReadVar(signature, out callable);
        }
        else
        {
            ReadVar(identifier, out callable);
        }
        int i = 0;
        if (callable == null)
            for (i = 0; i < argsArray.Count; i++)
            {
                var prev = argsArray[i].type;
                argsArray[i].type = DataType.Any;
                if (ReadVar(identifier + argsArray.Select(x => x.type).Aggregate("", (x, y) => x + " " + y), out callable))
                {
                    argsArray[i].type = prev;
                    break;
                }
                argsArray[i].type = prev;
            }
        if (callable == null) throw new FuncSignatureNotFoundError(signature);
        var func = callable!.GetCallableFunc() ?? throw new IlegalOperationError("The type '" + callable.type + "' is not callable");
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
        //TODO: handle constructors
        var val = CallFunction(func, argsList);
        V_Variable val_ = val == null || val.value == null ? V_Variable.Construct(DataType.None, "") : val;
        if (itm)
        {
            Console.WriteLine("< " + val_.ToString());
        }
        return val_;
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
        var val = funcInterpreter.ExecuteStatements(func.FunctionBody);
        CallStack.Pop();
        return val;
    }
    //
    [MarkStatement(StatementId.Declare)]
    public void DeclareStatement(CompiledStatement statement)
    {
        //statement = statement.Replace(":connect","=");
        bool unsetable = LexicalAnalyzer.StatementContainsSyntax(statement, "?");
        bool readonly_ = LexicalAnalyzer.StatementContainsSyntax(statement, "!");
        bool init = LexicalAnalyzer.StatementContainsSyntax(statement, "=");
        string identifier = LexicalAnalyzer.StatementGetFirst(statement, Lexer.LexerStructs.TokenType.DecleareIdentifier);
        if (!Utils.IsIdentifierValid(identifier, true))
        {
            throw new InvalidIdentifierError(identifier);
        }
        if (init)
        {
            var initVal = Evaluator.Evaluate(LexicalAnalyzer.StatementGetExpression(statement));
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

    [MarkStatement(StatementId.Unlink)]
    public void Unlink(CompiledStatement statement)
    {
        string identifer_ = LexicalAnalyzer.StatementGetIdentifier(statement);
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
    [MarkStatement(StatementId.Output)]
    public void OutputStatement(CompiledStatement statement)
    {
        Console.WriteLine(Evaluator.Evaluate(statement).ToString());
    }

    [MarkStatement(StatementId.Return)]
    public void FuncReturnStatement(CompiledStatement statement)
    {
        if (GetCurrentContext().InAFunc)
        {
            GetCurrentContext().ReturnValue = Evaluator.Evaluate(statement);
        }
        else
        {
            throw new UnexpectedTokenError("<").SetInfo("Return token may not be used outside of a function");
        }
    }

    [MarkStatement(StatementId.If)]
    public void IfStatement(CompiledStatement statement)
    {
        string expression = LexicalAnalyzer.StatementGetExpression(statement);
        bool result = (bool)Evaluator.Evaluate(expression, DataType.Bool).value;
        GetCurrentContext().Ignore = !result;
        if (result)
            GetCurrentContext().IfState = IfState.passed;
        else
            GetCurrentContext().IfState = IfState.failed;
    }
    [MarkStatement(StatementId.StartScope)]
    public void GenericStatementStart(CompiledStatement statement)
    {

    }
    //TODO: try catch
    /*[MarkStatement(StatementId.)]
    public void TryScopeStartStement(CompiledStatement statement))
    {
        GetCurrentContext().InTryScope = true;
    }
    [MarkStatement("catch ", true, scopeType: ScopeTypeEnum.catchScope, true)]
    public void CatchScopeStartStement(CompiledStatement statement))
    {

    }*/
    [MarkStatement(StatementId.EndScope)]
    public void EndScopeStatement(CompiledStatement statement)
    {

    }
    [MarkStatement(StatementId.Exit)]
    public void ExitProgrammStatement(CompiledStatement statement)
    {
        Environment.Exit(0);
    }
    [MarkStatement(StatementId.Else)]
    public void ElseScopeStatement(CompiledStatement statement)
    {

    }
    [MarkStatement(StatementId.ElseIf)]
    public void ElseIfScopeStatement(CompiledStatement statement)
    {
        string expression = LexicalAnalyzer.StatementGetExpression(statement);
        bool result = (bool)Evaluator.Evaluate(expression, DataType.Bool).value;
        GetCurrentContext().Ignore = !result;
        if (result)
            GetCurrentContext().IfState = IfState.passed;
        else
            GetCurrentContext().IfState = IfState.failed;
    }

    [MarkStatement(StatementId.Acquire)]
    public void AccessStatement(CompiledStatement statement)
    {
        if (GetCurrentContext().ScopeType != ScopeTypeEnum.topLevel)
        {
            throw new IlegalStatementContextError("Acquire", GetCurrentContext().ScopeType.ToString()).SetInfo("Acquire statement may only be used at the top level");
        }
        var file = LexicalAnalyzer.StatementGetIdentifier(statement) + ".vort";
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
    [MarkStatement(StatementId.Acquires)]
    public void AccessOnceStatement(CompiledStatement statement)
    {
        if (GetCurrentContext().ScopeType != ScopeTypeEnum.topLevel)
        {
            throw new IlegalStatementContextError("Safeacquire", GetCurrentContext().ScopeType.ToString()).SetInfo("Safeacquire statement may only be used at the top level");
        }
        var file = LexicalAnalyzer.StatementGetIdentifier(statement) + ".vort";
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
    [MarkStatement(StatementId.Release)]
    public void UnloadStatement(CompiledStatement statement)
    {
        var file = LexicalAnalyzer.StatementGetIdentifier(statement) + ".vort";
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

    [MarkStatement(StatementId.SetDirective)]
    public void SetDirectiveStatement(CompiledStatement statement)
    {
        var directive = LexicalAnalyzer.StatementGetIdentifier(statement);
        //TODO: fix naming directives
        /*if (directive.Length < 2)
        {
            if (directive[0][0] == '#')
            {
                if (GetCurrentFrame().currentLine == 0)
                {
                    var name = directive[0][1..];
                    name = name[0].ToString().ToUpper() + name[1..];
                    if (!Utils.IsIdentifierValid(name, true))
                    {
                        throw new InvalidIdentifierError(name);
                    }
                    VContext.RenameNew(name);
                    return;
                }
                else throw new IlegalStatementContextError("Module name statements have to be on the first line");
            }
            throw new ExpectedTokenError("directive and value");
        }
        else if (directive.Length > 2)
        {
            throw new UnexpectedTokenError(directive[2]);
        }*/
        string directiveName = LexicalAnalyzer.StatementGetIdentifier(statement);
        string value = LexicalAnalyzer.StatementGetExpression(statement);
        var thing = Directives.GetDirectiveField("DIR_" + directiveName, out var type);
        var value_ = Evaluator.Evaluate(value, Utils.CSharpTypeToVortexType(type));
        var fieldValue = thing.GetValue(null);
        if (fieldValue != null)
        {
            var constructedType = typeof(DirectiveDefinition<>).MakeGenericType(type);
            object newVal = Activator.CreateInstance(constructedType, value_.value)!;
            thing.SetValue(null, newVal);
        }


    }
    [MarkStatement(StatementId.Raise)]
    public void ThrowStatement(CompiledStatement statement)
    {
        if (statement.tokens.Length == 2)
        {
            var type = Evaluator.Evaluate(statement.tokens[0].leaf!, DataType.GroupType);
            var message = Evaluator.Evaluate(statement.tokens[1].leaf!, DataType.String);
            InternalStandartLibrary.ThrowError(type, message.value.ToString());
        }
        else if (statement.tokens.Length == 1)
        {
            var error = Evaluator.Evaluate(statement.tokens[0].leaf!, DataType.Error);
            throw (VortexError)error.value;
        }
    }

    [MarkStatement(StatementId.Assert)]
    public void AssertStatement(CompiledStatement statement)
    {
        if (statement.tokens.Length == 2)
        {
            var val1 = Evaluator.Evaluate(statement.tokens[0].leaf!, DataType.Any);
            var val2 = Evaluator.Evaluate(statement.tokens[1].leaf!, DataType.Any);
            var value = val1.ToString() == val2.ToString();
            if (!value)
            {
                throw new AssertionFailedError(val1.ToString(), val2.ToString());
            }
        }
        else if (statement.tokens.Length == 1)
        {
            var value = (bool)Evaluator.Evaluate(statement.tokens[0].leaf!, DataType.Bool).value;
            if (!value)
            {
                throw new AssertionFailedError("true", "false");
            }

        }
    }

    [MarkStatement(StatementId.Class)]
    public void ClassStatement(CompiledStatement statement)
    {
        var identifier = LexicalAnalyzer.StatementGetDeclareIdentifier(statement);
        GetCurrentContext().Name = identifier;
    }

    [MarkStatement(StatementId.While)]
    public void WhileStatement(CompiledStatement statement)
    {
        var condition = LexicalAnalyzer.StatementGetExpression(statement);
        GetCurrentContext().LoopCondition = condition;
        if ((bool)Evaluator.Evaluate(condition, DataType.Bool).value == false)
        {
            GetCurrentContext().Ignore = true;
        }

    }
    [MarkStatement(StatementId.Break)]
    public void BreakStatement(CompiledStatement statement)
    {
        bool found = false;
        foreach (var context in GetCurrentFrame().ScopeStack)
        {
            context.Ignore = true;
            if (context.ScopeType == ScopeTypeEnum.loopScope)
            {
                found = true;
            }
        }
        if (!found)
        {
            throw new IlegalOperationError("Could not find a loop to break from");
        }
    }

    public static bool ReadVar(string identifier, out V_Variable? val, VContext? context = null, DataType type = DataType.Any)
    {
        Dictionary<string, V_Variable> vars;
        val = null;
        V_Variable dottable = null!;
        while (identifier.Contains('.'))
        {
            int i = identifier.IndexOf('.');
            var dottable_ = identifier[..i];
            if (dottable == null)
            {
                dottable = Evaluator.Evaluate(dottable_);
            }
            else
            {
                dottable = dottable.GetField(dottable_);
            }
            identifier = identifier[(i + 1)..];
        }
        if (dottable != null)
        {
            val = dottable.GetField(identifier);
            return true;
        }
        if (context == null)
        {
            //gets all variables in the current frame
            vars = Utils.GetAllVars();
        }
        else
        {
            if (context.Name == "Released")
                throw new AccessingReleasedModuleError("unknown");
            //use the context variables
            vars = context.Variables;
        }

        //try to read a module
        if (context == null && (ActiveModules.TryGetValue(identifier, out var module) || InternalModules.TryGetValue(identifier, out module)))
        {
            val = V_Variable.Construct(DataType.Module, module, new() { readonly_ = true });
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
            if (context?.ScopeType == ScopeTypeEnum.internal_ || context == null)
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
            throw new UnmatchingDataTypeError(identifier + "(" + val.type.ToString() + ")", type.ToString());
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
        if (scope == null)
        {
            var frame = GetCurrentFrame();
            foreach (var item in frame.ScopeStack)
            {
                if (item.Variables.ContainsKey(identifier))
                {
                    var prevFlags = item.Variables[identifier].flags;
                    item.Variables[identifier] = value;
                    item.Variables[identifier].flags = prevFlags;
                    return true;
                }
            }
            return false;
        }
        scope ??= GetCurrentContext();

        if (scope.Variables.ContainsKey(identifier))
        {
            var prevFlags = scope.Variables[identifier].flags;
            scope.Variables[identifier] = value;
            scope.Variables[identifier].flags = prevFlags;
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

    public static bool DeclareVar(string identifier, V_Variable var, VContext? context = null)
    {
        context ??= GetCurrentContext();
        return context.Variables.TryAdd(identifier, var);
    }
    public static bool DefineFunc(string identifier, VFunc var)
    {
        return DeclareVar(identifier, V_Variable.Construct(DataType.Function, var));
    }
    public static VFrame NewFrame(VFile file, ScopeTypeEnum scopeType, int lineOffset, string name, VContext? context = null)
    {
        if (CallStack.Count == maxDepth)
        {
            throw new StackOverflowError();
        }
        VFrame frame = new(file, lineOffset, name);
        frame.ScopeStack.Push(context ?? new([], file, 0, scopeType, StartLine: GetCurrentFrame().currentLine) { InTryScope = GetCurrentContext().InTryScope });
        CallStack.Push(frame);
        return frame;
    }

}