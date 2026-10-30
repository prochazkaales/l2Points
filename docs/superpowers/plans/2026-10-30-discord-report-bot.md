# Discord report bot – Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Discord slash command `/report` that reads the party members from a Lineage II screenshot with Claude, matches them to players, lets the reporter pick the date and epics, and records attendance for everyone in one click.

**Architecture:** Discord calls an HTTP interactions endpoint inside `L2Points.Web` (no gateway, no separate bot). Pure logic (nick matching, event options and preselection) lives in `L2Points.Domain/Reports`; the report state machine and the writes (through `AttendanceService` with `actorIsAdmin: true`) live in `L2Points.Infrastructure` (`DiscordReportService`, table `DiscordReports`); the web layer verifies Ed25519 signatures, maps interactions to service calls, builds Discord messages and runs slow work (screenshot reading, submitting) on an in-process queue, editing the original message through Discord webhooks.

**Tech Stack:** .NET 10, ASP.NET Core minimal endpoint, EF Core (SQL Server, SQLite in tests), xUnit, `Anthropic` C# SDK 13.3.0, `BouncyCastle.Cryptography` 2.6.2 (Ed25519), Discord HTTP API v10, Bicep.

**Spec:** `docs/superpowers/specs/2026-10-30-discord-report-bot-design.md`

## Global Constraints

- Discord texts in Czech (exact copy in Task 6); code, comments, identifiers, commit messages in English.
- Current time only through `TimeProvider`; display times and dates in Europe/Prague (`AppTime`), date format `d. M.` as on the web.
- DB access only through `IDbContextFactory<AppDbContext>`, one short-lived context per operation; explicit transactions only via `Database.CreateExecutionStrategy().ExecuteAsync(...)`.
- New enums are stored as `int`; never renumber existing members.
- Secrets (`Discord__BotToken`, `Anthropic__ApiKey`, interaction tokens) never appear in logs, exceptions surfaced to Discord, or the repo.
- The feature is off unless `Discord__ApplicationId`, `Discord__PublicKey`, `Discord__GuildId`, `Discord__BotToken` and `Anthropic__ApiKey` are all set; then `/discord/interactions` returns 404 and nothing calls Discord or Anthropic. Local dev, tests and the CI smoke test run without them.
- Default model `claude-haiku-5-5`, overridable by `Anthropic__Model`.
- Report lifetime 15 minutes from creation; preselect window 6 hours; date options today and 6 days back (not before tracking start); max attachment 10 MB, types `image/png`, `image/jpeg`, `image/webp`, hosts `cdn.discordapp.com` and `media.discordapp.net` over https.
- Attendance written by a report has `CreatedByUserId = "discord-report:{reportId:N}"` (no FK on that column; this is the bot identity and the audit link).
- Candidates for matching: users with `IsActive && ShowInStats` (players and visible readers; hidden readers and deactivated accounts excluded).
- TDD for Domain and services; no TDD for Bicep/docs.
- Work on branch `feat/discord-report-bot`. The user's own app may run on :5167 and lock `bin/`; if a build fails on locked DLLs, build from a worktree.

## Review Focus

1. **Double submit** – the reporter double-clicks *Zapsat* or Discord redelivers the interaction; attendance must be written exactly once and both clicks must show the same result. Test in Task 4 (`Submit_concurrent_calls_write_once`).
2. **Someone else clicks the controls** – components on an ephemeral message are only visible to the author, but a crafted interaction from another user must not change or submit the report. Tests in Task 3 (`Other_user_cannot_change_report`) and Task 4 (`Submit_by_other_user_is_rejected`).
3. **Late click** – controls used after 15 minutes must not write and must say the report expired. Test in Task 4 (`Submit_after_lifetime_expires_report`).
4. **Screenshot without the panel, or a model answer that is not the expected JSON** – the reporter gets a clear message, nothing crashes. Tests in Task 3 (`Analyze_without_panel_fails_report`) and Task 5 (`Parse_invalid_json_throws`).
5. **Attachment that is not an image, too large, or not on Discord's CDN** – rejected before any download. Test in Task 6 (`AttachmentPolicy` tests).

---

### Task 1: Nick matching (Domain)

**Files:**
- Create: `src/L2Points.Domain/Reports/NickMatcher.cs` (types below in the same file)
- Test: `tests/L2Points.Domain.Tests/Reports/NickMatcherTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct NickCandidate(string UserId, string Nick);`
  - `public enum NickMatchKind { Exact, Similar }`
  - `public sealed record NickMatch(string ReadNick, string UserId, string Nick, NickMatchKind Kind);`
  - `public sealed record NickMatchResult(IReadOnlyList<NickMatch> Matches, IReadOnlyList<string> Unknown);`
  - `public static NickMatchResult NickMatcher.Match(IEnumerable<string> readNicks, IEnumerable<NickCandidate> candidates)`

Algorithm (decided here, the tests pin it):
- Read nicks are trimmed, empty ones dropped, duplicates (case-insensitive) collapsed keeping the first.
- `Key(s) = s.Trim().ToUpperInvariant()`; `Fold(s) = Key(s)` with `0→O`, `1→I`, `L→I`, `5→S`, `8→B`.
- Pass 1: `Key` equal → `Exact`; the candidate is claimed.
- Pass 2, for the remaining read nicks: distance `d = Levenshtein(Fold(read), Fold(candidate))` against unclaimed candidates; allowed when `d <= (Key(read).Length <= 6 ? 1 : 2)`. If the minimum `d` is shared by two or more candidates, the nick is unknown. Read nicks are resolved in ascending order of their best `d` (ties by read order); a candidate claimed meanwhile is skipped and the best is recomputed.
- `Matches` and `Unknown` keep the read order.

