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

        EnsureOutputDirectoryExists(request.OutputDirectory);

        var outputPaths = GetOutputPaths(request.OutputDirectory, request.OutputName);

        ValidateAllEddsDimensions(request);
        EnsureCanWriteOutputs(outputPaths, request.OverwriteExisting);

        var bcr = ChannelPacker.PackEddsBcr(request.BaseColor, request.Roughness);
        var nmo = ChannelPacker.PackEddsNmo(request.Normal, request.Metallic, request.AmbientOcclusion);

        _codec.Save(bcr, outputPaths.BcrPath, OutputFormat.Png);
        _codec.Save(nmo, outputPaths.NmoPath, OutputFormat.Png);

        return outputPaths;
    }

    public string PackBcrAndSave(
        TextureImage baseColor,
        TextureImage? roughness,
        string outputName,
        string outputDirectory,
        bool overwriteExisting = false)
    {
        EnsureOutputDirectoryExists(outputDirectory);
        ValidateBcrInputs(baseColor, roughness);

        var outputPath = GetOutputPaths(outputDirectory, outputName).BcrPath;
        EnsureCanWriteOutput(outputPath, overwriteExisting);

        var bcr = ChannelPacker.PackEddsBcr(baseColor, roughness);
        _codec.Save(bcr, outputPath, OutputFormat.Png);

        return outputPath;
    }

    public string PackNmoAndSave(
        TextureImage normal,
        TextureImage? metallic,
        TextureImage? ambientOcclusion,
        string outputName,
        string outputDirectory,
        bool overwriteExisting = false)
    {
        EnsureOutputDirectoryExists(outputDirectory);
        ValidateNmoInputs(normal, metallic, ambientOcclusion);

        var outputPath = GetOutputPaths(outputDirectory, outputName).NmoPath;
        EnsureCanWriteOutput(outputPath, overwriteExisting);

        var nmo = ChannelPacker.PackEddsNmo(normal, metallic, ambientOcclusion);
        _codec.Save(nmo, outputPath, OutputFormat.Png);

        return outputPath;
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
        ValidateBcrInputs(request.BaseColor, request.Roughness);
        ValidateNmoInputs(request.Normal, request.Metallic, request.AmbientOcclusion);

        ImageValidation.EnsureSameDimensions(request.BaseColor, request.Normal, "Normal");
    }

    private static void ValidateBcrInputs(TextureImage baseColor, TextureImage? roughness)
    {
        baseColor.Validate();

        if (roughness is not null)
        {
            roughness.Validate();
            ImageValidation.EnsureSameDimensions(baseColor, roughness, "Roughness");
        }
    }

    private static void ValidateNmoInputs(TextureImage normal, TextureImage? metallic, TextureImage? ambientOcclusion)
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

    private static void EnsureCanWriteOutput(string outputPath, bool overwriteExisting)
    {
        if (!overwriteExisting && File.Exists(outputPath))
        {
            throw new ChannelPackingException(
                $"Output file already exists: {Path.GetFileName(outputPath)}. Choose overwrite or change the file name.");
        }
    }

    private static void EnsureOutputDirectoryExists(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            throw new ChannelPackingException("The output directory could not be found.");
        }
    }
}
