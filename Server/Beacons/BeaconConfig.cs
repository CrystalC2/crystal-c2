using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Server.Beacons;

public sealed class BeaconConfig : IEntityTypeConfiguration<Beacon>
{
    public void Configure(EntityTypeBuilder<Beacon> builder)
    {
        builder.HasOne(g => g.Parent)
            .WithMany(g => g.Children)
            .HasForeignKey(g => g.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}