namespace VortexScript.Vortex;

public class VClass{
    public string Identifier { get; private set;}

    public VContext TopLevelContext { get; private set; }

    public VFunc Constructor { get; private set; }

    public VClass(string identifier,VFile origin){
        Identifier = identifier;
        TopLevelContext = new VContext([],origin,scopeType:ScopeTypeEnum.classScope);

        Constructor = new VFunc("innerConstruct",TopLevelContext.File!,[],-1){
            CSharpFunc = typeof(VClass).GetMethod("ConstructType"),
            IsConstructor = true
            
        };
    }

    public static VClassInstance ConstructType(VClass class_){
        return new VClassInstance(class_,new(){{"testVar",V_Variable.Construct(DataType.Number,5d)}});
    }
    
}

public class VClassInstance{
    public VClass Type { get; private set; }
    public Dictionary<string,V_Variable> InstanceVars{ get; private set; }

    public VClassInstance(VClass type,Dictionary<string,V_Variable> vars){
        Type = type;
        InstanceVars = vars;
    }

    public override string ToString(){
        return Type+"_instance";
    }
    
}