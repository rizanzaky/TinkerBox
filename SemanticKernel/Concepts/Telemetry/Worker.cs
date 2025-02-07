using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Telemetry;

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

        var chat = _kernel.GetRequiredService<IChatCompletionService>();

        var input = "";
        do
        {
            Console.Write("You: ");
            input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                Console.Write($"\nAgent: Mmm... you didn't say anything!\n\n");
                continue;
            }

            history.AddUserMessage(input);

            var result = await chat.GetChatMessageContentAsync(history, kernel: _kernel);
            if (!string.IsNullOrWhiteSpace(result.Content))
            {
                Console.Write($"\nAgent: {result.Content}\n\n");
                history.Add(result);
                continue;
            }

            history.Add(result);
        }
        while (input?.ToLower() != "exit");

        _hostApplicationLifetime.StopApplication();
    }
}