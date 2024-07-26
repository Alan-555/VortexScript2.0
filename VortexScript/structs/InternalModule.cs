using System.Reflection;

namespace Vortex{

    public  class InternalModule{
        [InternalFunc(DataType.Array)]
        public static V_Variable Array(string init){
            var data = Utils.ArgsEval(init,',') ?? throw new FuncOverloadNotFoundError("array",Utils.StringSplit(init,',').Length.ToString());
            return V_Variable.Construct(DataType.Array,data);
        }
        [InternalFunc(DataType.None)]
        public static V_Variable Print(string data){
            Console.WriteLine(data);
            return null;
        }
        [InternalFunc(DataType.Type)]
        public static V_Variable TypeOf(V_Variable var){
            return V_Variable.Construct(DataType.Type,var.type.ToString());
        }
        [InternalFunc(DataType.Type)]
        public static V_Variable MakeType(string var){
            return V_Variable.Construct(DataType.Type,var);
        }
        [InternalFunc(DataType.Any)]
        public static V_Variable New(DataType type,string var){
            try{
                return V_Variable.Construct(type,var);
            }
            catch (FormatException){
                throw new InvalidFormatError(var,type.ToString());
            }
        }
        [InternalFunc(DataType.Indexer)]
        public static V_Variable Indexer(int val){
            return V_Variable.Construct(DataType.Indexer,val);
        }
        [InternalFunc(DataType.None)]
        public static V_Variable ThrowError(V_Variable v,string message){
            var str = (GroupType)v.value;
            if(v.type!=DataType.GroupType){
                throw new InvalidFormatError(v.type.ToString(),"GroupType");
            }
            if(str.groupName!="error"){
                throw new UnmatchingGroupTypeError(str.groupName,"error");
            }
            var err = (Type)str.value;
            if(!err.IsSubclassOf(typeof(VortexError)))
                throw new InvalidFormatError(err.GetType().Name,"Error");
            if(err == typeof(ExpressionEvalError)){
                throw new IlegalOperationError("ExpressionEvalError may only be used during an expression evaluation (missing eval instance)");
            }
            try{
                throw (VortexError)Activator.CreateInstance(err,"!"+message);
            }
            catch(MissingMethodException){
                throw new IlegalOperationError("This error may not be raised using the raise statement");
            }
        }
        [InternalFunc(DataType.Error)]
        public static V_Variable Error(string message){
            return New(DataType.Error,message);
        }


    }

    public struct InternalModuleDefinition{
        public Dictionary<string,V_Variable> constants;
        public Dictionary<string,VFunc>  functions;
    }
}