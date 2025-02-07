using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Telemetry;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appSettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

var aspireTelemetryEndpoint = "http://localhost:4317";

// resource
var resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService("ConsoleAndAspireTelemetry");

// switch
AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", false);
AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnostics", true);

// providers
using (Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource("Microsoft.SemanticKernel*")
    .AddConsoleExporter()
    .AddOtlpExporter(options => options.Endpoint = new Uri(aspireTelemetryEndpoint))
    .Build())
using (Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter("Microsoft.SemanticKernel*")
    .AddConsoleExporter()
    .AddOtlpExporter(options => options.Endpoint = new Uri(aspireTelemetryEndpoint))
    .Build())
{
    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        // log provider
        builder.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.AddConsoleExporter();
            options.AddOtlpExporter(options => options.Endpoint = new Uri(aspireTelemetryEndpoint));
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });
        builder.SetMinimumLevel(LogLevel.Information);
    });

    var config = new
    {
        DeploymentName = configuration["SemanticKernel:DeploymentName"],
        Endpoint = configuration["SemanticKernel:Endpoint"],
        ApiKey = configuration["SemanticKernel:ApiKey"]
    };

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddAzureOpenAIChatCompletion(config.DeploymentName, config.Endpoint, config.ApiKey);

    builder.Services.AddSingleton(loggerFactory);

    builder.Services.AddTransient((serviceProvider) =>
    {
        var kernel = new Kernel(serviceProvider);
        return kernel;
    });

    builder.Services.AddHostedService<Worker>();

    using var host = builder.Build();

    await host.RunAsync();
}