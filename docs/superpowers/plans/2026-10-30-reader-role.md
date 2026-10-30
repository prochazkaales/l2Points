# Role Čtenář – Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a read-only Reader account type that sees the whole web like a player but cannot change anything, with an admin toggle that either keeps the reader in all statistics or hides them everywhere.

**Architecture:** A new Identity role `Reader` replaces `Player` on reader accounts, and a new `AppUser.ShowInStats` column (always `true` for non-readers) drives every statistics query with a plain `u.ShowInStats` filter. Write protection lives in the services and reads reader/hidden state from the DB (`PlayerQueries` helpers), never from cookie claims; the UI only mirrors it (locked grid, banner, disabled profile form, admin actions).

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core (SQL Server, SQLite in tests), Blazor Interactive Server, MudBlazor 9, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-30-reader-role-design.md`

## Global Constraints

- UI text in Czech; code, comments, identifiers, commit messages in English.
- Current time only through `TimeProvider`, never `DateTime.Now`/`UtcNow` in product code.
- DB access from components and services through `IDbContextFactory<AppDbContext>`; `PlayerAdminService` keeps one DI scope per operation for `UserManager`.
- Reader/hidden checks on the server read the DB, not claims (spec: "Kontroly na serveru čtou stav čtenáře z DB, ne z claimů v cookie").
- Invariant: a non-reader always has `ShowInStats = true`; only a reader may have `false`.
- A reader has role `Reader` **instead of** `Player`, never both, and never `Admin`.
- Switching player ↔ reader rotates the security stamp; changing `ShowInStats` does not.
- New enum members are appended at the end of existing enums.
- UI from MudBlazor components, dark/light, 375×812, 44 px touch targets (`--l2p-touch`), colours and spacing from `--l2p-*` tokens.
- TDD for rules and services; no TDD for pure UI markup.
- Work on branch `feature/reader-role`. The user's own app may run on :5167 and lock `bin/`; build and preview from a worktree (preview port 5170) if that happens.

## Review Focus

1. **Stale claims after a switch** – a circuit opened as admin or player that was just switched to reader still sends `actorIsAdmin: true` for up to a minute; `SetAttendanceAsync` must reject because the actor is a reader in the DB. Test in Task 3 (`SetAttendance_ReaderActorWithAdminFlag_IsRejected`).
2. **Inserting a hidden reader really stores `false`** – an EF `HasDefaultValue(true)` on a `bool` would silently turn `false` into `true` on insert. Test in Task 4 (`Create_HiddenReader_IsStoredHidden`).
3. **Hidden reader's jewels and attendance leak into totals** – gallery piece counts, week events' attendee lists and the leaderboard must ignore them. Tests in Task 2 (`Gallery_IgnoresHiddenReaderPieces`, `WeekOverview_EventsOmitHiddenReader`).
4. **Switching a hidden reader back to player** must show them again (`ShowInStats` reset to `true`). Test in Task 4 (`SetReader_BackToPlayer_ShowsInStatsAgain`).
5. **Config admin restored from a broken state** – if `Admin__UserName` account somehow carries `Reader` or `ShowInStats = false`, startup must normalise it. Test in Task 1 (`Seed_ConfigAdminWithReaderRole_IsNormalised`).

---

### Task 1: Reader role, `ShowInStats` column and DB helpers

**Files:**
- Modify: `src/L2Points.Infrastructure/Identity/Roles.cs`
- Modify: `src/L2Points.Infrastructure/Identity/AppUser.cs`
- Modify: `src/L2Points.Infrastructure/Identity/IdentitySeeder.cs`
- Modify: `src/L2Points.Infrastructure/Persistence/PlayerQueries.cs`
- Create: `src/L2Points.Infrastructure/Persistence/Migrations/<timestamp>_ReaderShowInStats.cs` (generated)
- Modify: `tests/L2Points.Infrastructure.Tests/TestData.cs`
- Test: `tests/L2Points.Infrastructure.Tests/IdentitySeederTests.cs`, `tests/L2Points.Infrastructure.Tests/PersistenceTests.cs`

**Interfaces:**
- Produces:
  - `Roles.Reader = "Reader"`
  - `AppUser.ShowInStats` (`bool`, initialiser `= true`)
  - `PlayerQueries.IsReaderAsync(this AppDbContext db, string userId, CancellationToken ct = default) : Task<bool>` – user has role `Reader` (join `UserRoles`/`Roles` like `PlayerAdminService.GetPlayersAsync`)
  - `PlayerQueries.IsHiddenFromStatsAsync(this AppDbContext db, string userId, CancellationToken ct = default) : Task<bool>` – user exists and `!ShowInStats`
  - `TestData.CreateReaderAsync(ServiceProvider provider, string name, DateOnly activeFrom, bool showInStats) : Task<AppUser>` – creates the user like `CreatePlayerAsync`, creates role `Reader` via `RoleManager<IdentityRole>` if missing, adds it, sets `ShowInStats`
  - `TestData.CreatePlayerAsync` stays as it is (no roles); services must therefore not assume a non-reader has role `Player` (see Task 4)

- [ ] **Step 1: Write the failing tests**

In `IdentitySeederTests`:
- `Seed_CreatesReaderRole` – after seeding, `RoleManager.RoleExistsAsync(Roles.Reader)` is `true`.
- `Seed_ConfigAdminWithReaderRole_IsNormalised` – seed once, then give the config admin role `Reader`, remove `Player`, set `ShowInStats = false`; seed again; assert roles contain `Admin`, `Player`, `SuperAdmin`, not `Reader`, and `ShowInStats == true`.

In `PersistenceTests`:
- `ShowInStats_FalseRoundTrips` – create `AppUser` with `ShowInStats = false` through `UserManager.CreateAsync`, reload in a new context, assert `false`.
- `ShowInStats_DefaultsToTrue` – `new AppUser().ShowInStats` is `true` and a user created without setting it reloads as `true`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/L2Points.Infrastructure.Tests --filter "FullyQualifiedName~IdentitySeederTests|FullyQualifiedName~PersistenceTests"`
Expected: build error (`Roles.Reader`, `ShowInStats` missing).