- [ ] **Step 1: Write the failing tests** (`NickMatcherTests`)
  - `Exact_ignores_case_and_whitespace` – read `" hubkins1010 "`, candidate `Hubkins1010` → one `Exact` match with `ReadNick == "hubkins1010"`.
  - `Zero_and_letter_O_are_similar` – `V0lume` vs `Volume` → `Similar`.
  - `Digit_one_and_letter_l_are_similar` – `1inaya` vs `Linaya` → `Similar`.
  - `Short_nick_allows_distance_one` – `Mayu` vs `Mayo` → `Similar`.
  - `Short_nick_rejects_distance_two` – `Mayu` vs `Mara` → `Unknown == ["Mayu"]`.
  - `Long_nick_allows_distance_two` – `Bosorka12` vs `Bosorka` + 2 chars, e.g. `Bosorkaxy` → `Similar`.
  - `Tie_is_unknown` – read `Asapo`, candidates `Asapa`, `Asape` → unknown.
  - `Exact_wins_over_earlier_similar` – read `["V0lume", "Volume"]`, candidate `Volume` → `Volume` Exact, `V0lume` unknown.
  - `Duplicates_collapse` – read `["Mayu", "MAYU"]` → one match.
  - `Empty_read_nicks_are_ignored` – read `["", "  "]` → no matches, no unknown.
- [ ] **Step 2: Run** `dotnet test tests/L2Points.Domain.Tests --filter NickMatcherTests` – Expected: FAIL (type missing).
- [ ] **Step 3: Implement** `NickMatcher.Match` and the types in `src/L2Points.Domain/Reports/NickMatcher.cs` (namespace `L2Points.Domain.Reports`, private Levenshtein helper).
- [ ] **Step 4: Run** the same command – Expected: PASS.
- [ ] **Step 5: Commit** `feat(domain): match screenshot nicks to players`

---

### Task 2: Event options and preselection (Domain)

**Files:**
- Create: `src/L2Points.Domain/Reports/ReportEventPlanner.cs` (with `ReportEventOption`)
- Test: `tests/L2Points.Domain.Tests/Reports/ReportEventPlannerTests.cs`

**Interfaces:**
- Consumes: `ScheduleCalculator.GetFixedOccurrences`, `AttendanceEntry(int ActivityId, DateOnly Date, EventSlot Slot)` (`L2Points.Domain.Stats`), `AppTime`.
- Produces:
  - `public sealed record ReportEventOption(AttendanceEntry Event, Activity Activity, DateTime? SpawnUtc);`
  - `public static class ReportEventPlanner`
    - `public static readonly TimeSpan PreselectWindow = TimeSpan.FromHours(6);`
    - `public const int DateOptionDays = 7;`
    - `IReadOnlyList<DateOnly> DateOptions(DateOnly todayPrague, DateOnly trackingStart)` – today first, descending, at most 7, none before `trackingStart`.
    - `IReadOnlyList<ReportEventOption> EventOptions(IEnumerable<Activity> activities, DateOnly date)` – fixed occurrences of `date` (`SpawnUtc = date + TimeUtc` as UTC, `null` when `TimeUtc` is null) plus every active `Flexible` activity (`Slot = None`, `SpawnUtc = null`); ordered by `Activity.SortOrder`, then `Slot`.
    - `IReadOnlyList<AttendanceEntry> Preselect(IEnumerable<Activity> activities, DateTimeOffset reportTime, IReadOnlySet<AttendanceEntry> recorded)` – over the UTC dates from `(reportTime - PreselectWindow).UtcDateTime` to `reportTime.UtcDateTime`, the options with `SpawnUtc` in `(reportTime - PreselectWindow, reportTime]` that are not in `recorded`; ordered like `EventOptions`, then by date.
    - `DateOnly DefaultDate(IReadOnlyList<AttendanceEntry> preselected, DateOnly todayPrague)` – latest preselected date, otherwise `todayPrague`.

Test catalog: `DefaultCatalog.CreateActivities()` with `Id = index + 1` assigned in a helper (the seed instances have `Id = 0`). 2026-10-30 is a Friday: chaotic QA, Core, Orfen, Zaken, evening spawns 18:00/18:30/19:00/19:30 UTC, morning 01:30/02:00/02:30/03:00 UTC.

- [ ] **Step 1: Write the failing tests** (`ReportEventPlannerTests`)
  - `Friday_options_are_flexible_then_chaotic_by_slot` – 13 options for 2026-10-30; first is `QueenAnt` (flexible, `SpawnUtc == null`); `ChaoticQueenAnt` Evening comes right before `ChaoticQueenAnt` Morning; no `Baium`.
  - `Sunday_options_include_antharas_without_time_and_castle_siege` – 2026-11-01: `Antharas` with `SpawnUtc == null`, `CastleSiege` with `2026-11-01T18:00Z`, no `Valakas`.
  - `Evening_block_is_preselected` – report `2026-10-30T19:45Z`, empty `recorded` → exactly the four chaotic Evening entries of 2026-10-30.
  - `Future_spawns_are_not_preselected` – report `2026-10-30T18:40Z` → `ChaoticQueenAnt` and `ChaoticCore` Evening only.
  - `Recorded_events_are_not_preselected` – same as the evening test with `ChaoticQueenAnt` Evening in `recorded` → three entries.
  - `Morning_block_does_not_reach_previous_evening` – report `2026-10-30T03:30Z` → the four Morning entries of 2026-10-30, nothing from 2026-10-29.
  - `After_midnight_preselects_previous_evening_and_default_date` – report `2026-10-30T23:30Z` (00:30 on 31. 10. in Prague) → the four chaotic Evening entries of 2026-10-30 (18:00–19:30 are within 6 h) and `DefaultDate(result, new DateOnly(2026, 10, 31)) == 2026-10-30`.
  - `Events_without_time_are_never_preselected` – report `2026-11-01T19:00Z` → `CastleSiege` only (no `Antharas`, no flexible).
  - `Default_date_without_preselection_is_today`.
  - `Date_options_stop_at_tracking_start` – today 2026-10-30, start 2026-10-26 → `[30, 29, 28, 27, 26]`; start 2026-01-01 → 7 dates ending 2026-10-24.
