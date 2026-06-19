using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Client.Sessions;

public partial class BeaconInteractionView : UserControl
{
    public BeaconInteractionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => InputBox.Focus();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is BeaconInteractionViewModel vm)
        {
            vm.FilteredEntries.CollectionChanged += (_, _) => ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        Dispatcher.UIThread.Post(() => OutputScroll.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not BeaconInteractionViewModel vm) return;

        switch (e.Key)
        {
            case Key.Up:
            {
                vm.HistoryUp();
                InputBox.CaretIndex = InputBox.Text?.Length ?? 0;
                e.Handled = true;
                return;
            }

            case Key.Down:
            {
                vm.HistoryDown();
                InputBox.CaretIndex = InputBox.Text?.Length ?? 0;
                e.Handled = true;
                return;
            }

            case Key.Tab:
            {
                vm.TryComplete();
                InputBox.CaretIndex = InputBox.Text?.Length ?? 0;
                e.Handled = true;
                return;
            }

            case Key.Enter:
            {
                vm.SubmitCommand.Execute(System.Reactive.Unit.Default).Subscribe();
                break;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e is { Key: Key.L, KeyModifiers: KeyModifiers.Control }
            && DataContext is BeaconInteractionViewModel vm)
        {
            vm.ClearEntries();
            e.Handled = true;
        }
    }

    private void OnScriptEditorClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BeaconInteractionViewModel vm)
            vm.OpenScriptEditor?.Invoke(vm.Session);
    }

    private void OnRemoveSessionClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BeaconInteractionViewModel vm)
            vm.Session.DeleteCommand.Execute(System.Reactive.Unit.Default).Subscribe();
    }
}
