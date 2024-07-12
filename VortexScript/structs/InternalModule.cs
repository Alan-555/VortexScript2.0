using System.Reflection;

namespace Vortex{

    public  class InternalModule{
        [InternalFunc(DataType.Array)]
        public static V_Variable Array(string init){
            var data = Utils.ArgsEval(init,',') ?? throw new FuncOverloadNotFoundError("array",Utils.StringSplit(init,',').Length.ToString());
            return new(DataType.Array,data);
        }
    }

    public struct InternalModuleDefinition{
        public Dictionary<string,V_Variable> constants;
        public Dictionary<string,VFunc>  functions;
        public static readonly V_Variable Test = new(DataType.Number,19);
    }
}