- [ ] **Step 3: Implement**

- `Roles.Reader` with a doc comment ("Read-only account: sees everything, changes nothing; replaces Player").
- `AppUser.ShowInStats { get; set; } = true;` with doc comment stating the invariant. **No** `HasDefaultValue` in `AppDbContext` (it would replace an inserted `false` by `true`).
- `IdentitySeeder`: add `Roles.Reader` to both role arrays (role creation list at line ~21 creates it; for the config admin, remove `Reader` if present before the `Admin/Player/SuperAdmin` loop), and set `ShowInStats = true` on the config admin when it is `false` (save with `UpdateAsync`, reuse `ThrowIfFailed`).
- `PlayerQueries`: the two helpers above.

- [ ] **Step 4: Add the migration**

Run: `dotnet ef migrations add ReaderShowInStats -p src/L2Points.Infrastructure -s src/L2Points.Web -o Persistence/Migrations`
Then edit the generated `AddColumn<bool>` for `ShowInStats` to `defaultValue: true` (existing users stay visible). Leave the model snapshot as generated.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test`
Expected: all green.

- [ ] **Step 6: Commit**

```bash
git add src/L2Points.Infrastructure tests/L2Points.Infrastructure.Tests
git commit -m "feat(infra): reader role and ShowInStats column"
```

---

### Task 2: Hide hidden readers from statistics

**Files:**
- Modify: `src/L2Points.Infrastructure/Services/OverviewService.cs` (both `db.Users` queries)
- Modify: `src/L2Points.Infrastructure/Services/JewelService.cs` (`LoadAllPlayersAsync`)
- Modify: `src/L2Points.Infrastructure/Services/PlayerStatsService.cs` (`GetPlayerDetailAsync`)
- Modify: `src/L2Points.Infrastructure/Services/PlayerDetail.cs`
- Test: `tests/L2Points.Infrastructure.Tests/OverviewServiceTests.cs`, `JewelServiceTests.cs`, `PlayerStatsServiceTests.cs`

**Interfaces:**
- Consumes: `AppUser.ShowInStats`, `TestData.CreateReaderAsync`, `PlayerQueries.IsReaderAsync`.
- Produces: `PlayerDetail` gains a last positional member `bool IsReader`. `JewelService.GetAssignmentsAsync` and `GetGalleryAsync` return only players with `ShowInStats`.

- [ ] **Step 1: Write the failing tests**

- `OverviewServiceTests.Leaderboard_OmitsHiddenReader` – a player and a hidden reader, both with attendance; `GetLeaderboardAsync(week, includeInactive: true)` rows contain only the player.
- `OverviewServiceTests.Leaderboard_IncludesShownReader` – a shown reader with attendance appears with their points.
- `OverviewServiceTests.WeekOverview_EventsOmitHiddenReader` – hidden reader attended an event the player also attended; `GetWeekOverviewAsync` rows omit the reader and that event's attendees contain only the player; `previousWeekPoints` excludes the reader's points from the previous week.
- `JewelServiceTests.Gallery_IgnoresHiddenReaderPieces` – player holds 1 piece, hidden reader 2 of the same type; gallery total for that type is `1`, holders only the player.
- `JewelServiceTests.Assignments_OmitHiddenReader` – `GetAssignmentsAsync().Players` has no hidden reader, has a shown reader.
- `PlayerStatsServiceTests.Detail_HiddenReader_IsNull` – `GetPlayerDetailAsync(hiddenReaderId)` returns `null`.
- `PlayerStatsServiceTests.Detail_ShownReader_IsMarked` – returns a detail with `IsReader == true`; a player's detail has `IsReader == false`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/L2Points.Infrastructure.Tests --filter "FullyQualifiedName~OverviewServiceTests|FullyQualifiedName~JewelServiceTests|FullyQualifiedName~PlayerStatsServiceTests"`
Expected: new tests fail (hidden reader present, `IsReader` missing).

