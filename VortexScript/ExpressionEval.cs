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
            /*ExpressionEvaluator eval = new(vars)
            {
                OptionForceIntegerNumbersEvaluationsAsDoubleByDefault = true,
                OptionInlineNamespacesEvaluationRule = InlineNamespacesEvaluationRule.BlockAll,
                OptionFluidPrefixingActive = false,
                OptionNewFunctionEvaluationActive = false,
                OptionNewKeywordEvaluationActive = false,
                OptionStaticMethodsCallActive = false,
                OptionStaticPropertiesGetActive = false,
                OptionInstanceMethodsCallActive = false,
                OptionInstancePropertiesGetActive = false,
                OptionIndexingActive = false,
                OptionStringEvaluationActive = true,
                OptionEvaluateFunctionActive = false,
                OptionVariableAssignationActive = false,
                OptionPropertyOrFieldSetActive = false,
                OptionIndexingAssignationActive = false,
                OptionScriptEvaluateFunctionActive = false
            };

            try{
                return eval.Evaluate(expression).ToString();
            }
            catch(ExpressionEvaluatorSyntaxErrorException  e){
               throw new ExpressionEvalError(e.Message);
            }
            catch (System.ArgumentOutOfRangeException e){
                throw new ExpressionEvalError("Expression failed to evaluate due to an IOOBE");
            }
            catch(Exception e) {
                throw new ExpressionEvalError("Unknown error has occured while evaluating expression '"+expression+"'");
            }*/
            if(requiredType!=DataType.None&&requiredType!=DataType.Any){
                if(result.type!=requiredType){
                    throw new UnmatchingDataTypeError(result.type.ToString(),requiredType.ToString());
                }
            }
            return result;
        }
    }
}