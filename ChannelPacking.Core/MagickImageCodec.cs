using ImageMagick;

namespace ChannelPacking.Core;

public sealed class MagickImageCodec : IImageCodec
{
    public TextureImage Load(string path)
    {
        ImageValidation.EnsureSupportedInputPath(path);

        try
        {
            using var image = new MagickImage(path);

            if (image.Width > int.MaxValue || image.Height > int.MaxValue)
            {
                throw new ChannelPackingException("Image dimensions are too large to process.");
            }

            image.Depth = 8;
            image.Alpha(AlphaOption.On);

            var bytes = image.ToByteArray(MagickFormat.Rgba);
            var texture = new TextureImage((int)image.Width, (int)image.Height, bytes, path);
            texture.Validate();
            return texture;
        }
        catch (ChannelPackingException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ChannelPackingException($"Could not read image: {Path.GetFileName(path)}", exception);
        }
    }

    public void Save(TextureImage image, string path, OutputFormat format)
    {
        image.Validate();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            var settings = new PixelReadSettings(
                (uint)image.Width,
                (uint)image.Height,
                StorageType.Char,
                PixelMapping.RGBA);

            using var output = new MagickImage(image.Rgba, settings)
            {
                Depth = 8,
                Format = ToMagickFormat(format)
            };

            output.Strip();
            output.Write(path);
        }
        catch (Exception exception)
        {
            throw new ChannelPackingException($"Could not save image: {Path.GetFileName(path)}", exception);
        }
    }

    private static MagickFormat ToMagickFormat(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Png => MagickFormat.Png,
            OutputFormat.Tga => MagickFormat.Tga,
            _ => throw new ChannelPackingException("Unsupported output format.")
        };
    }
}
