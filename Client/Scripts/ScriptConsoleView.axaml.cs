using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Client.Scripts;

public partial class ScriptConsoleView : Window
{
    public ScriptConsoleView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ScriptConsoleViewModel vm)
        {
            vm.Log.CollectionChanged += (_, _) =>
                Dispatcher.UIThread.Post(() => LogScroll.ScrollToEnd(), DispatcherPriority.Background);
        }
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e) => (DataContext as ScriptConsoleViewModel)?.Clear();
}