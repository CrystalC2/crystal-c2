using System.ComponentModel.DataAnnotations;
using CrystalC2.Beacons;
using Server.Core;

namespace Server.Beacons;

public sealed class Beacon : Entity
{
    public uint? ParentId { get; set; }
    public Beacon? Parent { get; set; }
    public ICollection<Beacon> Children { get; set; } = [];
    
    [MaxLength(25)]
    public string Listener { get; init; } = "";

    public byte[] SessionKey { get; init; } = [];

    [MaxLength(50)]
    public string User { get; init; } = "";
    
    [MaxLength(50)]
    public string? Impersonated { get; private set; }

    [MaxLength(50)]
    public string Computer { get; init; } = "";

    [MaxLength(25)]
    public string Process { get; init; } = "";
    
    public int ProcessId { get; init; }
    public uint MajorVersion { get; init; }
    public uint MinorVersion { get; init; }
    public uint BuildVersion { get; init; }
    public int CharSet { get; init; }
    public byte[] InternalAddress { get; init; } = [];
    public BeaconFlags Flags { get; init; }
    public DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; private set; }
    public BeaconHealth Health { get; private set; }

    public static Beacon Create(BeaconMetadata metadata, string listener)
    {
        var now = DateTimeOffset.UtcNow;

        return new Beacon
        {
            Id = metadata.Id,
            Listener = listener,
            SessionKey = metadata.SessionKey,
            User = metadata.User,
            Computer = metadata.Computer,
            Process = metadata.Process,
            ProcessId = metadata.ProcessId,
            MajorVersion = metadata.MajorVersion,
            MinorVersion = metadata.MinorVersion,
            BuildVersion = metadata.BuildVersion,
            CharSet = metadata.CharSet,
            InternalAddress = metadata.InternalAddress,
            Flags = metadata.Flags,
            FirstSeen = now,
            LastSeen = now,
            Health = BeaconHealth.Alive
        };
    }

    public void CheckIn()
    {
        LastSeen = DateTimeOffset.UtcNow;
        Health = BeaconHealth.Alive;
    }

    public void SetHealth(BeaconHealth health)
    {
        Health = health;
    }

    public void SetImpersonation(string? user)
    {
        Impersonated = user;
    }
}