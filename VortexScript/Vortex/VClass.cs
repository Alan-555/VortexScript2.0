namespace VortexScript.Vortex;

public class VClass{
    public string Identifier { get; private set;}

    public VContext TopLevelContext { get; private set; }

    public VClass(string identifier,VFile origin){
        Identifier = identifier;
        TopLevelContext = new VContext([],origin,scopeType:ScopeTypeEnum.classScope);
    }
    
}