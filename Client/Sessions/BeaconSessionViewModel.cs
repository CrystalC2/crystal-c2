using System;
using System.Threading.Tasks;
using Avalonia.Media;
using CrystalC2.Beacons;
using ReactiveUI;

namespace Client.Sessions;

internal sealed class BeaconSessionViewModel : ReactiveObject
{
    private BeaconHealth _health;
    private DateTimeOffset _lastSeen;
    private readonly Func<BeaconSessionViewModel, Task> _onDelete;

    public uint BeaconId { get; }
    public uint? ParentId { get; }
    public string Listener { get; }
    public string User { get; }
    public string? Impersonated { get; }
    public string Computer { get; }
    public string Process { get; }
    public int Pid { get; }
    public uint MajorVersion { get; }
    public uint MinorVersion { get; }
    public uint BuildVersion { get; }
    public int Charset { get; }
    public string InternalIp { get; }
    public bool IsAdmin { get; }
    public BeaconArch Arch { get; }
    public DateTimeOffset FirstSeen { get; }

    public BeaconHealth Health
    {
        get => _health;
        set
        {
            this.RaiseAndSetIfChanged(ref _health, value);
            this.RaisePropertyChanged(nameof(HealthBrush));
        }
    }

    public DateTimeOffset LastSeen
    {
        get => _lastSeen;
        set => this.RaiseAndSetIfChanged(ref _lastSeen, value);
    }

    public IBrush HealthBrush => Health switch
    {
        BeaconHealth.Alive => new SolidColorBrush(Color.Parse("#22C55E")),
        BeaconHealth.Lost => new SolidColorBrush(Color.Parse("#EAB308")),
        BeaconHealth.Dead => new SolidColorBrush(Color.Parse("#EF4444")),
        _ => new SolidColorBrush(Color.Parse("#6B7280"))
    };

    public string ArchDisplay => Arch switch
    {
        BeaconArch.X64 => "x64",
        BeaconArch.X86 => "x86",
        _ => "?"
    };

    public string UserDisplay
    {
        get
        {
            var user = IsAdmin ? $"* {User}" : User;
            return Impersonated is not null ? $"{user} [{Impersonated}]" : user;
        }
    }

    public string OsDisplay => $"{MajorVersion}.{MinorVersion}.{BuildVersion}";

    public string LastSeenDisplay => (DateTimeOffset.UtcNow - LastSeen) switch
    {
        { TotalSeconds: < 60 } ts => $"{(int)ts.TotalSeconds}s ago",
        { TotalMinutes: < 60 } ts => $"{(int)ts.TotalMinutes}m ago",
        { TotalHours: < 24 } ts => $"{(int)ts.TotalHours}h ago",
        var ts => $"{(int)ts.TotalDays}d ago"
    };

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> DeleteCommand { get; }

    public BeaconSessionViewModel(BeaconSession session, Func<BeaconSessionViewModel, Task> onDelete)
    {
        _onDelete = onDelete;
        BeaconId = session.BeaconId;
        ParentId = session.HasParentId ? session.ParentId : null;
        Listener = session.Listener;
        User = session.User;
        Impersonated = session.HasImpersonated ? session.Impersonated : null;
        Computer = session.Computer;
        Process = session.Process;
        Pid = session.Pid;
        MajorVersion = session.MajorVersion;
        MinorVersion = session.MinorVersion;
        BuildVersion = session.BuildVersion;
        Charset      = session.Charset;
        InternalIp   = session.InternalIp;
        IsAdmin = session.IsAdmin;
        Arch = session.Arch;
        FirstSeen = session.FirstSeen.ToDateTimeOffset();
        _lastSeen = session.LastSeen.ToDateTimeOffset();
        _health = session.Health;

        DeleteCommand = ReactiveCommand.CreateFromTask(() => _onDelete(this));
    }

    public void Update(BeaconSession session)
    {
        Health = session.Health;
        LastSeen = session.LastSeen.ToDateTimeOffset();
    }

    public void RefreshLastSeen() => this.RaisePropertyChanged(nameof(LastSeenDisplay));
}