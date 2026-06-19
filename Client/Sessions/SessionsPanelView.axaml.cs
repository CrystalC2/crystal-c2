using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Client.Scripts.ScriptEditor;

namespace Client.Sessions;

public partial class SessionsPanelView : UserControl
{
    private readonly Dictionary<uint, BeaconInteractionViewModel> _viewModels        = new();
    private readonly Dictionary<uint, TabItem>                    _tabs              = new();
    private readonly Dictionary<uint, ScriptEditorView>           _scriptEditorWindows    = new();
    private readonly Dictionary<uint, ScriptEditorViewModel>      _scriptEditorViewModels = new();

    public SessionsPanelView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        GraphView.SessionActivated += (_, session) => OpenInteraction(session);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is SessionsPanelViewModel vm)
            vm.Sessions.CollectionChanged += OnSessionsChanged;
    }

    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Remove:
            {
                if (e.OldItems is null) break;
                foreach (BeaconSessionViewModel session in e.OldItems)
                {
                    CloseAndDispose(session.BeaconId);
                    CloseAndDisposeScriptEditor(session.BeaconId);
                }
                break;
            }

            case NotifyCollectionChangedAction.Reset:
            {
                foreach (var id in _tabs.Keys.ToList())
                    CloseAndDispose(id);
                foreach (var id in _scriptEditorViewModels.Keys.ToList())
                    CloseAndDisposeScriptEditor(id);
                break;
            }
        }
    }

    private void CloseAndDispose(uint beaconId)
    {
        if (_tabs.Remove(beaconId, out var tab))
            InteractionTabs.Items.Remove(tab);

        if (_viewModels.Remove(beaconId, out var vm))
            _ = vm.DisposeAsync();

        UpdateNoTabsHint();
    }

    private void CloseAndDisposeScriptEditor(uint beaconId)
    {
        if (_scriptEditorWindows.Remove(beaconId, out var window))
            window.Close();

        if (_scriptEditorViewModels.Remove(beaconId, out var vm))
            _ = vm.DisposeAsync();
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { DataContext: BeaconSessionViewModel session })
            OpenInteraction(session);
    }

    private void OnInteractMenuItemClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: BeaconSessionViewModel session })
            OpenInteraction(session);
    }

    private void OpenInteraction(BeaconSessionViewModel session)
    {
        if (_tabs.TryGetValue(session.BeaconId, out var existing))
        {
            InteractionTabs.SelectedItem = existing;
            return;
        }

        var panelVm = (SessionsPanelViewModel)DataContext!;

        if (!_viewModels.TryGetValue(session.BeaconId, out var vm))
        {
            vm = panelVm.CreateInteraction(session);
            vm.OpenScriptEditor = sess => _ = OpenScriptEditorAsync(sess);
            _viewModels[session.BeaconId] = vm;
        }

        var content = new BeaconInteractionView { DataContext = vm };
        var header  = BuildTabHeader(session, () => CloseAndDispose(session.BeaconId));
        var tab     = new TabItem { Header = header, Content = content };

        _tabs[session.BeaconId] = tab;
        InteractionTabs.Items.Add(tab);
        InteractionTabs.SelectedItem = tab;

        UpdateNoTabsHint();
    }

    private static Control BuildTabHeader(BeaconSessionViewModel session, Action onClose)
    {
        var title = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#C9D1D9")),
            VerticalAlignment = VerticalAlignment.Center,
            TextDecorations = null,
            Text = session.Computer
        };

        var close = new Button
        {
            Content           = "x",
            Classes           = { "tab-close" },
            VerticalAlignment = VerticalAlignment.Center,
        };
        close.Click += (_, _) => onClose();

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing     = 5,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(title);
        panel.Children.Add(close);

        ToolTip.SetTip(panel, $"{session.UserDisplay}@{session.Computer}  ({session.Process}/{session.Pid})");
        return panel;
    }

    private void UpdateNoTabsHint() =>
        NoTabsHint.IsVisible = _tabs.Count == 0;

    // ── Script editor (still a floating window) ───────────────────────────────

    private Task OpenScriptEditorAsync(BeaconSessionViewModel session)
    {
        if (_scriptEditorWindows.TryGetValue(session.BeaconId, out var existing))
        {
            existing.Activate();
            return Task.CompletedTask;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner) return Task.CompletedTask;

        var panelVm = (SessionsPanelViewModel)DataContext!;
        var vm = panelVm.CreateScriptEditorViewModel(session);
        _scriptEditorViewModels[session.BeaconId] = vm;

        var window = new ScriptEditorView { DataContext = vm };
        _scriptEditorWindows[session.BeaconId] = window;

        window.Closed += (_, _) =>
        {
            _scriptEditorWindows.Remove(session.BeaconId);
            if (_scriptEditorViewModels.Remove(session.BeaconId, out var disposed))
                _ = disposed.DisposeAsync();
        };

        window.Show(owner);
        return Task.CompletedTask;
    }
}
