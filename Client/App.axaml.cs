using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Client.Listeners;
using Client.MainWindow;
using Client.Payloads;
using Client.Scripts;
using Client.Services;
using Client.Sessions;
using Client.Scripts.ScriptEditor;
using Microsoft.Extensions.DependencyInjection;

namespace Client;

public partial class App : Application
{
    public override void Initialize()
    {
        // Allow HTTP/2 without TLS for local server connections
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = BuildServices();

            LinkerServer.Start();
            desktop.Exit += (_, _) => LinkerServer.Stop();

            desktop.MainWindow = new MainWindowView
            {
                DataContext = services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<ServerConnection>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<LinkerApiClient>();

        services.AddSingleton<ListenerGrpcService>();
        services.AddSingleton<BeaconGrpcService>();
        services.AddSingleton<PayloadGrpcService>();
        services.AddTransient<TaskGrpcService>();

        services.AddSingleton<ScriptEngine>();

        services.AddSingleton<ListenersPanelViewModel>();
        services.AddSingleton<SessionsPanelViewModel>();
        services.AddSingleton<PayloadsPanelViewModel>();
        services.AddSingleton<ScriptsPanelViewModel>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddSingleton<Func<BeaconSessionViewModel, BeaconInteractionViewModel>>(sp =>
            session => new BeaconInteractionViewModel(
                session,
                sp.GetRequiredService<TaskGrpcService>(),
                sp.GetRequiredService<ScriptEngine>()));

        services.AddSingleton<Func<BeaconSessionViewModel, ScriptEditorViewModel>>(sp =>
            session => new ScriptEditorViewModel(
                session,
                sp.GetRequiredService<TaskGrpcService>(),
                sp.GetRequiredService<PayloadsPanelViewModel>().Payloads));

        var provider = services.BuildServiceProvider();

        var engine = provider.GetRequiredService<ScriptEngine>();
        engine.SetSessions(provider.GetRequiredService<SessionsPanelViewModel>());
        engine.SetPayloads(provider.GetRequiredService<PayloadsPanelViewModel>());

        return provider;
    }
}