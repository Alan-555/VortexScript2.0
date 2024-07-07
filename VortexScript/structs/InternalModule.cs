using System.Reflection;

namespace Vortex{

    public  class InternalModule{
    }

    public struct InternalModuleDefinition{
        public Dictionary<string,V_Variable> constants;
        public Dictionary<string,VFunc>  functions;
        public static readonly V_Variable Test = new(DataType.Number,19);
    }
}