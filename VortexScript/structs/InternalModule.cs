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

    }

    public struct InternalModuleDefinition{
        public Dictionary<string,V_Variable> constants;
        public Dictionary<string,VFunc>  functions;
    }
}