- [ ] **Step 3: Implement**

Add `.Where(u => u.ShowInStats)` before `.SelectPlayerInfo()` in every listed query. `GetPlayerDetailAsync`: filter the same way (hidden → `null`) and fill `IsReader` via `db.IsReaderAsync(userId, ct)`. `GetWeekSummaryAsync` (Můj týden card) stays unfiltered – a hidden reader still sees their own numbers. Update the `PlayerDetail` construction and any test or page that constructs it.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add src/L2Points.Infrastructure tests/L2Points.Infrastructure.Tests
git commit -m "feat(infra): keep hidden readers out of statistics"
```

---

### Task 3: Server-side write protection (grid lock, attendance, jewels)

**Files:**
- Modify: `src/L2Points.Domain/Attendances/WeekGridBuilder.cs`
- Modify: `src/L2Points.Infrastructure/Services/AttendanceService.cs`
- Modify: `src/L2Points.Infrastructure/Services/AttendanceChangeResult.cs`
- Modify: `src/L2Points.Infrastructure/Services/JewelService.cs` (`CheckAsync`)
- Modify: `src/L2Points.Infrastructure/Services/JewelChangeResult.cs`
- Test: `tests/L2Points.Domain.Tests/Attendances/WeekGridBuilderTests.cs`, `tests/L2Points.Infrastructure.Tests/AttendanceServiceTests.cs`, `JewelServiceTests.cs`

**Interfaces:**
- Consumes: `PlayerQueries.IsReaderAsync`, `PlayerQueries.IsHiddenFromStatsAsync`, `TestData.CreateReaderAsync`.
- Produces:
  - `WeekGridBuilder.Build(..., bool isAdmin, bool canEdit = true)` – when `false`, every cell has `IsEditable = false`.
  - `AttendanceChangeResult.ReadOnlyAccount` (appended).
  - `JewelChangeResult.HiddenPlayer` (appended).
  - `AttendanceService.GetWeekGridAsync` keeps its signature; when `!isAdmin` it locks the grid if `userId` is a reader (a non-admin only ever loads their own grid; `SetAttendanceAsync` already forbids other targets). Document this in the method's comment.

- [ ] **Step 1: Write the failing tests**

Domain:
- `Build_CannotEdit_LocksEveryCell` – same arrange as an existing editable-today test, `canEdit: false`; all cells `IsEditable == false`, `IsChecked` unchanged.

Infrastructure, `AttendanceServiceTests`:
- `Grid_Reader_IsFullyLocked` – reader, `GetWeekGridAsync(readerId, currentWeek, isAdmin: false)`; no cell editable (including today).
- `Grid_AdminViewingShownReader_IsEditable` – `isAdmin: true` on a shown reader's grid; today's scheduled cell editable.
- `SetAttendance_Reader_IsRejected` – reader sets own attendance today → `ReadOnlyAccount`, no row stored.
- `SetAttendance_ReaderActorWithAdminFlag_IsRejected` – reader as actor with `actorIsAdmin: true` targeting a player → `ReadOnlyAccount`.
- `SetAttendance_AdminOnHiddenReader_IsRejected` – admin (`actorIsAdmin: true`) on hidden reader → `ReadOnlyAccount`; also removal (`attended: false`) is rejected and the existing row stays.
- `SetAttendance_AdminOnShownReader_Succeeds` → `Ok`, row stored.

`JewelServiceTests`:
- `Assign_HiddenReader_IsRejected` → `HiddenPlayer`, no piece added; `RemoveOne_HiddenReader_IsRejected` → `HiddenPlayer`, piece kept.
- `Assign_ShownReader_Succeeds` → `Ok`.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~WeekGridBuilder|FullyQualifiedName~AttendanceServiceTests|FullyQualifiedName~JewelServiceTests"`
Expected: build errors for the new members.

