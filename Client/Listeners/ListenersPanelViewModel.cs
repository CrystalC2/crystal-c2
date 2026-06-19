using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Client.Services;
using CrystalC2.Listeners;
using ReactiveUI;

namespace Client.Listeners;

internal sealed class ListenersPanelViewModel(ListenerGrpcService service) : ViewModelBase, IDisposable
{
    private CancellationTokenSource? _cts;

    public ObservableCollection<ListenerItemViewModel> Listeners { get; } = [];

    public bool HasNoListeners
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

    public Task CreateListenerAsync(string name, uint port, byte[] x86Coff, byte[] x64Coff)
    {
        return service.CreateListenerAsync(name, port, x86Coff, x64Coff);
    }

    public Task DeleteListenerAsync(uint id)
    {
        return service.DeleteListenerAsync(id);
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

    private void HandleEvent(ListenerEvent evt)
    {
        switch (evt.Type)
        {
            case ListenerEventType.ListenerEventAdded:
            {
                if (Listeners.All(l => l.Id != evt.Listener.Id))
                {
                    Listeners.Add(new ListenerItemViewModel(evt.Listener));
                    
                    // save files
                    File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "Beacon", $"{evt.Listener.Name}.x64.zip"), evt.Listener.X64Coff.ToByteArray());
                    File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "Beacon", $"{evt.Listener.Name}.x86.zip"), evt.Listener.X86Coff.ToByteArray());
                    File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "Beacon", $"{evt.Listener.Name}.key"), evt.Listener.PublicKey.ToByteArray());
                }

                break;
            }
            
            case ListenerEventType.ListenerEventDeleted:
            {
                var existing = Listeners.FirstOrDefault(l => l.Id == evt.Listener.Id);

                if (existing is not null)
                {
                    Listeners.Remove(existing);
                    
                    // remove coff zip files
                    File.Delete(Path.Combine(AppContext.BaseDirectory, "Beacon", $"{evt.Listener.Name}.x64.zip"));
                    File.Delete(Path.Combine(AppContext.BaseDirectory, "Beacon", $"{evt.Listener.Name}.x86.zip"));
                    File.Delete(Path.Combine(AppContext.BaseDirectory, "Beacon", $"{evt.Listener.Name}.key"));
                }

                break;
            }

            case ListenerEventType.ListenerEventUnknown:
            default:
                break;
        }

        HasNoListeners = Listeners.Count == 0;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        
        Listeners.Clear();
    }
}