- [ ] **Step 2: Run** `dotnet test tests/L2Points.Domain.Tests --filter ReportEventPlannerTests` – Expected: FAIL.
- [ ] **Step 3: Implement** `ReportEventPlanner` in `src/L2Points.Domain/Reports/ReportEventPlanner.cs`.
- [ ] **Step 4: Run** the same command – Expected: PASS.
- [ ] **Step 5: Commit** `feat(domain): event options and preselection for reports`

---

### Task 3: Report entity and report service (create, analyze, choices)

**Files:**
- Create: `src/L2Points.Infrastructure/Reports/DiscordReport.cs` (entity + `DiscordReportStatus`)
- Create: `src/L2Points.Infrastructure/Reports/DiscordReportState.cs` (state, `ReportPlayer`, `ReportResult`, `ReportEventResult`, `ReportPlayerIssue`)
- Create: `src/L2Points.Infrastructure/Reports/IScreenshotReader.cs` (with `ScreenshotReadResult`, `ScreenshotReadException`)
- Create: `src/L2Points.Infrastructure/Reports/IDiscordReportService.cs` (with `DiscordReporter`, `DiscordReportView`, `DiscordReportOutcome`)
- Create: `src/L2Points.Infrastructure/Services/DiscordReportService.cs`
- Create: `src/L2Points.Infrastructure/Persistence/Configurations/DiscordReportConfiguration.cs`
- Modify: `src/L2Points.Infrastructure/Persistence/AppDbContext.cs` (`DbSet<DiscordReport> DiscordReports`)
- Modify: `src/L2Points.Infrastructure/DependencyInjection.cs`, `tests/L2Points.Infrastructure.Tests/TestServices.cs` (register `IDiscordReportService` → `DiscordReportService`, scoped; tests register a `FakeScreenshotReader` singleton)
- Create: migration `AddDiscordReports` (`dotnet ef migrations add AddDiscordReports -p src/L2Points.Infrastructure -s src/L2Points.Web -o Persistence/Migrations`)
- Test: `tests/L2Points.Infrastructure.Tests/DiscordReportServiceTests.cs`, `tests/L2Points.Infrastructure.Tests/FakeScreenshotReader.cs`

**Interfaces:**
- Consumes: Task 1 and Task 2 types, `CatalogQueries.LoadActivitiesAsync`, `CatalogQueries.GetTrackingStartAsync`, `AppTime.TodayInPrague`.
- Produces:
  - `public enum DiscordReportStatus { Processing, Pending, Submitting, Completed, Cancelled, Expired, Failed }`
  - `public class DiscordReport { Guid Id; string DiscordUserId (max 32); string DiscordUserName (max 100); string GuildId (max 32); string ChannelId (max 32); DateTime CreatedAtUtc; DateTime? CompletedAtUtc; DiscordReportStatus Status (int); string StateJson (required, unbounded) }`, index on `CreatedAtUtc`.
  - `public sealed record DiscordReportState` (System.Text.Json, defaults so `"{}"` deserialises): `bool PanelFound`, `IReadOnlyList<string> ReadNicks`, `IReadOnlyList<ReportPlayer> Players`, `IReadOnlyList<string> UnknownNicks`, `DateOnly? Date`, `IReadOnlyList<AttendanceEntry> SelectedEvents`, `ReportResult? Result`.
  - `public sealed record ReportPlayer(string UserId, string Nick, string ReadNick, NickMatchKind Kind, bool Confirmed);` – `Exact` always `Confirmed`, `Similar` starts `false`.
  - `public sealed record ReportResult(IReadOnlyList<ReportEventResult> Events);`, `public sealed record ReportEventResult(AttendanceEntry Event, string ActivityName, int Added, int AlreadyHad, IReadOnlyList<ReportPlayerIssue> Issues);`, `public sealed record ReportPlayerIssue(string Nick, AttendanceChangeResult Reason);`
  - `public interface IScreenshotReader { Task<ScreenshotReadResult> ReadPartyMembersAsync(byte[] image, string mediaType, IReadOnlyList<string> knownNicks, CancellationToken ct = default); }`, `public sealed record ScreenshotReadResult(bool PanelFound, IReadOnlyList<string> Nicks);`, `public sealed class ScreenshotReadException(string message) : Exception(message);`
  - `public sealed record DiscordReporter(string DiscordUserId, string DiscordUserName, string GuildId, string ChannelId);`
  - `public enum DiscordReportOutcome { Ok, NotFound, NotOwner, Expired, Finished }`
  - `public sealed record DiscordReportView(Guid Id, DiscordReportOutcome Outcome, DiscordReportStatus Status, string ReporterName, DiscordReportState State, IReadOnlyList<DateOnly> DateOptions, IReadOnlyList<ReportEventOption> EventOptions);` (`EventOptions` for `State.Date`, empty when no date)
  - `public interface IDiscordReportService` (all `CancellationToken ct = default`):
    - `Task<Guid> CreateAsync(DiscordReporter reporter, CancellationToken ct)` – status `Processing`, `CreatedAtUtc = now`, `StateJson = "{}"`.
    - `Task<DiscordReportView> AnalyzeAsync(Guid reportId, byte[] image, string mediaType, CancellationToken ct)`
    - `Task<DiscordReportView> MarkFailedAsync(Guid reportId, CancellationToken ct)`
    - `Task<DiscordReportView> GetAsync(Guid reportId, string discordUserId, CancellationToken ct)`
    - `Task<DiscordReportView> SelectDateAsync(Guid reportId, string discordUserId, DateOnly date, CancellationToken ct)`
    - `Task<DiscordReportView> SelectEventsAsync(Guid reportId, string discordUserId, IReadOnlyList<AttendanceEntry> events, CancellationToken ct)`
    - `Task<DiscordReportView> ConfirmSimilarAsync(Guid reportId, string discordUserId, IReadOnlyList<string> userIds, CancellationToken ct)`
    - `Task<DiscordReportView> CancelAsync(Guid reportId, string discordUserId, CancellationToken ct)`
    - `Task<DiscordReportView> SubmitAsync(Guid reportId, string discordUserId, CancellationToken ct)` – Task 4
  - `public static readonly TimeSpan DiscordReportService.Lifetime = TimeSpan.FromMinutes(15);`

