using System;
using System.Threading.Tasks;
using ReactiveUI;

namespace Client.Scripts;

internal sealed class ScriptItemViewModel : ReactiveObject
{
    public string Name { get; }
    public string FilePath { get; }
    public string Code { get; set; }
    public ScriptConsoleViewModel Console { get; }

    public bool IsRunning
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RunCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ReloadCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> UnloadCommand { get; }

    public ScriptItemViewModel(
        string name,
        string filePath,
        string code,
        Func<ScriptItemViewModel, Task> onRun,
        Func<ScriptItemViewModel, Task> onReload,
        Action<ScriptItemViewModel> onUnload)
    {
        Name = name;
        FilePath = filePath;
        Code = code;

        var canRun = this.WhenAnyValue(x => x.IsRunning, r => !r);
        RunCommand    = ReactiveCommand.CreateFromTask(() => onRun(this), canRun);
        ReloadCommand = ReactiveCommand.CreateFromTask(() => onReload(this), canRun);
        UnloadCommand = ReactiveCommand.Create(() => onUnload(this));

        Console = new ScriptConsoleViewModel(name, ReloadCommand);
    }
}