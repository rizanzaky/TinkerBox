using Microsoft.SemanticKernel;

namespace KernelOne
{
    public class SkipFunctionFilter : IFunctionInvocationFilter
    {
        public Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
        {
            // skip the function invocation
            return Task.CompletedTask;
        }
    }
}
