using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Client.Listeners;
using Client.Services;
using CrystalC2.Payloads;
using ReactiveUI;

namespace Client.Payloads;

internal sealed class PayloadsPanelViewModel(
    PayloadGrpcService service,
    LinkerApiClient linker,
    ListenersPanelViewModel listenersPanel)
    : ViewModelBase, IDisposable
{
    public ObservableCollection<ListenerItemViewModel> Listeners { get; } = listenersPanel.Listeners;

    private CancellationTokenSource? _cts;

    public ObservableCollection<PayloadItemViewModel> Payloads { get; } = [];

    public bool HasNoPayloads
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

    public void StartStreaming()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = StreamEventsAsync(_cts.Token);
    }

    public Task AddPayloadAsync(string name, byte[] bytes, string? yara, string? note)
    {
        return service.AddPayloadAsync(name, bytes, yara, note);
    }

    public Task DeletePayloadAsync(uint id) => service.DeletePayloadAsync(id);

    public async Task<LinkerResult> GenerateAsync(GeneratePayloadViewModel options)
    {
        if (options.SelectedListener is null)
        {
            throw new ArgumentNullException(nameof(options.SelectedListener));
        }
        
        var beaconSpec = Path.Combine(AppContext.BaseDirectory, "Beacon", "beacon.spec");

        var config = options.SpecPath is not null
            ? new[] { options.SpecPath }
            : [];

        var sleep  = int.TryParse(options.Sleep,  out var s) ? s : 60;
        var jitter = int.TryParse(options.Jitter, out var j) ? j : 20;
        var udc2 = Path.Combine(AppContext.BaseDirectory, "Beacon", $"{options.SelectedListener.Name}.{options.Arch}.zip");
        var pubkey = Path.Combine(AppContext.BaseDirectory, "Beacon", $"{options.SelectedListener.Name}.key");

        var request = new LinkerRequest
        {
            Params = new LinkerParams
            {
                Spec = beaconSpec,
                Arch = options.Arch
            },
            Config = config,
            Env = new LinkerEnv
            {
                Sleep    = sleep.ToString("D3"),
                Jitter   = jitter.ToString("D3"),
                Udc2     = udc2,
                PubKey   = pubkey,
                ExitFunc = options.ExitFunc == "ExitProcess" ? 1.ToString("D3") : 0.ToString("D3")
            },
            Yara = options.GenerateYara
        };

        var result = await linker.GenerateAsync(request);

        if (options.AddToStore)
        {
            var note = string.IsNullOrWhiteSpace(options.PayloadNote) ? null : options.PayloadNote;
            var yara = options.GenerateYara ? result.Yara : null;
            await service.AddPayloadAsync(options.PayloadName, result.Bytes, yara, note);
        }

        return result;
    }

    private async Task StreamEventsAsync(CancellationToken ct)
    {
        try
        {
            using var call = service.StreamEvents(ct);
            while (await call.ResponseStream.MoveNext(ct))
            {
                var evt = call.ResponseStream.Current;
                Dispatcher.UIThread.Post(() => HandleEvent(evt));
            }
        }
        catch (OperationCanceledException) { }
    }

    private void HandleEvent(PayloadEvent evt)
    {
        switch (evt.Type)
        {
            case PayloadEventType.PayloadEventAdded:
            {
                if (Payloads.All(p => p.Id != evt.Payload.Id))
                {
                    Payloads.Add(new PayloadItemViewModel(evt.Payload));
                }
                
                break;
            }
            
            case PayloadEventType.PayloadEventDeleted:
            {
                var existing = Payloads.FirstOrDefault(p => p.Id == evt.Payload.Id);
                
                if (existing is not null)
                {
                    Payloads.Remove(existing);
                }
                
                break;
            }

            case PayloadEventType.PayloadEventUnknown:
            default:
                break;
        }

        HasNoPayloads = Payloads.Count == 0;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        
        Payloads.Clear();
    }
}