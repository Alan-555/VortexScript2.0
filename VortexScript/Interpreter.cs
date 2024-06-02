using System.Reflection;
using System.Runtime.ExceptionServices;
using CodingSeb.ExpressionEvaluator;

namespace Vortex{
    public class Interpreter{
        //Internal stuff
        static MethodInfo[] statements=[];

        //Memory
        public readonly static string[] keywords = ["true","false","unset"];
                public static Dictionary<string,Variable> SuperGlobals {get; private set;} = new(){
            {"true", new Variable(DataType.Bool,true)},
            {"false", new Variable(DataType.Bool,false)},
            {"unset",new Variable(DataType.Unset,"")},
        };
        public static Stack<VFrame> CallStack {get;private set;} = [];


        //Instance    
        public VFile File {private set; get; }
        
        public Interpreter(VFile file){
            File = file;
            if(statements.Length==0)
                InitStatements();
        }
        
        public void InitStatements(){
            statements = Assembly.GetAssembly(typeof(Interpreter)).GetTypes()
                      .SelectMany(t => t.GetMethods())
                      .Where(m => m.GetCustomAttributes(typeof(MarkStatement), false).Length > 0)
                      .ToArray();
        }
        public void ExecuteFile(){
            ExecuteLines(File.ReadFile());
        }

         void ExecuteLines(string[] lines){
            foreach(string line in lines){
                try{    
                    ExecuteLine(line);
                    GetCurrentFrame().currentLine++;
                }
                catch (ExpressionEvalError e){
                    VortexError.ThrowError(e);
                }
                catch(VortexError e){
                    VortexError.ThrowError(e);
                }
                catch(Exception e){
                    Console.WriteLine("An Interpreter error has occured. This is extraordinary and should not happen. Please report this error. The execution will now be halted. C# exception follows.");
                    throw;
                }
            }
            if(GetCurrentContext().Depth!=0){
                if(GetCurrentContext().FuncBeingRead!=null)
                    VortexError.ThrowError(new FunctionBodyLeakError((GetCurrentContext().StartLine+1).ToString()));
                else
                    VortexError.ThrowError(new ScopeLeakError((GetCurrentContext().StartLine+1).ToString()));
            }
        }

        public void ExecuteLine(string line){
            line = Utils.RemoveInlineComments(line);
            line = line.Trim();
            var func = GetCurrentContext().FuncBeingRead;
            if(func!=null)
                func.FunctionBody = [.. func.FunctionBody, line];
            ExecuteStatement(line);

        }
        public void ExecuteStatement(string statement){
            if(statement=="")return;
            MethodInfo? statementToExec =null;
            foreach (var statement_ in statements){
                if(statement.StartsWith(Utils.GetStatementAttribute(statement_,StatementAttributes.beginsWith).ToString().Replace("\"",""))){
                    statementToExec = statement_;
                    break;
                }
            }
            try{
            if(statementToExec == null){
                if(GetCurrentContext().Ignore){
                    return;
                }
                //check for assignment
                if(AssignStatement(statement))
                    return;
                else
                if(FuncDeclaration(statement))
                    return;
                else
                if(CallFunctionStatement(statement)){
                    return;
                }
                else
                    throw new UnknownStatementError(statement);
            }
                bool endsScope = (bool)Utils.GetStatementAttribute(statementToExec,StatementAttributes.endScope).Value!;
                bool startsScope = (bool)Utils.GetStatementAttribute(statementToExec,StatementAttributes.mewScope).Value!;
                //border statement
                if(endsScope && startsScope){
                    if(statementToExec.Name=="ElseScopeStatement"){
                        var context = GetCurrentContext();
                        if(context.ScopeType!=ScopeTypeEnum.ifScope){
                            throw new IlegalStatementContextError("else",context.ScopeType.ToString());
                        }
                        CloseContext();
                        OpenNewContext(ScopeTypeEnum.elseScope);
                        GetCurrentContext().FuncBeingRead = context.FuncBeingRead;
                        if(context.SubsequentFramesIgnore){
                            GetCurrentContext().Ignore = true;
                            GetCurrentContext().SubsequentFramesIgnore = true;
                        }
                        else
                            GetCurrentContext().Ignore = !context.Ignore;
                    }
                    else if(statementToExec.Name=="ElseIfScopeStatement"){
                        var context = GetCurrentContext();
                        if(context.ScopeType!=ScopeTypeEnum.ifScope){
                            throw new IlegalStatementContextError("else if",context.ScopeType.ToString());
                        }
                        CloseContext();
                        OpenNewContext(ScopeTypeEnum.ifScope);
                        GetCurrentContext().FuncBeingRead = context.FuncBeingRead;
                        if(context.SubsequentFramesIgnore){
                            GetCurrentContext().Ignore = true;
                            GetCurrentContext().SubsequentFramesIgnore = true;
                        }
                        else
                            GetCurrentContext().Ignore = !context.Ignore;
                    }
                }
                else
                if(endsScope){
                    CloseContext();
                }
                else
                if(startsScope){
                    var prevContext = GetCurrentContext();
                    OpenNewContext((ScopeTypeEnum)Utils.GetStatementAttribute(statementToExec,StatementAttributes.scopeType).Value!);
                    //inhirit igonre flag
                    GetCurrentContext().Ignore = prevContext.Ignore;
                    //inhirit function
                    GetCurrentContext().FuncBeingRead = prevContext.FuncBeingRead;
                    if(prevContext.Ignore){
                        GetCurrentContext().SubsequentFramesIgnore = true;
                    }
                }
                if(!GetCurrentContext().Ignore)
                    statementToExec.Invoke(this,[statement]);
            }
            catch (VortexError){
                throw;
            }
            catch (Exception e){
                if(e.InnerException!=null)
                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                else
                    ExceptionDispatchInfo.Capture(e).Throw();
            }
        }

