using System.Reflection;

namespace Vortex{

    public  class InternalModule{
    }

    public struct InternalModuleDefinition{
        public Dictionary<string,V_Variable> constants;
        public Dictionary<string,VFunc>  functions;
    }
}