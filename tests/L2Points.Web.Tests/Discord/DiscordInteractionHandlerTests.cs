using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using L2Points.Domain.Stats;
using L2Points.Infrastructure.Reports;
using L2Points.Web.Discord;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace L2Points.Web.Tests.Discord;

public class DiscordInteractionHandlerTests
{
    private const string GuildId = "guild-1";
    private const string UserId = "user-1";
    private const string Token = "interaction-token";
    private const string AttachmentUrl = "https://cdn.discordapp.com/attachments/1/2/party.png";

    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ReportId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Ed25519PrivateKeyParameters _key;
    private readonly string _publicKey;
    private readonly FakeReports _reports = new();
    private readonly DiscordJobQueue _queue = new();

    public DiscordInteractionHandlerTests()
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var pair = generator.GenerateKeyPair();
        _key = (Ed25519PrivateKeyParameters)pair.Private;
        _publicKey = Convert.ToHexString(((Ed25519PublicKeyParameters)pair.Public).GetEncoded());
    }

    private DiscordInteractionHandler Handler(bool enabled = true)
    {
        var discord = enabled
            ? new DiscordOptions { ApplicationId = "app-1", PublicKey = _publicKey, GuildId = GuildId, BotToken = "bot-token" }
            : new DiscordOptions();
        var anthropic = new AnthropicOptions { ApiKey = enabled ? "api-key" : null };
        return new DiscordInteractionHandler(
            Options.Create(discord), Options.Create(anthropic), _reports, _queue, new FakeTimeProvider(Now));
    }

    private Task<DiscordHandlerResult> SendAsync(JsonObject interaction, bool enabled = true)
    {
        var body = Encoding.UTF8.GetBytes(interaction.ToJsonString());
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        return Handler(enabled).HandleAsync(body, Sign(timestamp, body), timestamp, CancellationToken.None);
    }

    private string Sign(string timestamp, byte[] body)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, _key);
        var message = Encoding.UTF8.GetBytes(timestamp).Concat(body).ToArray();
        signer.BlockUpdate(message, 0, message.Length);
        return Convert.ToHexString(signer.GenerateSignature());
    }

    private static JsonObject Interaction(int type, JsonObject? data = null, string? guildId = GuildId) => new()
    {
        ["id"] = "interaction-1",
        ["type"] = type,
        ["token"] = Token,
        ["guild_id"] = guildId,
        ["channel_id"] = "channel-1",
        ["member"] = new JsonObject
        {
            ["user"] = new JsonObject { ["id"] = UserId, ["username"] = "alice", ["global_name"] = "Alice" },
        },
        ["data"] = data,
    };

    private static JsonObject ReportCommand(string contentType = "image/png", long size = 1000) =>
        Interaction(DiscordInteractionType.ApplicationCommand, new JsonObject
        {
            ["name"] = "report",
            ["options"] = new JsonArray(new JsonObject { ["name"] = "screen", ["type"] = 11, ["value"] = "att-1" }),
            ["resolved"] = new JsonObject
            {
                ["attachments"] = new JsonObject
                {
                    ["att-1"] = new JsonObject
                    {
                        ["id"] = "att-1",
                        ["url"] = AttachmentUrl,
                        ["content_type"] = contentType,
                        ["size"] = size,
                    },
                },
            },
        });

    private static JsonObject Component(string action, params string[] values) =>
        Interaction(DiscordInteractionType.MessageComponent, new JsonObject
        {
            ["custom_id"] = DiscordCustomId.Format(ReportId, action),
            ["component_type"] = 2,
            ["values"] = new JsonArray(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()),
        });

    private static JsonElement Json(DiscordHandlerResult result) =>
        JsonSerializer.SerializeToElement(result.Body, DiscordJson.Options);

    private static string? Content(DiscordHandlerResult result) =>
        Json(result).GetProperty("data").GetProperty("content").GetString();

    /// <summary>Everything currently in the queue; the queue never completes, so reading stops after a short wait.</summary>
    private async Task<List<DiscordJob>> DrainQueueAsync()
    {
        var jobs = new List<DiscordJob>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        try
        {
            await foreach (var job in _queue.ReadAllAsync(cts.Token))
                jobs.Add(job);
        }
        catch (OperationCanceledException)
        {
        }

        return jobs;
    }

    private static DiscordReportView View(
        DiscordReportOutcome outcome = DiscordReportOutcome.Ok,
        DiscordReportStatus status = DiscordReportStatus.Pending) =>
        new(ReportId, outcome, status, "Alice", new DiscordReportState { PanelFound = true }, [], []);

    [Fact]
    public async Task Disabled_returns_404()
    {
        var result = await SendAsync(Interaction(DiscordInteractionType.Ping), enabled: false);

        Assert.Equal(404, result.StatusCode);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task Bad_signature_returns_401()
    {
        var body = Encoding.UTF8.GetBytes(Interaction(DiscordInteractionType.Ping).ToJsonString());
        var timestamp = Now.ToUnixTimeSeconds().ToString();
        var signature = Sign(timestamp, Encoding.UTF8.GetBytes("{\"type\":2}"));

        var result = await Handler().HandleAsync(body, signature, timestamp, CancellationToken.None);

        Assert.Equal(401, result.StatusCode);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task Ping_returns_pong()
    {
        var result = await SendAsync(Interaction(DiscordInteractionType.Ping, guildId: null));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(1, Json(result).GetProperty("type").GetInt32());
    }

    [Theory]
    [InlineData("other-guild")]
    [InlineData(null)]
    public async Task Other_guild_gets_notice_and_no_report(string? guildId)
    {
        var interaction = ReportCommand();
        interaction["guild_id"] = guildId;

        var result = await SendAsync(interaction);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(DiscordResponseType.ChannelMessage, Json(result).GetProperty("type").GetInt32());
        Assert.Equal(DiscordMessages.Texts.WrongGuild, Content(result));
        Assert.Equal(64, Json(result).GetProperty("data").GetProperty("flags").GetInt32());
        Assert.Empty(_reports.Created);
        Assert.Empty(await DrainQueueAsync());
    }

    [Fact]
    public async Task Report_command_creates_report_queues_analysis_and_defers_ephemerally()
    {
        var result = await SendAsync(ReportCommand());

        Assert.Equal(200, result.StatusCode);
        var json = Json(result);
        Assert.Equal(DiscordResponseType.DeferredChannelMessage, json.GetProperty("type").GetInt32());
        Assert.Equal(64, json.GetProperty("data").GetProperty("flags").GetInt32());

        var reporter = Assert.Single(_reports.Created);
        Assert.Equal(new DiscordReporter(UserId, "Alice", GuildId, "channel-1"), reporter);

        var job = Assert.IsType<AnalyzeJob>(Assert.Single(await DrainQueueAsync()));
        Assert.Equal(new AnalyzeJob(_reports.NextId, Token, AttachmentUrl, "image/png"), job);
    }

    [Fact]
    public async Task Report_command_without_global_name_uses_username()
    {
        var interaction = ReportCommand();
        interaction["member"]!["user"]!["global_name"] = null;

        await SendAsync(interaction);

        Assert.Equal("alice", Assert.Single(_reports.Created).DiscordUserName);
    }

    [Fact]
    public async Task Report_with_pdf_gets_attachment_notice()
    {
        var result = await SendAsync(ReportCommand(contentType: "application/pdf"));

        Assert.Equal(DiscordResponseType.ChannelMessage, Json(result).GetProperty("type").GetInt32());
        Assert.Equal(DiscordMessages.Texts.BadAttachment, Content(result));
        Assert.Empty(_reports.Created);
        Assert.Empty(await DrainQueueAsync());
    }

    [Fact]
    public async Task Component_from_other_user_gets_notice()
    {
        _reports.Next = View(DiscordReportOutcome.NotOwner, DiscordReportStatus.Failed);

        var result = await SendAsync(Component(DiscordCustomId.Cancel));

        Assert.Equal(DiscordResponseType.UpdateMessage, Json(result).GetProperty("type").GetInt32());
        Assert.Equal(DiscordMessages.Texts.NotOwner, Content(result));
        Assert.Equal(0, Json(result).GetProperty("data").GetProperty("components").GetArrayLength());
        Assert.Equal(["cancel"], _reports.Calls);
        Assert.Equal((ReportId, UserId), _reports.Target);
    }

    [Fact]
    public async Task Date_select_updates_the_panel()
    {
        _reports.Next = View();

        var result = await SendAsync(Component(DiscordCustomId.Date, "2026-10-29"));

        var json = Json(result);
        Assert.Equal(DiscordResponseType.UpdateMessage, json.GetProperty("type").GetInt32());
        Assert.NotEqual(0, json.GetProperty("data").GetProperty("components").GetArrayLength());
        Assert.Equal(["date 2026-10-29"], _reports.Calls);
    }

    [Fact]
    public async Task Events_select_passes_parsed_events()
    {
        _reports.Next = View();
        var entry = new AttendanceEntry(3, new DateOnly(2026, 10, 30), L2Points.Domain.Activities.EventSlot.Evening);

        await SendAsync(Component(DiscordCustomId.Events, DiscordCustomId.EventValue(entry)));

        Assert.Equal([$"events {entry}"], _reports.Calls);
    }

    [Fact]
    public async Task Unknown_custom_id_gets_closed_notice()
    {
        var interaction = Component(DiscordCustomId.Cancel);
        interaction["data"]!["custom_id"] = "something-else";

        var result = await SendAsync(interaction);

        Assert.Equal(DiscordResponseType.ChannelMessage, Json(result).GetProperty("type").GetInt32());
        Assert.Equal(DiscordMessages.Texts.Closed, Content(result));
        Assert.Empty(_reports.Calls);
    }

    [Fact]
    public async Task Submit_queues_job_and_defers_update()
    {
        _reports.Next = View();

        var result = await SendAsync(Component(DiscordCustomId.Submit));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("""{"type":6}""", JsonSerializer.Serialize(result.Body, DiscordJson.Options));
        Assert.Equal(["get"], _reports.Calls);
        var job = Assert.IsType<SubmitJob>(Assert.Single(await DrainQueueAsync()));
        Assert.Equal(new SubmitJob(ReportId, UserId, Token), job);
    }

    [Fact]
    public async Task Submit_of_expired_report_gets_notice_and_no_job()
    {
        _reports.Next = View(DiscordReportOutcome.Expired, DiscordReportStatus.Expired);

        var result = await SendAsync(Component(DiscordCustomId.Submit));

        Assert.Equal(DiscordResponseType.UpdateMessage, Json(result).GetProperty("type").GetInt32());
        Assert.Equal(DiscordMessages.Texts.Expired, Content(result));
        Assert.Equal(0, Json(result).GetProperty("data").GetProperty("components").GetArrayLength());
        Assert.Empty(await DrainQueueAsync());
    }

    private sealed class FakeReports : IDiscordReportService
    {
        public Guid NextId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");

        public List<DiscordReporter> Created { get; } = [];

        public List<string> Calls { get; } = [];

        /// <summary>Report and Discord user of the last call.</summary>
        public (Guid ReportId, string? UserId) Target { get; private set; }

        public DiscordReportView? Next { get; set; }

        private Task<DiscordReportView> Record(string call, Guid reportId, string? discordUserId = null)
        {
            Calls.Add(call);
            Target = (reportId, discordUserId);
            return Task.FromResult(Next ?? throw new InvalidOperationException("No view configured."));
        }

        public Task<Guid> CreateAsync(DiscordReporter reporter, CancellationToken ct = default)
        {
            Created.Add(reporter);
            return Task.FromResult(NextId);
        }

        public Task<DiscordReportView> AnalyzeAsync(Guid reportId, byte[] image, string mediaType, CancellationToken ct = default) =>
            Record("analyze", reportId);

        public Task<DiscordReportView> MarkFailedAsync(Guid reportId, CancellationToken ct = default) => Record("failed", reportId);

        public Task<DiscordReportView> GetAsync(Guid reportId, string discordUserId, CancellationToken ct = default) =>
            Record("get", reportId, discordUserId);

        public Task<DiscordReportView> SelectDateAsync(Guid reportId, string discordUserId, DateOnly date, CancellationToken ct = default) =>
            Record($"date {date:yyyy-MM-dd}", reportId, discordUserId);

        public Task<DiscordReportView> SelectEventsAsync(
            Guid reportId, string discordUserId, IReadOnlyList<AttendanceEntry> events, CancellationToken ct = default) =>
            Record($"events {string.Join(",", events)}", reportId, discordUserId);

        public Task<DiscordReportView> ConfirmSimilarAsync(
            Guid reportId, string discordUserId, IReadOnlyList<string> userIds, CancellationToken ct = default) =>
            Record($"similar {string.Join(",", userIds)}", reportId, discordUserId);

        public Task<DiscordReportView> CancelAsync(Guid reportId, string discordUserId, CancellationToken ct = default) =>
            Record("cancel", reportId, discordUserId);

        public Task<DiscordReportView> SubmitAsync(Guid reportId, string discordUserId, CancellationToken ct = default) =>
            Record("submit", reportId, discordUserId);
    }
}
