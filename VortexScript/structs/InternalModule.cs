namespace Vortex{

    public class InternalModule{

        public string Name { get; set; }
        public VContext ModuleContext {get;set;}

        public InternalModule(string name, VContext moduleContext){
            Name = name;
            ModuleContext = moduleContext;
        }

    }
}