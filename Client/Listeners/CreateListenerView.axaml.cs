using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Client.Listeners;

public partial class CreateListenerView : Window
{
    public CreateListenerView()
    {
        InitializeComponent();
    }

    private static FilePickerFileType ZipFileType { get; } = new("ZIP Files")
    {
        Patterns = ["*.zip"],
        MimeTypes = ["application/zip"]
    };

    private async void OnBrowseX86Clicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select x86 COFF",
            AllowMultiple = false,
            FileTypeFilter = [ZipFileType]
        });

        if (files.Count > 0 && DataContext is CreateListenerViewModel vm)
        {
            vm.X86Path = files[0].Path.LocalPath;
        }
    }

    private async void OnBrowseX64Clicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select x64 COFF",
            AllowMultiple = false,
            FileTypeFilter = [ZipFileType]
        });

        if (files.Count > 0 && DataContext is CreateListenerViewModel vm)
        {
            vm.X64Path = files[0].Path.LocalPath;
        }
    }

    private void OnCreateClicked(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}