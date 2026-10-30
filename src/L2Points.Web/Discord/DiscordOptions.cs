using L2Points.Infrastructure.Reports;

namespace L2Points.Web.Discord;

/// <summary>Settings of the Discord application (env <c>Discord__ApplicationId</c>, <c>Discord__PublicKey</c>, <c>Discord__GuildId</c>, <c>Discord__BotToken</c>).</summary>
public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public string? ApplicationId { get; set; }

    /// <summary>Ed25519 public key of the application as hex; verifies interaction signatures.</summary>
    public string? PublicKey { get; set; }

    public string? GuildId { get; set; }

    public string? BotToken { get; set; }

    /// <summary>The report bot runs only when Discord and the Anthropic API key are fully configured.</summary>
    public static bool IsEnabled(DiscordOptions discord, AnthropicOptions anthropic) =>
        !string.IsNullOrWhiteSpace(discord.ApplicationId)
        && !string.IsNullOrWhiteSpace(discord.PublicKey)
        && !string.IsNullOrWhiteSpace(discord.GuildId)
        && !string.IsNullOrWhiteSpace(discord.BotToken)
        && !string.IsNullOrWhiteSpace(anthropic.ApiKey);
}