Behaviour:
- Guard order on every user call: unknown id → `NotFound`; different `DiscordUserId` → `NotOwner`; status `Completed`/`Cancelled`/`Expired`/`Failed` → `Finished` (view of the stored state); `now > CreatedAtUtc + Lifetime` → set `Expired`, outcome `Expired`; otherwise the operation, outcome `Ok`.
- `AnalyzeAsync` (only from `Processing`): candidates = `IsActive && ShowInStats` users; calls the reader with their nicks; `!PanelFound` or no nicks → `Failed` with `PanelFound` stored; otherwise matches, loads attendances of all matched users for the preselect dates into `recorded` (an event counts as recorded when any matched user has it), `Preselect`, `Date = DefaultDate(...)`, `SelectedEvents = preselected on Date`, status `Pending`. Reader exceptions propagate (the worker calls `MarkFailedAsync`).
- `SelectDateAsync`: date must be in `DateOptions`, otherwise state unchanged; new `SelectedEvents` = preselection recomputed from `CreatedAtUtc` restricted to the new date.
- `SelectEventsAsync`: keeps only events present in `EventOptions` for `Date`.
- `ConfirmSimilarAsync`: `Confirmed = userIds.Contains(UserId)` for `Similar` players; `Exact` unchanged.
- `CancelAsync`: `Cancelled`, `CompletedAtUtc = now`.
- Choices are allowed only in `Pending`.

- [ ] **Step 1: Write the failing tests** (`DiscordReportServiceTests`, `FakeTimeProvider` at `2026-10-30T19:45Z`, seeded catalog, players `Hubkins1010`, `Volume`, `Mayu`, a hidden reader `Ghost`, a deactivated `Gone`; fake reader returns configured nicks)
  - `Analyze_matches_and_preselects_evening_block` – reader `["Hubkins1010", "V0lume", "Stranger"]` → status `Pending`; players Hubkins1010 (Exact, confirmed) and Volume (Similar, not confirmed); unknown `["Stranger"]`; `Date == 2026-10-30`; four chaotic Evening events selected.
  - `Analyze_excludes_hidden_and_deactivated_accounts` – reader `["Ghost", "Gone"]` → both unknown.
  - `Analyze_passes_candidate_nicks_to_reader` – the fake recorded `knownNicks` contain `Hubkins1010` and not `Ghost`.
  - `Analyze_without_panel_fails_report` – reader `PanelFound = false` → status `Failed`, `State.PanelFound == false`.
  - `Already_recorded_events_are_not_preselected` – Mayu has `ChaoticQueenAnt` Evening 2026-10-30, reader `["Mayu"]` → three events selected.
  - `Select_date_recomputes_events` – after analyze, `SelectDateAsync(2026-10-29)` → `SelectedEvents` empty, `EventOptions` are Thursday's; a date outside the options leaves the state unchanged.
  - `Select_events_ignores_events_not_offered` – selecting a Baium entry on Friday is dropped.
  - `Confirm_similar_sets_only_listed_players`.
  - `Other_user_cannot_change_report` – `SelectDateAsync` with another Discord id → `NotOwner`, state unchanged.
  - `Changes_after_lifetime_expire_report` – advance the clock 16 minutes → `Expired`, status stored `Expired`.
  - `Cancel_finishes_report` – then any call returns `Finished`.
- [ ] **Step 2: Run** `dotnet test tests/L2Points.Infrastructure.Tests --filter DiscordReportServiceTests` – Expected: FAIL.
- [ ] **Step 3: Implement** the entity, configuration, state types, interface, `DiscordReportService` (except `SubmitAsync`, which throws `NotImplementedException` until Task 4) and DI registrations; generate the migration and check it creates only `DiscordReports`.
- [ ] **Step 4: Run** the same command and `dotnet build` – Expected: PASS, build clean.
- [ ] **Step 5: Commit** `feat(infrastructure): Discord report state and choices`

---

### Task 4: Submitting a report exactly once

**Files:**
- Modify: `src/L2Points.Infrastructure/Services/DiscordReportService.cs`
- Test: `tests/L2Points.Infrastructure.Tests/DiscordReportServiceTests.cs`

