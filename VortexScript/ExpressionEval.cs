using System.Diagnostics;
using Vorteval;
namespace Vortex
{
    public class ExpressionEval
    {
        public static V_Variable Evaluate(string expression,DataType requiredType = DataType.None)
        {
            expression =  expression.Trim();
            Evaluator ev = new();
            var w = Utils.StartWatch();
            var result = ev.Evaluate(expression);
            Utils.StopWatch(w, "Expression "+expression);
            if(requiredType!=DataType.None&&requiredType!=DataType.Any){
                if(result.type!=requiredType){
                    throw new UnmatchingDataTypeError(result.type.ToString(),requiredType.ToString());
                }
            }
            return result;
        }
    }
}