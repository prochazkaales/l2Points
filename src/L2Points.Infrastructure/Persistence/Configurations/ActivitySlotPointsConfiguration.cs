using L2Points.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace L2Points.Infrastructure.Persistence.Configurations;

public class ActivitySlotPointsConfiguration : IEntityTypeConfiguration<ActivitySlotPoints>
{
    public void Configure(EntityTypeBuilder<ActivitySlotPoints> builder)
    {
        builder.HasKey(s => new { s.ActivityId, s.Slot });
        builder.Property(s => s.Slot).HasConversion<int>();
    }
}
