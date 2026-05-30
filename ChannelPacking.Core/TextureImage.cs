namespace ChannelPacking.Core;

public sealed record TextureImage(
    int Width,
    int Height,
    byte[] Rgba,
    string? SourcePath = null)
{
    public int PixelCount => Width * Height;

    public void Validate()
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new ChannelPackingException("Image dimensions must be greater than zero.");
        }

        if (Rgba.Length != Width * Height * 4)
        {
            throw new ChannelPackingException("Image pixel data is not valid RGBA data.");
        }
    }
}
