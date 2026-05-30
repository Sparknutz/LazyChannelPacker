namespace ChannelPacking.Core;

public static class ChannelPacker
{
    public static TextureImage Pack(PackRequest request)
    {
        var reference = FirstImage(request);
        if (reference is null)
        {
            throw new ChannelPackingException("Add at least one image before packing.");
        }

        ValidateSlot(reference, request.Red, "Red");
        ValidateSlot(reference, request.Green, "Green");
        ValidateSlot(reference, request.Blue, "Blue");
        ValidateSlot(reference, request.Alpha, "Alpha");

        var output = new byte[reference.Rgba.Length];

        for (var index = 0; index < output.Length; index += 4)
        {
            output[index] = ReadOrDefault(request.Red, index, 0);
            output[index + 1] = ReadOrDefault(request.Green, index, 0);
            output[index + 2] = ReadOrDefault(request.Blue, index, 0);
            output[index + 3] = ReadOrDefault(request.Alpha, index, 255);
        }

        return new TextureImage(reference.Width, reference.Height, output);
    }

    public static TextureImage PackEddsBcr(TextureImage baseColor, TextureImage roughness)
    {
        baseColor.Validate();
        roughness.Validate();
        ImageValidation.EnsureSameDimensions(baseColor, roughness, "Roughness");

        return Pack(new PackRequest(
            new PackSlot(baseColor, TextureChannel.Red),
            new PackSlot(baseColor, TextureChannel.Green),
            new PackSlot(baseColor, TextureChannel.Blue),
            new PackSlot(roughness, TextureChannel.Red)));
    }

    public static TextureImage PackEddsNmo(TextureImage normal, TextureImage? metallic, TextureImage? ambientOcclusion)
    {
        normal.Validate();

        if (metallic is not null)
        {
            metallic.Validate();
            ImageValidation.EnsureSameDimensions(normal, metallic, "Metallic");
        }

        if (ambientOcclusion is not null)
        {
            ambientOcclusion.Validate();
            ImageValidation.EnsureSameDimensions(normal, ambientOcclusion, "Ambient Occlusion");
        }

        return Pack(new PackRequest(
            new PackSlot(normal, TextureChannel.Red),
            new PackSlot(normal, TextureChannel.Green),
            metallic is null ? null : new PackSlot(metallic, TextureChannel.Red),
            ambientOcclusion is null ? null : new PackSlot(ambientOcclusion, TextureChannel.Red)));
    }

    private static TextureImage? FirstImage(PackRequest request)
    {
        return request.Red?.Image
            ?? request.Green?.Image
            ?? request.Blue?.Image
            ?? request.Alpha?.Image;
    }

    private static void ValidateSlot(TextureImage reference, PackSlot? slot, string targetName)
    {
        if (slot is null)
        {
            return;
        }

        slot.Image.Validate();
        ImageValidation.EnsureSameDimensions(reference, slot.Image, targetName);
    }

    private static byte ReadOrDefault(PackSlot? slot, int targetIndex, byte defaultValue)
    {
        if (slot is null)
        {
            return defaultValue;
        }

        var sourceIndex = targetIndex + ChannelSplitter.ChannelIndex(slot.SourceChannel);
        var value = slot.Image.Rgba[sourceIndex];
        return slot.Invert ? (byte)(255 - value) : value;
    }
}
