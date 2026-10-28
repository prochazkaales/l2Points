using L2Points.Domain.Jewels;
using L2Points.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace L2Points.Infrastructure.Persistence.Configurations;

public class UserJewelConfiguration : IEntityTypeConfiguration<UserJewel>
{
    public void Configure(EntityTypeBuilder<UserJewel> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.UserId).HasMaxLength(450).IsRequired();

        builder.HasIndex(j => j.UserId);

        // The Domain does not know AppUser, so the relationship is declared from this side only.
        builder.HasOne<AppUser>().WithMany().HasForeignKey(j => j.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<JewelType>().WithMany().HasForeignKey(j => j.JewelTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
