using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ImageProcessor.Models;
using ImageMagick; // Required for MagickFormat and MagickImage
using System.Collections.Concurrent;
using Azure; // Required for RequestFailedException

namespace ImageProcessor.Repositories;

public class RemoteImageStore(string connectionString, int[] sizes)
{
    // Use ConcurrentDictionary for thread-safe container client caching
    private readonly ConcurrentDictionary<string, BlobContainerClient> _containerClients = new();
    private readonly string _connectionString = connectionString;
    private readonly int[] _sizes = [.. sizes.OrderBy(s => s)]; // Ensure sizes are sorted

    // Constants for metadata keys
    private const string WidthMetadataKey = "width";
    private const string HeightMetadataKey = "height";

    // Repositories/RemoteImageStore.cs

    public async Task<ImageMetadata> UploadSizesFromLocal(LocalImage localImage)
    {
        // Use localImage properties directly for original dimensions
        var imageMetadata = new ImageMetadata(localImage.WidthPixels, localImage.HeightPixels);

        string containerName = GetContainerNameFromRelativePath(localImage.RelativeImagePath);
        // Get blob name relative to container (e.g., path/image.jpg)
        string blobNameBase = GetBlobNameFromRelativePath(localImage.RelativeImagePath);

        BlobContainerClient containerClient = GetOrCreateBlobContainerClient(containerName);

        // --- Upload Original Image ---
        var origBlobClient = containerClient.GetBlobClient(blobNameBase);
        var originalMetadata = new Dictionary<string, string>
        {
            { WidthMetadataKey, localImage.WidthPixels.ToString() },
            { HeightMetadataKey, localImage.HeightPixels.ToString() } // Keep height in blob metadata
        };
        var blobUploadOptions = new BlobUploadOptions
        {
            Metadata = originalMetadata,
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = GetMimeType(localImage.ImageFormat)
            }
        };

        try
        {
            Console.WriteLine($"Uploading original: {containerName}/{blobNameBase} ({localImage.WidthPixels}x{localImage.HeightPixels})");
            await origBlobClient.UploadAsync(localImage.ImagePath, blobUploadOptions);
            // Original dimensions are stored in ImageMetadata directly, not in Formats
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error uploading original image {localImage.ImagePath} to {blobNameBase}: {ex.Message}");
            // Depending on desired behavior, might return null or throw
            throw;
        }

        // --- Determine Formats to Generate ---
        WebImageFormat originalFormat = GetWebImageFormat(localImage.ImageFormat);
        WebImageFormat[] resizeFormats = GetResizeFormats(originalFormat);

        if (!resizeFormats.Any())
        {
            Console.WriteLine($"Skipping resize for {localImage.ImagePath} due to unsupported original format: {localImage.ImageFormat}");
            return imageMetadata; // Return metadata with only original dimensions
        }

        // --- Resize and Upload Other Versions ---
        string baseNameWithoutExt = Path.ChangeExtension(blobNameBase, null); // e.g., path/image

