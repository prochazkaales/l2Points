namespace L2Points.Infrastructure.Reports;

/// <summary>Settings of the Anthropic API used to read report screenshots (env <c>Anthropic__ApiKey</c>, <c>Anthropic__Model</c>).</summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-haiku-5-5";
}
