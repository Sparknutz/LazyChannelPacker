namespace ChannelPacking.Core;

public sealed record EddsPackRequest(
    TextureImage BaseColor,
    TextureImage Normal,
    TextureImage? Roughness,
    TextureImage? Metallic,
    TextureImage? AmbientOcclusion,
    string OutputName,
    string OutputDirectory,
    bool OverwriteExisting = false);
