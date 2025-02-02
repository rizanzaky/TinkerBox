using KernelOne;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

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

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAzureOpenAIChatCompletion(config.DeploymentName, config.Endpoint, config.ApiKey);

builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Information));

builder.Services.AddSingleton<LightsPlugin>();
builder.Services.AddSingleton<KernelPluginCollection>((serviceProvider) =>
    [
        KernelPluginFactory.CreateFromObject(serviceProvider.GetRequiredService<LightsPlugin>())
    ]
);

builder.Services.AddTransient((serviceProvider) =>
{
    var pluginCollection = serviceProvider.GetRequiredService<KernelPluginCollection>();
    return new Kernel(serviceProvider, pluginCollection);
});

builder.Services.AddHostedService<Worker>();

using var host = builder.Build();

await host.RunAsync();