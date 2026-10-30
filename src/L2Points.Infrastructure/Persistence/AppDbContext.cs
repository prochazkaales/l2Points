using L2Points.Domain.Activities;
using L2Points.Domain.Attendances;
using L2Points.Domain.Jewels;
using L2Points.Domain.Settings;
using L2Points.Infrastructure.Identity;
using L2Points.Infrastructure.Reports;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace L2Points.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Activity> Activities => Set<Activity>();

    public DbSet<ActivitySpawn> ActivitySpawns => Set<ActivitySpawn>();

    public DbSet<ActivitySlotPoints> ActivitySlotPoints => Set<ActivitySlotPoints>();

    public DbSet<Attendance> Attendances => Set<Attendance>();

    public DbSet<JewelType> JewelTypes => Set<JewelType>();

    public DbSet<UserJewel> UserJewels => Set<UserJewel>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<DiscordReport> DiscordReports => Set<DiscordReport>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(user =>
        {
            user.Property(u => u.Nick).HasMaxLength(AppUser.NickMaxLength).IsRequired();
            user.HasIndex(u => u.Nick).IsUnique();
        });

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
