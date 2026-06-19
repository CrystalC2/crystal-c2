using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Client.Payloads;

public partial class GeneratePayloadView : Window
{
    private static readonly FilePickerFileType SpecFileType = new("Spec Files")
    {
        Patterns = ["*.spec"],
    };

    public GeneratePayloadView()
    {
        InitializeComponent();
    }

    private async void OnBrowseSpecClicked(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Config Spec",
            AllowMultiple = false,
            FileTypeFilter = [SpecFileType]
        });

        if (files.Count > 0 && DataContext is GeneratePayloadViewModel vm)
        {
            vm.SpecPath = files[0].Path.LocalPath;
        }
    }

    private void OnGenerateClicked(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(false);
}