using Microsoft.EntityFrameworkCore;
using Server.Beacons;
using Server.Listeners;
using Server.Payloads;
using Server.Tasks;

namespace Server.Core;

internal sealed class CrystalDb(DbContextOptions<CrystalDb> options) : DbContext(options)
{
    public DbSet<Listener> Listeners => Set<Listener>();
    public DbSet<Payload> Payloads => Set<Payload>();
    public DbSet<Beacon> Beacons => Set<Beacon>();
    public DbSet<BeaconTask> Tasks => Set<BeaconTask>();
}