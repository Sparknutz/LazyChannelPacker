using ChannelPacking.Core;

namespace ChannelPacking.Tests;

public sealed class ChannelPackingCoreTests
{
    [Fact]
    public void Split_ReturnsGrayscaleChannelImages()
    {
        var image = CreateImage(2, 1,
            10, 20, 30, 40,
            50, 60, 70, 80);

        var split = ChannelSplitter.Split(image);

        Assert.Equal(new byte[] { 40, 40, 40, 255, 80, 80, 80, 255 }, split[TextureChannel.Alpha].Rgba);
        Assert.Equal(new byte[] { 10, 10, 10, 255, 50, 50, 50, 255 }, split[TextureChannel.Red].Rgba);
    }

    [Fact]
    public void Pack_FillsMissingRgbWithBlackAndAlphaWithWhite()
    {
        var redSource = CreateImage(1, 1, 25, 50, 75, 100);

        var packed = ChannelPacker.Pack(new PackRequest(
            new PackSlot(redSource, TextureChannel.Green),
            null,
            null,
            null));

        Assert.Equal(new byte[] { 50, 0, 0, 255 }, packed.Rgba);
    }

    [Fact]
    public void Pack_SupportsChannelRemapAndInvert()
    {
        var redSource = CreateImage(1, 1, 10, 20, 30, 40);
        var greenSource = CreateImage(1, 1, 50, 60, 70, 80);

        var packed = ChannelPacker.Pack(new PackRequest(
            new PackSlot(redSource, TextureChannel.Blue),
            new PackSlot(greenSource, TextureChannel.Red, Invert: true),
            null,
            null));

        Assert.Equal(new byte[] { 30, 205, 0, 255 }, packed.Rgba);
    }

    [Fact]
    public void EddsBcr_UsesBaseColorRgbAndRoughnessRedAsAlpha()
    {
        var baseColor = CreateImage(1, 1, 1, 2, 3, 4);
        var roughness = CreateImage(1, 1, 101, 102, 103, 104);

        var packed = ChannelPacker.PackEddsBcr(baseColor, roughness);

        Assert.Equal(new byte[] { 1, 2, 3, 101 }, packed.Rgba);
    }

    [Fact]
    public void EddsNmo_UsesNormalRgMetallicRedAndAoRed()
    {
        var normal = CreateImage(1, 1, 10, 20, 30, 40);
        var metallic = CreateImage(1, 1, 50, 60, 70, 80);
        var ao = CreateImage(1, 1, 90, 100, 110, 120);

        var packed = ChannelPacker.PackEddsNmo(normal, metallic, ao);

        Assert.Equal(new byte[] { 10, 20, 50, 90 }, packed.Rgba);
    }

    [Fact]
    public void EddsNmo_WhenAoIsMissing_UsesOpaqueAlpha()
    {
        var normal = CreateImage(1, 1, 10, 20, 30, 40);
        var metallic = CreateImage(1, 1, 50, 60, 70, 80);

        var packed = ChannelPacker.PackEddsNmo(normal, metallic, null);

        Assert.Equal(new byte[] { 10, 20, 50, 255 }, packed.Rgba);
    }

    [Fact]
    public void EddsNmo_WhenMetallicIsMissing_UsesBlackBlueChannel()
    {
        var normal = CreateImage(1, 1, 10, 20, 30, 40);
        var ao = CreateImage(1, 1, 90, 100, 110, 120);

        var packed = ChannelPacker.PackEddsNmo(normal, null, ao);

        Assert.Equal(new byte[] { 10, 20, 0, 90 }, packed.Rgba);
    }

    [Fact]
    public void EddsNmo_WhenMetallicAndAoAreMissing_UsesBlackBlueAndOpaqueAlpha()
    {
        var normal = CreateImage(1, 1, 10, 20, 30, 40);

        var packed = ChannelPacker.PackEddsNmo(normal, null, null);

        Assert.Equal(new byte[] { 10, 20, 0, 255 }, packed.Rgba);
    }

