using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Vortex
{
    public class Interpreter
    {
        //Config
        public static readonly int maxDepth = 512;
        public static readonly string version = "beta 1.0.0";
        public static readonly bool debug = false;



        //Internal stuff
        public static MethodInfo[] statements = [];
        public static Dictionary<string, VContext> InternalModules = new();

        //Memory
        public static Dictionary<string, VContext> ActiveModules { get; private set; } = new();
        public static string[] keywords = [];

        public static Stack<VFrame> CallStack { get; private set; } = [];


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
            statements = Assembly.GetAssembly(typeof(Interpreter)).GetTypes()
                      .SelectMany(t => t.GetMethods())
                      .Where(m => m.GetCustomAttributes(typeof(MarkStatement), false).Length > 0)
                      .ToArray();
            foreach (var var in SuperGlobals.SuperGlobalVars)
            {
                keywords = [.. keywords, var.Key];
            }
            var listOfBs = (
               from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
               from type in domainAssembly.GetTypes()
               where typeof(InternalModule).IsAssignableFrom(type)
               select type).ToArray();
            //TODO: do not load in advance? Load dynamically?
            foreach (var type in listOfBs)
            {
                var method = type
                .GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(InternalFunc), false).Length > 0)
                .Select(mi => new VFunc(mi.Name, null, Utils.ConvertMethodInfoToArgs(mi), -1) { CSharpFunc = mi, returnType = (DataType)Utils.GetStatementAttribute<InternalFunc>(mi, 0).Value! })
                .ToDictionary(x => {var id = x.Identifier;id=id[0].ToString().ToLower()+id[1..];return id;}, x => x);

                Dictionary<string, V_Variable> constants = new();
                type.GetFields(BindingFlags.Public | BindingFlags.Static )
                .ToList()
                .ForEach(f =>
                {
                    if(f.GetValue(null)!=null){
                        var val = (V_Variable)f.GetValue(null)!;
                        constants.Add(f.Name, val);
                    }
                });
                InternalModules.Add(type.Name[8..], new(constants, method, scopeType: ScopeTypeEnum.internal_));
            }
        }
        public void ExecuteFile()
        {
            ExecuteLines(File.ReadFile());
        }

        V_Variable? ExecuteLines(string[] lines)
        {
            foreach (string line in lines)
            {
                try
                {
                    ExecuteLine(line);
                }
                catch (ExpressionEvalError e)
                {
                    if (GetCurrentContext().TryParrentContext != null)
                    {
                        //TODO: catch logic
                    }
                    else
                        VortexError.ThrowError(e);
                }
                catch (VortexError e)
                {
                    VortexError.ThrowError(e);
                }
                catch (Exception e)
                {
                    Console.WriteLine("An Interpreter error has occured. This is extraordinary and should not happen. Please report this error. The execution will now be halted. C# exception follows.");
                    throw;
                }
                GetCurrentFrame().currentLine++;
                if (GetCurrentContext().ReturnValue != null)
                {
                    return GetCurrentContext().ReturnValue;
                }

            }
            if (GetCurrentContext().Depth != 0)
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
                    if (CallFunctionStatement(statement, out _, GetCurrentContext()))
                    {
                        return;
                    }
                    else
                        throw new UnknownStatementError(statement);
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
                        var context = GetCurrentContext();
                        if (context.ScopeType != ScopeTypeEnum.tryScope)
                        {
                            throw new IlegalStatementContextError("catch", context.ScopeType.ToString());
                        }
                        CloseContext();
                        OpenNewContext(ScopeTypeEnum.catchScope);
                        if (context.SubsequentFramesIgnore)
                        {
                            GetCurrentContext().Ignore = true;
                            GetCurrentContext().SubsequentFramesIgnore = true;
                        }
                        else
                            GetCurrentContext().Ignore = !context.Ignore;
                    }
                }
                else
                if (endsScope)
                {
                    CloseContext();
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

            var newC = new VContext([], [], GetCurrentFrame().ScopeStack.Count, type, StartLine: GetCurrentFrame().currentLine);
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
            return GetCurrentFrame().ScopeStack.First();
        }
        public bool AssignStatement(string statement)
        {
            if (Utils.StringContains(statement, "="))
            {
                var middle = Utils.StringIndexOf(statement, "=");
                if (middle < 1)
                {
                    throw new UnexpectedTokenError("=").SetInfo("Identifier expected prior");

                }
                if (middle + 1 == statement.Length)
                {
                    throw new UnexpectedEndOfStatementError("Expression");
                }
                string identifier = statement[0..(middle - 1)];
                if (middle == 1)
                {
                    identifier = statement[0].ToString();
                }
                string expression = statement[(middle + 1)..];
                if (Utils.IsIdentifierValid(identifier))
                {
                    if (SetVar(identifier, ExpressionEval.Evaluate(expression)))
                    {
                        return true;
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
                    throw new UnexpectedTokenError("=").SetInfo("Identifier expected prior");
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
                if (!Utils.IsIdentifierValid(identifier))
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
                foreach (var arg in argsArray)
                {
                    if (!Utils.IsIdentifierValid(arg))
                    {
                        throw new InvalidIdentifierError(arg);
                    }
                    if (argsList.Any(x => x.name == arg))
                    {
                        throw new DuplicateVariableError(arg);
                    }
                    argsList.Add(new(arg));
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
            GetCurrentContext().Functions.Add(func.Identifier, func);

        }
        public static bool CallFunctionStatement(string statement, out V_Variable? val, VContext? context = null)
        {
            if (Utils.StringContains(statement, "(") && Utils.StringContains(statement, ")") && !statement.EndsWith(" :"))
            {
                int argsStart = Utils.StringIndexOf(statement, "(");
                int argsEnd = Utils.StringLastIndexOf(statement, ')');
                if(argsEnd!=statement.Length-1){
                    throw new UnexpectedTokenError(statement[(argsEnd+1)..]);
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
                if (i != -1&&i<argsStart)
                {
                    module = identifier[..i];
                    identifier = identifier[(i+1)..];
                }
                if (!Utils.IsIdentifierValid(identifier))
                {
                    throw new InvalidIdentifierError(identifier);
                }
                if (module != "")
                {
                    if(!TryGetModule(module,out context)){
                        throw new UnknownNameError(module);
                    }
                }
                if (!ReadFunction(identifier, out var func, context))
                {
                    if (!ReadVar(identifier, out var x))
                    {
                        throw new UnknownNameError(identifier);
                    }
                    else
                    {
                        throw new UnmatchingDataTypeError(x.type.ToString(), "VFunc");
                    }
                }
                string args = statement[(argsStart + 1)..argsEnd];
                var argsArray = Utils.StringSplit(args, ',');
                if (argsArray.Length == 1 && argsArray[0] == "")
                {
                    argsArray = [];
                }
                if (argsArray.Length != func.Args.Length)
                {//TODO: defualt params
                    throw new FuncOverloadNotFoundError(func.Identifier, argsArray.Length.ToString());
                }
                Dictionary<string, V_Variable> argsList = [];
                i = 0;
                foreach (var arg in argsArray)
                {
                    argsList.Add(func.Args[i].name, ExpressionEval.Evaluate(arg, func.Args[i].enforcedType));
                    i++;
                }
                val = CallFunction(func, argsList);
                return true;
            }
            val = null;
            return false;
        }

        public static V_Variable? CallFunction(VFunc func, Dictionary<string, V_Variable> args)
        {
            if (func.CSharpFunc != null)
            {
                var args_ = args.Select(x => Utils.CastToCSharpType(x.Value.type, x.Value.value.ToString())).ToArray();
                object? val_ = null;
                try{
                    val_ = func.CSharpFunc.Invoke(null, args_);
                }
                catch(ArgumentException){
                    throw new FuncOverloadNotFoundError(func.Identifier,"-");
                }
                return new V_Variable(func.returnType, val_);
            }
            NewFrame(func.File, ScopeTypeEnum.functionScope, func.StartLine + 1, func.GetFullPath());
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
            statement = Utils.StringRemoveSpaces(statement);
            bool unsetable = statement[1] == '?';
            bool readonly_ = statement[1] == '!';
            if (unsetable || readonly_)
            {
                statement = statement.Remove(1, 1);
            }
            bool init = Utils.StringContains(statement, "=");
            string identifier = init ? statement[1..statement.IndexOf('=')] : statement[1..];
            if (!Utils.IsIdentifierValid(identifier))
            {
                throw new InvalidIdentifierError(identifier);
            }
            if (init)
            {
                if (statement.EndsWith('='))
                {
                    throw new UnexpectedEndOfStatementError("Expression");
                }
                var initVal = ExpressionEval.Evaluate(statement[(statement.IndexOf('=') + 1)..]);
                initVal.unsetable = unsetable;
                initVal.readonly_ = readonly_;
                bool failed = false;

                if (!DeclareVar(identifier, initVal))
                    failed = true;

                if (failed)
                {
                    throw new VariableAlreadyDeclaredError(identifier);
                }
            }
            else
            {
                if (readonly_)
                {
                    throw new IlegalDeclarationError("A read-only variable has to be initialized");
                }
                if (!DeclareVar(identifier, new(DataType.Unset, "unset", unsetable)))
                {
                    throw new VariableAlreadyDeclaredError(identifier);
                }
            }

        }
        [MarkStatement(">", false)]
        public void OutputStatement(string statement)
        {
            Console.WriteLine(ExpressionEval.Evaluate(statement[1..]).value);
        }

        [MarkStatement("<", false)]
        public void FuncReturnStatement(string statement)
        {
            if (GetCurrentContext().InAFunc)
            {
                GetCurrentContext().ReturnValue = ExpressionEval.Evaluate(statement[1..]);
            }
            else
            {
                throw new UnexpectedTokenError("<").SetInfo("Return token may not be used outside of a function");
            }
        }

        [MarkStatement("if", true, ScopeTypeEnum.ifScope)]
        public void IfStatement(string statement)
        {
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
            bool result = (bool)ExpressionEval.Evaluate(expression, DataType.Bool).value;
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
            GetCurrentContext().InTryStatement = true;

        }
        [MarkStatement("catch :", true, scopeType: ScopeTypeEnum.catchScope, true)]
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
            bool result = (bool)ExpressionEval.Evaluate(expression, DataType.Bool).value;
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
            file_.InterpretThisFile();
            CallStack.Pop();

        }
        [MarkStatement("safeacquire  ", false)]
        public void AccessOnceStatement(string statement)
        {
            if (GetCurrentContext().ScopeType != ScopeTypeEnum.topLevel)
            {
                throw new IlegalStatementContextError("Safeacquire", GetCurrentContext().ScopeType.ToString()).SetInfo("Safeacquire statement may only be used at the top level");
            }
            var file = statement[8..] + ".vort";
            if (ActiveModules.ContainsKey(file))
            {
                return;
            }
            VFile file_ = new(file);
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

            if (!ActiveModules.TryGetValue(file, out VContext? value))
            {
                throw new UnknownNameError(file);
            }
            if (GetCurrentFrame().VFile.GetFileName() == file)
            {
                throw new IlegalStatementContextError("Release", GetCurrentContext().ScopeType.ToString()).SetInfo("Cannot release the currently executed module");
            }
            if (value.IsMain)
            {
                throw new IlegalStatementContextError("Release", GetCurrentContext().ScopeType.ToString()).SetInfo("Cannot release the entrypoint module");
            }
            ActiveModules.Remove(file);
        }



        // public static bool ReadVar(string identifier, out Variable val,Context? scope = null){
        //     scope ??= GetCurrentContext();
        //     var combined = scope.Variables.Concat(SuperGlobals).ToDictionary(x=>x.Key,x=>x.Value);
        //     var res = combined.TryGetValue(identifier, out val);
        //     if(!val.unsetable&&val.type==DataType.Unset)
        //         throw new ReadingUnsetValue(identifier);
        //     return res;
        // }
        public static bool ReadVar(string identifier, out V_Variable val, VContext? context = null, bool ignoreTopLevel = false)
        {
            Dictionary<string, V_Variable> vars;
            if (context == null)
            {
                vars = Utils.GetAllVars();
            }
            else
            {
                vars = context.Variables;
            }
            if (SuperGlobals.SuperGlobalVars.TryGetValue(identifier, out var l))
            {
                val = l();
                return true;
            }
            var res = vars.TryGetValue(identifier, out val);
            if (!res)
            {
                if (context?.ScopeType == ScopeTypeEnum.internal_ || ignoreTopLevel)
                {
                    return false;
                }
                if (!GetCurrentFrame().VFile.TopLevelContext.Variables.TryGetValue(identifier, out val))
                {
                    return false;
                }
                else
                {
                    res = true;
                }
            }

            if (!val.unsetable && val.type == DataType.Unset)
                throw new ReadingUnsetValueError(identifier);
            return res;
        }
        public static bool ReadFunction(string identifier, out VFunc val, VContext? context = null, bool ignoreTopLevel = false)
        {
            /*if(SuperGlobals.TryGetValue(identifier, out  val)){
                return true;
            }*/
            Dictionary<string, VFunc> funcs;
            if (context != null)
            {
                funcs = GetAllModuleFuncs(context);
            }
            else
            {
                funcs = GetAllFunctions();
            }
            var res = funcs.TryGetValue(identifier, out val);
            if (!res)
            {
                if (context?.ScopeType == ScopeTypeEnum.internal_ || ignoreTopLevel)
                {
                    return false;
                }
                if (!GetCurrentFrame().VFile.TopLevelContext.Functions.TryGetValue(identifier, out val))
                {
                    return false;
                }
                else
                {
                    res = true;
                }
            }
            return res;
        }
        public static Dictionary<string, VFunc> GetAllFunctions()
        {
            var funcs = new Dictionary<string, VFunc> { };
            foreach (var func in GetCurrentFrame().ScopeStack.Last().Functions)
            {
                funcs.Add(func.Key, func.Value);
            }
            foreach (var func in GetCurrentFrame().VFile.TopLevelContext.Functions)
            {
                funcs.TryAdd(func.Key, func.Value);
            }
            foreach (var func_ in ActiveModules)
            {
                foreach (var func in func_.Value.Functions)
                {
                    funcs.TryAdd(func.Key, func.Value);
                }
            }
            return funcs;
        }
        public static bool TryGetModule(string name,out VContext module)
        {
            return ActiveModules.TryGetValue(name, out module)||InternalModules.TryGetValue(name, out module);
        }
        public static Dictionary<string, VFunc> GetAllModuleFuncs(VContext module)
        {
            return module.Functions;
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
        public bool SetVar(string identifier, V_Variable value)
        {
            var vars = Utils.GetAllVars();
            if (vars.ContainsKey(identifier))
            {
                if (vars[identifier].readonly_)
                    throw new AssigmentToReadonlyVarError(identifier);
                vars[identifier] = value;
                return true;
            }
            return false;
        }

        public static bool DeclareVar(string identifier, V_Variable var)
        {

            return GetCurrentContext().Variables.TryAdd(identifier, var);
        }
        public static VFrame NewFrame(VFile file, ScopeTypeEnum scopeType, int lineOffset, string name)
        {
            if (CallStack.Count == maxDepth)
            {
                throw new StackOverflowError();
            }
            VFrame frame = new(file, lineOffset, name);
            frame.ScopeStack.Push(new([], [], 0, scopeType, StartLine: GetCurrentFrame().currentLine));
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
}