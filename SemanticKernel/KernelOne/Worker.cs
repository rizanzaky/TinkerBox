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
                FunctionChoiceBehavior = FunctionChoiceBehavior.Required()
            };

            var chat = _kernel.GetRequiredService<IChatCompletionService>();

            // System message instructing AI
            history.Add(
                new()
                {
                    Role = AuthorRole.System,
                    Content = "Check history to know if the user needs content streaming or not-streaming."
                            + " If you can't find it, call the function `get_use_streaming_content` to determine the result type."
                }
            );

            history.Add(
                new()
                {
                    Role = AuthorRole.Assistant,
                    Items = [
                        new FunctionCallContent(
                            functionName: "get_use_streaming_content",
                            pluginName: "ContentResultType",
                            id: "0001"
                        )
                    ]
                }
            );

            history.Add(
                new()
                {
                    Role = AuthorRole.Tool,
                    Items = [
                        new FunctionResultContent(
                            functionName: "get_use_streaming_content",
                            pluginName: "ContentResultType",
                            callId: "0001",
                            result: "not-stream"
                        )
                    ]
                }
            );

            bool? streamContent = null;
            if (streamContent == null)
            {
                var useStreamOrNot = history.SelectMany(s => s.Items.OfType<FunctionResultContent>());
                var aiResponse = useStreamOrNot.FirstOrDefault()?.Result;
                if (Equals(aiResponse, "stream"))
                {
                    streamContent = true;
                    history.AddSystemMessage("User needs result streamed");
                }
                else if (Equals(aiResponse, "not-stream"))
                {
                    streamContent = false;
                    history.AddSystemMessage("User needs result not streamed");
                }
            }

            var input = "";
            do
            {
                int currentChatHistoryLength = history.Count;

                Console.Write("You: ");
                input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                history.AddUserMessage(input);

                if (streamContent == true)
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
                    history.Add(result);
                }

                for (int i = currentChatHistoryLength; i < history.Count; i++)
                {
                    Console.WriteLine(history[i]);
                }
            } while (input?.ToLower() != "exit");

            _hostApplicationLifetime.StopApplication();
        }
    }
}
