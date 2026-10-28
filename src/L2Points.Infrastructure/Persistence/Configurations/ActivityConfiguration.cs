using L2Points.Domain.Activities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace L2Points.Infrastructure.Persistence.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(a => a.Code).IsUnique();
        builder.Property(a => a.Name).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Category).HasConversion<int>();
        builder.Property(a => a.ScheduleType).HasConversion<int>();
        builder.Property(a => a.WeeklyDay).HasConversion<int?>();

        builder.HasMany(a => a.Spawns).WithOne().HasForeignKey(s => s.ActivityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(a => a.SlotPoints).WithOne().HasForeignKey(s => s.ActivityId).OnDelete(DeleteBehavior.Cascade);
    }
}
