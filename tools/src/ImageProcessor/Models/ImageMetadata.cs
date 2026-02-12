using System.Text.Json.Serialization;

namespace ImageProcessor.Models;

public class ImageMetadata
{
    [JsonPropertyName("originalWidth")]
    public int OriginalWidth { get; set; }

    [JsonPropertyName("originalHeight")]
    public int OriginalHeight { get; set; }

    [JsonPropertyName("formatWidths")] // Use camelCase JSON name
    public Dictionary<WebImageFormat, List<int>> FormatWidths { get; set; }

    // Optional: Placeholder for potential future use
    // [JsonPropertyName("alt")]
    // public string? AltText { get; set; }

    public ImageMetadata()
    {
        FormatWidths = [];
    }

    public ImageMetadata(int originalWidth, int originalHeight) : this()
    {
        OriginalWidth = originalWidth;
        OriginalHeight = originalHeight;
    }

    // Optional Helper: Method to add width and handle dictionary/list creation/sorting
    public void AddFormatWidth(WebImageFormat format, int width)
    {
         if (width <= 0) return; // Ignore invalid widths

        if (!FormatWidths.TryGetValue(format, out var widths))
        {
            widths = [];
            FormatWidths[format] = widths;
        }

        // Add width if it doesn't exist, then sort
        if (!widths.Contains(width))
        {
            widths.Add(width);
            widths.Sort(); // Keep the list sorted immediately
        }
    }

    // Helper to ensure all width lists are sorted before serialization
    // Call this before saving if AddFormatWidth wasn't used exclusively
    public void EnsureWidthsAreSorted()
    {
        foreach(var format in FormatWidths.Keys)
        {
            FormatWidths[format]?.Sort();
        }
    }
}