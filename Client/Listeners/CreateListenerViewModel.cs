using ReactiveUI;

namespace Client.Listeners;

internal sealed class CreateListenerViewModel : ReactiveObject
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

    public string Port
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    } = "1337";

    public string? X86Path
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    }

    public string? X64Path
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsValid));
        }
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Name) &&
        uint.TryParse(Port, out var port) &&
        port is > 0 and <= 65535 &&
        X86Path is not null &&
        X64Path is not null;
}