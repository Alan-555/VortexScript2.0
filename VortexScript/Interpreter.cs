using System.Reflection;
using System.Runtime.ExceptionServices;
using CodingSeb.ExpressionEvaluator;

namespace Vortex{
    public class Interpreter{
        //Internal stuff
        MethodInfo[] statements;

        //Memory
        public string File {private set; get; }
    
        public Stack<Context> ScopeStack {get; private set;}

        public int Line {private set; get; }

        public static Interpreter Instance {get; private set;}

        public static Dictionary<string,Variable> SuperGlobals {get; private set;} = new(){
            {"true", new Variable(DataType.Bool,true)},
            {"false", new Variable(DataType.Bool,false)},
            {"unset",new Variable(DataType.Unset,"")},
        };
        public Interpreter(string file){
            Instance = this;
            ScopeStack = new ();
            File = file;
            ScopeStack.Push(new Context([],0,ScopeTypeEnum.topLevel));
            InitStatements();
            ExecuteFile(FileReader.ReadFile(File));
        }
        
        public void InitStatements(){
            statements = Assembly.GetAssembly(typeof(Interpreter)).GetTypes()
                      .SelectMany(t => t.GetMethods())
                      .Where(m => m.GetCustomAttributes(typeof(MarkStatement), false).Length > 0)
                      .ToArray();
        }

        public void ExecuteFile(string[] lines){
            foreach(string line in lines){
                try{    
                    ExecuteLine(line);
                    Line++;
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
                if(!AssignStatement(statement))
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

        public Context OpenNewContext(ScopeTypeEnum type){
            var newC = new Context([],ScopeStack.Count,type,StartLine:Line);
            ScopeStack.Push(newC);
            return newC;
        }
        public void CloseContext(){
            ScopeStack.Pop();
        }
        public Context GetCurrentContext(){
            return ScopeStack.First();
        }
        public bool AssignStatement(string statement){
            if(Utils.StringContains(statement,"=")){
                var middle = Utils.StringIndexOf(statement,"=");
                if(middle<1){
                    var e = new UnexpectedTokenError("=");
                    e.info = "Identifier expected prior";
                    throw e;

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


        public static bool ReadVar(string identifier, out Variable val,Context? scope = null){
            scope ??= Instance.GetCurrentContext();
            var combined = scope.Variables.Concat(SuperGlobals).ToDictionary(x=>x.Key,x=>x.Value);
            var res = combined.TryGetValue(identifier, out val);
            if(!val.unsetable&&val.type==DataType.Unset)
                throw new ReadingUnsetValue(identifier);
            return res;
        }
        public static bool ReadVar(string identifier, out Variable val,Dictionary<string,Variable> vars ){
            if(SuperGlobals.TryGetValue(identifier, out  val)){
                return true;
            }
            var res = vars.TryGetValue(identifier, out val);
            if(!res){
                return false;
            }
            if(!val.unsetable&&val.type==DataType.Unset)
                throw new ReadingUnsetValue(identifier);
            return res;
        }
        public bool SetVar(string identifier, Variable value,Context? scope = null){
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