**Interfaces:**
- Consumes: `AttendanceService.SetAttendanceAsync(actorUserId, actorIsAdmin, targetUserId, activityId, date, slot, attended, ct)`.
- Produces: `SubmitAsync` as declared in Task 3; `public static string DiscordReportService.ActorId(Guid reportId) => $"discord-report:{reportId:N}";`

Behaviour:
- After the guards, a `Pending` report with no confirmed players or no selected events returns `Ok` unchanged.
- Claim with one conditional update: `ExecuteUpdateAsync` setting `Status = Submitting` where `Id == reportId && Status == Pending`; 0 rows → reload and return `Finished` with the stored state.
- Before writing, load which (confirmed user, event) pairs already exist → `AlreadyHad`. Every other pair goes through `SetAttendanceAsync(ActorId(id), true, userId, ...)`: `Ok` → `Added`, anything else → `ReportPlayerIssue(nick, result)`.
- Store `Result` in the state, status `Completed`, `CompletedAtUtc = now`; outcome `Ok`.

- [ ] **Step 1: Write the failing tests**
  - `Submit_writes_attendance_for_confirmed_players` – Volume not confirmed → rows only for Hubkins1010 and Mayu on the four evening events; `CreatedByUserId == DiscordReportService.ActorId(id)` on every row; per-event `Added == 2`.
  - `Submit_counts_existing_attendance` – Mayu already has `ChaoticCore` Evening (selected manually) → that event `Added == 1`, `AlreadyHad == 1`, no duplicate row.
  - `Submit_reports_weekly_limit` – Mayu has six `QueenAnt` rows in the week of 2026-10-26 (inserted directly on other days); selecting `QueenAnt` 2026-10-30 → issue `WeeklyLimitReached` for Mayu.
  - `Submit_twice_returns_same_result` – second call `Finished`, same `Result`, row count unchanged.
  - `Submit_concurrent_calls_write_once` – `Task.WhenAll` of two `SubmitAsync` calls → one `Ok`, one `Finished`, row count as for one submit.
  - `Submit_by_other_user_is_rejected` – `NotOwner`, no rows.
  - `Submit_after_lifetime_expires_report` – clock +16 min → `Expired`, no rows.
- [ ] **Step 2: Run** `dotnet test tests/L2Points.Infrastructure.Tests --filter DiscordReportServiceTests` – Expected: the new tests FAIL.
- [ ] **Step 3: Implement** `SubmitAsync` and `ActorId`.
- [ ] **Step 4: Run** the same command – Expected: PASS.
- [ ] **Step 5: Commit** `feat(infrastructure): submit Discord report exactly once`

---

### Task 5: Reading the screenshot with Claude

**Files:**
- Modify: `src/L2Points.Infrastructure/L2Points.Infrastructure.csproj` (`<PackageReference Include="Anthropic" Version="13.3.0" />`)
- Create: `src/L2Points.Infrastructure/Reports/AnthropicOptions.cs`
- Create: `src/L2Points.Infrastructure/Reports/ClaudeScreenshotReader.cs`
- Modify: `src/L2Points.Infrastructure/DependencyInjection.cs` (`Configure<AnthropicOptions>(section "Anthropic")`, `AddSingleton<IScreenshotReader, ClaudeScreenshotReader>()`)
- Test: `tests/L2Points.Infrastructure.Tests/ClaudeScreenshotReaderTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class AnthropicOptions { public const string SectionName = "Anthropic"; public string? ApiKey { get; set; } public string Model { get; set; } = "claude-haiku-5-5"; }`
  - `ClaudeScreenshotReader(IOptions<AnthropicOptions> options) : IScreenshotReader` – creates one `AnthropicClient { ApiKey = options.ApiKey }` lazily (no network or key check in the constructor).
  - `internal static ScreenshotReadResult ClaudeScreenshotReader.ParseResponse(string json)` – trims nicks, drops empty ones; malformed JSON or missing properties → `ScreenshotReadException`.

Request: `client.Messages.Create(new MessageCreateParams { Model = options.Model, MaxTokens = 4096, OutputConfig = new OutputConfig { Effort = Effort.Low, Format = new JsonOutputFormat { Schema = ... } }, Messages = [user: image block (base64, `mediaType`) + text prompt] })`. Schema: object, `additionalProperties: false`, required `panelFound` (boolean) and `nicks` (array of string). The image block type is not in the SDK docs this plan was written from (likely `ImageBlockParam` with a base64 image source); write it and let the compiler name it. `StopReason` other than `end_turn` (refusal, max_tokens) or no text block → `ScreenshotReadException`. Prompt (fixed):

```text
The image is a Lineage II screenshot. Find the "Command Channel Info" window and, in it, the "Party Members" panel (a table with the columns "Party Members" and "Class"). Return the character names from the "Party Members" column exactly as written, character by character, top to bottom. Ignore the "Leader" list on the left, the "Owner" field, character names in the game world and any other text. The list of known players below only helps to tell ambiguous characters apart (0/O, 1/l/I); never return a name that is not in the panel and never replace what you read with a name from the list. If there is no "Party Members" panel, return panelFound false and an empty list.

Known players: {comma-separated nicks}
```

