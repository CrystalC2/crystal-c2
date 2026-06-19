using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Client.Scripts;

public partial class ScriptsPanelView : UserControl
{
    private static readonly FilePickerFileType LuaFileType = new("Lua Scripts")
    {
        Patterns  = ["*.lua"],
        MimeTypes = ["text/plain"]
    };

    private readonly Dictionary<string, ScriptConsoleView> _consoles = new();

    public ScriptsPanelView()
    {
        InitializeComponent();
    }

    private void OnToggleExpandedClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ScriptsPanelViewModel vm)
        {
            vm.ToggleExpanded();
        }
    }

    private async void OnLoadScriptClicked(object? sender, RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Load Lua Script",
            AllowMultiple = false,
            FileTypeFilter = [LuaFileType]
        });

        if (files.Count == 0) return;
        if (DataContext is not ScriptsPanelViewModel vm) return;

        var path = files[0].Path.LocalPath;
        var code = await File.ReadAllTextAsync(path);
        var script = vm.AddScript(path, code);
        OpenOrFocusConsole(script);
    }

    private void OnRunScriptClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ScriptItemViewModel script }) return;
        
        OpenOrFocusConsole(script);
        
        if (DataContext is ScriptsPanelViewModel vm)
        {
            _ = vm.RunScriptAsync(script);
        }
    }

    private void OnOpenConsoleClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ScriptItemViewModel script }) return;
        OpenOrFocusConsole(script);
    }

    private void OpenOrFocusConsole(ScriptItemViewModel script)
    {
        if (_consoles.TryGetValue(script.FilePath, out var existing))
        {
            existing.Activate();
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var window = new ScriptConsoleView { DataContext = script.Console };
        _consoles[script.FilePath] = window;
        window.Closed += (_, _) => _consoles.Remove(script.FilePath);
        window.Show(owner);
    }
}