        public VContext OpenNewContext(ScopeTypeEnum type){

            var newC = new VContext([], [],GetCurrentFrame().ScopeStack.Count,type,StartLine:GetCurrentFrame().currentLine);
            GetCurrentFrame().ScopeStack.Push(newC);
            return newC;
        }
        public void CloseContext(){
            if(GetCurrentContext().FuncBeingRead!=null&&GetCurrentContext().FuncTopLevel){
                FinishFuncDeclaration();
            }
            else{
                if(GetCurrentContext().ScopeType==ScopeTypeEnum.topLevel){
                    throw new UnexpectedTokenError(";").SetInfo("Top level statement may not be closed. Use exit() instead");
                }
                GetCurrentFrame().ScopeStack.Pop();

            }
        }


        public static VFrame GetCurrentFrame(){
            return CallStack.First();
        }
        public static VContext GetCurrentContext(){
            return GetCurrentFrame().ScopeStack.First();
        }
        public bool AssignStatement(string statement){
            if(Utils.StringContains(statement,"=")){
                var middle = Utils.StringIndexOf(statement,"=");
                if(middle<1){
                    throw new UnexpectedTokenError("=").SetInfo("Identifier expected prior");

                }
                if(middle+1==statement.Length){
                    throw new UnexpectedEndOfStatementError("Expression");
                }
                string identifier = statement[0..(middle-1)];
                if(middle==1){
                    identifier = statement[0].ToString();
                }
                string expression = statement[(middle + 1)..];
                if(Utils.IsIdentifierValid(identifier)){
                    if(SetVar(identifier,ExpressionEval.Evaluate(expression))){
                        return true;
                    }
                }
            }
            return false;
        }
        public bool FuncDeclaration(string statement){

            if(Utils.StringContains(statement,"(")&&Utils.StringContains(statement,")")&&statement.EndsWith(" :")){
                if(GetCurrentContext().FuncBeingRead!=null||GetCurrentContext().ScopeType!=ScopeTypeEnum.topLevel){
                    throw new IlegalDeclarationError("function").SetInfo("Functions may only be declared at the top level");
                }
                int argsStart = Utils.StringIndexOf(statement,"(");
                int argsEnd = Utils.StringIndexOf(statement,")");
                if(argsStart==0){
                   throw new UnexpectedTokenError("=").SetInfo("Identifier expected prior");
                }
                else if (argsStart==-1){
                    throw new ExpectedTokenError("(");
                }
                if(argsEnd==-1){
                    throw new ExpectedTokenError(")");
                }
                string identifier = statement[0..statement.IndexOf('(')];
                if(!Utils.IsIdentifierValid(identifier)){
                    throw new InvalidIdentifierError(identifier);
                }
                string args = statement[(argsStart+1)..argsEnd];
                var argsArray = args.Split(',');
                if(argsArray.Length==1&&argsArray[0]==""){
                    argsArray = [];
                }
                List<VFuncArg> argsList = new List<VFuncArg>();
                foreach (var arg in argsArray)
                {
                    argsList.Add(new(arg));
                }
                VFunc func = new(identifier,File, [.. argsList],GetCurrentFrame().currentLine);
                var c = OpenNewContext(ScopeTypeEnum.functionScope);
                c.Ignore = true;
                c.SubsequentFramesIgnore = true;
                c.FuncBeingRead = func;
                c.FuncTopLevel = true;
                return true;
            }
            return false;
        }
        public void FinishFuncDeclaration(){
            var func = GetCurrentContext().FuncBeingRead;
            func!.FunctionBody = func.FunctionBody[..^1];
            GetCurrentFrame().ScopeStack.Pop();
            GetCurrentContext().Functions.Add(func.Identifier,func);
            
        }
        public bool CallFunctionStatement(string statement){
            if(Utils.StringContains(statement,"(")&&Utils.StringContains(statement,")")&&!statement.EndsWith(" :")){
                int argsStart = Utils.StringIndexOf(statement,"(");
                int argsEnd = Utils.StringIndexOf(statement,")");
                if(argsStart==0){
                   throw new UnexpectedTokenError("=").SetInfo("Function identifier expected prior");
                }
                else if (argsStart==-1){
                    throw new ExpectedTokenError("(");
                }
                if(argsEnd==-1){
                    throw new ExpectedTokenError(")");
                }
                string identifier = statement[0..statement.IndexOf('(')];
                if(!Utils.IsIdentifierValid(identifier)){
                    throw new InvalidIdentifierError(identifier);
                }
                if(!ReadFunction(identifier, out var func)){
                    if(!ReadVar(identifier, out var x)){
                        throw new UnknownNameError(identifier);
                    }
                    else{
                        throw new UnmatchingDataTypeError(x.type.ToString(),"VFunc");
                    }
                }
                string args = statement[(argsStart+1)..argsEnd];
                var argsArray = Utils.StringSplit(args,',');
                if(argsArray.Length==1&&argsArray[0]==""){
                    argsArray = [];
                }
                if(argsArray.Length!=func.Args.Length){//TODO: defualt params
                    throw new FuncOverloadNotFoundError(func.Identifier,argsArray.Length.ToString());
                }
                Dictionary<string,Variable> argsList = [];
                int i = 0;
                foreach (var arg in argsArray)
                {
                    argsList.Add(func.Args[i].name,ExpressionEval.Evaluate(arg,func.Args[i].enforcedType));
                    i++;
                }
                CallFunction(func,argsList);
                return true;
            }
            return false;
        }

