using ImageProcessor.Models;
using Microsoft.Extensions.Configuration;

namespace ImageProcessor.Configuration;

public class ConsoleConfiguration
{
    public ConsoleConfiguration(string metadataFilePath, List<string> imageDirectories, string azureStorageConnectionString, int[] sizes, ApplicationCommand command = ApplicationCommand.ProcessImages)
    {
        MetadataFilePath = metadataFilePath;
        ImageDirectories = imageDirectories;
        AzureStorageConnectionString = azureStorageConnectionString;
        Sizes = sizes;
        Command = command;
    }

    public ConsoleConfiguration(IConfigurationSection configSection)
    {
        MetadataFilePath =
            configSection.GetValue<string>(nameof(MetadataFilePath)) ??
            throw new ApplicationException($"{nameof(MetadataFilePath)} must be set in configuration");

        ImageDirectories =
            configSection.GetSection(nameof(ImageDirectories)).Get<List<string>>() ??
            throw new ApplicationException($"{nameof(ImageDirectories)} must be set in configuration");

        AzureStorageConnectionString =
            configSection.GetValue<string>(nameof(AzureStorageConnectionString)) ??
            throw new ApplicationException($"{nameof(AzureStorageConnectionString)} must be set in configuration");

        Sizes =
            configSection.GetSection(nameof(Sizes)).Get<int[]>() ??
            throw new ApplicationException($"{nameof(Sizes)} must be set in configuration");
            
        Command = configSection.GetValue<ApplicationCommand>(nameof(Command), ApplicationCommand.ProcessImages);
    }

    public string MetadataFilePath { get; set; }

    public List<string> ImageDirectories { get; set; }

    public string AzureStorageConnectionString { get; set;  }

    public int[] Sizes { get; set;  }

    public int MaxDegreeOfParallelization { get; set; } = 16;
    
    public ApplicationCommand Command { get; set; }
}