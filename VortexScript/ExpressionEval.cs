using System.Diagnostics;
using CodingSeb.ExpressionEvaluator;
using Vorteval;
namespace Vortex
{
    public class ExpressionEval
    {
        public static Variable Evaluate(string expression,DataType requiredType = DataType.None)
        {
            expression =  expression.Trim();
            Evaluator ev = new(Utils.GetAllVars());
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var result = ev.Evaluate(expression);
            sw.Stop();
            TimeSpan ts = sw.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
        Console.WriteLine("RunTime " + elapsedTime);
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
            if(requiredType!=DataType.None){
                if(result.type!=requiredType){
                    throw new UnmatchingDataTypeError(result.type.ToString(),requiredType.ToString());
                }
            }
            return result;
        }
    }
}