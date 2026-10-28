using L2Points.Domain.Activities;
using L2Points.Domain.Attendances;
using L2Points.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace L2Points.Infrastructure.Persistence.Configurations;

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.UserId).HasMaxLength(450).IsRequired();
        builder.Property(a => a.Slot).HasConversion<int>();
        // Intentionally no FK: the audit reference must survive deletion of the creating user.
        builder.Property(a => a.CreatedByUserId).HasMaxLength(450).IsRequired();

        builder.HasIndex(a => new { a.UserId, a.ActivityId, a.Date, a.Slot }).IsUnique();
        builder.HasIndex(a => a.Date);

        // The Domain does not know AppUser, so the relationship is declared from this side only.
        builder.HasOne<AppUser>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Activity>().WithMany().HasForeignKey(a => a.ActivityId).OnDelete(DeleteBehavior.Restrict);
    }
}
