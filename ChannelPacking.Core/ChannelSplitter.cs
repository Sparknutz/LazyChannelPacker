namespace ChannelPacking.Core;

public static class ChannelSplitter
{
    public static IReadOnlyDictionary<TextureChannel, TextureImage> Split(TextureImage image)
    {
        image.Validate();

        return new Dictionary<TextureChannel, TextureImage>
        {
            [TextureChannel.Red] = ToGrayscale(image, TextureChannel.Red),
            [TextureChannel.Green] = ToGrayscale(image, TextureChannel.Green),
            [TextureChannel.Blue] = ToGrayscale(image, TextureChannel.Blue),
            [TextureChannel.Alpha] = ToGrayscale(image, TextureChannel.Alpha)
        };
    }

    public static TextureImage ToGrayscale(TextureImage image, TextureChannel channel)
    {
        image.Validate();

        var output = new byte[image.Rgba.Length];
        var channelIndex = ChannelIndex(channel);

        for (var source = 0; source < image.Rgba.Length; source += 4)
        {
            var value = image.Rgba[source + channelIndex];
            output[source] = value;
            output[source + 1] = value;
            output[source + 2] = value;
            output[source + 3] = 255;
        }

        return new TextureImage(image.Width, image.Height, output, image.SourcePath);
    }

    internal static int ChannelIndex(TextureChannel channel)
    {
        return channel switch
        {
            TextureChannel.Red => 0,
            TextureChannel.Green => 1,
            TextureChannel.Blue => 2,
            TextureChannel.Alpha => 3,
            _ => throw new ChannelPackingException("Unsupported channel.")
        };
    }
}