        foreach (var size in _sizes) // _sizes should be sorted class member
        {
            // Only resize if the target size is smaller than the original width
            if (localImage.WidthPixels > size)
            {
                foreach (var format in resizeFormats)
                {
                    string tempResizedImagePath = string.Empty;
                    try
                    {
                        // Calculate resized height (still needed for blob metadata)
                        int resizedHeight = CalculateResizedHeight(localImage.WidthPixels, localImage.HeightPixels, size);
                        if (resizedHeight <= 0)
                        {
                            Console.WriteLine($"  Skipping size {size} for {blobNameBase} due to invalid calculated height.");
                            continue;
                        }

                        tempResizedImagePath = localImage.ResizeAndSaveToTemp(format, size); // ImageMagick handles format conversion

                        // Construct blob name: path/image_SIZEw.FORMAT
                        string formatExtension = format.ToString().ToLowerInvariant();
                        string resizedImageBlobName = $"{baseNameWithoutExt}_{size}w.{formatExtension}";

                        var resizedBlobClient = containerClient.GetBlobClient(resizedImageBlobName);
                        // Create metadata for the *blob* itself (optional but good practice)
                        var resizedMetadata = new Dictionary<string, string>
                    {
                        { WidthMetadataKey, size.ToString() },
                        { HeightMetadataKey, resizedHeight.ToString() }
                    };
                        var resizedBlobUploadOptions = new BlobUploadOptions
                        {
                            Metadata = resizedMetadata,
                            HttpHeaders = new BlobHttpHeaders
                            {
                                ContentType = GetMimeType(format)
                            }
                        };

                        Console.WriteLine($"  Uploading resized: {containerName}/{resizedImageBlobName} ({size}x{resizedHeight})");
                        await resizedBlobClient.UploadAsync(tempResizedImagePath, resizedBlobUploadOptions);

                        // Add only the successfully uploaded WIDTH to the app's metadata object
                        imageMetadata.AddFormatWidth(format, size);
                    }
                    catch (Exception ex)
                    {
                        // Log specific error for this size/format combination
                        Console.WriteLine($"Error processing/uploading size {size} format {format} for {localImage.ImagePath}: {ex.Message}");
                        // Continue with other sizes/formats
                    }
                    finally
                    {
                        // Clean up temp file regardless of success/failure
                        if (!string.IsNullOrEmpty(tempResizedImagePath) && File.Exists(tempResizedImagePath))
                        {
                            try { File.Delete(tempResizedImagePath); } catch { /* Ignore cleanup error */ }
                        }
                    }
                }
            }
        }

