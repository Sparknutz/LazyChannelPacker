using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChannelPacking.Core;
using Microsoft.Win32;

namespace ChannelPacking.App;

public partial class MainWindow : Window
{
    private static readonly Brush NeutralBrush = new SolidColorBrush(Color.FromRgb(184, 192, 188));
    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(251, 113, 133));
    private static readonly Brush SuccessBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));

    private readonly IImageCodec _codec = new MagickImageCodec();
    private readonly Dictionary<TextureChannel, SlotState> _packSlots = [];
    private readonly Dictionary<string, SlotState> _eddsSlots = [];

    private TextureImage? _unpackSource;
    private IReadOnlyDictionary<TextureChannel, TextureImage>? _unpackedChannels;

    public MainWindow()
    {
        InitializeComponent();

        foreach (var channel in Enum.GetValues<TextureChannel>())
        {
            _packSlots[channel] = new SlotState();
        }

        foreach (var name in new[] { "BaseColor", "Normal", "Roughness", "Metallic", "AmbientOcclusion" })
        {
            _eddsSlots[name] = new SlotState();
        }
    }

    private async void ChooseUnpackImageClick(object sender, RoutedEventArgs e)
    {
        var path = ChooseInputFile();
        if (path is not null)
        {
            await LoadUnpackSourceAsync(path);
        }
    }

    private async void UnpackDrop(object sender, DragEventArgs e)
    {
        var path = GetDroppedFilePath(e);
        if (path is not null)
        {
            await LoadUnpackSourceAsync(path);
        }
    }

    private async Task LoadUnpackSourceAsync(string path)
    {
        await RunWithProgressAsync(UnpackProgress, UnpackFeedbackText, async () =>
        {
            var image = await Task.Run(() => _codec.Load(path));
            _unpackSource = image;
            _unpackedChannels = null;
            UnpackPathText.Text = DescribeImage(path, image);
            UnpackPreviewImage.Source = ToBitmapSource(image);
            ClearUnpackPreviews();
            SetFeedback(UnpackFeedbackText, "Image loaded. Press Unpack Image to preview and save channels.", isError: false);
        });
    }

    private void ClearUnpackImageClick(object sender, RoutedEventArgs e)
    {
        _unpackSource = null;
        _unpackedChannels = null;
        UnpackPathText.Text = "Drop image here or choose a file.";
        UnpackPreviewImage.Source = null;
        ClearUnpackPreviews();
        UnpackProgress.Value = 0;
        SetFeedback(UnpackFeedbackText, "Source image cleared.", isError: false);
    }

    private async void UnpackImageClick(object sender, RoutedEventArgs e)
    {
        await RunWithProgressAsync(UnpackProgress, UnpackFeedbackText, async () =>
        {
            if (_unpackSource is null)
            {
                throw new ChannelPackingException("Choose or drop an image before unpacking.");
            }

            var channels = await Task.Run(() => ChannelSplitter.Split(_unpackSource));
            _unpackedChannels = channels;

            RedChannelImage.Source = ToBitmapSource(channels[TextureChannel.Red]);
            GreenChannelImage.Source = ToBitmapSource(channels[TextureChannel.Green]);
            BlueChannelImage.Source = ToBitmapSource(channels[TextureChannel.Blue]);
            AlphaChannelImage.Source = ToBitmapSource(channels[TextureChannel.Alpha]);

            SetFeedback(UnpackFeedbackText, "Channels unpacked.", isError: false);
        });
    }

    private async void SaveAllUnpackedClick(object sender, RoutedEventArgs e)
    {
        await RunWithProgressAsync(UnpackProgress, UnpackFeedbackText, async () =>
        {
            if (_unpackSource?.SourcePath is null || _unpackedChannels is null)
            {
                throw new ChannelPackingException("Unpack an image before saving channels.");
            }

            var directory = Path.GetDirectoryName(_unpackSource.SourcePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ChannelPackingException("Could not find the source image directory.");
            }

            var stem = Path.GetFileNameWithoutExtension(_unpackSource.SourcePath);
            var format = OutputFormats.PreferredForTiffSource(_unpackSource);
            var extension = OutputFormats.ExtensionFor(format);
            await Task.Run(() =>
            {
                SaveUnpackedChannel(TextureChannel.Red, Path.Combine(directory, $"{stem}_R{extension}"), format);
                SaveUnpackedChannel(TextureChannel.Green, Path.Combine(directory, $"{stem}_G{extension}"), format);
                SaveUnpackedChannel(TextureChannel.Blue, Path.Combine(directory, $"{stem}_B{extension}"), format);
                SaveUnpackedChannel(TextureChannel.Alpha, Path.Combine(directory, $"{stem}_A{extension}"), format);
            });

            SetFeedback(UnpackFeedbackText, $"Saved channels to {directory}.", isError: false);
        });
    }

    private async void SaveUnpackedChannelClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string channelName)
        {
            return;
        }

        await RunWithProgressAsync(UnpackProgress, UnpackFeedbackText, async () =>
        {
            if (_unpackedChannels is null)
            {
                throw new ChannelPackingException("Unpack an image before saving a channel.");
            }

            var channel = ParseTextureChannel(channelName);
            var preferredFormat = _unpackSource is null
                ? OutputFormat.Png
                : OutputFormats.PreferredForTiffSource(_unpackSource);
            var path = ChooseOutputFile($"{channelName}{OutputFormats.ExtensionFor(preferredFormat)}", preferredFormat);
            if (path is null)
            {
                SetFeedback(UnpackFeedbackText, "Save cancelled.", isError: false);
                return;
            }

            var format = OutputFormats.FromPath(path);
            await Task.Run(() => _codec.Save(_unpackedChannels[channel], path, format));
            SetFeedback(UnpackFeedbackText, $"Saved {channelName} channel.", isError: false);
        });
    }

    private async void ChoosePackSlotClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            var path = ChooseInputFile();
            if (path is not null)
            {
                await LoadPackSlotAsync(ParseTextureChannel(tag), path);
            }
        }
    }

    private async void PackSlotDrop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            var path = GetDroppedFilePath(e);
            if (path is not null)
            {
                await LoadPackSlotAsync(ParseTextureChannel(tag), path);
            }
        }
    }

    private async Task LoadPackSlotAsync(TextureChannel targetChannel, string path)
    {
        await RunWithProgressAsync(PackProgress, PackFeedbackText, async () =>
        {
            var image = await Task.Run(() => _codec.Load(path));
            _packSlots[targetChannel] = new SlotState(image, path);
            SetPackSlotUi(targetChannel, image, path);
            SetFeedback(PackFeedbackText, $"{targetChannel} slot loaded.", isError: false);
        });
    }

    private async void PackImageClick(object sender, RoutedEventArgs e)
    {
        await RunWithProgressAsync(PackProgress, PackFeedbackText, async () =>
        {
            if (_packSlots.Values.All(slot => slot.Image is null))
            {
                throw new ChannelPackingException("Add at least one channel image before packing.");
            }

            var preferredFormat = PreferredPackedOutputFormat();
            var outputPath = ChooseOutputFile($"packed{OutputFormats.ExtensionFor(preferredFormat)}", preferredFormat);
            if (outputPath is null)
            {
                SetFeedback(PackFeedbackText, "Pack cancelled.", isError: false);
                return;
            }

            var request = new PackRequest(
                BuildPackSlot(TextureChannel.Red, PackRedChannel, PackRedInvert),
                BuildPackSlot(TextureChannel.Green, PackGreenChannel, PackGreenInvert),
                BuildPackSlot(TextureChannel.Blue, PackBlueChannel, PackBlueInvert),
                BuildPackSlot(TextureChannel.Alpha, PackAlphaChannel, PackAlphaInvert));

            var format = OutputFormats.FromPath(outputPath);
            await Task.Run(() =>
            {
                var packed = ChannelPacker.Pack(request);
                _codec.Save(packed, outputPath, format);
            });

            SetFeedback(PackFeedbackText, $"Packed image saved to {outputPath}.", isError: false);
        });
    }

    private void ClearPackSlotsClick(object sender, RoutedEventArgs e)
    {
        foreach (var channel in Enum.GetValues<TextureChannel>())
        {
            ClearPackSlot(channel);
        }

        PackProgress.Value = 0;
        SetFeedback(PackFeedbackText, "Pack slots cleared.", isError: false);
    }

    private void ClearPackSlotClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string tag)
        {
            var channel = ParseTextureChannel(tag);
            ClearPackSlot(channel);
            PackProgress.Value = 0;
            SetFeedback(PackFeedbackText, $"{channel} slot cleared.", isError: false);
        }
    }

    private void ClearPackSlot(TextureChannel channel)
    {
        _packSlots[channel] = new SlotState();
        SetPackSlotUi(channel, null, null);
    }

    private async void ChooseEddsSlotClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string slotName)
        {
            var path = ChooseInputFile();
            if (path is not null)
            {
                await LoadEddsSlotAsync(slotName, path);
            }
        }
    }

    private async void EddsSlotDrop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string slotName)
        {
            var path = GetDroppedFilePath(e);
            if (path is not null)
            {
                await LoadEddsSlotAsync(slotName, path);
            }
        }
    }

    private async Task LoadEddsSlotAsync(string slotName, string path)
    {
        await RunWithProgressAsync(EddsProgress, EddsFeedbackText, async () =>
        {
            var image = await Task.Run(() => _codec.Load(path));
            _eddsSlots[slotName] = new SlotState(image, path);
            SetEddsSlotUi(slotName, image, path);
            SetFeedback(EddsFeedbackText, $"{DisplayEddsSlotName(slotName)} loaded.", isError: false);
        });
    }

    private async void PackEddsClick(object sender, RoutedEventArgs e)
    {
        await PackEddsAsync(EddsPackMode.Both);
    }

    private async void PackEddsBcrOnlyClick(object sender, RoutedEventArgs e)
    {
        await PackEddsAsync(EddsPackMode.BcrOnly);
    }

    private async void PackEddsNmoOnlyClick(object sender, RoutedEventArgs e)
    {
        await PackEddsAsync(EddsPackMode.NmoOnly);
    }

    private async Task PackEddsAsync(EddsPackMode mode)
    {
        EddsPackJob job;

        try
        {
            job = BuildEddsPackJob(mode, overwriteExisting: false);
        }
        catch (ChannelPackingException exception)
        {
            SetFeedback(EddsFeedbackText, exception.Message, isError: true);
            return;
        }

        var outputPaths = EddsPacker.GetOutputPaths(job.OutputDirectory, job.OutputName, job.BcrOutputFormat, job.NmoOutputFormat);
        var existingOutputFiles = ExistingOutputFiles(outputPaths, mode).ToArray();

        if (existingOutputFiles.Length > 0)
        {
            var message =
                "These files already exist:\n\n" +
                string.Join("\n", existingOutputFiles.Select(Path.GetFileName)) +
                "\n\nClick OK to overwrite them, or Cancel to stop.";

            var answer = MessageBox.Show(
                this,
                message,
                "Overwrite Enfusion outputs?",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.OK)
            {
                SetFeedback(EddsFeedbackText, "Pack cancelled. Existing files were left unchanged.", isError: false);
                return;
            }

            job = job with { OverwriteExisting = true };
        }

        await RunWithProgressAsync(EddsProgress, EddsFeedbackText, async () =>
        {
            var packer = new EddsPacker(_codec);
            var savedPaths = await Task.Run(() => SaveEddsJob(packer, job));
            SetFeedback(EddsFeedbackText, $"Saved {FormatSavedFileNames(savedPaths)} to {job.OutputDirectory}.", isError: false);
        });
    }

    private void ClearEddsSlotsClick(object sender, RoutedEventArgs e)
    {
        foreach (var slotName in _eddsSlots.Keys.ToArray())
        {
            _eddsSlots[slotName] = new SlotState();
            SetEddsSlotUi(slotName, null, null);
        }

        EddsOutputNameTextBox.Text = string.Empty;
        EddsProgress.Value = 0;
        SetFeedback(EddsFeedbackText, "Images and file name cleared.", isError: false);
    }

    private void ClearEddsSlotClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is string slotName)
        {
            ClearEddsSlot(slotName);
            EddsProgress.Value = 0;
            SetFeedback(EddsFeedbackText, $"{DisplayEddsSlotName(slotName)} cleared.", isError: false);
        }
    }

    private void ClearEddsSlot(string slotName)
    {
        _eddsSlots[slotName] = new SlotState();
        SetEddsSlotUi(slotName, null, null);
    }

    private void FileDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private PackSlot? BuildPackSlot(TextureChannel targetChannel, ComboBox sourceChannelBox, CheckBox invertBox)
    {
        var slot = _packSlots[targetChannel];
        if (slot.Image is null)
        {
            return null;
        }

        return new PackSlot(slot.Image, SourceChannelFromCombo(sourceChannelBox), invertBox.IsChecked == true);
    }

    private TextureImage RequiredEddsImage(string slotName)
    {
        var image = _eddsSlots[slotName].Image;
        if (image is null)
        {
            throw new ChannelPackingException($"{DisplayEddsSlotName(slotName)} is required.");
        }

        return image;
    }

    private EddsPackJob BuildEddsPackJob(EddsPackMode mode, bool overwriteExisting)
    {
        var baseColor = mode is EddsPackMode.Both or EddsPackMode.BcrOnly
            ? RequiredEddsImage("BaseColor")
            : _eddsSlots["BaseColor"].Image;
        var normal = mode is EddsPackMode.Both or EddsPackMode.NmoOnly
            ? RequiredEddsImage("Normal")
            : _eddsSlots["Normal"].Image;
        var roughness = _eddsSlots["Roughness"].Image;
        var metallic = _eddsSlots["Metallic"].Image;
        var ao = _eddsSlots["AmbientOcclusion"].Image;
        var outputDirectory = GetEddsOutputDirectory(mode, baseColor, normal);
        var bcrFormat = OutputFormats.PreferredForTiffSources(baseColor, roughness);
        var nmoFormat = OutputFormats.PreferredForTiffSources(normal, metallic, ao);

        EddsPacker.GetOutputPaths(outputDirectory, EddsOutputNameTextBox.Text, bcrFormat, nmoFormat);

        return new EddsPackJob(
            mode,
            baseColor,
            normal,
            roughness,
            metallic,
            ao,
            EddsOutputNameTextBox.Text,
            outputDirectory,
            overwriteExisting,
            bcrFormat,
            nmoFormat);
    }

    private static string GetEddsOutputDirectory(EddsPackMode mode, TextureImage? baseColor, TextureImage? normal)
    {
        if (mode == EddsPackMode.NmoOnly && baseColor?.SourcePath is null)
        {
            return SourceDirectory(normal ?? throw new ChannelPackingException("Normal is required."), "Normal");
        }

        return SourceDirectory(baseColor ?? throw new ChannelPackingException("Base Color is required."), "Base Color");
    }

    private static string SourceDirectory(TextureImage image, string imageName)
    {
        if (image.SourcePath is null)
        {
            throw new ChannelPackingException($"{imageName} must be loaded from a file.");
        }

        var outputDirectory = Path.GetDirectoryName(image.SourcePath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ChannelPackingException($"Could not find the {imageName} directory.");
        }

        return outputDirectory;
    }

    private static string[] SaveEddsJob(EddsPacker packer, EddsPackJob job)
    {
        return job.Mode switch
        {
            EddsPackMode.Both => SaveBothEddsOutputs(packer, job),
            EddsPackMode.BcrOnly =>
            [
                packer.PackBcrAndSave(
                    job.BaseColor ?? throw new ChannelPackingException("Base Color is required."),
                    job.Roughness,
                    job.OutputName,
                    job.OutputDirectory,
                    job.OverwriteExisting)
            ],
            EddsPackMode.NmoOnly =>
            [
                packer.PackNmoAndSave(
                    job.Normal ?? throw new ChannelPackingException("Normal is required."),
                    job.Metallic,
                    job.AmbientOcclusion,
                    job.OutputName,
                    job.OutputDirectory,
                    job.OverwriteExisting)
            ],
            _ => throw new ChannelPackingException("Unknown Enfusion pack mode.")
        };
    }

    private static string[] SaveBothEddsOutputs(EddsPacker packer, EddsPackJob job)
    {
        var result = packer.PackAndSave(new EddsPackRequest(
            job.BaseColor ?? throw new ChannelPackingException("Base Color is required."),
            job.Normal ?? throw new ChannelPackingException("Normal is required."),
            job.Roughness,
            job.Metallic,
            job.AmbientOcclusion,
            job.OutputName,
            job.OutputDirectory,
            job.OverwriteExisting));

        return [result.BcrPath, result.NmoPath];
    }

    private static string FormatSavedFileNames(IEnumerable<string> paths)
    {
        return string.Join(" and ", paths.Select(path => Path.GetFileName(path)));
    }

    private OutputFormat PreferredPackedOutputFormat()
    {
        return OutputFormats.PreferredForTiffSources(_packSlots.Values.Select(slot => slot.Image).ToArray());
    }

    private static IEnumerable<string> ExistingOutputFiles(EddsPackResult outputPaths, EddsPackMode mode)
    {
        if ((mode == EddsPackMode.Both || mode == EddsPackMode.BcrOnly) && File.Exists(outputPaths.BcrPath))
        {
            yield return outputPaths.BcrPath;
        }

        if ((mode == EddsPackMode.Both || mode == EddsPackMode.NmoOnly) && File.Exists(outputPaths.NmoPath))
        {
            yield return outputPaths.NmoPath;
        }
    }

    private void SaveUnpackedChannel(TextureChannel channel, string path, OutputFormat format)
    {
        if (_unpackedChannels is null)
        {
            throw new ChannelPackingException("Unpack an image before saving channels.");
        }

        _codec.Save(_unpackedChannels[channel], path, format);
    }

    private static async Task RunWithProgressAsync(ProgressBar progress, TextBlock feedback, Func<Task> action)
    {
        progress.IsIndeterminate = true;
        progress.Value = 0;
        SetFeedback(feedback, "Working...", isError: false);

        try
        {
            await action();
            progress.IsIndeterminate = false;
            progress.Value = 100;
        }
        catch (ChannelPackingException exception)
        {
            progress.IsIndeterminate = false;
            progress.Value = 0;
            SetFeedback(feedback, exception.Message, isError: true);
        }
        catch (Exception exception)
        {
            progress.IsIndeterminate = false;
            progress.Value = 0;
            SetFeedback(feedback, $"Unexpected error: {exception.Message}", isError: true);
        }
    }

    private static void SetFeedback(TextBlock feedback, string message, bool isError)
    {
        feedback.Text = message;
        feedback.Foreground = isError
            ? ErrorBrush
            : message.Contains("Saved", StringComparison.OrdinalIgnoreCase) || message.Contains("loaded", StringComparison.OrdinalIgnoreCase) || message.Contains("unpacked", StringComparison.OrdinalIgnoreCase)
                ? SuccessBrush
                : NeutralBrush;
    }

    private static string? ChooseInputFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.tga;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.tga;*.tif;*.tiff|All files (*.*)|*.*",
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? ChooseOutputFile(string suggestedFileName, OutputFormat preferredFormat)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = "PNG image (*.png)|*.png|TGA image (*.tga)|*.tga|TIFF image (*.tif;*.tiff)|*.tif;*.tiff",
            FilterIndex = SaveDialogFilterIndex(preferredFormat),
            DefaultExt = OutputFormats.ExtensionFor(preferredFormat),
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? GetDroppedFilePath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        return files.FirstOrDefault();
    }

    private static TextureChannel ParseTextureChannel(string channelName)
    {
        return Enum.Parse<TextureChannel>(channelName, ignoreCase: true);
    }

    private static TextureChannel SourceChannelFromCombo(ComboBox comboBox)
    {
        return comboBox.SelectedIndex switch
        {
            0 => TextureChannel.Red,
            1 => TextureChannel.Green,
            2 => TextureChannel.Blue,
            3 => TextureChannel.Alpha,
            _ => TextureChannel.Red
        };
    }

    private static int SaveDialogFilterIndex(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.Png => 1,
            OutputFormat.Tga => 2,
            OutputFormat.Tiff => 3,
            _ => 1
        };
    }

    private static BitmapSource ToBitmapSource(TextureImage image)
    {
        image.Validate();

        var bgra = new byte[image.Rgba.Length];
        for (var index = 0; index < image.Rgba.Length; index += 4)
        {
            bgra[index] = image.Rgba[index + 2];
            bgra[index + 1] = image.Rgba[index + 1];
            bgra[index + 2] = image.Rgba[index];
            bgra[index + 3] = image.Rgba[index + 3];
        }

        var bitmap = BitmapSource.Create(
            image.Width,
            image.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            bgra,
            image.Width * 4);

        bitmap.Freeze();
        return bitmap;
    }

    private static string DescribeImage(string path, TextureImage image)
    {
        return $"{Path.GetFileName(path)} - {image.Width} x {image.Height}";
    }

    private void ClearUnpackPreviews()
    {
        RedChannelImage.Source = null;
        GreenChannelImage.Source = null;
        BlueChannelImage.Source = null;
        AlphaChannelImage.Source = null;
    }

    private void SetPackSlotUi(TextureChannel channel, TextureImage? image, string? path)
    {
        var text = path is null || image is null ? "Drop image here." : DescribeImage(path, image);
        var preview = image is null ? null : ToBitmapSource(image);

        switch (channel)
        {
            case TextureChannel.Red:
                PackRedPathText.Text = text;
                PackRedPreview.Source = preview;
                break;
            case TextureChannel.Green:
                PackGreenPathText.Text = text;
                PackGreenPreview.Source = preview;
                break;
            case TextureChannel.Blue:
                PackBluePathText.Text = text;
                PackBluePreview.Source = preview;
                break;
            case TextureChannel.Alpha:
                PackAlphaPathText.Text = text;
                PackAlphaPreview.Source = preview;
                break;
        }
    }

    private void SetEddsSlotUi(string slotName, TextureImage? image, string? path)
    {
        var text = path is null || image is null
            ? IsOptionalEddsSlot(slotName) ? "Optional." : "Required."
            : DescribeImage(path, image);
        var preview = image is null ? null : ToBitmapSource(image);

        switch (slotName)
        {
            case "BaseColor":
                EddsBaseColorPathText.Text = text;
                EddsBaseColorPreview.Source = preview;
                break;
            case "Normal":
                EddsNormalPathText.Text = text;
                EddsNormalPreview.Source = preview;
                break;
            case "Roughness":
                EddsRoughnessPathText.Text = text;
                EddsRoughnessPreview.Source = preview;
                break;
            case "Metallic":
                EddsMetallicPathText.Text = text;
                EddsMetallicPreview.Source = preview;
                break;
            case "AmbientOcclusion":
                EddsAoPathText.Text = text;
                EddsAoPreview.Source = preview;
                break;
        }
    }

    private static string DisplayEddsSlotName(string slotName)
    {
        return slotName switch
        {
            "BaseColor" => "Base Color",
            "AmbientOcclusion" => "Ambient Occlusion",
            _ => slotName
        };
    }

    private static bool IsOptionalEddsSlot(string slotName)
    {
        return slotName is "Roughness" or "Metallic" or "AmbientOcclusion";
    }

    private enum EddsPackMode
    {
        Both,
        BcrOnly,
        NmoOnly
    }

    private sealed record EddsPackJob(
        EddsPackMode Mode,
        TextureImage? BaseColor,
        TextureImage? Normal,
        TextureImage? Roughness,
        TextureImage? Metallic,
        TextureImage? AmbientOcclusion,
        string OutputName,
        string OutputDirectory,
        bool OverwriteExisting,
        OutputFormat BcrOutputFormat,
        OutputFormat NmoOutputFormat);

    private sealed record SlotState(TextureImage? Image = null, string? Path = null);
}
