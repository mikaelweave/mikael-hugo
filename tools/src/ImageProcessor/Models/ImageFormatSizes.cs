namespace ImageProcessor.Models;

// Renamed for clarity, now holds available widths per format
public class AvailableFormats
{
    // Stores WebImageFormat -> List<int> (widths)
    private readonly Dictionary<WebImageFormat, List<int>> _formatWidths;

    public AvailableFormats()
    {
        _formatWidths = new Dictionary<WebImageFormat, List<int>>();
    }

    public AvailableFormats(Dictionary<WebImageFormat, List<int>> formatWidths)
    {
        // Sort widths on initialization
        _formatWidths = formatWidths.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Distinct().OrderBy(w => w).ToList() // Ensure distinct and sorted
        );
    }

    // Expose as IReadOnlyDictionary for encapsulation
    public IReadOnlyDictionary<WebImageFormat, List<int>> FormatWidths => _formatWidths;

    // Simplified Add method - only takes width
    public void Add(WebImageFormat format, int width)
    {
        if (width <= 0) return; // Ignore invalid widths

        if (!_formatWidths.TryGetValue(format, out var widths))
        {
            widths = new List<int>();
            _formatWidths[format] = widths;
        }

        // Add width if it doesn't exist, then sort
        if (!widths.Contains(width))
        {
            widths.Add(width);
            widths.Sort(); // Keep the list sorted
        }
    }
}