# Lazy Channel Packer

Lazy Channel Packer is a portable Windows texture channel packing tool for game artists and modders.

It can unpack image channels, pack custom RGBA channels, and quickly create Enfusion/Arma Reforger-style `_BCR` and `_NMO` texture maps.

## Download

Download the latest portable ZIP from the releases page:

https://github.com/Sparknutz/LazyChannelPacker/releases

Extract the ZIP and run:

```text
LazyChannelPacker.exe
```

No installer is required.

## Features

- Unpack PNG, JPG, JPEG, TGA, TIF, and TIFF images into R, G, B, and A grayscale channels.
- Pack separate images into custom R, G, B, and A output channels.
- TIFF inputs default to TIFF outputs with an embedded sRGB profile.
- Use the Enfusion Packer tab to create:
  - `<name>_BCR` from Base Color RGB plus Roughness alpha.
  - `<name>_NMO` from Normal RG plus optional Metallic and optional Ambient Occlusion.
- Missing Metallic fills black.
- Missing Ambient Occlusion fills opaque white.
- Input dimensions must match exactly.

## Enfusion / Arma Reforger Notes

For Enfusion texture workflows:

- `_BCR`: RGB = Base Color, A = Roughness
- `_NMO`: R/G = Normal, B = Metallic, A = Ambient Occlusion

The app creates packed PNG or TIFF source textures. Import them through Workbench to generate `.edds` files.

This project is unofficial and is not affiliated with Bohemia Interactive.

## License

Lazy Channel Packer is source-available for non-commercial use only.

Commercial use, resale, paid redistribution, or use in paid products/services is not allowed without permission.

See `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt`.