- [ ] **Step 1: Write the failing tests** – `Parse_valid_response` (`{"panelFound":true,"nicks":["Hubkins1010"," SemteX ",""]}` → `["Hubkins1010","SemteX"]`), `Parse_panel_not_found`, `Parse_invalid_json_throws`, `Parse_missing_nicks_throws`, `Default_model_is_haiku`.
- [ ] **Step 2: Run** `dotnet test tests/L2Points.Infrastructure.Tests --filter ClaudeScreenshotReaderTests` – Expected: FAIL.
- [ ] **Step 3: Implement** `AnthropicOptions`, `ClaudeScreenshotReader`, DI.
- [ ] **Step 4: Run** the same command and `dotnet test` – Expected: PASS (no test calls the network).
- [ ] **Step 5: Commit** `feat(infrastructure): read party members from a screenshot with Claude`

---

### Task 6: Discord building blocks (signature, attachment policy, messages)

**Files:**
- Modify: `src/L2Points.Web/L2Points.Web.csproj` (`<PackageReference Include="BouncyCastle.Cryptography" Version="2.6.2" />`)
- Create: `src/L2Points.Web/Discord/DiscordOptions.cs`, `DiscordSignature.cs`, `AttachmentPolicy.cs`, `DiscordCustomId.cs`, `DiscordPayloads.cs`, `DiscordMessages.cs`
- Test: `tests/L2Points.Web.Tests/Discord/DiscordSignatureTests.cs`, `AttachmentPolicyTests.cs`, `DiscordCustomIdTests.cs`, `DiscordMessagesTests.cs`

**Interfaces:**
- Consumes: Task 3 view types, `DisplayFormat.SlotLabel`, `AppTime`.
- Produces:
  - `public sealed class DiscordOptions { public const string SectionName = "Discord"; string? ApplicationId, PublicKey, GuildId, BotToken; public static bool IsEnabled(DiscordOptions discord, AnthropicOptions anthropic); }` – all five values non-empty.
  - `public static bool DiscordSignature.Verify(string publicKeyHex, string? signatureHex, string? timestamp, ReadOnlySpan<byte> body, DateTimeOffset now)` – Ed25519 (`Ed25519PublicKeyParameters` + `Ed25519Signer`) over UTF-8 `timestamp` bytes followed by `body`; false on missing/invalid hex, wrong lengths, non-numeric timestamp, or `|now - timestamp| > 5 min`.
  - `public enum AttachmentCheck { Ok, NotImage, TooLarge, UntrustedHost }`, `public static AttachmentCheck AttachmentPolicy.Check(string? url, string? contentType, long size)`, `public const long AttachmentPolicy.MaxBytes = 10 * 1024 * 1024;`
  - `DiscordCustomId`: `string Format(Guid reportId, string action)` → `"r:{id:N}:{action}"`; `bool TryParse(string? value, out Guid reportId, out string action)`; actions `Date = "date"`, `Events = "events"`, `Similar = "similar"`, `Submit = "submit"`, `Cancel = "cancel"`; `string EventValue(AttendanceEntry e)` → `"{ActivityId}|{yyyy-MM-dd}|{(int)Slot}"`, `bool TryParseEventValue(string value, out AttendanceEntry e)`.
  - `DiscordPayloads.cs`: `DiscordInteraction` (the used parts of the payload: `id`, `type`, `token`, `guild_id`, `channel_id`, `member.user.{id,username,global_name}`, `data.{name, options[].value, resolved.attachments[id].{url,content_type,size}, custom_id, values}`), `DiscordMessage` (`content`, `flags`, `components`, `allowed_mentions`) and `DiscordInteractionResponse(int Type, DiscordMessage? Data)`; shared `DiscordJson.Options` (`JsonNamingPolicy.SnakeCaseLower`, nulls omitted).
  - `DiscordMessages` (all return the message record, never throw):
    - `ReportPanel(DiscordReportView view)` – content and up to four action rows: date select (`custom_id` `Date`, one value, labels `ddd d. M.` in cs-CZ, current date default), events select (`Events`, `min_values 0`, `max_values` = option count, label `Activity.Name` + `" – Večerní|Ranní"` for slots + `" (HH:mm)"` Prague spawn time when known, selected ones `default`; more than 25 options → drop flexible ones from the end until 25), similar select only when there are `Similar` players (`Similar`, `min_values 0`, label `"{ReadNick} → {Nick}"`, value `UserId`), buttons *Zapsat* (style 3, disabled when no confirmed player or no selected event) and *Zrušit* (style 2).
    - `Result(DiscordReportView view)`, `PublicSummary(DiscordReportView view)` (no flags, `allowed_mentions = { parse: [] }`), `Notice(string text)`.

Exact copy:
- Panel content: `**Nalezení hráči:** {nicks}` / `**Podobné nicky – potvrď v menu:** {read → nick, …}` / `**Neznámé nicky (nezapíšou se):** {nicks}`; with no matched player: `Na screenu není nikdo z aplikace. Přečtené nicky: {nicks}` and no *Zapsat*.
- Placeholders: `Vyber den`, `Vyber epicy`, `Potvrď podobné nicky`.
- Public summary: `{d. M.} – {event labels joined by ", "}: zapsáno {n} hráčů. Report: {ReporterName}` (n = distinct players with `Added` or `AlreadyHad`; Czech plural `1 hráč`, `2–4 hráči`, otherwise `hráčů`), then one line per issue `{Nick}: {Activity} – {reason}` with reasons `týdenní limit` (`WeeklyLimitReached`), `účet jen ke čtení` (`ReadOnlyAccount`), otherwise `nezapsáno`.
- Result (ephemeral): `Zapsáno.` followed by the same lines as the public summary.
- Notices: `Report vypršel, pošli /report znovu.` · `Tenhle report patří někomu jinému.` · `Tenhle bot funguje jen na serveru party.` · `Na screenu jsem nenašel panel Party Members. Pošli screen rozbalené party.` · `Screen se nepodařilo zpracovat, zkus to znovu.` · `Příloha musí být obrázek PNG, JPG nebo WEBP do 10 MB.` · `Report zrušen, nic se nezapsalo.` · `Report už je uzavřený.`

