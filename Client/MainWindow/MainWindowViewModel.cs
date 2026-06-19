using System;
using System.Reactive;
using System.Threading.Tasks;
using Client.Listeners;
using Client.Payloads;
using Client.Scripts;
using Client.Services;
using Client.Sessions;
using ReactiveUI;

namespace Client.MainWindow;

internal sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ServerConnection _connection;

    private bool _isConnected;

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isConnected, value);
            this.RaisePropertyChanged(nameof(IsNotConnected));
            this.RaisePropertyChanged(nameof(ConnectedTo));
        }
    }

    public bool IsNotConnected => !_isConnected;

    public bool IsConnecting
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string ServerAddress
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "http://localhost:5184";

    public string Username
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "neo";

    public string Password
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "changeme";

    public string? ConnectionError
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string ConnectedTo => _connection.ServerAddress;

    public ListenersPanelViewModel ListenersPanel { get; }
    public SessionsPanelViewModel SessionsPanel { get; }
    public PayloadsPanelViewModel PayloadsPanel { get; }
    public ScriptsPanelViewModel ScriptsPanel { get; }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }

    public MainWindowViewModel(
        ServerConnection connection,
        ListenersPanelViewModel listenersPanel,
        SessionsPanelViewModel sessionsPanel,
        PayloadsPanelViewModel payloadsPanel,
        ScriptsPanelViewModel scriptsPanel)
    {
        _connection = connection;
        ListenersPanel = listenersPanel;
        SessionsPanel = sessionsPanel;
        PayloadsPanel = payloadsPanel;
        ScriptsPanel = scriptsPanel;

        var canConnect = this.WhenAnyValue(x => x.IsConnecting, c => !c);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync, canConnect);
        DisconnectCommand = ReactiveCommand.Create(Disconnect);
    }

    private async Task ConnectAsync()
    {
        IsConnecting = true;
        ConnectionError = null;

        try
        {
            await _connection.ConnectAsync(ServerAddress, Username, Password);
            IsConnected = true;
            ListenersPanel.StartStreaming();
            SessionsPanel.StartStreaming();
            PayloadsPanel.StartStreaming();
        }
        catch (Exception ex)
        {
            ConnectionError = ex.Message;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private void Disconnect()
    {
        ListenersPanel.Dispose();
        SessionsPanel.Dispose();
        PayloadsPanel.Dispose();
        _connection.Disconnect();
        IsConnected = false;
    }

    public void Dispose()
    {
        ListenersPanel.Dispose();
        SessionsPanel.Dispose();
        PayloadsPanel.Dispose();
        _connection.Dispose();
    }
}