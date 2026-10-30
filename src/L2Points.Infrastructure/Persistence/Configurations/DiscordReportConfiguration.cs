using L2Points.Infrastructure.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace L2Points.Infrastructure.Persistence.Configurations;

public class DiscordReportConfiguration : IEntityTypeConfiguration<DiscordReport>
{
    public void Configure(EntityTypeBuilder<DiscordReport> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.DiscordUserId).HasMaxLength(DiscordReport.DiscordIdMaxLength).IsRequired();
        builder.Property(r => r.DiscordUserName).HasMaxLength(DiscordReport.DiscordUserNameMaxLength).IsRequired();
        builder.Property(r => r.GuildId).HasMaxLength(DiscordReport.DiscordIdMaxLength).IsRequired();
        builder.Property(r => r.ChannelId).HasMaxLength(DiscordReport.DiscordIdMaxLength).IsRequired();
        builder.Property(r => r.Status).HasConversion<int>();
        builder.Property(r => r.StateJson).IsRequired();

        builder.HasIndex(r => r.CreatedAtUtc);
    }
}