- [ ] **Step 3: Implement**

- `WeekGridBuilder`: `editable = canEdit && AttendanceRules.CanEdit(...) && (...)`.
- `SetAttendanceAsync`: keep the existing `Forbidden` check first; right after creating the context, return `ReadOnlyAccount` when `db.IsReaderAsync(actorUserId)` or `db.IsHiddenFromStatsAsync(targetUserId)` – before the edit-window check and before the removal branch.
- `JewelService.CheckAsync`: after the unknown-player check, return `HiddenPlayer` when `db.IsHiddenFromStatsAsync(userId)`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat: readers cannot change attendance, hidden readers cannot be edited"
```

---

### Task 4: Player admin – create readers, switch type, toggle visibility

**Files:**
- Modify: `src/L2Points.Infrastructure/Services/PlayerAdminService.cs`
- Modify: `src/L2Points.Infrastructure/Services/AdminPlayer.cs`
- Modify: `src/L2Points.Infrastructure/Services/PlayerAdminResult.cs`
- Test: `tests/L2Points.Infrastructure.Tests/PlayerAdminServiceTests.cs`

**Interfaces:**
- Consumes: `Roles.Reader`, `AppUser.ShowInStats`, `TestData.CreateReaderAsync`.
- Produces:
  - `AdminPlayer` gains trailing `bool IsReader, bool ShowInStats`.
  - `PlayerAdminStatus.AdminCannotBeReader`, `ReaderCannotBeAdmin`, `NotReader` (appended).
  - `CreateAsync(string login, string nick, DateOnly? activeFrom, bool isReader = false, bool showInStats = true, CancellationToken ct = default)` – role `Reader` instead of `Player` when `isReader`; `ShowInStats = isReader ? showInStats : true`.
  - `SetReaderAsync(string actorUserId, string userId, bool isReader, CancellationToken ct = default) : Task<PlayerAdminResult>`
  - `SetShowInStatsAsync(string actorUserId, string userId, bool show, CancellationToken ct = default) : Task<PlayerAdminResult>`
  - `SetAdminAsync` additionally returns `ReaderCannotBeAdmin` when granting Admin to a reader.

Check order (spec: aktér → neznámý hráč → vlastní účet → super admin → admin):
- `SetReaderAsync`: `IsAllowedActorAsync` → `ActorNotAllowed`; missing user → `UnknownPlayer`; `actorUserId == userId` → `CannotChangeSelf`; `isReader` and target in role `Admin` (covers super admin) → `AdminCannotBeReader`; already in the requested state → `Ok` without changes; else swap `Player`↔`Reader` (remove the old role only when the user has it, add the new one only when missing: older accounts and test users may lack `Player`), set `ShowInStats = true` (both directions: spec default "zapnuto" on switch to reader, invariant on switch to player), `UpdateSecurityStampAsync`.
- `SetShowInStatsAsync`: `ActorNotAllowed` → `UnknownPlayer` → `!show` on a non-reader → `NotReader` → unchanged → `Ok`; else save with `UpdateAsync` (no stamp rotation).
- `SetAdminAsync`: after the `RequiresSuperAdmin` check, `isAdmin` on a reader → `ReaderCannotBeAdmin`.

- [ ] **Step 1: Write the failing tests**

- `Create_Reader_GetsReaderRoleOnly` – roles `== [Reader]`, `ShowInStats == false` when `showInStats: false`.
- `Create_HiddenReader_IsStoredHidden` – create with `isReader: true, showInStats: false`; `GetPlayersAsync` row has `IsReader == true`, `ShowInStats == false`; `OverviewService.GetLeaderboardAsync` (resolve from provider) has no row for them.
- `Create_PlayerIgnoresShowInStatsFalse` – `isReader: false, showInStats: false` → `ShowInStats == true`, role `Player`.
- `SetReader_Player_BecomesShownReader` – roles `[Reader]`, `ShowInStats == true`, security stamp changed, attendance rows untouched (count before == after).
- `SetReader_BackToPlayer_ShowsInStatsAgain` – hidden reader → player: roles `[Player]`, `ShowInStats == true`.
- `SetReader_Admin_IsRejected` – regular admin as target (actor = super admin) → `AdminCannotBeReader`; super admin as target (actor = regular admin) → `AdminCannotBeReader`.
- `SetReader_Self_IsRejected` → `CannotChangeSelf`.
- `SetReader_InactiveActor_IsRejected` → `ActorNotAllowed`.
- `SetShowInStats_Reader_TogglesWithoutStamp` – `false` then `true`; stamp unchanged.
- `SetShowInStats_HidePlayer_IsRejected` → `NotReader`.
- `SetAdmin_Reader_IsRejected` – super admin grants Admin to a reader → `ReaderCannotBeAdmin`, roles unchanged.
- `GetPlayers_IncludesHiddenReaders` – list contains hidden reader with flags set.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/L2Points.Infrastructure.Tests --filter "FullyQualifiedName~PlayerAdminServiceTests"`
Expected: build errors for the new members.

