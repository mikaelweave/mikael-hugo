using ImageProcessor.Configuration;
using ImageProcessor.Models;
using ImageProcessor.Repositories;
using System.Text.Json; // Required for JsonSerializerOptions

namespace ImageProcessor
{
    public class ApplicationRunner
    {
        // Keep the serializer options consistent with MetadataStore
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            WriteIndented = true // Make the output JSON readable
        };

        public static async Task RunApplication(ConsoleConfiguration consoleConfiguration)
        {
            // Use using statement for IDisposable resources
            using MetadataStore metadataStore = new(consoleConfiguration.MetadataFilePath);
            // RemoteImageStore isn't IDisposable currently
            RemoteImageStore remoteImageStore = new(consoleConfiguration.AzureStorageConnectionString, consoleConfiguration.Sizes);

            try
            {
                 switch (consoleConfiguration.Command)
                 {
                     case ApplicationCommand.ProcessImages:
                         await ProcessImages(consoleConfiguration, metadataStore, remoteImageStore);
                         break;
                     case ApplicationCommand.RebuildMetadata:
                         await RebuildMetadata(consoleConfiguration, metadataStore, remoteImageStore);
                         break;
                     default:
                         Console.WriteLine($"Unknown command: {consoleConfiguration.Command}");
                         break;
                 }
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"Unhandled application error: {ex.Message}");
                 Console.WriteLine(ex.StackTrace);
                 // Optionally set exit code
            }
            // metadataStore.Dispose() will be called automatically by 'using' statement (which calls Save)
            Console.WriteLine("Application finished.");
        }

        // Pass stores as arguments
        private static async Task ProcessImages(ConsoleConfiguration config, MetadataStore metadataStore, RemoteImageStore remoteImageStore)
        {
            Console.WriteLine("Checking for untracked local images...");

            // Filter out images that are already in the metadata store
            var untrackedLocalImages = LocalImage.GetUntrackedLocalImages(config.ImageDirectories, metadataStore.KnownImagePaths)
                .Where(image => !metadataStore.Contains(image.RelativeImagePath))
                .ToList();

            Console.WriteLine($"Found {untrackedLocalImages.Count} new image(s) to process.");
            if (untrackedLocalImages.Count == 0)
            {
                Console.WriteLine("No new images to process.");
                return;
            }

            Console.WriteLine("Resizing and uploading...");

            var semaphore = new SemaphoreSlim(config.MaxDegreeOfParallelization);
            var tasks = new List<Task>();
            int processedCount = 0;
            int skippedCount = 0;

            foreach (var localImage in untrackedLocalImages)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        Console.WriteLine($"Processing: {localImage.RelativeImagePath}");
                        ImageMetadata imageMetadata = await remoteImageStore.UploadSizesFromLocal(localImage);

                        if (metadataStore.TryAdd(localImage.RelativeImagePath, imageMetadata))
                        {
                            Console.WriteLine($"--> Added metadata for: {localImage.RelativeImagePath}");
                            Interlocked.Increment(ref processedCount);
                        }
                        else
                        {
                            Console.WriteLine($"--> Skipped (already exists in metadata): {localImage.RelativeImagePath}");
                            Interlocked.Increment(ref skippedCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing {localImage.RelativeImagePath}: {ex.Message}");
                    }
                    finally
                    {
                        localImage.Dispose();
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            Console.WriteLine($"Processing complete. Added: {processedCount}, Skipped: {skippedCount}. Metadata will be saved.");
        }

        // Pass stores as arguments
        private static async Task RebuildMetadata(ConsoleConfiguration config, MetadataStore metadataStore, RemoteImageStore remoteImageStore)
        {
            Console.WriteLine("Scanning Azure Blob Storage to rebuild metadata file...");

            try
            {
                int maxParallelContainers = Math.Max(1, Math.Min(Environment.ProcessorCount, 8)); // Adjust as needed
                Console.WriteLine($"Using parallel processing with up to {maxParallelContainers} concurrent containers.");

                // Scan blob storage - returns Dictionary<string, ImageMetadata>
                var scannedMetadata = await remoteImageStore.ScanBlobStorageForImagesParallel(maxParallelContainers);

                Console.WriteLine($"Scan complete. Found {scannedMetadata.Count} images in Azure Blob Storage.");

                if (scannedMetadata.Any())
                {
                     Console.WriteLine("Sample of images found:");
                     int sampleCount = Math.Min(5, scannedMetadata.Count);
                     foreach (var item in scannedMetadata.Take(sampleCount))
                     {
                          // Display more relevant info from ImageMetadata
                          Console.WriteLine($" - {item.Key}: {item.Value.OriginalWidth}x{item.Value.OriginalHeight}, {item.Value.FormatWidths.Count} format(s), {item.Value.FormatWidths.Values.SelectMany(w => w).Count()} resized version(s)");
                     }
                     if (scannedMetadata.Count > sampleCount) Console.WriteLine($" - ...and {scannedMetadata.Count - sampleCount} more.");
                } else {
                     Console.WriteLine("No images found in blob storage matching criteria.");
                }

                 // Replace data in the store instead of serializing directly here
                 Console.WriteLine("Replacing data in metadata store...");
                 metadataStore.ReplaceAll(scannedMetadata);

                 // Save will be handled by Dispose, but can force save here if needed:
                 // metadataStore.Save();

                 Console.WriteLine($"Metadata store updated with {scannedMetadata.Count} entries. File will be saved.");
                 // Size check can happen after dispose or if Save is called explicitly
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during metadata rebuild: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}