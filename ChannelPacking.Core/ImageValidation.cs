namespace ChannelPacking.Core;

public static class ImageValidation
{
    private static readonly HashSet<string> SupportedInputExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tga"
    };

    public static void EnsureSupportedInputPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ChannelPackingException("Choose an image file first.");
        }

        if (!File.Exists(path))
        {
            throw new ChannelPackingException($"The file does not exist: {path}");
        }

        var extension = Path.GetExtension(path);
        if (!SupportedInputExtensions.Contains(extension))
        {
            throw new ChannelPackingException("Supported image formats are PNG, JPG, JPEG, and TGA.");
        }
    }

    public static void EnsureSameDimensions(TextureImage expected, TextureImage actual, string actualName)
    {
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            throw new ChannelPackingException(
                $"{actualName} dimensions must match {expected.Width} x {expected.Height}. " +
                $"Current size is {actual.Width} x {actual.Height}.");
        }
    }

    public static string SanitizeFileNameStem(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ChannelPackingException($"{fieldName} is required.");
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Trim().Select(character =>
            character == ' ' || invalid.Contains(character) ? '_' : character).ToArray());

        while (cleaned.Contains("__", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);
        }

        cleaned = cleaned.Trim('_');

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            throw new ChannelPackingException($"{fieldName} must contain at least one valid file name character.");
        }

        return cleaned;
    }
}
