using System.Collections.ObjectModel;
using Client.Listeners;
using ReactiveUI;

namespace Client.Payloads;

internal sealed class GeneratePayloadViewModel(ObservableCollection<ListenerItemViewModel> listeners) : ViewModelBase
{
    public ObservableCollection<ListenerItemViewModel> Listeners { get; } = listeners;
    public string[] ArchOptions { get; } = ["x86", "x64"];
    public string[] ExitFuncOptions { get; } = ["ExitProcess", "ExitThread"];

    public ListenerItemViewModel? SelectedListener
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    }

    public string Arch
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "x64";

    public string ExitFunc
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "ExitProcess";

    public string Sleep
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    } = "60";

    public string Jitter
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    } = "20";

    public string? SpecPath
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool GenerateYara
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool AddToStore
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    }

    public string PayloadName
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    } = string.Empty;

    public string? PayloadNote
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsValid =>
        SelectedListener is not null &&
        !string.IsNullOrWhiteSpace(Sleep) &&
        !string.IsNullOrWhiteSpace(Jitter) &&
        (!AddToStore || !string.IsNullOrWhiteSpace(PayloadName));
}