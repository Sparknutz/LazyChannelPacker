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
            await Task.Run(() =>
            {
                SaveUnpackedChannel(TextureChannel.Red, Path.Combine(directory, $"{stem}_R.png"));
                SaveUnpackedChannel(TextureChannel.Green, Path.Combine(directory, $"{stem}_G.png"));
                SaveUnpackedChannel(TextureChannel.Blue, Path.Combine(directory, $"{stem}_B.png"));
                SaveUnpackedChannel(TextureChannel.Alpha, Path.Combine(directory, $"{stem}_A.png"));
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
            var path = ChooseOutputFile($"{channelName}.png");
            if (path is null)
            {
                SetFeedback(UnpackFeedbackText, "Save cancelled.", isError: false);
                return;
            }

            var format = OutputFormatFromPath(path);
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

            var outputPath = ChooseOutputFile("packed.png");
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

            var format = OutputFormatFromPath(outputPath);
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
            _packSlots[channel] = new SlotState();
            SetPackSlotUi(channel, null, null);
        }

        PackProgress.Value = 0;
        SetFeedback(PackFeedbackText, "Pack slots cleared.", isError: false);
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
        EddsPackRequest request;

        try
        {
            request = BuildEddsPackRequest(overwriteExisting: false);
        }
        catch (ChannelPackingException exception)
        {
            SetFeedback(EddsFeedbackText, exception.Message, isError: true);
            return;
        }

        var outputPaths = EddsPacker.GetOutputPaths(request.OutputDirectory, request.OutputName);
        var existingOutputFiles = ExistingOutputFiles(outputPaths).ToArray();

        if (existingOutputFiles.Length > 0)
        {
            var message =
                "These files already exist:\n\n" +
                string.Join("\n", existingOutputFiles.Select(Path.GetFileName)) +
                "\n\nClick OK to overwrite them, or Cancel to stop.";

            var answer = MessageBox.Show(
                this,
                message,
                "Overwrite EDDS outputs?",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.OK)
            {
                SetFeedback(EddsFeedbackText, "Pack cancelled. Existing files were left unchanged.", isError: false);
                return;
            }

            request = request with { OverwriteExisting = true };
        }

        await RunWithProgressAsync(EddsProgress, EddsFeedbackText, async () =>
        {
            var packer = new EddsPacker(_codec);
            var result = await Task.Run(() => packer.PackAndSave(request));
            SetFeedback(EddsFeedbackText, $"Saved {Path.GetFileName(result.BcrPath)} and {Path.GetFileName(result.NmoPath)} to {request.OutputDirectory}.", isError: false);
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

    private EddsPackRequest BuildEddsPackRequest(bool overwriteExisting)
    {
        var baseColor = RequiredEddsImage("BaseColor");
        var normal = RequiredEddsImage("Normal");
        var roughness = RequiredEddsImage("Roughness");
        var metallic = _eddsSlots["Metallic"].Image;
        var ao = _eddsSlots["AmbientOcclusion"].Image;

        if (baseColor.SourcePath is null)
        {
            throw new ChannelPackingException("Base Color must be loaded from a file.");
        }

        var outputDirectory = Path.GetDirectoryName(baseColor.SourcePath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ChannelPackingException("Could not find the Base Color directory.");
        }

        EddsPacker.GetOutputPaths(outputDirectory, EddsOutputNameTextBox.Text);

        return new EddsPackRequest(
            baseColor,
            normal,
            roughness,
            metallic,
            ao,
            EddsOutputNameTextBox.Text,
            outputDirectory,
            overwriteExisting);
    }

    private static IEnumerable<string> ExistingOutputFiles(EddsPackResult outputPaths)
    {
        if (File.Exists(outputPaths.BcrPath))
        {
            yield return outputPaths.BcrPath;
        }

        if (File.Exists(outputPaths.NmoPath))
        {
            yield return outputPaths.NmoPath;
        }
    }

    private void SaveUnpackedChannel(TextureChannel channel, string path)
    {
        if (_unpackedChannels is null)
        {
            throw new ChannelPackingException("Unpack an image before saving channels.");
        }

        _codec.Save(_unpackedChannels[channel], path, OutputFormat.Png);
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
            Filter = "Images (*.png;*.jpg;*.jpeg;*.tga)|*.png;*.jpg;*.jpeg;*.tga|All files (*.*)|*.*",
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string? ChooseOutputFile(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = "PNG image (*.png)|*.png|TGA image (*.tga)|*.tga",
            DefaultExt = ".png",
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

    private static OutputFormat OutputFormatFromPath(string path)
    {
        return string.Equals(Path.GetExtension(path), ".tga", StringComparison.OrdinalIgnoreCase)
            ? OutputFormat.Tga
            : OutputFormat.Png;
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
        return slotName is "Metallic" or "AmbientOcclusion";
    }

    private sealed record SlotState(TextureImage? Image = null, string? Path = null);
}
