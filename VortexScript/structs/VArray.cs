using System.Linq;
namespace Vortex
{
    class VArray : List<V_Variable>
    {
        public override string ToString()
        {
            var arr = this;
            if(arr.Count==0)
                return "[]";
            string ret = "[";
            arr.ForEach(x=>ret += x.value.ToString()+",");
            ret = ret[..^1]+"]";
            return ret;
        }
    }
}