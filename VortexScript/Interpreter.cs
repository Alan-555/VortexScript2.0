using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Vortex
{
    public class Interpreter
    {
        //Config
        public static readonly int maxDepth = 256;
        public static readonly string version = "beta 1.0.0";
        public static readonly bool debug = false;

        public static bool itm = false;


        //directives
        public static bool DIR_BufferInput = false;

        //Internal stuff
        public static MethodInfo[] statements = [];
        public static Dictionary<string, VContext> InternalModules = new();
        public static Dictionary<string, SpecialAssigment> OperToSpecialAssigment = new(){
            {"+",SpecialAssigment.Add},
            {"-",SpecialAssigment.Remove},
            {".",SpecialAssigment.Clear},
            {"*",SpecialAssigment.Mult},
            {"/",SpecialAssigment.Div},
        };

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
                .ToDictionary(x => { var id = x.Identifier; id = id[0].ToString().ToLower() + id[1..]; return id; }, x => x);
                var vv = V_Variable.Construct(DataType.Number, 3);
                Dictionary<string, V_Variable> constants = new();
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
                InternalModules.Add(type.Name[8..], new(constants, method, scopeType: ScopeTypeEnum.internal_) { Name = type.Name[8..] });
            }
            //add types
            var types = Enum.GetNames(typeof(DataType)).ToList();
            foreach (var type in types)
            {
                SuperGlobals.SuperGlobalVars.Add(type,()=>V_Variable.Construct(DataType.Type,type));
            }

        }
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
                    ExecuteLines([Console.ReadLine()!]);
                }
            }
            else
            {
                ExecuteLines(File.ReadFile());
            }
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
                catch (Exception)
                {
                    Console.Error.WriteLine("---------------Execution halted on internal exception---------------");
                    Console.Error.WriteLine("An Interpreter error has occured, this event is extraordinary. Please report this error.");
                    Console.Error.WriteLine("C# exception: ");
                    throw;
                }
                GetCurrentFrame().currentLine++;
                if (GetCurrentContext().ReturnValue != null)
                {
                    return GetCurrentContext().ReturnValue;
                }

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
            if (!GetCurrentFrame().ScopeStack.TryPeek(out _))
            {
                return new([], []);
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
                char special = statement[middle - 1];
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
                    if ("+-*/.".Contains(special.ToString()) && OperToSpecialAssigment.TryGetValue(special.ToString(), out var spec))
                    {
                        return SetSpecial(identifier, ExpressionEval.Evaluate(expression), spec);
                    }


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
            if (!GetCurrentContext().Functions.TryAdd(func.Identifier, func))
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
                    throw new InvalidIdentifierError(identifier);
                }
                if (module != "")
                {
                    if (!TryGetModule(module, out context))
                    {
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
                        args_[i] = args_[i].ToString();
                    }
                    else if (arg.ParameterType == typeof(V_Variable))
                    {
                        args_[i] = args.ElementAt(i).Value;
                    }
                    else if(arg.ParameterType==typeof(bool)){
                        args_[i] = (bool)args.ElementAt(i).Value.value==true;
                    }
                    else if(arg.ParameterType==typeof(int)){
                        args_[i] = (int)Math.Floor((double)args.ElementAt(i).Value.value);
                    }
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
            if (statement.Length < 2)
            {
                throw new ExpectedTokenError("Var modifier or an identifier");
            }
            bool unsetable = statement[1] == '?';
            bool readonly_ = statement[1] == '!';
            bool unlink = statement[1] == '$'; //TODO: fix. WHen undeclaring in a scope the top level takes precesnde and is remove first
            if (unsetable || readonly_)
            {
                statement = statement.Remove(1, 1);
            }
            if (unlink)
            {
                string identifer_ = statement[2..];
                if (!GetCurrentContext().Variables.Remove(identifer_))
                {
                    throw new UnknownNameError(identifer_);
                }
                return;
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
            Console.WriteLine(ExpressionEval.Evaluate(statement[1..]).ToString());
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
            if(statement.Length<4){
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
        [MarkStatement("safeacquire ", false)]
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
            var value_ = ExpressionEval.Evaluate(value, type);
            dynamic theDir = thing.GetValue(null);
            var valueField = theDir.GetType().GetField("value", BindingFlags.Instance | BindingFlags.Public);

            if (valueField != null && valueField.FieldType.IsAssignableFrom(value_.value.GetType()))
            {
                valueField.SetValue(theDir, value_.value);
            }
            else
            {
                throw new ArgumentException($"Value type mismatch: Expected {valueField.FieldType}, got {value.GetType()}");
            }


        }

        

        public static T ConvertToGeneric<T>(object obj)
        {
            return (T)obj;
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

            if (!val.flags.unsetable && val.type == DataType.Unset)
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
            foreach (var item in InternalModules.First().Value.Functions)
            {
                funcs.TryAdd(item.Key, item.Value);
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
        public static bool TryGetModule(string name, out VContext module)
        {
            return ActiveModules.TryGetValue(name, out module) || InternalModules.TryGetValue(name, out module);
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
                     (old.value as VArray).RemoveAll(x=>x.value.Equals(value.value));
                }
            }
            return false;
        }
        public static bool SetSpecial(string identifier, V_Variable v, SpecialAssigment sa)
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
                var result = ExpressionEval.Evaluate(expression);
                return SetVar(identifier, result);
            }
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

    public enum SpecialAssigment
    {
        Add,
        Remove,
        Clear,
        Mult,
        Div,

    }
}