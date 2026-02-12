using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ImageProcessor.Configuration;
using ImageProcessor.Models;
using ImageProcessor.Repositories;
using System.Reflection;
// using System.Text.Json; // No longer needed unless directly parsing json for asserts
using Xunit;

namespace ImageProcessor.Test.E2E;

[Collection("Sequential")] // Ensures tests using shared resources run sequentially
public class ApplicationTests : IDisposable
{
    // Constants
    private const string ConnectionString = "UseDevelopmentStorage=true"; // Assumes Azurite is running
    private static readonly int[] TestSizes = { 100, 200, 300 }; // Target sizes for resizing
    private const int BeaversOriginalWidth = 600;
    private const int BeaversOriginalHeight = 400;
    private const int KwpOriginalWidth = 500;
    private const int KwpOriginalHeight = 333; // Approximate for 1.5 ratio

    // Test state
    private readonly string _testIdentifier;
    private readonly string _baseTempDirectory;
    private readonly string _testInstanceDirectory;
    private readonly string _metadataFilePath;
    private readonly ConsoleConfiguration _consoleConfiguration;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _beaversFileName = "beavers.jpg";
    private readonly string _kwpFileName = "kwp.png";
    private readonly string _containerName;


    public ApplicationTests()
    {
        _testIdentifier = Guid.NewGuid().ToString("N");
        _containerName = $"test-{_testIdentifier}";

        _baseTempDirectory = Path.Combine(Path.GetTempPath(), "KwpImageProcessorTests");
        _testInstanceDirectory = Path.Combine(_baseTempDirectory, _containerName);
        Directory.CreateDirectory(_testInstanceDirectory);

        _metadataFilePath = Path.Combine(_testInstanceDirectory, "srcsets.json");

        CopyEmbeddedResourcesToDirectory(_testInstanceDirectory);

        _consoleConfiguration = new ConsoleConfiguration(
            _metadataFilePath,
            new List<string> { _testInstanceDirectory }, // Point to dir containing images
            ConnectionString,
            TestSizes
        );

        _blobServiceClient = new BlobServiceClient(ConnectionString);
    }

