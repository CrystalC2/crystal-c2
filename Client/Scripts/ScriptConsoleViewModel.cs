using System;
using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;

namespace Client.Scripts;

internal sealed class ScriptConsoleViewModel(string scriptName, ReactiveCommand<Unit, Unit> reloadCommand) : ReactiveObject
{
    public string Title => $"{scriptName}";
    public ReactiveCommand<Unit, Unit> ReloadCommand { get; } = reloadCommand;
    public ObservableCollection<ScriptLogEntry> Log { get; } = [];

    public void Append(string message) => Log.Add(new ScriptLogEntry(message, DateTimeOffset.Now));
    public void Clear() => Log.Clear();
}