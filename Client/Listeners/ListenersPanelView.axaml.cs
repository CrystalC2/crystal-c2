using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Client.Listeners;

public partial class ListenersPanelView : UserControl
{
    public ListenersPanelView()
    {
        InitializeComponent();
    }

    private void OnToggleExpandedClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ListenersPanelViewModel vm)
        {
            vm.ToggleExpanded();
        }
    }

    private async void OnCreateListenerClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var dialog = new CreateListenerView();
        var vm = new CreateListenerViewModel();
        dialog.DataContext = vm;

        var confirmed = await dialog.ShowDialog<bool>(owner);
        
        if (!confirmed || !vm.IsValid) return;

        try
        {
            var x86Bytes = vm.X86Path is not null ? await File.ReadAllBytesAsync(vm.X86Path) : [];
            var x64Bytes = vm.X64Path is not null ? await File.ReadAllBytesAsync(vm.X64Path) : [];

            await ((ListenersPanelViewModel)DataContext!)
                .CreateListenerAsync(vm.Name, uint.Parse(vm.Port), x86Bytes, x64Bytes);
        }
        catch (Exception ex)
        {
            // TODO: show error toast
            await Console.Error.WriteLineAsync($"Create listener failed: {ex.Message}");
        }
    }

    private async void OnDeleteListenerClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not ListenerItemViewModel item) return;
        if (DataContext is not ListenersPanelViewModel vm) return;

        try
        {
            await vm.DeleteListenerAsync(item.Id);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Delete listener failed: {ex.Message}");
        }
    }
}