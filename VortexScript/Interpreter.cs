using System.Reflection;
using System.Runtime.ExceptionServices;
using CodingSeb.ExpressionEvaluator;

namespace Vortex{
    public class Interpreter{
        //Internal stuff
        static MethodInfo[] statements=[];

        //Memory

        public static Stack<VFrame> CallStack {get;private set;} = [];

        public VFile File {private set; get; }
    
        public VFrame FileFrame {private set; get; }

        public static Dictionary<string,Variable> SuperGlobals {get; private set;} = new(){
            {"true", new Variable(DataType.Bool,true)},
            {"false", new Variable(DataType.Bool,false)},
            {"unset",new Variable(DataType.Unset,"")},
        };
        public Interpreter(VFile file){
            File = file;
            File.FileInterpreter = this;
            FileFrame = new(File,0);
            CallStack.Push(FileFrame);
            FileFrame.ScopeStack.Push(new VContext([],[],0,ScopeTypeEnum.topLevel));
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
            ExecuteFile(File.ReadFile());
        }

         void ExecuteFile(string[] lines){
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
        public void ExecuteStatement(String statement){
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
                //check for assignment
                if(AssignStatement(statement))
                    return;
                else
                if(!FuncDeclaration(statement))
                    throw new UnknownStatementError(statement);
                else
                    return;
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
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
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
                GetCurrentFrame().ScopeStack.Pop();

            }
        }

        public void OpenNewFrame(VFile file,int lineSart){
            var frame = new VFrame(file,lineSart);
            CallStack.Push(frame);
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
            if(GetCurrentContext().FuncBeingRead!=null||GetCurrentContext().ScopeType!=ScopeTypeEnum.topLevel){
                throw new IlegalDeclarationError("function").SetInfo("Functions may only be declared at the top level");
            }
            if(Utils.StringContains(statement,"(")&&Utils.StringContains(statement,")")&&statement.EndsWith(" :")){
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
                List<VFuncArg> argsList = new List<VFuncArg>();
                foreach (var arg in argsArray)
                {
                    argsList.Add(new(arg));
                }
                VFunc func = new(identifier,File, [.. argsList]);
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

                if(!GetCurrentContext().Variables.TryAdd(identifier, initVal))
                  failed = true;

                if(failed){
                    throw new VariableAlreadyDeclaredError(identifier);
                }
            }
            else{
                if(!GetCurrentContext().Variables.TryAdd(identifier,new(DataType.Unset,"unset",unsetable))){
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
        public static bool ReadVar(string identifier, out Variable val,Dictionary<string,Variable> vars ){
            if(SuperGlobals.TryGetValue(identifier, out  val)){
                return true;
            }
            var res = vars.TryGetValue(identifier, out val);
            if(!res){
                return false;
            }
            if(!val.unsetable&&val.type==DataType.Unset)
                throw new ReadingUnsetValueError(identifier);
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
    }

    enum StatementAttributes
    {
        beginsWith=0,
        mewScope=1,
        scopeType=2,
        endScope=3
    }
}