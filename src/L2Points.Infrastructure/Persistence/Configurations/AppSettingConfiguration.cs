using L2Points.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace L2Points.Infrastructure.Persistence.Configurations;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(64);
        builder.Property(s => s.Value).HasMaxLength(256).IsRequired();
    }
}
