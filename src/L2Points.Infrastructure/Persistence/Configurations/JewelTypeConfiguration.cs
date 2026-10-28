using L2Points.Domain.Jewels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace L2Points.Infrastructure.Persistence.Configurations;

public class JewelTypeConfiguration : IEntityTypeConfiguration<JewelType>
{
    public void Configure(EntityTypeBuilder<JewelType> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Code).HasMaxLength(JewelType.CodeMaxLength).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(JewelType.NameMaxLength).IsRequired();
        builder.Property(t => t.ImagePath).HasMaxLength(JewelType.ImagePathMaxLength).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();
    }
}