        return imageMetadata;
    }

    // Helper to get container client safely
    private BlobContainerClient GetOrCreateBlobContainerClient(string containerName)
    {
        return _containerClients.GetOrAdd(containerName, name =>
        {
            var client = new BlobServiceClient(_connectionString).GetBlobContainerClient(name);
            try
            {
                // Ensure container exists (idempotent)
                client.CreateIfNotExists(publicAccessType: PublicAccessType.Blob);
            }
            catch (RequestFailedException ex)
            {
                // Handle potential permission issues, etc.
                Console.WriteLine($"Warning: Failed to ensure container '{name}' exists: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: An unexpected error occurred ensuring container '{name}' exists: {ex.Message}");
            }
            return client;
        });
    }

    // Helper to get resize formats based on original
    private WebImageFormat[] GetResizeFormats(WebImageFormat originalFormat) => originalFormat switch
    {
        WebImageFormat.Png => [WebImageFormat.Png, WebImageFormat.WebP], // Generate WebP from PNG too
        WebImageFormat.Jpg => [WebImageFormat.Jpg, WebImageFormat.WebP],
        _ => [], // Don't resize other formats by default
    };

    // Centralized aspect ratio calculation
    private int CalculateResizedHeight(int originalWidth, int originalHeight, int targetWidth)
    {
        if (originalWidth <= 0 || originalHeight <= 0 || targetWidth <= 0) return 0;
        // Use double for precision during calculation
        return (int)Math.Round((double)originalHeight * targetWidth / originalWidth);
    }

    // Map MagickFormat to WebImageFormat
    private WebImageFormat GetWebImageFormat(MagickFormat format) => format switch
    {
        MagickFormat.Png => WebImageFormat.Png,
        MagickFormat.Jpg or MagickFormat.Jpeg => WebImageFormat.Jpg, // Consolidate Jpeg to Jpg
        MagickFormat.WebP => WebImageFormat.WebP, // If original could be WebP
        _ => throw new ArgumentException($"Unsupported MagickFormat for web conversion: {format}"), // Be stricter here
    };

    // Map WebImageFormat to MIME type for blob Content-Type headers
    private static string GetMimeType(WebImageFormat format) => format switch
    {
        WebImageFormat.Jpg => "image/jpeg",
        WebImageFormat.Png => "image/png",
        WebImageFormat.WebP => "image/webp",
        _ => "application/octet-stream",
    };

    // Map MagickFormat to MIME type for blob Content-Type headers
    private static string GetMimeType(MagickFormat format) => format switch
    {
        MagickFormat.Jpg or MagickFormat.Jpeg => "image/jpeg",
        MagickFormat.Png => "image/png",
        MagickFormat.WebP => "image/webp",
        _ => "application/octet-stream",
    };

    // Helper to parse container name from relative path (e.g., "container/path/img.jpg" -> "container")
    private string GetContainerNameFromRelativePath(string relativePath)
    {
        var parts = relativePath.Split(new[] { '/' }, 2);
        return parts.Length > 0 ? parts[0] : throw new ArgumentException($"Invalid relative image path format: {relativePath}");
    }

    // Helper to parse blob name from relative path (e.g., "container/path/img.jpg" -> "path/img.jpg")
    private string GetBlobNameFromRelativePath(string relativePath)
    {
        var parts = relativePath.Split(new[] { '/' }, 2);
        return parts.Length > 1 ? parts[1] : throw new ArgumentException($"Invalid relative image path format for blob name: {relativePath}");
    }


    // --- Rebuild Metadata Logic ---

    // Changed return type to use ImageMetadata
    public async Task<Dictionary<string, ImageMetadata>> ScanBlobStorageForImagesParallel(int maxParallelContainers = 8) // Increased default slightly
    {
        // Use ConcurrentDictionary for thread-safe aggregation
        var allMetadata = new ConcurrentDictionary<string, ImageMetadata>();
        var blobServiceClient = new BlobServiceClient(_connectionString);
        List<BlobContainerItem> containers = [];

        try
        {
            // Use await foreach to populate the list
            await foreach (var containerItem in blobServiceClient.GetBlobContainersAsync())
            {
                containers.Add(containerItem);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error listing blob containers: {ex.Message}");
            return new Dictionary<string, ImageMetadata>(); // Return empty on failure
        }


        int totalContainers = containers.Count;
        Console.WriteLine($"Found {totalContainers} containers in Azure Storage.");
        if (totalContainers == 0) return new Dictionary<string, ImageMetadata>();

        using var semaphore = new SemaphoreSlim(maxParallelContainers);
        var tasks = new List<Task>();

        foreach (var container in containers)
        {
            await semaphore.WaitAsync(); // Wait for a slot

            // Use Task.Run to offload the async work properly within the semaphore loop
            var task = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine($"Starting scan: {container.Name}");
                    var containerClient = blobServiceClient.GetBlobContainerClient(container.Name);
                    await ScanBlobStorageContainer(containerClient, allMetadata); // Pass ConcurrentDictionary
                    Console.WriteLine($"Completed scan: {container.Name}");
                }
                catch (Exception ex)
                {
                    // Log container-level errors
                    Console.WriteLine($"Error scanning container {container.Name}: {ex.Message}");
                }
                finally
                {
                    semaphore.Release(); // Release the slot
                }
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks); // Wait for all container scans to finish

        // Convert ConcurrentDictionary back to regular Dictionary for return
        return new Dictionary<string, ImageMetadata>(allMetadata);
    }

    private async Task ScanBlobStorageContainer(BlobContainerClient containerClient, ConcurrentDictionary<string, ImageMetadata> allMetadata)
    {
        // Group blobs by base name (path/image without _sizeW.ext)
        var imageGroups = new Dictionary<string, List<BlobItem>>();
        int blobCount = 0;
        string containerName = containerClient.Name;

        try
        {
            // Fetch metadata along with blobs to potentially avoid extra GetProperties calls later
            await foreach (var blob in containerClient.GetBlobsAsync(BlobTraits.Metadata))
            {
                blobCount++;
                if (blobCount % 500 == 0) Console.WriteLine($"  [{containerName}] Scanned {blobCount} blobs...");

                // Basic filtering for relevant web image types
                if (!IsWebImageBlob(blob.Name)) continue;

                // Group by base name (e.g., path/to/image)
                string baseName = GetBaseImageNameFromBlobName(blob.Name);
                if (!imageGroups.TryGetValue(baseName, out var group))
                {
                    group = new List<BlobItem>();
                    imageGroups[baseName] = group;
                }
                group.Add(blob);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [{containerName}] Error listing blobs: {ex.Message}");
            return; // Stop processing this container on listing error
        }

        Console.WriteLine($"  [{containerName}] Found {blobCount} total blobs, identified {imageGroups.Count} potential unique images.");
        if (!imageGroups.Any()) return;

        // Process image groups in parallel within this container scan
        int maxParallelImageProcessing = Math.Max(1, Environment.ProcessorCount);
        using var imageSemaphore = new SemaphoreSlim(maxParallelImageProcessing);
        var imageTasks = new List<Task>();

        foreach (var group in imageGroups)
        {
            await imageSemaphore.WaitAsync();

            var imageTask = Task.Run(async () => // Ensure async lambda
            {
                string baseGroupName = group.Key; // For logging
                try
                {
                    // Find the original blob (no _sizeW suffix) - crucial first step
                    var originalBlobItem = FindOriginalBlobItem(group.Value);
                    if (originalBlobItem == null)
                    {
                        Console.WriteLine($"  [{containerName}] Warning: Could not identify original blob for group '{baseGroupName}'. Skipping group.");
                        return;
                    }

                    string originalBlobName = originalBlobItem.Name; // e.g. path/to/image.jpg
                                                                     // Construct the canonical key for our metadata dictionary: container/path/to/image.jpg
                    string metadataKey = $"{containerName}/{originalBlobName}";

                    // --- Get Original Dimensions ---
                    int originalWidth = 0;
                    int originalHeight = 0;

                    // 1. Try metadata from the original blob item (fetched during listing)
                    if (originalBlobItem.Metadata != null &&
                    originalBlobItem.Metadata.TryGetValue(WidthMetadataKey, out string? wStr) && int.TryParse(wStr, out originalWidth) &&
                    originalBlobItem.Metadata.TryGetValue(HeightMetadataKey, out string? hStr) && int.TryParse(hStr, out originalHeight) &&
                    originalWidth > 0 && originalHeight > 0)
                    {
                        // Dimensions found in existing blob metadata
                    }
                    else
                    {
                        // 2. Fallback: Get properties/content of original blob AND update metadata if needed
                        var originalBlobClient = containerClient.GetBlobClient(originalBlobName);
                        // This helper function tries GetProperties, then analyzes content, then updates metadata
                        (originalWidth, originalHeight) = await GetAndEnsureBlobDimensions(originalBlobClient);
                    }

                    // If we STILL couldn't determine original dimensions, skip this image group
                    if (originalWidth <= 0 || originalHeight <= 0)
                    {
                        Console.WriteLine($"  [{containerName}] Warning: Could not determine original dimensions for '{originalBlobName}'. Skipping group '{baseGroupName}'.");
                        return;
                    }

                    // --- Create Metadata Object ---
                    // Use the determined original dimensions
                    var imageMetadata = new ImageMetadata(originalWidth, originalHeight);

                    // --- Process Resized Blobs in the Group ---
                    foreach (var blobItem in group.Value)
                    {
                        // Skip the original blob itself - we only want *resized* widths in Formats
                        if (blobItem.Name.Equals(originalBlobName, StringComparison.OrdinalIgnoreCase)) continue;

                        // Extract format and width from the blob name (e.g., path/image_320w.webp -> (WebP, 320))
                        (WebImageFormat format, int? width) = ExtractFormatAndWidthFromBlobName(blobItem.Name);

                        if (width.HasValue) // This is a resized blob
                        {
                            // Add only the WIDTH to the metadata object's Formats collection
                            imageMetadata.AddFormatWidth(format, width.Value);
                            // We no longer store the resized height in the metadata file
                        }
                        // else: This means it wasn't a blob with a _sizeW suffix, which should have been caught
                        // by the check skipping the original blob, unless naming is inconsistent.
                    }

                    // --- Fix Content-Type for all blobs in this group ---
                    foreach (var blobItem in group.Value)
                    {
                        await EnsureBlobContentType(containerClient, blobItem.Name);
                    }

                    // Add the completed metadata (with original dims + list of resized widths per format)
                    // Using AddOrUpdate provides thread-safety and handles potential race conditions if
                    // a key were somehow processed twice (though the semaphore per group should prevent this).
                    allMetadata.AddOrUpdate(metadataKey, imageMetadata, (key, existingVal) => imageMetadata);

                }
                catch (Exception ex)
                {
                    // Log error specific to processing this image group
                    Console.WriteLine($"  [{containerName}] Error processing image group '{baseGroupName}': {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    imageSemaphore.Release(); // Release semaphore slot
                }
            });
            imageTasks.Add(imageTask);
        }

        await Task.WhenAll(imageTasks); // Wait for all images in this container to be processed
    }

    // Finds the blob item most likely to be the original (no _sizeW suffix)
    private BlobItem? FindOriginalBlobItem(List<BlobItem> relatedBlobs)
    {
        return relatedBlobs.FirstOrDefault(blob =>
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(blob.Name);
            // Check if the last part after '_' ends with 'w' and is preceded by a number
            int lastUnderscore = nameWithoutExt.LastIndexOf('_');
            if (lastUnderscore == -1) return true; // No underscore, likely original

            string suffix = nameWithoutExt.Substring(lastUnderscore + 1); // e.g., "1000w"
                                                                          // Check if it ends with 'w' and the part before 'w' is numeric
            return !(suffix.Length > 1 && suffix.EndsWith('w') && int.TryParse(suffix.Substring(0, suffix.Length - 1), out _));
        });
    }

    // Gets dimensions, attempting metadata first, then content analysis, and updates metadata if needed
    private async Task<(int width, int height)> GetAndEnsureBlobDimensions(BlobClient blobClient)
    {
        int width = 0;
        int height = 0;

        try
        {
            // 1. Try getting properties and checking metadata
            BlobProperties properties = await blobClient.GetPropertiesAsync();
            if (properties.Metadata.TryGetValue(WidthMetadataKey, out string? wStr) && int.TryParse(wStr, out width) &&
                properties.Metadata.TryGetValue(HeightMetadataKey, out string? hStr) && int.TryParse(hStr, out height) &&
                width > 0 && height > 0)
            {
                return (width, height); // Found in metadata
            }

            // 2. Fallback: Analyze content
            Console.WriteLine($"    Metadata missing/invalid for {blobClient.Name}. Analyzing content...");
            (width, height) = await GetImageDimensionsFromContent(blobClient);

            // 3. Update metadata if analysis succeeded
            if (width > 0 && height > 0)
            {
                await UpdateBlobDimensionMetadata(blobClient, width, height, properties.Metadata);
            }
            else
            {
                Console.WriteLine($"    Failed to analyze content for {blobClient.Name}. Dimensions unknown.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Error getting/ensuring dimensions for {blobClient.Name}: {ex.Message}");
            return (0, 0); // Return 0,0 on error
        }
        return (width, height);
    }


    // Checks if a blob name looks like a web image format we handle
    private static bool IsWebImageBlob(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName) || blobName.StartsWith(".") || blobName.Contains("/._"))
        {
            return false; // Skip hidden/system files
        }
        string? extension = Path.GetExtension(blobName)?.ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" => true,
            _ => false,
        };
    }

    // Extracts base name (e.g., "path/image_100w.jpg" -> "path/image")
    private string GetBaseImageNameFromBlobName(string blobName)
    {
        string nameWithoutExtension = Path.ChangeExtension(blobName, null); // path/image_100w

        int lastUnderscore = nameWithoutExtension.LastIndexOf('_');
        if (lastUnderscore != -1)
        {
            string suffix = nameWithoutExtension.Substring(lastUnderscore + 1);
            if (suffix.Length > 1 && suffix.EndsWith('w') && int.TryParse(suffix.Substring(0, suffix.Length - 1), out _))
            {
                // It has a size suffix, remove it
                return nameWithoutExtension.Substring(0, lastUnderscore); // path/image
            }
        }
        // No size suffix found
        return nameWithoutExtension; // path/image
    }

    // Extracts format and width (e.g., "path/image_320w.webp" -> (WebP, 320))
    private (WebImageFormat format, int? width) ExtractFormatAndWidthFromBlobName(string blobName)
    {
        string? extension = (Path.GetExtension(blobName)?.ToLowerInvariant()) ?? throw new ArgumentException($"Blob name '{blobName}' is missing a valid file extension.");

        WebImageFormat format = extension switch
        {
            ".jpg" or ".jpeg" => WebImageFormat.Jpg,
            ".png" => WebImageFormat.Png,
            ".webp" => WebImageFormat.WebP,
            _ => throw new ArgumentException($"Unsupported blob extension: {extension}") // Should be caught by IsWebImageBlob
        };

        string nameWithoutExtension = Path.GetFileNameWithoutExtension(blobName); // image_320w
        int? width = null;
        int lastUnderscore = nameWithoutExtension.LastIndexOf('_');
        if (lastUnderscore != -1)
        {
            string suffix = nameWithoutExtension.Substring(lastUnderscore + 1); // 320w
            if (suffix.Length > 1 && suffix.EndsWith('w') && int.TryParse(suffix.Substring(0, suffix.Length - 1), out int parsedWidth))
            {
                width = parsedWidth;
            }
        }

        return (format, width);
    }

    // Downloads image content to extract dimensions (keep this fallback)
    private async Task<(int width, int height)> GetImageDimensionsFromContent(BlobClient blobClient)
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()); // Use temp path
        try
        {
            await blobClient.DownloadToAsync(tempFilePath);
            using var image = new MagickImage(tempFilePath);
            return (image.Width, image.Height);
        }
        catch (MagickException magickEx)
        {
            Console.WriteLine($"    ImageMagick error analyzing {blobClient.Name}: {magickEx.Message}");
            return (0, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Error downloading/analyzing {blobClient.Name}: {ex.Message}");
            return (0, 0);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* Ignore delete error */ }
            }
        }
    }

    // Updates blob metadata, preserving existing metadata
    private async Task UpdateBlobDimensionMetadata(BlobClient blobClient, int width, int height, IDictionary<string, string> existingMetadata)
    {
        // Create a mutable copy or new dictionary
        var newMetadata = new Dictionary<string, string>(existingMetadata, StringComparer.OrdinalIgnoreCase);

        bool changed = false;
        if (!newMetadata.ContainsKey(WidthMetadataKey) || newMetadata[WidthMetadataKey] != width.ToString())
        {
            newMetadata[WidthMetadataKey] = width.ToString();
            changed = true;
        }
        if (!newMetadata.ContainsKey(HeightMetadataKey) || newMetadata[HeightMetadataKey] != height.ToString())
        {
            newMetadata[HeightMetadataKey] = height.ToString();
            changed = true;
        }


        if (changed)
        {
            try
            {
                await blobClient.SetMetadataAsync(newMetadata);
                Console.WriteLine($"    Updated metadata for {blobClient.Name} -> {width}x{height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error updating metadata for {blobClient.Name}: {ex.Message}");
            }
        }
        else
        {
            // Console.WriteLine($"    Metadata already correct for {blobClient.Name}");
        }
    }

    // Ensures a blob has the correct Content-Type header based on its file extension
    private async Task EnsureBlobContentType(BlobContainerClient containerClient, string blobName)
    {
        string expectedContentType = GetMimeTypeFromExtension(blobName);
        if (expectedContentType == "application/octet-stream") return; // Skip unknown types

        try
        {
            var blobClient = containerClient.GetBlobClient(blobName);
            BlobProperties properties = await blobClient.GetPropertiesAsync();

            if (!string.Equals(properties.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
            {
                await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders
                {
                    ContentType = expectedContentType
                });
                Console.WriteLine($"    Fixed Content-Type for {blobName}: {properties.ContentType} -> {expectedContentType}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Error fixing Content-Type for {blobName}: {ex.Message}");
        }
    }

    // Map file extension to MIME type
    private static string GetMimeTypeFromExtension(string blobName)
    {
        string? extension = Path.GetExtension(blobName)?.ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
    }
}