namespace Vortex{
    public struct V_Variable
    {
        public DataType type;
        public object value;
        public bool unsetable;
        public bool readonly_ = false;
        public V_Variable(DataType type, object value,bool unsetable = false,bool readonly_ = false){
            if(value is double v && double.IsInfinity(v)){
                this.type = DataType.NaN;
                this.value = "NaN";
            }
            else{
                this.type = type;
                this.value = value;
            }
            this.unsetable = unsetable;
            if(type==DataType.Unset)
                this.value = "unset";
            if(value==null)
                this.value = "unset";
            this.readonly_ = readonly_;
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
        Int = 5,
        NaN = 6,
        Array = 7
    }
}