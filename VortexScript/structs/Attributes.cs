namespace Vortex
{
    public class MarkStatement : System.Attribute
    {
        public string StartsWith { get; private set; }
        public bool StartsNewScope { get; private set; }
        public ScopeTypeEnum ScopeType{ get; private set; }
        public bool EndsScope { get; private set; }
        public MarkStatement(string StartsWith, bool StartsNewScope, ScopeTypeEnum scopeType = ScopeTypeEnum.topLevel,bool endsScope = false)
        {
            this.StartsWith = StartsWith;
            this.StartsNewScope = StartsNewScope;
            ScopeType = scopeType;
            EndsScope = endsScope;
        }
    }
    public class InternalFunc : Attribute
    {
        public DataType ReturnType { get; set; }
    
        public InternalFunc(DataType returnType)
        {
            ReturnType = returnType;
        }
    
    }
}