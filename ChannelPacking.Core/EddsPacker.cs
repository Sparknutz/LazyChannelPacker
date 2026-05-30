namespace ChannelPacking.Core;

public sealed class EddsPacker
{
    private readonly IImageCodec _codec;

    public EddsPacker(IImageCodec codec)
    {
        _codec = codec;
    }

    public EddsPackResult PackAndSave(EddsPackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Directory.Exists(request.OutputDirectory))
        {
            throw new ChannelPackingException("The Base Color directory could not be found.");
        }

        var outputPaths = GetOutputPaths(request.OutputDirectory, request.OutputName);

        ValidateAllEddsDimensions(request);
        EnsureCanWriteOutputs(outputPaths, request.OverwriteExisting);

        var bcr = ChannelPacker.PackEddsBcr(request.BaseColor, request.Roughness);
        var nmo = ChannelPacker.PackEddsNmo(request.Normal, request.Metallic, request.AmbientOcclusion);

        _codec.Save(bcr, outputPaths.BcrPath, OutputFormat.Png);
        _codec.Save(nmo, outputPaths.NmoPath, OutputFormat.Png);

        return outputPaths;
    }

    public static EddsPackResult GetOutputPaths(string outputDirectory, string outputName)
    {
        var sanitizedName = ImageValidation.SanitizeFileNameStem(outputName, "Output file name");
        return new EddsPackResult(
            Path.Combine(outputDirectory, $"{sanitizedName}_BCR.png"),
            Path.Combine(outputDirectory, $"{sanitizedName}_NMO.png"));
    }

    private static void ValidateAllEddsDimensions(EddsPackRequest request)
    {
        request.BaseColor.Validate();
        request.Normal.Validate();
        request.Roughness.Validate();

        ImageValidation.EnsureSameDimensions(request.BaseColor, request.Normal, "Normal");
        ImageValidation.EnsureSameDimensions(request.BaseColor, request.Roughness, "Roughness");

        if (request.Metallic is not null)
        {
            request.Metallic.Validate();
            ImageValidation.EnsureSameDimensions(request.BaseColor, request.Metallic, "Metallic");
        }

        if (request.AmbientOcclusion is not null)
        {
            ImageValidation.EnsureSameDimensions(request.BaseColor, request.AmbientOcclusion, "Ambient Occlusion");
        }
    }

    private static void EnsureCanWriteOutputs(EddsPackResult outputPaths, bool overwriteExisting)
    {
        if (overwriteExisting)
        {
            return;
        }

        var existing = new[] { outputPaths.BcrPath, outputPaths.NmoPath }
            .Where(File.Exists)
            .Select(Path.GetFileName)
            .ToArray();

        if (existing.Length > 0)
        {
            throw new ChannelPackingException(
                $"Output file already exists: {string.Join(", ", existing)}. Choose overwrite or change the file name.");
        }
    }
}
