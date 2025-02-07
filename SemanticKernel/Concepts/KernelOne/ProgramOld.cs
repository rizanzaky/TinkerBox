using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace KernelOne
{
    internal class ProgramOld
    {
        internal async Task Main()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<Program>()
                .Build();

            var config = new
            {
                DeploymentName = configuration["SemanticKernel:DeploymentName"],
                Endpoint = configuration["SemanticKernel:Endpoint"],
                ApiKey = configuration["SemanticKernel:ApiKey"]
            };

            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(config.DeploymentName, config.Endpoint, config.ApiKey);
            builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Error));
            var kernel = builder.Build();

            var chat = kernel.GetRequiredService<IChatCompletionService>();
            kernel.Plugins.AddFromType<LightsPlugin>("Lights");
            var settings = new OpenAIPromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };
            var history = new ChatHistory();

            string? prompt = null;
            do
            {
                Console.Write("You: ");
                prompt = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    continue;
                }

                history.AddUserMessage(prompt);
                var result = await chat.GetChatMessageContentAsync(history, settings, kernel);
                Console.WriteLine($"\nAI: {result.Content}\n\n");
                history.AddMessage(result.Role, result.Content ?? string.Empty);
            } while (prompt != "exit");
        }
    }
}
