using ReactiveUI;

namespace Client.Payloads;

internal sealed class AddPayloadViewModel : ViewModelBase
{
    public string Name
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    } = string.Empty;

    public string? FilePath
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    }
    
    public string? YaraPath
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string? Note
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        FilePath is not null;
}