        public void CallFunction(VFunc func,Dictionary<string,Variable> args){
            NewFrame(func.File,ScopeTypeEnum.functionScope,func.StartLine+1,func.GetFullPath());
            Interpreter funcInterpreter = new(func.File);
            foreach (var arg in args)
            {
                DeclareVar(arg.Key,arg.Value);
            }
            funcInterpreter.ExecuteLines(func.FunctionBody);
            CallStack.Pop();
        }
        //
        [MarkStatement("$",false)]
        public void DeclareStatement(string statement){
            statement = Utils.StringRemoveSpaces(statement);
            bool unsetable = statement[1]=='!';
            if(unsetable){
                statement = statement.Remove(1,1);
            }
            bool init = Utils.StringContains(statement,"=");
            string identifier =  init  ? statement[1..statement.IndexOf('=')] : statement[1..];
            if(!Utils.IsIdentifierValid(identifier)){
                throw new InvalidIdentifierError(identifier);
            }
            if(init){
                var initVal = ExpressionEval.Evaluate(statement[(statement.IndexOf('=') + 1)..]);
                initVal.unsetable = unsetable;
                bool failed = false;

                if(!DeclareVar(identifier, initVal))
                  failed = true;

                if(failed){
                    throw new VariableAlreadyDeclaredError(identifier);
                }
            }
            else{
                if(!DeclareVar(identifier,new(DataType.Unset,"unset",unsetable))){
                        throw new VariableAlreadyDeclaredError(identifier);
                    }
            }
            
        }
        [MarkStatement(">",false)]
        public void OutputStatement(string statement){
            Console.WriteLine(ExpressionEval.Evaluate(statement[1..]).value);
        }