    [Fact]
    public void EddsPacker_SanitizesNamesAndSavesPngOutputs()
    {
        var codec = new CapturingCodec();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"channel-packing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var image = CreateImage(1, 1, 1, 2, 3, 4);
            var packer = new EddsPacker(codec);

            var result = packer.PackAndSave(new EddsPackRequest(
                image,
                image,
                image,
                image,
                null,
                "painted metal",
                outputDirectory));

            Assert.EndsWith("painted_metal_BCR.png", result.BcrPath);
            Assert.EndsWith("painted_metal_NMO.png", result.NmoPath);
            Assert.Equal(OutputFormat.Png, codec.Saved[0].Format);
            Assert.Equal(OutputFormat.Png, codec.Saved[1].Format);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void EddsPacker_RequiresOutputName()
    {
        var codec = new CapturingCodec();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"channel-packing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var image = CreateImage(1, 1, 1, 2, 3, 4);
            var packer = new EddsPacker(codec);

            var exception = Assert.Throws<ChannelPackingException>(() => packer.PackAndSave(new EddsPackRequest(
                image,
                image,
                image,
                image,
                null,
                "",
                outputDirectory)));

            Assert.Contains("Output file name is required", exception.Message);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void EddsPacker_RejectsExistingOutputsUnlessOverwriteIsAllowed()
    {
        var codec = new CapturingCodec();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"channel-packing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);

        try
        {
            var image = CreateImage(1, 1, 1, 2, 3, 4);
            var packer = new EddsPacker(codec);
            File.WriteAllText(Path.Combine(outputDirectory, "shared_name_BCR.png"), "existing");

            var exception = Assert.Throws<ChannelPackingException>(() => packer.PackAndSave(new EddsPackRequest(
                image,
                image,
                image,
                image,
                null,
                "shared name",
                outputDirectory)));

            Assert.Contains("already exists", exception.Message);
            Assert.Empty(codec.Saved);

            var result = packer.PackAndSave(new EddsPackRequest(
                image,
                image,
                image,
                image,
                null,
                "shared name",
                outputDirectory,
                OverwriteExisting: true));

            Assert.EndsWith("shared_name_BCR.png", result.BcrPath);
            Assert.EndsWith("shared_name_NMO.png", result.NmoPath);
            Assert.Equal(2, codec.Saved.Count);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void MagickCodec_RoundTripsPngRgbaData()
    {
        var codec = new MagickImageCodec();
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"channel-packing-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "roundtrip.png");

        try
        {
            var image = CreateImage(2, 1,
                10, 20, 30, 40,
                50, 60, 70, 255);

            codec.Save(image, path, OutputFormat.Png);
            var loaded = codec.Load(path);

            Assert.Equal(image.Width, loaded.Width);
            Assert.Equal(image.Height, loaded.Height);
            Assert.Equal(image.Rgba, loaded.Rgba);
        }
        finally
        {
            Directory.Delete(outputDirectory, recursive: true);
        }
    }

    [Fact]
    public void Pack_RejectsDimensionMismatch()
    {
        var onePixel = CreateImage(1, 1, 1, 2, 3, 4);
        var twoPixels = CreateImage(2, 1, 1, 2, 3, 4, 5, 6, 7, 8);

        var exception = Assert.Throws<ChannelPackingException>(() => ChannelPacker.Pack(new PackRequest(
            new PackSlot(onePixel, TextureChannel.Red),
            new PackSlot(twoPixels, TextureChannel.Green),
            null,
            null)));

        Assert.Contains("Green dimensions must match", exception.Message);
    }

    private static TextureImage CreateImage(int width, int height, params byte[] rgba)
    {
        return new TextureImage(width, height, rgba);
    }

    private sealed class CapturingCodec : IImageCodec
    {
        public List<SavedImage> Saved { get; } = [];

        public TextureImage Load(string path)
        {
            throw new NotSupportedException();
        }

        public void Save(TextureImage image, string path, OutputFormat format)
        {
            Saved.Add(new SavedImage(image, path, format));
        }
    }

    private sealed record SavedImage(TextureImage Image, string Path, OutputFormat Format);
}
