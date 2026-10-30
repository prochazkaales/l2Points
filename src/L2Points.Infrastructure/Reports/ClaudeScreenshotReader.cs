using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

namespace L2Points.Infrastructure.Reports;

/// <summary>Reads the "Party Members" panel of a Lineage II Command Channel Info window with a Claude vision model.</summary>
public sealed class ClaudeScreenshotReader(IOptions<AnthropicOptions> options) : IScreenshotReader
{
    private const string PromptTemplate =
        """
        The image is a Lineage II screenshot. Find the "Command Channel Info" window and, in it, the "Party Members" panel (a table with the columns "Party Members" and "Class"). Return the character names from the "Party Members" column exactly as written, character by character, top to bottom. Ignore the "Leader" list on the left, the "Owner" field, character names in the game world and any other text. The list of known players below only helps to tell ambiguous characters apart (0/O, 1/l/I); never return a name that is not in the panel and never replace what you read with a name from the list. If there is no "Party Members" panel, return panelFound false and an empty list.

        Known players: {0}
        """;

    // Created on first use so that building the container never needs the API key.
    private readonly Lazy<AnthropicClient> _client = new(() => new AnthropicClient { ApiKey = options.Value.ApiKey });

    public async Task<ScreenshotReadResult> ReadPartyMembersAsync(
        byte[] image, string mediaType, IReadOnlyList<string> knownNicks, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new ScreenshotReadException("The Anthropic API key is not configured.");
        }

        var parameters = new MessageCreateParams
        {
            Model = settings.Model,
            MaxTokens = 4096,
            OutputConfig = new OutputConfig
            {
                Effort = Effort.Low,
                Format = new JsonOutputFormat { Schema = BuildSchema() },
            },
            Messages =
            [
                new UserMessageParam
                {
                    Content = new List<ContentBlockParam>
                    {
                        new ImageBlockParam
                        {
                            Source = new Base64ImageSource
                            {
                                Data = Convert.ToBase64String(image),
                                MediaType = ToMediaType(mediaType),
                            },
                        },
                        new TextBlockParam { Text = string.Format(PromptTemplate, string.Join(", ", knownNicks)) },
                    },
                },
            ],
        };

        Message response;
        try
        {
            response = await _client.Value.Messages.Create(parameters, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Only the exception type is carried over, the message could contain request details.
            throw new ScreenshotReadException($"The screenshot reading service failed ({ex.GetType().Name}).");
        }

        if (response.StopReason != StopReason.EndTurn)
        {
            throw new ScreenshotReadException($"The model did not finish reading the screenshot (stop reason: {response.StopReason}).");
        }

        var text = response.Content
            .Select(block => block.TryPickText(out var textBlock) ? textBlock.Text : null)
            .FirstOrDefault(t => t is not null);
        if (text is null)
        {
            throw new ScreenshotReadException("The model returned no text.");
        }

        return ParseResponse(text);
    }

    internal static ScreenshotReadResult ParseResponse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("panelFound", out var panelFound)
                || panelFound.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !root.TryGetProperty("nicks", out var nicks)
                || nicks.ValueKind != JsonValueKind.Array)
            {
                throw new ScreenshotReadException("The model response has an unexpected shape.");
            }

            var result = new List<string>();
            foreach (var item in nicks.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new ScreenshotReadException("The model response has an unexpected shape.");
                }

                var nick = item.GetString()!.Trim();
                if (nick.Length > 0)
                {
                    result.Add(nick);
                }
            }

            return new ScreenshotReadResult(panelFound.GetBoolean(), result);
        }
        catch (JsonException)
        {
            throw new ScreenshotReadException("The model response is not valid JSON.");
        }
    }

    private static Dictionary<string, JsonElement> BuildSchema() => new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["panelFound"] = new { type = "boolean" },
            ["nicks"] = new { type = "array", items = new { type = "string" } },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[] { "panelFound", "nicks" }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    private static MediaType ToMediaType(string mediaType) => mediaType.ToLowerInvariant() switch
    {
        "image/png" => MediaType.ImagePng,
        "image/jpeg" or "image/jpg" => MediaType.ImageJpeg,
        "image/gif" => MediaType.ImageGif,
        "image/webp" => MediaType.ImageWebP,
        _ => throw new ScreenshotReadException($"Unsupported image type '{mediaType}'."),
    };
}
