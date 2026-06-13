namespace ChannelPacking.Core;

public enum OutputFormat
{
    Png,
    Tga,
    Tiff
}

public static class OutputFormats
{
    public static OutputFormat FromPath(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".tga" => OutputFormat.Tga,
            ".tif" or ".tiff" => OutputFormat.Tiff,
            _ => OutputFormat.Png
        };
    }

    public static string ExtensionFor(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Png => ".png",
            OutputFormat.Tga => ".tga",
            OutputFormat.Tiff => ".tiff",
            _ => throw new ChannelPackingException("Unsupported output format.")
        };
    }

    public static OutputFormat PreferredForTiffSource(TextureImage image)
    {
        return PreferredForTiffSources(image);
    }

    public static OutputFormat PreferredForTiffSources(params TextureImage?[] images)
    {
        return images.Any(image => image?.SourcePath is not null && FromPath(image.SourcePath) == OutputFormat.Tiff)
            ? OutputFormat.Tiff
            : OutputFormat.Png;
    }
}