- [ ] **Step 3: Implement** the members above. `GetPlayersAsync` projects `IsReader` with the same `UserRoles`/`Roles` subquery pattern and `u.ShowInStats`. Update existing callers of `CreateAsync`/`AdminPlayer` so the solution builds (web page passes defaults until Task 5).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test`
Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(infra): create readers, switch player/reader, toggle stats visibility"
```

---

### Task 5: Admin UI for readers

**Files:**
- Modify: `src/L2Points.Web/Components/Admin/PlayerActionState.cs`
- Modify: `src/L2Points.Web/Components/Admin/PlayerActions.razor`
- Modify: `src/L2Points.Web/Components/Admin/PlayerEditDialog.razor`
- Modify: `src/L2Points.Web/Components/Pages/Admin/AdminPlayers.razor`
- Modify: `src/L2Points.Web/Components/Pages/Admin/AdminAttendance.razor`
- Modify: `src/L2Points.Web/Components/Pages/Admin/AdminJewels.razor` (message for `HiddenPlayer`)
- Test: `tests/L2Points.Web.Tests/Admin/PlayerActionStateTests.cs`

**Interfaces:**
- Consumes: `AdminPlayer.IsReader/ShowInStats`, `PlayerAdminService.CreateAsync/SetReaderAsync/SetShowInStatsAsync`, new `PlayerAdminStatus` and `JewelChangeResult` members.
- Produces: `PlayerActionState` gains trailing `bool CanSwitchToReader, string? SwitchToReaderHint` (switching back to player is always allowed for a non-self reader; readers are never admins).
  - `CanSwitchToReader = !player.IsReader && !isSelf && !player.IsAdmin`
  - `SwitchToReaderHint` = `"Vlastní účet"` when self, `AdminReaderText = "Admina nejde přepnout na čtenáře."` when admin, else `null`.

- [ ] **Step 1: Write the failing tests** in `PlayerActionStateTests`:
- `Player_CanSwitchToReader`, `Self_CannotSwitchToReader` (hint "Vlastní účet"), `Admin_CannotSwitchToReader` (hint `AdminReaderText`, also for super-admin actor), `Reader_DoesNotOfferSwitchToReader` (`CanSwitchToReader == false`, hint `null`).

- [ ] **Step 2: Run** `dotnet test tests/L2Points.Web.Tests --filter "FullyQualifiedName~PlayerActionStateTests"` – expected build error.

- [ ] **Step 3: Implement `PlayerActionState`**, run the tests – expected PASS.

- [ ] **Step 4: Implement the pages** (no TDD):
- `PlayerEditDialog` (create mode only): `MudRadioGroup` Hráč / Čtenář and, for Čtenář, `MudCheckBox` „Zobrazovat ve statistikách“ default unchecked; title „Nový účet“. `Values` record gains `bool IsReader, bool ShowInStats`. Edit mode unchanged.
- `PlayerActions` menu: „Přepnout na čtenáře“ (disabled with `SwitchToReaderHint` like the other disabled items) for non-readers, „Přepnout na hráče“ for readers, and for readers a „Zobrazovat ve statistikách“ toggle item (icon `Visibility`/`VisibilityOff`, text „Skrýt ze statistik“ / „Zobrazit ve statistikách“). New callbacks `OnSetReader(bool)` and `OnSetShowInStats(bool)`. Hide „Udělit admina“ for readers.
- `AdminPlayers`: button text „Nový účet“; wire the callbacks to `SetReaderAsync`/`SetShowInStatsAsync` through the existing `ChangeAsync`; chips „čtenář“ (outlined, default colour) and „skrytý“ in both the table and the mobile list; for a hidden reader render the nick as plain text instead of `MudLink`; messages: `AdminCannotBeReader` → `PlayerActionState.AdminReaderText`, `ReaderCannotBeAdmin` → „Čtenáři nejde dát admina, nejdřív ho přepni na hráče.“, `NotReader` → „Ze statistik jde skrýt jen čtenář.“.
- `AdminAttendance`: player picker lists only `ShowInStats` players (filter `_players` after load; keep `?player=` of a hidden reader treated as not found).
- `AdminJewels`: `JewelChangeResult.HiddenPlayer` → „Skrytému čtenáři nejde měnit šperky.“

