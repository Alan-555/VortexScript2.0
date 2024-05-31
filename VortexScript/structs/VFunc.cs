using Vorteval;

namespace Vortex
{
    public class VFunc
    {
        public string Identifier { get; private set; }
        public VFile  File { get; private set; }
        public VFuncArg[] Args { get; private set; }

        public string[] FunctionBody { get;  set; } = [];

        public VFunc(string indetifier,VFile file,VFuncArg[] args){
            Identifier = indetifier;
            File = file;
            Args = args;
        }
    }

    public class VFuncArg{
        public DataType enforcedType;
        public Token defaultValue;
        public string name;

        public VFuncArg(string name,Token defaultValue =default, DataType enforcedType = DataType.Any)
        {
            this.name = name;
            this.defaultValue = defaultValue;
            this.enforcedType = enforcedType;
        }
    }
}
