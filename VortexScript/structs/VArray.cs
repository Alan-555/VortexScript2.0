using System.Linq;
namespace Vortex
{
    class VArray : List<V_Variable>
    {
        public override string ToString()
        {
            if(Count==0)
                return "[]";
            string ret = "[";
            ForEach(x=>ret += x.value.ToString()+",");
            ret = ret[..^1]+"]";
            return ret;
        }
    }
}