    public void Dispose()
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            containerClient.DeleteIfExists();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error cleaning up container {_containerName}: {ex.Message}");
        }

        try
        {
            if (Directory.Exists(_testInstanceDirectory))
            {
                Directory.Delete(_testInstanceDirectory, true);
            }
        }
        catch (Exception ex) // Catch broader exception for cleanup issues
        {
            Console.WriteLine($"Error cleaning up directory {_testInstanceDirectory}: {ex.Message}");
        }
    }

    // --- Test Cases ---

    [Fact]
    public async Task ProcessImages_UploadsNewImages_AndCreatesCorrectMetadata()
    {
        // Arrange
        _consoleConfiguration.Command = ApplicationCommand.ProcessImages;
        string beaversRelPath = _beaversFileName;
        string kwpRelPath = _kwpFileName;

        // Act
        await ApplicationRunner.RunApplication(_consoleConfiguration);

        // Assert - Blobs
        await VerifyExpectedBlobsExist(_containerName);

        // Assert - Metadata File
        Assert.True(File.Exists(_metadataFilePath), "Metadata file was not created.");
        // Use 'using' for IDisposable MetadataStore
        using var metadataStore = new MetadataStore(_metadataFilePath);

        // Verify Beavers.jpg metadata
        VerifyImageMetadata(metadataStore, _containerName, beaversRelPath,
            BeaversOriginalWidth, BeaversOriginalHeight,
            new List<(WebImageFormat Format, int Width)> // EXPECTED RESIZED (Format, Width)
            {
                (WebImageFormat.Jpg, 100), (WebImageFormat.Jpg, 200), (WebImageFormat.Jpg, 300),
                (WebImageFormat.WebP, 100), (WebImageFormat.WebP, 200), (WebImageFormat.WebP, 300),
            });

        // Verify Kwp.png metadata
        VerifyImageMetadata(metadataStore, _containerName, kwpRelPath,
            KwpOriginalWidth, KwpOriginalHeight,
            new List<(WebImageFormat Format, int Width)> // EXPECTED RESIZED (Format, Width)
            {
                (WebImageFormat.Png, 100), (WebImageFormat.Png, 200), (WebImageFormat.Png, 300),
                (WebImageFormat.WebP, 100), (WebImageFormat.WebP, 200), (WebImageFormat.WebP, 300), // WebP from PNG
            });
    }

    [Fact]
    public async Task RebuildMetadata_WithExistingBlobs_RebuildsCorrectMetadata()
    {
        // Arrange
        await UploadTestBlobsToAzure(withMetadata: true); // Upload blobs WITH metadata
        SafeDeleteFile(_metadataFilePath);
        _consoleConfiguration.Command = ApplicationCommand.RebuildMetadata;
        string beaversRelPath = _beaversFileName;
        string kwpRelPath = _kwpFileName;

        // Act
        await ApplicationRunner.RunApplication(_consoleConfiguration);

        // Assert
        Assert.True(File.Exists(_metadataFilePath), "Metadata file was not created by rebuild.");
        using var metadataStore = new MetadataStore(_metadataFilePath);

        // Verify Beavers.jpg metadata (Rebuild should find original and resized widths)
        VerifyImageMetadata(metadataStore, _containerName, beaversRelPath,
            BeaversOriginalWidth, BeaversOriginalHeight,
            new List<(WebImageFormat Format, int Width)>
            {
                (WebImageFormat.Jpg, 100), (WebImageFormat.Jpg, 200), (WebImageFormat.Jpg, 300),
                (WebImageFormat.WebP, 100), (WebImageFormat.WebP, 200), (WebImageFormat.WebP, 300),
            });

        // Verify Kwp.png metadata (Rebuild should find original and resized widths)
        VerifyImageMetadata(metadataStore, _containerName, kwpRelPath,
           KwpOriginalWidth, KwpOriginalHeight,
           new List<(WebImageFormat Format, int Width)>
           {
                (WebImageFormat.Png, 100), (WebImageFormat.Png, 200), (WebImageFormat.Png, 300),
                (WebImageFormat.WebP, 100), (WebImageFormat.WebP, 200), (WebImageFormat.WebP, 300), // WebP from PNG
           });
    }

    [Fact]
    public async Task RebuildMetadata_WithNoExistingBlobs_CreatesEmptyMetadataFile()
    {
        // Arrange
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();
        SafeDeleteFile(_metadataFilePath);
        _consoleConfiguration.Command = ApplicationCommand.RebuildMetadata;

        // Act
        await ApplicationRunner.RunApplication(_consoleConfiguration);

        // Assert
        Assert.True(File.Exists(_metadataFilePath), "Metadata file was not created for empty container.");
        var fileContent = File.ReadAllText(_metadataFilePath).Trim();
        Assert.True(fileContent == "{}" || fileContent == "{\n}", $"Expected empty JSON object, but got: {fileContent}");
    }

    [Fact]
    public async Task RebuildMetadata_WithoutBlobDimensionMetadata_ExtractsAndStoresDimensions()
    {
        // Arrange
        await UploadTestBlobsToAzure(withMetadata: false); // Upload blobs WITHOUT metadata
        SafeDeleteFile(_metadataFilePath);
        _consoleConfiguration.Command = ApplicationCommand.RebuildMetadata;
        string beaversRelPath = _beaversFileName;
        string kwpRelPath = _kwpFileName;

        // Act
        await ApplicationRunner.RunApplication(_consoleConfiguration);

        // Assert - File Exists
        Assert.True(File.Exists(_metadataFilePath), "Metadata file was not created by rebuild (no blob metadata).");
        using var metadataStore = new MetadataStore(_metadataFilePath);

        // Assert - Dimensions were extracted (Verification is same as standard rebuild)
        VerifyImageMetadata(metadataStore, _containerName, beaversRelPath,
            BeaversOriginalWidth, BeaversOriginalHeight, // Expect originals to be extracted
            new List<(WebImageFormat Format, int Width)>
            {
                (WebImageFormat.Jpg, 100), (WebImageFormat.Jpg, 200), (WebImageFormat.Jpg, 300),
                (WebImageFormat.WebP, 100), (WebImageFormat.WebP, 200), (WebImageFormat.WebP, 300),
            });

        VerifyImageMetadata(metadataStore, _containerName, kwpRelPath,
           KwpOriginalWidth, KwpOriginalHeight, // Expect originals to be extracted
           new List<(WebImageFormat Format, int Width)>
           {
                (WebImageFormat.Png, 100), (WebImageFormat.Png, 200), (WebImageFormat.Png, 300),
                (WebImageFormat.WebP, 100), (WebImageFormat.WebP, 200), (WebImageFormat.WebP, 300), // WebP from PNG
           });
    }

    // --- Verification Helpers ---

    private async Task VerifyExpectedBlobsExist(string containerName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        Assert.True(await containerClient.ExistsAsync(), $"Container '{containerName}' does not exist.");

        var expectedBlobNames = new List<string>
        {
            _beaversFileName, _kwpFileName,
            $"{Path.GetFileNameWithoutExtension(_beaversFileName)}_{TestSizes[0]}w.jpg",
            $"{Path.GetFileNameWithoutExtension(_beaversFileName)}_{TestSizes[0]}w.webp",
            $"{Path.GetFileNameWithoutExtension(_beaversFileName)}_{TestSizes[1]}w.jpg",
            $"{Path.GetFileNameWithoutExtension(_beaversFileName)}_{TestSizes[1]}w.webp",
            $"{Path.GetFileNameWithoutExtension(_beaversFileName)}_{TestSizes[2]}w.jpg",
            $"{Path.GetFileNameWithoutExtension(_beaversFileName)}_{TestSizes[2]}w.webp",
            $"{Path.GetFileNameWithoutExtension(_kwpFileName)}_{TestSizes[0]}w.png",
            $"{Path.GetFileNameWithoutExtension(_kwpFileName)}_{TestSizes[0]}w.webp", // WebP from PNG
            $"{Path.GetFileNameWithoutExtension(_kwpFileName)}_{TestSizes[1]}w.png",
            $"{Path.GetFileNameWithoutExtension(_kwpFileName)}_{TestSizes[1]}w.webp",
            $"{Path.GetFileNameWithoutExtension(_kwpFileName)}_{TestSizes[2]}w.png",
            $"{Path.GetFileNameWithoutExtension(_kwpFileName)}_{TestSizes[2]}w.webp",
        };

        var actualBlobNames = new HashSet<string>();
        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            actualBlobNames.Add(blobItem.Name);
        }

        foreach (string expectedName in expectedBlobNames)
        {
            Assert.Contains(expectedName, actualBlobNames);
        }
        Assert.Equal(expectedBlobNames.Count, actualBlobNames.Count);
    }

    // Updated Verification method for simplified metadata (no resized heights)
    private void VerifyImageMetadata(
        MetadataStore metadataStore,
        string containerName,
        string imageRelativePath, // e.g., beavers.jpg
        int expectedOriginalWidth,
        int expectedOriginalHeight,
        List<(WebImageFormat Format, int Width)> expectedResizedFormatsAndWidths) // Expect (Format, Width) tuples
    {
        string metadataKey = $"{containerName}/{imageRelativePath}"; // Key format: container/image.ext

        Assert.True(metadataStore.TryGet(metadataKey, out var imageMetadata), $"Metadata not found for key: {metadataKey}");
        Assert.NotNull(imageMetadata);

        // Verify Original Dimensions stored at the top level
        Assert.Equal(expectedOriginalWidth, imageMetadata.OriginalWidth);
        Assert.Equal(expectedOriginalHeight, imageMetadata.OriginalHeight);

        // Verify Resized Formats and Widths stored in FormatWidths
        Assert.NotNull(imageMetadata.FormatWidths);

        // Flatten the actual resized data into a list of (Format, Width) tuples
        var actualResizedFormatsAndWidths = imageMetadata.FormatWidths
            .SelectMany(kvp => kvp.Value.Select(width => (Format: kvp.Key, Width: width)))
            .OrderBy(t => t.Format.ToString()).ThenBy(t => t.Width) // Order for consistent comparison
            .ToList();

        // Order the expected list similarly
        var expectedOrderedResized = expectedResizedFormatsAndWidths
           .OrderBy(t => t.Format.ToString()).ThenBy(t => t.Width)
           .ToList();

        // Assert counts first for easier debugging
        Assert.Equal(expectedOrderedResized.Count, actualResizedFormatsAndWidths.Count);

        // Assert each item matches
        for (int i = 0; i < expectedOrderedResized.Count; i++)
        {
            Assert.Equal(expectedOrderedResized[i], actualResizedFormatsAndWidths[i]);
        }
    }

    // --- Test Setup Helpers ---

    private async Task UploadTestBlobsToAzure(bool withMetadata)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(publicAccessType: PublicAccessType.Blob);

        string beaversFilePath = Path.Combine(_testInstanceDirectory, _beaversFileName);
        string kwpFilePath = Path.Combine(_testInstanceDirectory, _kwpFileName);

        var beaversMetadata = withMetadata ? new Dictionary<string, string> { { "width", BeaversOriginalWidth.ToString() }, { "height", BeaversOriginalHeight.ToString() } } : null;
        var kwpMetadata = withMetadata ? new Dictionary<string, string> { { "width", KwpOriginalWidth.ToString() }, { "height", KwpOriginalHeight.ToString() } } : null;

        await UploadBlobAsync(containerClient, _beaversFileName, beaversFilePath, beaversMetadata);
        await UploadBlobAsync(containerClient, _kwpFileName, kwpFilePath, kwpMetadata);

        foreach (int size in TestSizes)
        {
            int beaversHeight = CalculateHeight(BeaversOriginalWidth, BeaversOriginalHeight, size);
            int kwpHeight = CalculateHeight(KwpOriginalWidth, KwpOriginalHeight, size);

            var jpgMetadata = withMetadata ? new Dictionary<string, string> { { "width", size.ToString() }, { "height", beaversHeight.ToString() } } : null;
            var webpMetadata = jpgMetadata;
            var pngMetadata = withMetadata ? new Dictionary<string, string> { { "width", size.ToString() }, { "height", kwpHeight.ToString() } } : null;
            var webpFromPngMetadata = pngMetadata;

            await UploadBlobAsync(containerClient, $"{Path.GetFileNameWithoutExtension(_beaversFileName)}_{size}w.jpg", beaversFilePath, jpgMetadata);
            await UploadBlobAsync(containerClient, $"{Path.GetFileNameWithoutExtension(_beaversFileName)}_{size}w.webp", beaversFilePath, webpMetadata);

            await UploadBlobAsync(containerClient, $"{Path.GetFileNameWithoutExtension(_kwpFileName)}_{size}w.png", kwpFilePath, pngMetadata);
            await UploadBlobAsync(containerClient, $"{Path.GetFileNameWithoutExtension(_kwpFileName)}_{size}w.webp", kwpFilePath, webpFromPngMetadata);
        }
    }

    private async Task UploadBlobAsync(BlobContainerClient containerClient, string blobName, string sourceFilePath, IDictionary<string, string>? metadata)
    {
        var blobClient = containerClient.GetBlobClient(blobName);

        // Create options object
        var options = new BlobUploadOptions
        {
            Metadata = metadata // Assign metadata here
                                // Optionally set HttpHeaders if needed, e.g.:
                                // HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" }
        };

        // Use the overload that takes the file path (string) and BlobUploadOptions
        // This overload implicitly overwrites if the blob exists.
        await blobClient.UploadAsync(sourceFilePath, options);
    }

    private void CopyEmbeddedResourcesToDirectory(string destinationDirectory)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourcePrefix = $"{assembly.GetName().Name}.TestFiles.";

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (resourceName.StartsWith(resourcePrefix))
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) continue;
                    var fileName = resourceName.Substring(resourcePrefix.Length);
                    var destinationPath = Path.Combine(destinationDirectory, fileName);
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
            }
        }
    }

    private void SafeDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Could not delete file {filePath}: {ex.Message}");
        }
    }

    private int CalculateHeight(int originalWidth, int originalHeight, int targetWidth)
    {
        if (originalWidth <= 0 || originalHeight <= 0 || targetWidth <= 0) return 0;
        return (int)Math.Round((double)originalHeight * targetWidth / originalWidth);
    }
}