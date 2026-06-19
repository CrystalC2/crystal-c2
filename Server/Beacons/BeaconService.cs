using System.Net;
using CrystalC2.Beacons;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Server.Core;

namespace Server.Beacons;

[Authorize]
public sealed class BeaconService(
    IRepository<Beacon> beaconDb,
    IBroker<Beacon> beaconBroker)
    : BeaconProtoService.BeaconProtoServiceBase
{
    public override async Task StreamSessions(Empty request, IServerStreamWriter<BeaconSession> responseStream,
        ServerCallContext context)
    {
        // read the first bunch from the database
        var beacons = await beaconDb.ListAsync(context.CancellationToken);

        foreach (var beacon in beacons)
            await responseStream.WriteAsync(ToSession(beacon), context.CancellationToken);

        // set up a pub/sub to get checkin notifications
        var (reader, subscription) = beaconBroker.Subscribe();

        using (subscription)
            await foreach (var beacon in reader.ReadAllAsync(context.CancellationToken))
                await responseStream.WriteAsync(ToSession(beacon), context.CancellationToken);
    }

    public override async Task<Empty> DeleteSession(DeleteSessionRequest request, ServerCallContext context)
    {
        var beacon = await beaconDb.GetByIdAsync(request.BeaconId, context.CancellationToken);

        if (beacon is not null)
        {
            await beaconDb.DeleteAsync(beacon, context.CancellationToken);
            await beaconDb.SaveChangesAsync(context.CancellationToken);
        }

        return new Empty();
    }

    private static BeaconSession ToSession(Beacon beacon)
    {
        var session = new BeaconSession
        {
            BeaconId = beacon.Id,
            Listener = beacon.Listener,
            User = beacon.User,
            Computer = beacon.Computer,
            Process = beacon.Process,
            Pid = beacon.ProcessId,
            MajorVersion = beacon.MajorVersion,
            MinorVersion = beacon.MinorVersion,
            BuildVersion = beacon.BuildVersion,
            Charset = beacon.CharSet,
            InternalIp = new IPAddress(beacon.InternalAddress).ToString(),
            Arch = beacon.Flags.HasFlag(BeaconFlags.X86) ? BeaconArch.X86 : BeaconArch.X64,
            IsAdmin = beacon.Flags.HasFlag(BeaconFlags.Admin),
            FirstSeen = Timestamp.FromDateTimeOffset(beacon.FirstSeen),
            LastSeen = Timestamp.FromDateTimeOffset(beacon.LastSeen),
            Health = beacon.Health
        };

        if (beacon.ParentId.HasValue)
            session.ParentId = beacon.ParentId.Value;

        if (!string.IsNullOrEmpty(beacon.Impersonated))
            session.Impersonated = beacon.Impersonated;

        return session;
    }
}