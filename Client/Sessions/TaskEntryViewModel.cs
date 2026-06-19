using System;
using Avalonia.Media;
using CrystalC2.Tasks;
using ReactiveUI;

namespace Client.Sessions;

internal sealed class TaskEntryViewModel(string command) : ReactiveObject
{
    public string Command { get; } = command;

    public uint? TaskId { get; set; }

    internal Action<TaskResponse>? Observer { get; set; }

    public TaskStatus Status
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(StatusDisplay));
            this.RaisePropertyChanged(nameof(StatusBrush));
        }
    } = TaskStatus.Pending;

    public string? Output
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? HighlightTerm
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string StatusDisplay => Status switch
    {
        TaskStatus.Pending => "pending",
        TaskStatus.Tasked => "tasked",
        TaskStatus.Complete => "complete",
        TaskStatus.Error => "error",
        _ => "unknown"
    };

    public IBrush StatusBrush => Status switch
    {
        TaskStatus.Pending => new SolidColorBrush(Color.Parse("#EAB308")),
        TaskStatus.Tasked => new SolidColorBrush(Color.Parse("#3B82F6")),
        TaskStatus.Complete => new SolidColorBrush(Color.Parse("#22C55E")),
        TaskStatus.Error => new SolidColorBrush(Color.Parse("#EF4444")),
        _ => new SolidColorBrush(Color.Parse("#6B7280"))
    };
}