- [ ] **Step 1: Write the failing tests**
  - Signature: `Valid_signature_passes` (keypair from `Ed25519KeyPairGenerator` in the test), `Tampered_body_fails`, `Missing_headers_fail`, `Invalid_hex_fails`, `Old_timestamp_fails` (6 min).
  - Attachment: `Png_on_cdn_is_ok`, `Pdf_is_not_image`, `Over_10_MB_is_too_large`, `Other_host_is_untrusted`, `Http_is_untrusted`.
  - Custom id: round trip of `Format`/`TryParse` and `EventValue`/`TryParseEventValue`; `TryParse("x:y")` is false.
  - Messages: `Panel_with_similar_has_four_rows`, `Submit_disabled_without_confirmed_players`, `Events_are_truncated_to_25`, `Event_label_has_slot_and_prague_time` (`Chaotic Core – Večerní (19:30)` for 18:30 UTC on 2026-10-30), `Public_summary_text_and_no_mentions`, `Panel_without_matches_has_no_submit`.
- [ ] **Step 2: Run** `dotnet test tests/L2Points.Web.Tests --filter FullyQualifiedName~Discord` – Expected: FAIL.
- [ ] **Step 3: Implement** the files above.
- [ ] **Step 4: Run** the same command – Expected: PASS.
- [ ] **Step 5: Commit** `feat(web): Discord signature, attachment policy and messages`

---

### Task 7: Interactions endpoint, background jobs and command registration

**Files:**
- Create: `src/L2Points.Web/Discord/DiscordInteractionHandler.cs`, `DiscordJobQueue.cs`, `DiscordJobWorker.cs`, `DiscordRestClient.cs`, `DiscordCommandRegistrar.cs`, `DiscordSetup.cs`
- Modify: `src/L2Points.Web/Program.cs` (`builder.Services.AddDiscordReports(builder.Configuration);`, `app.MapDiscordInteractions();` before `MapRazorComponents`)
- Test: `tests/L2Points.Web.Tests/Discord/DiscordInteractionHandlerTests.cs`

**Interfaces:**
- Consumes: Task 3–6.
- Produces:
  - `public abstract record DiscordJob;` `public sealed record AnalyzeJob(Guid ReportId, string InteractionToken, string AttachmentUrl, string ContentType) : DiscordJob;` `public sealed record SubmitJob(Guid ReportId, string DiscordUserId, string InteractionToken) : DiscordJob;`
  - `public sealed class DiscordJobQueue` (singleton, unbounded `Channel<DiscordJob>`): `bool TryEnqueue(DiscordJob job)`, `IAsyncEnumerable<DiscordJob> ReadAllAsync(CancellationToken ct)`.
  - `public sealed record DiscordHandlerResult(int StatusCode, object? Body);`
  - `public sealed class DiscordInteractionHandler(IOptions<DiscordOptions>, IOptions<AnthropicOptions>, IDiscordReportService, DiscordJobQueue, TimeProvider)` (scoped): `Task<DiscordHandlerResult> HandleAsync(byte[] body, string? signature, string? timestamp, CancellationToken ct)`.
  - `DiscordRestClient` (typed `HttpClient`, base `https://discord.com/api/v10/`): `Task EditOriginalAsync(string interactionToken, DiscordMessage message, CancellationToken ct)` (`PATCH webhooks/{appId}/{token}/messages/@original`), `Task SendFollowupAsync(string interactionToken, DiscordMessage message, CancellationToken ct)` (`POST webhooks/{appId}/{token}`), `Task RegisterCommandsAsync(CancellationToken ct)` (`PUT applications/{appId}/guilds/{guildId}/commands`, header `Authorization: Bot {token}`), `Task<byte[]> DownloadAttachmentAsync(string url, CancellationToken ct)` (re-checks `AttachmentPolicy`, stops reading past `MaxBytes`, 20 s timeout).
  - `DiscordSetup.AddDiscordReports(this IServiceCollection, IConfiguration)`, `DiscordSetup.MapDiscordInteractions(this WebApplication)` – `POST /discord/interactions`, `.AllowAnonymous().DisableAntiforgery()`, reads the raw body and the `X-Signature-Ed25519` / `X-Signature-Timestamp` headers.

Handler flow (response `type`s: 1 PONG, 4 message, 5 deferred message, 6 deferred update, 7 update message; `flags: 64` = ephemeral):
1. Feature disabled → 404. Bad signature → 401.
2. Interaction `type 1` → `{ type: 1 }`.
3. `guild_id` missing or not `GuildId` → type 4 ephemeral notice, no report.
4. `type 2`, `data.name == "report"` → attachment from `data.resolved.attachments[options[0].value]`; `AttachmentPolicy.Check` fails → type 4 ephemeral notice; else `CreateAsync` (`DiscordUserName` = `global_name ?? username`), `TryEnqueue(AnalyzeJob)`, respond `{ type: 5, data: { flags: 64 } }`.
5. `type 3` → `DiscordCustomId.TryParse` (fail → type 4 notice `Report už je uzavřený.`); `date`/`events`/`similar`/`cancel` → the service call, respond type 7 with `ReportPanel` (outcome `Ok` and `Pending`), `Result`/`Notice` otherwise; `submit` → `GetAsync`; when `Ok` and `Pending`, `TryEnqueue(SubmitJob)` and respond `{ type: 6 }`, else type 7 notice.

