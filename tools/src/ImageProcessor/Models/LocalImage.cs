using System.Collections.ObjectModel;
using ImageMagick;

namespace ImageProcessor.Models;

public class LocalImage(string imagePath, string relativeImagePath) : IDisposable
{
    private MagickImage? imageData = null;
    private bool disposed = false;

    public string ImagePath { get; } = imagePath;

    public string RelativeImagePath { get; } = relativeImagePath;

    private MagickImage ImageData
    {
        get
        {
            if (imageData == null)
            {
                LoadImage();
            }

            return imageData!;
        }
    }

    public int WidthPixels => (int)ImageData.Width;

    public int HeightPixels => (int)ImageData.Height;

    public MagickFormat ImageFormat => ImageData.Format;

    private void LoadImage()
    {
        // Optionally, add thread safety here if needed.
        if (imageData == null)
        {
            imageData = new MagickImage(ImagePath);
        }
    }

    public string ResizeAndSaveToTemp(WebImageFormat format, int width, int? quality = null)
    {
        var newImage = ImageData.Clone();
        newImage.Resize((uint)width, 0);
        string extension;

        // Determine the format and set the extension
        if (format == WebImageFormat.Jpg)
        {
            newImage.Format = MagickFormat.Jpeg;
            extension = ".jpg";
        }
        else if (format == WebImageFormat.WebP)
        {
            newImage.Format = MagickFormat.WebP;
            extension = ".webp";
        }
        else if (format == WebImageFormat.Png)
        {
            newImage.Format = MagickFormat.Png;
            extension = ".png";
        }
        else
        {
            throw new ArgumentException("Unsupported image format.");
        }

        // Set quality for JPEG and WebP
        if (newImage.Format == MagickFormat.Jpeg || newImage.Format == MagickFormat.WebP)
        {
            if (quality is not null && quality.Value > 0 && quality.Value <= 100)
            {
                newImage.Quality = (uint)quality.Value;
            }
        }

        // Generate a temporary path with the correct extension
        string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);

        newImage.Write(tempPath);
        return tempPath;
    }

    public static ReadOnlyCollection<LocalImage> GetUntrackedLocalImages(List<string> directoryPaths, ReadOnlyCollection<string> pathsToIgnore)
    {
        List<LocalImage> newImages = [];

        foreach (string directoryPath in directoryPaths)
        {
            var allImageFiles = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
            .Where(file => (file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                         || file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                         || file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                         && !pathsToIgnore.Any(x => x.Equals(file.Replace(directoryPath, string.Empty).TrimStart('/'), StringComparison.CurrentCultureIgnoreCase))
                         && new FileInfo(file).DirectoryName != directoryPath);

            newImages.AddRange(allImageFiles.Select(x => new LocalImage(x, GetRelativePath(x, directoryPath))));
        }

        
        return newImages.AsReadOnly();
    }

    private static string GetRelativePath(string imageAbsPath, string imageRootFolder) => imageAbsPath.Replace(imageRootFolder, string.Empty).TrimStart(Path.DirectorySeparatorChar).Replace(Path.DirectorySeparatorChar, '/');

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                imageData?.Dispose();
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}