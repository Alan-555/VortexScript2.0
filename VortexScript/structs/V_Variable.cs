namespace Vortex{
    public struct V_Variable
    {
        public DataType type;
        public object value;
        public bool unsetable;
        public V_Variable(DataType type, object value,bool unsetable = false){
            this.type = type;
            this.value = value;
            this.unsetable = unsetable;
            if(type==DataType.Unset)
                this.value = "unset";
        }
    }

    public enum DataType
    {
        None = -1,
        String = 0,
        Number = 1,
        Bool = 2,
        Unset = 3,
        Any = 4,
        Int = 5
    }
}