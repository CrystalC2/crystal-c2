using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Client.Payloads;
using Client.Scripts;
using Client.Services;
using Client.Sessions;
using CrystalC2.Tasks;
using Google.Protobuf;
using ReactiveUI;

namespace Client.Scripts.ScriptEditor;

internal sealed class ScriptEditorViewModel : ReactiveObject, IAsyncDisposable
{
    private readonly TaskGrpcService _service;
    private readonly Encoding _ansi;
    private readonly ObservableCollection<PayloadItemViewModel> _payloads;
    private readonly CancellationTokenSource _cts = new();

    public BeaconSessionViewModel Session { get; }
    public string Title => $"Railgun @ {Session.Computer} ({Session.Process})";

    // Wired by view code-behind to bridge AvaloniaEdit text
    public Func<string>? GetSource { get; set; }
    public Action<string>? SetSource { get; set; }

    // Wired by view code-behind for file dialogs
    public Func<Task<(string Path, string Content)?>>? RequestOpenFile { get; set; }
    public Func<string, Task<bool>>? RequestSaveFile { get; set; }

    private bool _statusIsError;

    public string Status
    {
        get;
        private set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(StatusForeground));
        }
    } = string.Empty;

    public bool StatusIsError
    {
        get => _statusIsError;
        private set
        {
            this.RaiseAndSetIfChanged(ref _statusIsError, value);
            this.RaisePropertyChanged(nameof(StatusForeground));
        }
    }

    public string StatusForeground => _statusIsError ? "#EF4444" : "#3FB950";

    public string Output
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool HasOutput
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<RailgunArgViewModel> Args { get; } = [];
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> AddArgCommand { get; }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> GoCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> LoadCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> SaveCommand { get; }

    public ScriptEditorViewModel(
        BeaconSessionViewModel session,
        TaskGrpcService service,
        ObservableCollection<PayloadItemViewModel> payloads)
    {
        Session = session;
        _service = service;
        _ansi = ResolveAnsiEncoding(session.Charset);
        _payloads = payloads;

        AddArgCommand = ReactiveCommand.Create(() => Args.Add(new RailgunArgViewModel(Args, _payloads)));

        GoCommand   = ReactiveCommand.CreateFromTask(GoAsync);
        LoadCommand = ReactiveCommand.CreateFromTask(LoadAsync);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);

        _ = _service.StartAsync(OnResponse, _cts.Token);
    }

    private static Encoding ResolveAnsiEncoding(int charset)
    {
        try { return charset > 0 ? Encoding.GetEncoding(charset) : Encoding.UTF8; }
        catch { return Encoding.UTF8; }
    }

    private Task GoAsync()
    {
        var source = GetSource?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            Status = "Nothing to run.";
            StatusIsError = false;
            return Task.CompletedTask;
        }

        byte[] compiled;
        try
        {
            compiled = ScriptEngine.CompileRailgunScript(source, _ansi);
        }
        catch (Exception ex)
        {
            Status = $"Compile error: {ex.Message}";
            StatusIsError = true;
            return Task.CompletedTask;
        }

        byte[] argsBuf = [];
        if (Args.Count > 0)
        {
            try { argsBuf = BuildArgBuffer(); }
            catch (Exception ex)
            {
                Status = $"Args error: {ex.Message}";
                StatusIsError = true;
                return Task.CompletedTask;
            }
        }

        var payload = argsBuf.Length > 0 ? [.. compiled, .. argsBuf] : compiled;

        Status = "Sent.";
        StatusIsError = false;
        Output = string.Empty;
        HasOutput = false;

        _ = _service.SendAsync(new TaskRequest
        {
            BeaconId    = Session.BeaconId,
            TaskType    = TaskType.Railgun,
            TaskData    = ByteString.CopyFrom(payload),
            CommandLine = "railgun"
        });

        return Task.CompletedTask;
    }

    private async Task LoadAsync()
    {
        var result = await (RequestOpenFile?.Invoke() ?? Task.FromResult<(string, string)?>(null));
        if (result is null) return;

        SetSource?.Invoke(result.Value.Content);
        Status = $"Loaded  {result.Value.Path}";
        StatusIsError = false;
    }

    private async Task SaveAsync()
    {
        var source = GetSource?.Invoke() ?? string.Empty;
        var ok = await (RequestSaveFile?.Invoke(source) ?? Task.FromResult(false));
        if (ok)
        {
            Status = "Saved.";
            StatusIsError = false;
        }
    }

    private void OnResponse(TaskResponse response)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!response.HasOutput) return;
            var chunk = Encoding.UTF8.GetString(response.Output.ToByteArray());
            Output = string.IsNullOrEmpty(Output) ? chunk : Output + chunk;
            HasOutput = true;
        });
    }

    private byte[] BuildArgBuffer()
    {
        using var ms = new MemoryStream();
        foreach (var arg in Args)
        {
            if (arg.IsPayloadArg)
            {
                if (arg.SelectedPayload is null)
                    throw new InvalidOperationException("no payload selected");
                var bytes = arg.SelectedPayload.Bytes;
                WriteBE(ms, bytes.Length);  // i: length consumed by arg_int()
                WriteBE(ms, bytes.Length);  // b: blob length prefix consumed by arg_bytes()
                ms.Write(bytes);
            }
            else
            {
                ms.Write(ScriptEngine.PackRailgunArgs([$"{arg.SelectedType}:{arg.Value}"], _ansi));
            }
        }
        return ms.ToArray();

        static void WriteBE(Stream s, int v)
        {
            s.WriteByte((byte)(v >> 24));
            s.WriteByte((byte)(v >> 16));
            s.WriteByte((byte)(v >>  8));
            s.WriteByte((byte) v);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        await _service.DisposeAsync();
    }
}

internal sealed class RailgunArgViewModel : ReactiveObject
{
    public static IReadOnlyList<string> AvailableTypes { get; } = ["i", "z", "Z", "payload"];

    public ObservableCollection<PayloadItemViewModel> Payloads { get; }

    public string SelectedType
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsTextArg));
            this.RaisePropertyChanged(nameof(IsPayloadArg));
        }
    } = "z";

    public bool IsTextArg    => SelectedType != "payload";
    public bool IsPayloadArg => SelectedType == "payload";

    public string Value
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public PayloadItemViewModel? SelectedPayload
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RemoveCommand { get; }

    internal RailgunArgViewModel(
        ObservableCollection<RailgunArgViewModel> owner,
        ObservableCollection<PayloadItemViewModel> payloads)
    {
        Payloads = payloads;
        RemoveCommand = ReactiveCommand.Create(() => { owner.Remove(this); });
    }
}