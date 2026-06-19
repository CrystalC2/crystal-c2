using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FluentAvalonia.UI.Controls;

namespace Client.Payloads;

public partial class PayloadsPanelView : UserControl
{
    public PayloadsPanelView()
    {
        InitializeComponent();
    }

    private void OnToggleExpandedClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PayloadsPanelViewModel vm)
        {
            vm.ToggleExpanded();
        }
    }

    private async void OnGeneratePayloadClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var panelVm = (PayloadsPanelViewModel)DataContext!;
        var dialog = new GeneratePayloadView();
        var vm = new GeneratePayloadViewModel(panelVm.Listeners);
        dialog.DataContext = vm;

        var confirmed = await dialog.ShowDialog<bool>(owner);
        
        if (!confirmed || !vm.IsValid) return;

        try
        {
            var result = await panelVm.GenerateAsync(vm);

            if (vm.AddToStore) return;

            var exit = vm.ExitFunc.Equals("ExitProcess", StringComparison.OrdinalIgnoreCase) ? "xprocess" : "xthread";
            var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Generated Payload",
                SuggestedFileName = $"{vm.SelectedListener!.Name}.{vm.Arch}.{exit}.bin",
                FileTypeChoices = [BinFileType]
            });

            if (file is not null)
            {
                await using var stream = await file.OpenWriteAsync();
                await stream.WriteAsync(result.Bytes);
            }

            if (result.Yara is null) return;
            {
                var yaraFile = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save YARA Rules",
                    SuggestedFileName = $"{vm.SelectedListener!.Name}.{vm.Arch}.{exit}.yar",
                    FileTypeChoices = [YaraFileType]
                });

                if (yaraFile is null) return;

                await using var stream = await yaraFile.OpenWriteAsync();
                await stream.WriteAsync(System.Text.Encoding.UTF8.GetBytes(result.Yara));
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Generate Failed", ex.Message);
        }
    }

    private async void OnAddPayloadClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new AddPayloadView();
        var vm = new AddPayloadViewModel();
        dialog.DataContext = vm;

        var confirmed = await dialog.ShowDialog<bool>(owner);
        
        if (!confirmed || !vm.IsValid) return;

        try
        {
            var bytes = await File.ReadAllBytesAsync(vm.FilePath!);
            var yara = string.IsNullOrWhiteSpace(vm.YaraPath) ? null : await File.ReadAllTextAsync(vm.YaraPath);
            var note  = string.IsNullOrWhiteSpace(vm.Note) ? null : vm.Note;

            await ((PayloadsPanelViewModel)DataContext!)
                .AddPayloadAsync(vm.Name, bytes, yara, note);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Add Payload Failed", ex.Message);
        }
    }

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

    private async void OnExportPayloadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not PayloadItemViewModel item) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title           = "Export Payload",
            SuggestedFileName = $"{item.Name}.bin",
            FileTypeChoices = [BinFileType]
        });

        if (file is null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(item.Bytes);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Export Failed", ex.Message);
        }
    }

    private async void OnDeletePayloadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not PayloadItemViewModel item) return;
        if (DataContext is not PayloadsPanelViewModel vm) return;

        try
        {
            await vm.DeletePayloadAsync(item.Id);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Delete Failed", ex.Message);
        }
    }

    private static async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title          = title,
            Content        = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}