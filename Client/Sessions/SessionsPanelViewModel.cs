using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Client.Listeners;
using Client.Scripts;
using Client.Services;
using Client.Scripts.ScriptEditor;
using CrystalC2.Beacons;
using ReactiveUI;

namespace Client.Sessions;

internal sealed class SessionsPanelViewModel : ViewModelBase, IDisposable
{
    private readonly BeaconGrpcService _service;
    private readonly ScriptEngine _engine;
    private readonly Func<BeaconSessionViewModel, BeaconInteractionViewModel> _interactionFactory;
    private readonly Func<BeaconSessionViewModel, ScriptEditorViewModel> _scriptEditorFactory;
    private readonly DispatcherTimer _ticker;
    private CancellationTokenSource? _cts;

    public ObservableCollection<BeaconSessionViewModel> Sessions { get; } = [];
    public ObservableCollection<ListenerItemViewModel> Listeners { get; }

    public bool HasNoSessions
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public string? SortColumn
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool SortAscending
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public ReactiveCommand<string, System.Reactive.Unit> SortCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ShowTableCommand { get; }
    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ShowGraphCommand { get; }

    public bool IsGraphView
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public double TableViewOpacity => IsGraphView ? 0.4 : 1.0;
    public double GraphViewOpacity => IsGraphView ? 1.0 : 0.4;

    public string ListenerHeader => ColHeader("Listener");
    public string IpHeader => ColHeader("IP");
    public string UserHeader => ColHeader("User");
    public string ComputerHeader => ColHeader("Computer");
    public string PidHeader => ColHeader("PID");
    public string ProcessHeader => ColHeader("Process");
    public string ArchHeader => ColHeader("Arch");
    public string OsHeader => ColHeader("OS");
    public string LastHeader => ColHeader("Last");

    private string ColHeader(string col) =>
        SortColumn != col ? col : SortAscending ? $"{col} ▲" : $"{col} ▼";

    public SessionsPanelViewModel(
        BeaconGrpcService service,
        ScriptEngine engine,
        Func<BeaconSessionViewModel, BeaconInteractionViewModel> interactionFactory,
        Func<BeaconSessionViewModel, ScriptEditorViewModel> scriptEditorFactory,
        ListenersPanelViewModel listenersPanel)
    {
        _service = service;
        _engine = engine;
        _interactionFactory  = interactionFactory;
        _scriptEditorFactory = scriptEditorFactory;
        Listeners = listenersPanel.Listeners;
        Sessions.CollectionChanged += (_, _) => HasNoSessions = Sessions.Count == 0;
        SortCommand = ReactiveCommand.Create<string>(SortBy);
        ShowTableCommand = ReactiveCommand.Create(() => { IsGraphView = false; });
        ShowGraphCommand = ReactiveCommand.Create(() => { IsGraphView = true; });

        this.WhenAnyValue(x => x.IsGraphView).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(TableViewOpacity));
            this.RaisePropertyChanged(nameof(GraphViewOpacity));
        });

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _ticker.Tick += (_, _) =>
        {
            foreach (var s in Sessions) s.RefreshLastSeen();
        };
        _ticker.Start();
    }

    private void SortBy(string column)
    {
        if (SortColumn == column)
        {
            SortAscending = !SortAscending;
        }
        else
        {
            SortColumn = column;
            SortAscending = true;
        }

        ApplySort();
        RaiseHeadersChanged();
    }

    private void ApplySort()
    {
        if (SortColumn is null) return;

        IEnumerable<BeaconSessionViewModel> ordered = SortColumn switch
        {
            "Listener" => SortAscending
                ? Sessions.OrderBy(s => s.Listener)
                : Sessions.OrderByDescending(s => s.Listener),
            "IP" => SortAscending ? Sessions.OrderBy(s => s.InternalIp) : Sessions.OrderByDescending(s => s.InternalIp),
            "User" => SortAscending
                ? Sessions.OrderBy(s => s.UserDisplay)
                : Sessions.OrderByDescending(s => s.UserDisplay),
            "Computer" => SortAscending
                ? Sessions.OrderBy(s => s.Computer)
                : Sessions.OrderByDescending(s => s.Computer),
            "PID" => SortAscending ? Sessions.OrderBy(s => s.Pid) : Sessions.OrderByDescending(s => s.Pid),
            "Process" => SortAscending ? Sessions.OrderBy(s => s.Process) : Sessions.OrderByDescending(s => s.Process),
            "Arch" => SortAscending ? Sessions.OrderBy(s => s.Arch) : Sessions.OrderByDescending(s => s.Arch),
            "OS" => SortAscending ? Sessions.OrderBy(s => s.OsDisplay) : Sessions.OrderByDescending(s => s.OsDisplay),
            "Last" => SortAscending ? Sessions.OrderBy(s => s.LastSeen) : Sessions.OrderByDescending(s => s.LastSeen),
            _ => Sessions
        };

        var list = ordered.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var from = Sessions.IndexOf(list[i]);
            if (from != i) Sessions.Move(from, i);
        }
    }

    private void RaiseHeadersChanged()
    {
        this.RaisePropertyChanged(nameof(ListenerHeader));
        this.RaisePropertyChanged(nameof(IpHeader));
        this.RaisePropertyChanged(nameof(UserHeader));
        this.RaisePropertyChanged(nameof(ComputerHeader));
        this.RaisePropertyChanged(nameof(PidHeader));
        this.RaisePropertyChanged(nameof(ProcessHeader));
        this.RaisePropertyChanged(nameof(ArchHeader));
        this.RaisePropertyChanged(nameof(OsHeader));
        this.RaisePropertyChanged(nameof(LastHeader));
    }

    public BeaconInteractionViewModel CreateInteraction(BeaconSessionViewModel session) =>
        _interactionFactory(session);

    public ScriptEditorViewModel CreateScriptEditorViewModel(BeaconSessionViewModel session) =>
        _scriptEditorFactory(session);

    public void StartStreaming()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = StreamSessionsAsync(_cts.Token);
    }

    private async Task StreamSessionsAsync(CancellationToken ct)
    {
        try
        {
            using var call = _service.StreamSessions(ct);
            while (await call.ResponseStream.MoveNext(ct))
            {
                var session = call.ResponseStream.Current;
                Dispatcher.UIThread.Post(() => UpsertSession(session));
            }
        }
        catch (OperationCanceledException) { }
    }

    private void UpsertSession(BeaconSession session)
    {
        var existing = Sessions.FirstOrDefault(s => s.BeaconId == session.BeaconId);

        if (existing is not null)
        {
            existing.Update(session);
        }
        else
        {
            Sessions.Add(new BeaconSessionViewModel(session, DeleteSessionAsync));
        }

        ApplySort();
    }

    private async Task DeleteSessionAsync(BeaconSessionViewModel vm)
    {
        await _service.DeleteSessionAsync(vm.BeaconId);
        Dispatcher.UIThread.Post(() => Sessions.Remove(vm));
    }

    public void Dispose()
    {
        _ticker.Stop();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        Sessions.Clear();
    }
}