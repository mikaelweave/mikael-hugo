using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageProcessor.Models;

namespace ImageProcessor.Repositories;

// Consider renaming MetadataStore if splitting files later (e.g., SingleFileMetadataStore)
public class MetadataStore(string metadataFilePath) : IDisposable
{
    // Changed value type to ImageMetadata
    private Dictionary<string, ImageMetadata>? _imageData = null;
    private bool _disposed = false;
    private readonly string _metadataFilePath = metadataFilePath; // Store path locally

    // Use instance lock object unless there's a specific reason for static
    private readonly object _lockObject = new object();

    // Keep the options simple - the converter is applied via attribute now
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true // Make the output JSON readable
    };

    // Changed return type and internal type
    private Dictionary<string, ImageMetadata> ImageData
    {
        get
        {
            // Double-check locking pattern for thread safety if needed,
            // but for console app maybe simpler load is fine if not accessed concurrently before load.
            if (_imageData is null)
            {
                 lock(_lockObject) // Lock during load/initialization
                 {
                      // Check again inside lock
                      if (_imageData is null)
                      {
                           _imageData = LoadMetadataFromFile();
                      }
                 }
            }
            return _imageData;
        }
    }

    private Dictionary<string, ImageMetadata> LoadMetadataFromFile()
    {
        Console.WriteLine($"Loading metadata from: {_metadataFilePath}");
        if (!File.Exists(_metadataFilePath))
        {
             Console.WriteLine("Metadata file not found. Initializing empty store.");
             // Return empty dictionary if file doesn't exist instead of throwing
             return new Dictionary<string, ImageMetadata>();
            // OR: throw new FileNotFoundException("Metadata file not found.", _metadataFilePath);
        }

        try
        {
            string json = File.ReadAllText(_metadataFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                 Console.WriteLine("Metadata file is empty. Initializing empty store.");
                 return new Dictionary<string, ImageMetadata>();
            }

            // Deserialize the dictionary of ImageMetadata objects
            var loadedData = JsonSerializer.Deserialize<Dictionary<string, ImageMetadata>>(json, _serializerOptions);

            if (loadedData == null)
            {
                 Console.WriteLine("Warning: Metadata file parsed to null. Initializing empty store.");
                 return new Dictionary<string, ImageMetadata>();
            }
            Console.WriteLine($"Successfully loaded {loadedData.Count} entries from metadata file.");
            return loadedData;
        }
        catch (JsonException jsonEx)
        {
            throw new ApplicationException($"Error parsing metadata file '{_metadataFilePath}'. Malformed JSON? Details: {jsonEx.Message}", jsonEx);
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"Failed to load metadata file '{_metadataFilePath}'. Details: {ex.Message}", ex);
        }
    }

    private string EncodeKey(string path)
    {
        // Replace spaces with %20. Use UrlEncode for more robustness if needed.
        // return HttpUtility.UrlEncode(path).Replace("+", "%20"); // UrlEncode turns space into +, replace it back
        return path.Replace(" ", "%20"); // Simpler if only spaces are the issue
    }

    public ReadOnlyCollection<string> KnownImagePaths => ImageData.Keys.ToList().AsReadOnly();

    // Changed value type parameter
    public void Add(string path, ImageMetadata metadata)
    {
        string encodedKey = EncodeKey(path);
        lock (_lockObject)
        {
            // Use TryAdd for cleaner logic
            if (!ImageData.TryAdd(encodedKey, metadata))
            {
                 // Log or handle overwrite? Current behavior throws.
                 // For process-images, maybe warning and skip is better?
                 // For rebuild-metadata, overwrite might be desired.
                 Console.WriteLine($"Warning: Image already exists in metadata, skipping add: {encodedKey}");
                 // throw new InvalidOperationException($"Image already exists in metadata: {encodedKey}");
            }
        }
    }

    // Changed value type parameter
    public bool TryAdd(string path, ImageMetadata metadata)
    {
        string encodedKey = EncodeKey(path);
        lock (_lockObject)
        {
            return ImageData.TryAdd(encodedKey, metadata);
        }
    }

    // Changed value type parameter
    public void Update(string path, ImageMetadata metadata)
    {
        string encodedKey = EncodeKey(path);
        lock (_lockObject)
        {
            if (!ImageData.ContainsKey(encodedKey))
            {
                // Consider using TryUpdate or specific logic based on command
                Console.WriteLine($"Warning: Image does not exist in metadata, adding instead: {encodedKey}");
                // throw new KeyNotFoundException($"Image does not exist in metadata: {encodedKey}");
            }
            // Using the indexer will add or update
            ImageData[encodedKey] = metadata;
        }
    }

    public void Clear()
    {
         lock(_lockObject)
         {
              ImageData.Clear();
         }
    }

    // Added method to replace all data, useful for RebuildMetadata
    public void ReplaceAll(Dictionary<string, ImageMetadata> newImageData)
    {
        var encodedDictionary = newImageData
            .ToDictionary(kvp => EncodeKey(kvp.Key), kvp => kvp.Value);

        lock(_lockObject)
        {
            _imageData = new Dictionary<string, ImageMetadata>(encodedDictionary);
        }
    }


    public void Remove(string path)
    {
        string encodedKey = EncodeKey(path); // Encode the key
        lock (_lockObject)
        {
            if (!ImageData.Remove(encodedKey))
            {
                 Console.WriteLine($"Warning: Image to remove does not exist in metadata (key: {encodedKey}, original path: {path})");
            }
        }
    }

    // Changed return type
    public ImageMetadata Get(string path)
    {
        string encodedKey = EncodeKey(path); // Encode the key
        if (ImageData.TryGetValue(encodedKey, out var metadata))
        {
            return metadata;
        }
        // Use original path in error message for clarity
        throw new KeyNotFoundException($"Image does not exist in metadata (original path: {path}, tried key: {encodedKey})");
    }

     public bool TryGet(string path, out ImageMetadata? metadata)
    {
         // No lock needed for read if underlying Dictionary is used safely post-initialization
         // Add lock if concurrent modification is possible during reads.
         string encodedKey = EncodeKey(path); // Encode the key
         return ImageData.TryGetValue(encodedKey, out metadata);
    }

    public bool Contains(string path)
    {
         // No lock needed for read if underlying Dictionary is used safely post-initialization
        string encodedKey = EncodeKey(path); // Encode the key
        return ImageData.ContainsKey(encodedKey);
    }

    // Save method remains largely the same, but serializes the new structure
    public void Save()
    {
        string json;
        Dictionary<string, ImageMetadata> dataToSerialize; // Variable to hold the final data

        lock (_lockObject) // Lock during filtering and serialization
        {
             if (_imageData == null)
             {
                  Console.WriteLine("Warning: Attempted to save null metadata. Skipping.");
                  return;
             }

            // --- Filter out entries with empty or null FormatWidths ---
            var skippedEntries = new List<string>(); // To log which keys were skipped
            dataToSerialize = _imageData
                .Where(kvp => {
                    // Check if FormatWidths exists AND has any entries in its dictionary
                    bool isValid = kvp.Value.FormatWidths != null && kvp.Value.FormatWidths.Any();
                    if (!isValid)
                    {
                        skippedEntries.Add(kvp.Key); // Track skipped key
                    }
                    return isValid; // Keep only if valid
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            // ----------------------------------------------------------

            // Log the skipped entries if any were found
            if (skippedEntries.Any())
            {
                Console.WriteLine($"Warning: Skipped {skippedEntries.Count} metadata entries during save due to empty 'formatWidths':");
                foreach(var skippedKey in skippedEntries.Take(20)) // Log first 20 skipped
                {
                    Console.WriteLine($"  - {skippedKey}");
                }
                if (skippedEntries.Count > 20) {
                    Console.WriteLine($"  ... and {skippedEntries.Count - 20} more.");
                }
            }


            // Ensure remaining entries have sorted widths before serializing
            foreach(var metadataEntry in dataToSerialize.Values)
            {
                 metadataEntry.EnsureWidthsAreSorted(); // Assumes this helper exists in ImageMetadata
            }

            // Serialize the *filtered* dictionary
            json = JsonSerializer.Serialize(dataToSerialize, _serializerOptions);
        }

        try
        {
             // Ensure directory exists before writing
             string? directory = Path.GetDirectoryName(_metadataFilePath);
             if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
             {
                  Directory.CreateDirectory(directory);
             }

             File.WriteAllText(_metadataFilePath, json);
             Console.WriteLine($"Metadata saved to {_metadataFilePath}");
        }
        catch (Exception ex)
        {
             // Log error appropriately
            Console.WriteLine($"Error saving metadata file '{_metadataFilePath}': {ex.Message}");
            // Consider re-throwing or handling more gracefully
        }

    }

    // Dispose pattern ensures Save is called
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Save data on dispose
                Save();
            }
            // Release large fields (though _imageData is managed)
            _imageData = null;
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

     ~MetadataStore()
    {
        Dispose(false);
    }
}