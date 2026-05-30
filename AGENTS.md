# Lazy Channel Packer Guidance

These instructions apply to all work in this repository.

## Product Goal

- Build a fast, reliable Windows desktop channel packing tool for artists and game-texture workflows.
- MVP scope: split a PNG/JPG/TGA image into R, G, B, and A grayscale channel outputs; pack separate images into R, G, B, and A slots; provide a specialized `Enfusion Packer` workflow for BCR/NMO texture outputs; preview results; export packed images.
- Optimize for correctness, predictable behavior, and a fast first usable build before adding advanced pipeline features.

## Stack

- Use C# with modern .NET and WPF for the Windows desktop app.
- Keep image-processing logic in a standalone core library, separate from WPF UI concerns.
- Use Magick.NET-Q8-x64 for MVP image loading/saving and pixel access unless a later task explicitly changes the image backend.
- Use xUnit or the repo's established test framework once the solution exists.

## Architecture

- Prefer a solution layout like:
  - `ChannelPacking.Core` for channel split/pack logic, validation, image models, and codecs.
  - `ChannelPacking.App` for WPF views, view models, commands, drag/drop, file dialogs, and previews.
  - `ChannelPacking.Tests` for unit and golden-image tests.
- UI code should call core services; do not duplicate pixel/channel logic in WPF code-behind.
- Keep public core APIs small and deterministic so they are easy to test.
- Avoid adding abstractions until they remove real duplication or isolate a concrete dependency such as the image codec.

## MVP Behavior

- Supported input formats: PNG, JPG/JPEG, and TGA.
- Supported MVP export formats: PNG and TGA.
- Treat channel data as 8-bit per channel for the first release.
- Split tab:
  - Accept one dropped or selected image.
  - Show source preview and R/G/B/A grayscale channel previews.
  - Allow saving individual channels and saving all available channels.
- Pack tab:
  - Provide R, G, B, and A target slots.
  - Each slot accepts a dropped or selected image.
  - Each slot can choose which source channel to read from.
  - Each slot supports invert.
  - Missing RGB slots fill black.
  - Missing alpha fills opaque white.
- `Enfusion Packer` tab:
  - Provide five drop/select areas: Base Color, Normal, Roughness, Metallic, and Ambient Occlusion.
  - Require one user-provided output base name before processing.
  - Make clear in the UI that the base name saves two files: `<name>_BCR.png` and `<name>_NMO.png`.
  - Sanitize spaces in the output base name by converting them to `_`.
  - Save outputs in the Base Color image directory.
  - Create `<name>_BCR.png` from Base Color RGB plus Roughness as alpha.
  - Create `<name>_NMO.png` from Normal R/G, Metallic as blue, and Ambient Occlusion as alpha.
  - Allow Metallic to be omitted; when omitted, use black for the NMO blue channel.
  - Allow Ambient Occlusion to be omitted; when omitted, use opaque alpha for the NMO output.
  - If either EDDS output file already exists, show an overwrite-or-cancel confirmation before writing.
  - The EDDS clear button should clear both loaded images and the output base name.
  - Validate required inputs and dimensions before writing any output.
- Require exact matching dimensions for all packed slot inputs.
- Do not auto-resize, resample, gamma-convert, or color-manage images in the MVP unless explicitly requested.
- Strip metadata on export unless a future task requires preserving it.

## Validation And Errors

- Validate file existence, supported extension/format, decode success, bit depth compatibility, dimensions, and output path before processing.
- Surface errors in user-facing language in the WPF app.
- Keep lower-level exceptions wrapped or translated at the UI boundary so the app does not crash on bad input.
- Process image work off the UI thread so 4K/8K textures do not freeze the interface.

## Testing Expectations

- Add focused tests for all core channel operations before or alongside UI work.
- Use tiny deterministic fixture images for exact channel-value tests.
- Cover split, pack, channel remap, invert, missing-channel fills, alpha handling, dimension mismatch, unsupported formats, and corrupt-file handling.
- Add round-trip tests where practical: split then pack and verify channel data.
- For UI changes, verify the app builds and the main workflows can be launched manually if automated WPF UI testing is not available.

## Tooling And MCP Use

- Use the Microsoft Learn MCP server for current .NET, WPF, MSIX, Windows packaging, code signing, drag/drop, file dialog, and Windows desktop API guidance.
- Use the OpenAI Developer Docs MCP server only for Codex, MCP, OpenAI API, or OpenAI tooling questions.
- Prefer local shell commands for repo operations: `dotnet build`, `dotnet test`, `git status`, and NuGet restore.
- Do not add extra MCP servers unless they solve a concrete problem that existing tools cannot cover.
- Multi-agent work is optional and should only be used when explicitly requested or when implementation can be safely split into independent ownership areas.

## Release Direction

- Target public Windows download eventually, so keep packaging and signing in mind.
- Initial release can be a self-contained `win-x64` build.
- Plan for installer/signing work after core workflows are correct and tested.

## Coding Style

- Follow existing repo conventions once code exists.
- Keep edits narrowly scoped to the requested behavior.
- Prefer clear names over comments; add comments only when they explain non-obvious image-processing decisions.
- Avoid unrelated refactors or formatting churn.
