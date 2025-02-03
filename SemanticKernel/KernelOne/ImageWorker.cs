using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace KernelOne
{
    public class ImageWorker : BackgroundService
    {
        private readonly Kernel _kernel;

        public ImageWorker(Kernel kernel)
        {
            _kernel = kernel;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var chat = _kernel.GetRequiredService<IChatCompletionService>();

                // Load an image from disk.
                byte[] bytes = File.ReadAllBytes("data/africa.jpg");
                var base64String = Convert.ToBase64String(bytes);
                var dataUrl = $"data:image/jpeg;base64,{base64String}";

                // Create a chat history with a system message instructing
                // the LLM on its required role.
                var chatHistory = new ChatHistory("Your job is describing images.");

                // Add a user message with both the image and a question
                // about the image.
                chatHistory.AddUserMessage(
                [
                    new TextContent("What’s in this image?"),
                    new ImageContent(new Uri(dataUrl)),
                ]);

                // Invoke the chat completion model.
                var reply = await chat.GetChatMessageContentAsync(chatHistory);
                Console.WriteLine(reply.Content);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }
    }
}