Worker (`BackgroundService`, one DI scope per job, never lets an exception escape the loop, logs without tokens):
- `AnalyzeJob` → download, `AnalyzeAsync`, `EditOriginalAsync` with the panel or the panel-not-found notice; on any exception `MarkFailedAsync` and the processing-failed notice.
- `SubmitJob` → `SubmitAsync`; `EditOriginalAsync` with `Result` (or the notice for the outcome); when `Ok`, `SendFollowupAsync(PublicSummary)`.

`DiscordCommandRegistrar` (`BackgroundService`, only when enabled): registers once at start with body `[{"name":"report","description":"Zapsat účast party ze screenu","type":1,"default_member_permissions":"0","contexts":[0],"options":[{"type":11,"name":"screen","description":"Screen rozbalené party (Party Members)","required":true}]}]`; any failure is logged as a warning and never stops the app.

- [ ] **Step 1: Write the failing tests** (`DiscordInteractionHandlerTests`, fake `IDiscordReportService`, real `DiscordJobQueue`, keypair generated in the test, `FakeTimeProvider`)
  - `Disabled_returns_404`, `Bad_signature_returns_401`, `Ping_returns_pong`.
  - `Other_guild_gets_notice_and_no_report` – fake records no `CreateAsync`.
  - `Report_command_creates_report_queues_analysis_and_defers_ephemerally` – `{ type: 5, data.flags: 64 }`, one `AnalyzeJob` in the queue.
  - `Report_with_pdf_gets_attachment_notice`.
  - `Component_from_other_user_gets_notice` – fake returns `NotOwner` → type 7 with `Tenhle report patří někomu jinému.`
  - `Submit_queues_job_and_defers_update` – `{ type: 6 }`, one `SubmitJob`.
- [ ] **Step 2: Run** `dotnet test tests/L2Points.Web.Tests --filter DiscordInteractionHandlerTests` – Expected: FAIL.
- [ ] **Step 3: Implement** the files above and wire `Program.cs`.
- [ ] **Step 4: Run** `dotnet test` and `dotnet run --project src/L2Points.Web` without Discord config, then `curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:5167/discord/interactions` – Expected: all tests PASS, `404`, no warnings about Discord in the log.
- [ ] **Step 5: Commit** `feat(web): Discord interactions endpoint and background jobs`

---

### Task 8: Azure, deployment docs and project docs

**Files:**
- Modify: `infra/main.bicep`, `infra/deploy.ps1`, `docs/deployment.md`, `scripts/smoke-test.sh`, `CLAUDE.md`, `docs/superpowers/plans/2026-10-28-00-roadmap.md`

Decisions:
- Bicep: new params `discordApplicationId`, `discordPublicKey`, `discordGuildId` (strings, default `''`) and `@secure()` `discordBotToken`, `anthropicApiKey` (default `''`). `var discordEnabled = !empty(discordApplicationId)`. Key Vault secrets `discord-bot-token` and `anthropic-api-key` are written only when the corresponding secure param is non-empty (so updates keep the stored values, like the existing secrets). When `discordEnabled`, the container app gets the two Key Vault secret references and env `Discord__ApplicationId`, `Discord__PublicKey`, `Discord__GuildId` (values) and `Discord__BotToken`, `Anthropic__ApiKey` (`secretRef`) – built with `concat(...)` of conditional arrays.
- `deploy.ps1`: optional `-DiscordApplicationId`, `-DiscordPublicKey`, `-DiscordGuildId` and switch `-SetDiscordSecrets` (prompts for the bot token and the Anthropic key with `Read-Host -AsSecureString`, passed only through the temporary parameter file). On update, missing Discord values are read from the running app (`Get-EnvValue`). Enabling Discord for the first time (no `Discord__ApplicationId` on the app) without `-SetDiscordSecrets` throws with a message explaining the switch.
- `docs/deployment.md`: new section *Discord bot* with the six manual steps from the spec (Developer Portal, console.anthropic.com with a monthly spend limit, `deploy.ps1 -DiscordApplicationId … -DiscordPublicKey … -DiscordGuildId … -SetDiscordSecrets`, Interactions Endpoint URL `https://ca-l2points.graytree-049f5bba.polandcentral.azurecontainerapps.io/discord/interactions`, invite with scope `applications.commands`, command permissions in Server Settings → Integrations), switching the model via `Anthropic__Model`, and how to look up a report (`DiscordReports`, attendance `CreatedByUserId = 'discord-report:<id>'`).
- `scripts/smoke-test.sh`: `POST /discord/interactions` without config returns `404`.
- `CLAUDE.md`: Discord bot in the status line, `Reports/` and `DiscordReportService` in Infrastructure, `Discord/` in Web, the new configuration keys, and the rule that report writes use `discord-report:{id}` as actor. Roadmap: a row or note for the Discord bot with this plan's file name.

- [ ] **Step 1: Edit** `main.bicep`; run `az bicep build --file infra/main.bicep` (if the Azure CLI is not installed, say so in the task report) – Expected: no errors.
- [ ] **Step 2: Edit** `deploy.ps1`; run `pwsh -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile('infra/deploy.ps1',[ref]$null,[ref]$e) | Out-Null; $e"` – Expected: no parse errors.
- [ ] **Step 3: Edit** the smoke test, `docs/deployment.md`, `CLAUDE.md`, roadmap.
- [ ] **Step 4: Run** `dotnet build` and `dotnet test` – Expected: PASS.
- [ ] **Step 5: Commit** `feat(infra): Discord bot configuration and docs`
