namespace ChannelPacking.Core;

public interface IImageCodec
{
    TextureImage Load(string path);

    void Save(TextureImage image, string path, OutputFormat format);
}
