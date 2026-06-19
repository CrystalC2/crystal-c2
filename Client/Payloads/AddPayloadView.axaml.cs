using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Client.Payloads;

public partial class AddPayloadView : Window
{
    private static readonly FilePickerFileType BinFileType = new("Binary Files")
    {
        Patterns = ["*.bin"],
        MimeTypes = ["application/octet-stream"]
    };

    private static readonly FilePickerFileType YaraFileType = new("YARA Rules")
    {
        Patterns = ["*.yar", "*.yara"],
        MimeTypes = ["text/plain"]
    };

    public AddPayloadView()
    {
        InitializeComponent();
    }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Payload",
            AllowMultiple = false,
            FileTypeFilter = [BinFileType]
        });

        if (files.Count <= 0 || DataContext is not AddPayloadViewModel vm) return;

        vm.FilePath = files[0].Path.LocalPath;

        if (string.IsNullOrWhiteSpace(vm.Name))
        {
            vm.Name = Path.GetFileNameWithoutExtension(vm.FilePath);
        }
    }

    private async void OnBrowseYaraClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select YARA Rule",
            AllowMultiple = false,
            FileTypeFilter = [YaraFileType]
        });

        if (files.Count > 0 && DataContext is AddPayloadViewModel vm)
        {
            vm.YaraPath = files[0].Path.LocalPath;
        }
    }

    private void OnAddClicked(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}