        [MarkStatement("if",true,ScopeTypeEnum.ifScope)]
        public void IfStatement(string statement){
            if(statement[2]!=' '){
                throw new ExpectedTokenError(" ");
            }
            if(!statement.EndsWith(" :")){
                throw new ExpectedTokenError(" :");
            }
            int end = Utils.StringIndexOf(statement," :");
            string expression = statement[3..end];
            bool result = (bool)ExpressionEval.Evaluate(expression,DataType.Bool).value;
            GetCurrentContext().Ignore = !result;
            if(result){
                GetCurrentContext().SubsequentFramesIgnore = true;
            }
        }
        [MarkStatement(":",true,scopeType:ScopeTypeEnum.genericScope)]
        public void GenericStatementStart(string statement){

        }
        [MarkStatement(";",false,endsScope:true)]
        public void EndScopeStatement(string statement){
            
        }
        [MarkStatement("else :",true,ScopeTypeEnum.elseScope,true)]
        public void ElseScopeStatement(string statement){
            
        }
         [MarkStatement("else if",true,ScopeTypeEnum.elseScope,true)]
        public void ElseIfScopeStatement(string statement){
            if(statement[7]!=' '){
                throw new ExpectedTokenError(" ");
            }
            if(!statement.EndsWith(" :")){
                throw new ExpectedTokenError(" :");
            }
            int end = Utils.StringIndexOf(statement," :");
            string expression = statement[7..end];
            bool result = (bool)ExpressionEval.Evaluate(expression,DataType.Bool).value;
            GetCurrentContext().Ignore = !result;
            if(result){
                GetCurrentContext().SubsequentFramesIgnore = true;
            }
        }


        // public static bool ReadVar(string identifier, out Variable val,Context? scope = null){
        //     scope ??= GetCurrentContext();
        //     var combined = scope.Variables.Concat(SuperGlobals).ToDictionary(x=>x.Key,x=>x.Value);
        //     var res = combined.TryGetValue(identifier, out val);
        //     if(!val.unsetable&&val.type==DataType.Unset)
        //         throw new ReadingUnsetValue(identifier);
        //     return res;
        // }
        public static bool ReadVar(string identifier, out Variable val,Dictionary<string,Variable>? vars = null ){
            vars ??= Utils.GetAllVars();
            if(SuperGlobals.TryGetValue(identifier, out  val)){
                return true;
            }
            var res = vars.TryGetValue(identifier, out val);
            if(!res){
                if(!GetCurrentFrame().VFile.TopLevelContext.Variables.TryGetValue(identifier, out val)){
                    return false;
                }
                else{
                    res = true;
                }
            }

            if(!val.unsetable&&val.type==DataType.Unset)
                throw new ReadingUnsetValueError(identifier);
            return res;
        }
        public static bool ReadFunction(string identifier, out VFunc val){
            /*if(SuperGlobals.TryGetValue(identifier, out  val)){
                return true;
            }*/
            var funcs = new Dictionary<string,VFunc>{};
            foreach (var func in GetCurrentFrame().ScopeStack.Last().Functions){
                funcs.Add(func.Key,func.Value);
            }
            var res = funcs.TryGetValue(identifier, out val);
            if(!res){
                if(!GetCurrentFrame().VFile.TopLevelContext.Functions.TryGetValue(identifier, out val)){
                    return false;
                }
                else{
                    res = true;
                }
            }
            return res;
        }
        public bool SetVar(string identifier, Variable value,VContext? scope = null){
            scope ??= GetCurrentContext();
            if(scope.Variables.ContainsKey(identifier)){
                scope.Variables[identifier] = value;
                return true;
            }
            return false;
        }
        public bool SetVar(string identifier, Variable value){
            var vars = Utils.GetAllVars();
            if(vars.ContainsKey(identifier)){
                vars[identifier] = value;
                return true;
            }
            return false;
        }

        public bool DeclareVar(string identifier,Variable var){

            return GetCurrentContext().Variables.TryAdd(identifier,var);
        }
        public VFrame NewFrame(VFile file,ScopeTypeEnum scopeType,int lineOffset,string name){
            VFrame frame = new(file,lineOffset,name);
            frame.ScopeStack.Push(new ([],[],0,scopeType,StartLine:GetCurrentFrame().currentLine));
            CallStack.Push(frame);
            return frame;
        }
    }

    enum StatementAttributes
    {
        beginsWith=0,
        mewScope=1,
        scopeType=2,
        endScope=3
    }
}