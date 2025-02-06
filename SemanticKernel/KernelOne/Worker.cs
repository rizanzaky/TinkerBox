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
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false)
            };

            var chat = _kernel.GetRequiredService<IChatCompletionService>();

            history.Add(
                new()
                {
                    Role = AuthorRole.Assistant,
                    Items = [
                        new FunctionCallContent(
                            functionName: "get_use_streaming_content",
                            pluginName: "ContentResultType",
                            id: "0001",
                            arguments: new () { { "type", "stream" } }
                        ),
                        new FunctionCallContent(
                            functionName: "get_use_streaming_content",
                            pluginName: "ContentResultType",
                            id: "0002",
                            arguments: new () { { "type", "not-stream" } }
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
                            result: "stream"
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
                            callId: "0002",
                            result: "not-stream"
                        )
                    ]
                }
            );

            history.Add(
                new()
                {
                    Role = AuthorRole.System,
                    Content = "If the user says to give results streamed or not-streamed, invoke the function 'get_use_streaming_content' with the correct argument"
                }
            );

            bool streamContent = false;

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

                if (streamContent == true)
                {
                    var result = chat.GetStreamingChatMessageContentsAsync(history, settings, _kernel);
                    Console.Write($"\nAgent: ");
                    var contentBuilder = new FunctionCallContentBuilder();
                    await foreach (var item in result)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Content))
                        {
                            Console.Write(item.Content);
                        }

                        contentBuilder.Append(item);
                    }
                    Console.Write("\n\n");

                    var fs = contentBuilder.Build();
                    if (fs.Any())
                    {
                        var fcContent = new ChatMessageContent(role: AuthorRole.Assistant, content: null);
                        history.Add(fcContent);
                        foreach (var functionCall in fs)
                        {
                            fcContent.Items.Add(functionCall);

                            // we're skipping this with filters
                            //var functionResult = await functionCall.InvokeAsync(_kernel);
                            //history.Add(functionResult.ToChatMessage());

                            var type = (string)(functionCall.Arguments?.GetValueOrDefault("type") ?? "");
                            streamContent = type == "stream";
                            Console.Write($"Understood, answers to you will now be {type}ed\n\n");
                            history.Add(new FunctionResultContent(functionCall, type).ToChatMessage());
                        }
                    }
                }
                else
                {
                    var result = await chat.GetChatMessageContentAsync(history, settings, _kernel);
                    if (!string.IsNullOrWhiteSpace(result.Content))
                    {
                        Console.Write($"\nAgent: {result.Content}\n\n");
                        history.Add(result);
                        continue;
                    }

                    history.Add(result);
                    var functionCalls = FunctionCallContent.GetFunctionCalls(result);
                    if (!functionCalls.Any())
                    {
                        break;
                    }

                    foreach (var functionCall in functionCalls)
                    {
                        try
                        {
                            // we're skipping this with filters
                            //var resultContent = await functionCall.InvokeAsync(_kernel);
                            //history.Add(resultContent.ToChatMessage());

                            var type = (string)(functionCall.Arguments?.GetValueOrDefault("type") ?? "");
                            streamContent = type == "stream";
                            Console.Write($"\nAgent: Understood, answers to you will now be {type}ed\n\n");
                            history.Add(new FunctionResultContent(functionCall, type).ToChatMessage());
                        }
                        catch (Exception ex)
                        {
                            history.Add(new FunctionResultContent(functionCall, ex).ToChatMessage());
                        }
                    }
                }
            } while (input?.ToLower() != "exit");

            _hostApplicationLifetime.StopApplication();
        }
    }
}
