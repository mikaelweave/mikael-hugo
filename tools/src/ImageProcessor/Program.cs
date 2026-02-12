using ImageProcessor;
using ImageProcessor.Configuration;
using ImageProcessor.Models;
using Microsoft.Extensions.Configuration;

// Enable loading configuration from json files
var builder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

IConfigurationRoot configuration = builder.Build();
var consoleConfiguration = new ConsoleConfiguration(configuration.GetSection("ConsoleConfiguration"));

// Process command-line arguments
if (args.Length > 0)
{
    if (args[0].Equals("rebuild-metadata", StringComparison.OrdinalIgnoreCase))
    {
        consoleConfiguration.Command = ApplicationCommand.RebuildMetadata;
    }
    else if (args[0].Equals("process-images", StringComparison.OrdinalIgnoreCase))
    {
        consoleConfiguration.Command = ApplicationCommand.ProcessImages;
    }
    else
    {
        Console.WriteLine($"Unknown command: {args[0]}");
        Console.WriteLine("Available commands: process-images, rebuild-metadata");
        return;
    }
}

await ApplicationRunner.RunApplication(consoleConfiguration);