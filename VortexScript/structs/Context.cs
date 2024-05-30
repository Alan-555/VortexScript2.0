namespace Vortex
{
    public class Context
    {
        public Dictionary<string, Variable> Variables {private set; get;} //Local variables
        public int Depth {private set; get;} //How deep we are
        public ScopeTypeEnum ScopeType {private set; get;} //Type of the scope
        public bool Ignore {set; get;} //When true, code will not be executed. Scopes won't be ignored
        public int StartLine {private set; get;} //The line that this scope was started at
        public bool SubsequentFramesIgnore {set; get;} //When true, all subsequent frames in the stack will inhirit ignore flag and this flag, no matter the actual condition
        public Context(Dictionary<string, Variable> vars, int depth = 0, ScopeTypeEnum scopeType = ScopeTypeEnum.genericScope, bool ignore = false, int StartLine = 0 ){
            Variables = vars;
            Depth = depth;
            ScopeType = scopeType;
            Ignore = ignore;
            this.StartLine = StartLine;
            SubsequentFramesIgnore = false;
        }
    }

    public enum ScopeTypeEnum{
        topLevel = 0, //top level of a file
        ifScope = 1, //the scope immediately after an if statement 
        elseScope = 2, //the scope immediately after an else statement
        functionScope = 3, //scope inside a function
        genericScope = 4 //scope defined by the user
    }
}