- [ ] **Step 5: Verify in the browser** (dev server, dark + light, 375×812): create a hidden reader, switch a player to reader and back, toggle visibility; chips, menu hints and the 44 px targets look right; `/admin/attendance` and `/admin/jewels` pickers omit the hidden reader. `dotnet test` green.

- [ ] **Step 6: Commit**

```bash
git add src/L2Points.Web tests/L2Points.Web.Tests
git commit -m "feat(web): admin creates readers, switches type and stats visibility"
```

---

### Task 6: Reader-facing UI and docs

**Files:**
- Modify: `src/L2Points.Web/Components/Pages/MyWeek.razor`
- Modify: `src/L2Points.Web/Components/Attendance/EditableWeekGrid.razor` (`MessageFor`)
- Modify: `src/L2Points.Web/Components/Account/ChangePassword.razor`
- Modify: `src/L2Points.Web/Components/Pages/PlayerDetail.razor`
- Modify: `CLAUDE.md`, `docs/superpowers/specs/2026-10-28-l2points-design.md`, `docs/superpowers/plans/2026-10-28-00-roadmap.md`

**Interfaces:**
- Consumes: `Roles.Reader`, locked grid from `AttendanceService` (Task 3), `AttendanceChangeResult.ReadOnlyAccount`, `PlayerDetail.IsReader`.

- [ ] **Step 1: Implement** (no TDD, pure UI):
- `MyWeek`: `_isReader = state.User.IsInRole(Roles.Reader)`; when true, a `MudAlert Severity.Info` above the summary: „Jsi přihlášen jako čtenář. Účast tady nejde zapisovat.“ Grid and summary stay as they are (the grid comes back locked from the server).
- `EditableWeekGrid.MessageFor`: `ReadOnlyAccount` → „Účet čtenáře nejde upravovat.“
- `ChangePassword` (static SSR): load the user in `OnInitializedAsync` and `_isReader = await UserManager.IsInRoleAsync(user, Roles.Reader)`; when true show the notice in the existing `account-error`-like style (add an `account-info` class in the account CSS using tokens) „Účet čtenáře si heslo měnit nemůže.“, wrap the inputs and submit in `<fieldset disabled>`; in `ChangePasswordAsync` re-check `IsInRoleAsync(user, Roles.Reader)` and add the same message to `_errors` without changing anything.
- `PlayerDetail`: in `TitleSuffix` add chip „čtenář“ when `detail.IsReader`, styled like the „neaktivní“ chip.

- [ ] **Step 2: Verify in the browser** as a shown and a hidden reader (dark, light, 375×812): Můj týden shows banner, summary and a fully locked grid, week switching works; `/profile` form disabled with the notice, a forced POST (submit with the fieldset re-enabled via devtools) shows the error and the password is unchanged; leaderboard shows the shown reader and not the hidden one; `/players/{hiddenId}` shows „Hráč nenalezen.“; no admin menu.

- [ ] **Step 3: Update docs**:
- `CLAUDE.md`: roles line (Admin, Player, SuperAdmin, **Reader**), domain rules bullet for the reader (no writes, `ShowInStats`, reader instead of Player, never Admin, checks from DB), `PlayerQueries` helpers, `AppUser.ShowInStats` invariant.
- Main spec: short "Role Čtenář" subsection pointing to `2026-10-30-reader-role-design.md`.
- Roadmap, deferred notes: "Admin services other than `PlayerAdminService` do not re-check the actor in the DB; a demoted admin's circuit keeps admin pages for up to a minute (pre-existing, not worsened by readers)."

- [ ] **Step 4: Run** `dotnet build` and `dotnet test` – expected green.

- [ ] **Step 5: Commit**

```bash
git add src/L2Points.Web CLAUDE.md docs
git commit -m "feat(web): read-only reader UI on My week, profile and player detail; docs"
```
