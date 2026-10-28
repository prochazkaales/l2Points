using L2Points.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace L2Points.Infrastructure.Persistence.Configurations;

public class ActivitySpawnConfiguration : IEntityTypeConfiguration<ActivitySpawn>
{
    public void Configure(EntityTypeBuilder<ActivitySpawn> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.DayOfWeek).HasConversion<int>();
        builder.Property(s => s.Slot).HasConversion<int>();
        builder.HasIndex(s => new { s.ActivityId, s.DayOfWeek, s.Slot }).IsUnique();
    }
}
