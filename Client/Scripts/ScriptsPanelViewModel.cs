using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Client.Services;
using ReactiveUI;

namespace Client.Scripts;

internal sealed class ScriptsPanelViewModel : ViewModelBase
{
    private readonly ScriptEngine _engine;
    private readonly SettingsService _settings;

    public ScriptsPanelViewModel(ScriptEngine engine, SettingsService settings)
    {
        _engine   = engine;
        _settings = settings;
        _ = LoadSavedScriptsAsync();
    }

    public ObservableCollection<ScriptItemViewModel> Scripts { get; } = [];

    private readonly Dictionary<ScriptItemViewModel, IReadOnlyList<string>> _scriptCommands = new();

    public bool HasNoScripts
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool IsExpanded
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public void ToggleExpanded() => IsExpanded = !IsExpanded;

    public ScriptItemViewModel AddScript(string filePath, string code)
    {
        var vm = LoadScript(filePath, code);
        var s = _settings.Load();
        
        if (!s.Scripts.Contains(filePath, StringComparer.OrdinalIgnoreCase))
        {
            s.Scripts.Add(filePath);
        }
        
        _settings.Save(s);

        return vm;
    }

    private ScriptItemViewModel LoadScript(string filePath, string code)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var vm = new ScriptItemViewModel(name, filePath, code, RunScriptAsync, ReloadScriptAsync, UnloadScript);
        Scripts.Add(vm);
        HasNoScripts = false;
        _ = RunScriptAsync(vm);
        return vm;
    }

    private async Task LoadSavedScriptsAsync()
    {
        var s = _settings.Load();
        var validPaths = new List<string>();

        foreach (var path in s.Scripts)
        {
            if (!File.Exists(path)) continue;

            var code = await File.ReadAllTextAsync(path);
            LoadScript(path, code);
            validPaths.Add(path);
        }

        if (validPaths.Count != s.Scripts.Count)
        {
            s.Scripts = validPaths;
            _settings.Save(s);
        }
    }

    internal async Task RunScriptAsync(ScriptItemViewModel vm)
    {
        if (_scriptCommands.TryGetValue(vm, out var oldCmds))
        {
            _engine.UnregisterCommands(oldCmds);
            _scriptCommands.Remove(vm);
        }

        vm.IsRunning = true;

        try
        {
            var cmds = await _engine.RunAsync(
                vm.Code,
                vm.FilePath,
                msg => Dispatcher.UIThread.Post(() => vm.Console.Append(msg)),
                CancellationToken.None);

            if (cmds.Count > 0)
            {
                _scriptCommands[vm] = cmds;
            }
        }
        catch (Exception ex)
        {
            vm.Console.Append($"[error] {ex.Message}");
        }
        finally
        {
            vm.IsRunning = false;
        }
    }

    private async Task ReloadScriptAsync(ScriptItemViewModel vm)
    {
        vm.Code = await File.ReadAllTextAsync(vm.FilePath);
        vm.Console.Clear();
        await RunScriptAsync(vm);
    }

    private void UnloadScript(ScriptItemViewModel vm)
    {
        if (_scriptCommands.TryGetValue(vm, out var cmds))
        {
            _engine.UnregisterCommands(cmds);
            _scriptCommands.Remove(vm);
        }

        Scripts.Remove(vm);
        HasNoScripts = Scripts.Count == 0;

        var s = _settings.Load();
        s.Scripts.Remove(vm.FilePath);
        _settings.Save(s);
    }
}