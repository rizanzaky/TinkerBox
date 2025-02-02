using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace KernelOne
{
    public class Worker : BackgroundService
    {
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly Kernel _kernel;

        public Worker(IHostApplicationLifetime hostApplicationLifetime, Kernel kernel)
        {
            _hostApplicationLifetime = hostApplicationLifetime;
            _kernel = kernel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var history = new ChatHistory();
            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var chat = _kernel.GetRequiredService<IChatCompletionService>();

            var input = "";
            do
            {
                Console.Write("You: ");
                input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                history.AddUserMessage(input);
                if (input.Contains("stream"))
                {
                    var result = chat.GetStreamingChatMessageContentsAsync(history, settings, _kernel);
                    Console.Write($"\nAgent: ");
                    await foreach (var item in result)
                    {
                        Console.Write(item.Content);
                    }
                    Console.Write("\n\n");
                }
                else
                {
                    var result = await chat.GetChatMessageContentAsync(history, settings, _kernel);
                    Console.Write($"\nAgent: {result.Content}\n\n");
                }
            } while (input?.ToLower() != "exit");

            _hostApplicationLifetime.StopApplication();
        }
    }
}
