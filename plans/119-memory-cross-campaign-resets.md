# Plan 119: Reset per-campaign state on every new campaign and find what a second game keeps (plans 001 and 014)

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Two Parts: finish Part A (its done criteria included) before Part B. Do
> not edit `plans/README.md`: every WP branch shares it, so the integrator records this plan's status
> and test counts when merging. Put the numbers listed in Step B3's hand-off in your final message.
> Every code block starts at column 0: copy it as shown.
>
> **Drift check (run first, Step A0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/Features/Enlistment TAOM.Tests/Features/Enlistment Main/Features/SpecialResources TAOM.Tests/Features/SpecialResources Main/Features/HeroRace TAOM.Tests/Features/HeroRace Main/SubModule.cs docs/features/enlistment.md docs/features/special-resources.md docs/features/hero-race.md docs/investigations`
> Earlier WPs of this programme are expected to have changed some of these paths (listed in Step
> A0.2). Compare the live files against "Current state" and STOP only on a real mismatch (Steps A0.4
> and A0.5 decide).

## Status

- **Priority**: P1
- **Effort**: M (four port commits made from path checkouts and path-limited applies, one new doc)
- **Risk**: MED (load-path behaviour of a shared feature: Enlistment now resets on every load, on a
  new campaign and at game end, and clears a stale record when a save holds no Enlistment data; the
  code is byte-identical to the reviewed 1.5.x end state, but no 1.4.x build or game run has
  exercised it yet)
- **Depends on**: WP105 (`plans/105-composition-root.md`), merged together with its own dependency
  WP104 (`plans/104-guarded-patch-categories.md`), which brings `TAOM.Tests/Infrastructure/RepoPaths.cs`
  (`RepoPath`, `ReadSource`, `CSharpFiles`, `MentionsOutsideComments`, `CodeRoots = { "Main" }`) and
  `a0db0558b`'s `RepoPaths` conversion of `TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs`.
  Soft, not required: WP114 (`plans/114-save-growth-investigation.md`) owns the shared MEMORY-05
  probe (`prevCampaign=` token); Part B links its doc when it is merged.
- **Category**: bug (per-campaign state that survives into the next campaign), docs (investigation)
- **Wave / lane**: wave 2, lane C
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (Enlistment,
  SpecialResources and HeroRace have no 1.5.x feature module at `e45f6df2c`, so the one wiring line
  stays hand-written in `Main/SubModule.cs`), D7 (co-op is not supported on 1.4.x: the co-op client
  branches in the ported code come along unchanged and untested), D8 (`v2.0.28` subjects), D10 (an
  ordinary session executes this), D19 (git through the Bash tool only)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd`; 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`; 2026-11-04
- **Issue**: Port of #656 (plan 014, Enlistment session scope) and #1035 (hero races on an old save).
  The SpecialResources half of plan 001 and the new-game race log line carry no 1.5.x issue, and the
  MEMORY-05 procedure is new work: orchestrator creates the issue(s) before implementation lands.

## Why this matters

A player who starts a new campaign, or loads another save, without restarting the game runs the
second campaign on state the first one left in process-lifetime singletons. On 1.4.x three features
still do this. Enlistment keeps the old campaign's cached commander party and army (a new campaign
never reaches its load-only reset) and four per-session latches that no session reset clears (the
town-stop dwell anchor, the shore-leave offer latch and its cooldown, the hourly army-rhythm
snapshot, the commander-loss popup latch): a soldier can be held in a town after his commander
left, never be offered shore leave, or lose his lord in silence. SpecialResources keeps the old balances and its first save writes them into the new file.
A save without race data loaded after another campaign takes that campaign's hero races (#1035),
and every new campaign logs a misleading "pre-TAOM save" warning. The 1.5.x line fixed all of it
(plans 014 and 001, #1035); this WP ports those fixes, byte-identical where the 1.4.x files match.

The same family is MEMORY-05 (verified CONFIRMED in `plans/_audit/2026-11-04-port148/verify/V07-memory.md`):
on v1.4.8 a second campaign in one process settles 1.8 GB above the first, a return to the main menu
holds 10,273 MB against a 6.6 GB cold menu, and the managed heap reads 279 to 523 MB at menu returns
against 73 to 83 MB cold. V07 also shows the ports are correctness fixes, not the memory fix: on the
1.5.x line the retention stayed after all three landed. So Part B writes the procedure that names
the real holder. Code reading already points at a strong candidate: three container-singleton
behaviors keep the finished campaign's `CampaignGameStarter` (every behavior and game model of that
campaign) reachable until the next campaign launches. The E2 player loaded 12 saves in one process
(V01), so this is not a corner case.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (`BANNERLORD_GAME_DIR`).
  Never write into the game install.
- Base branch: local `bannerlord-1.4.5` after WP101 plus every WP merged before this one; WP104 and
  WP105 must be among them.
- Worktree: `C:\Users\mikew\source\repos\taom-port\119`, branch `port148/119-memory-cross-campaign-resets`.
  The folder is outside the session's working directory, so a `cd` does not persist between Bash
  calls: start EVERY command with `cd /c/Users/mikew/source/repos/taom-port/119 &&`.
- Python: `python` (if Git Bash cannot find it, `/c/Python314/python.exe`).

### Files and roles

| File | Role in this WP |
|---|---|
| `Main/Features/Enlistment/` (10 files, listed by Step A2's command) | Plan 014: session resets on the attachment service, wait-menu presenter, rhythm snapshot and reconciler; the load, new-campaign and stop-end edges. Transplanted from `aac0e05bf` |
| `TAOM.Tests/Features/Enlistment/` (9 files, 2 new) | Plan 014 tests, transplanted from `aac0e05bf` |
| `Main/SubModule.cs` (single-owner) | One comment and one call added inside `OnGameEnd`'s best-effort teardown block |
| `docs/features/enlistment.md` | Plan 014 doc hunks (3-way apply) |
| `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` | Plan 001: null-first load, new-game wipe that reads no hero |
| `TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs` (new) | 5 plan 001 tests against real storage |
| `docs/features/special-resources.md` | Plan 001 doc hunks |
| `Main/Features/HeroRace/RacePersistenceService.cs`, `IRacePersistenceService.cs`, `RacePersistenceBehavior.cs` | `_isNewCampaign` (from `6ae924638`), `ResetForNewSession` and the constructor reset (`eb5cf0289`) |
| `TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs`, `RacePersistenceBehaviorTests.cs`, `RacePersistenceWiringTests.cs` (new) | HeroRace tests (`6ae924638`, `eb6d18431`, `eb5cf0289`) |
| `docs/features/hero-race.md` | Two diagram lines and one changelog line, by hand |
| `docs/investigations/second-game-retention-2026-11.md` (new) | Part B: the MEMORY-05 heap-snapshot procedure |

### Planning-time verification (this session; no build or test was run)

- Cherry-pick simulation `git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`
  for every pack commit: results in the Disposition table. The whole plan 014 series squashed
  (`--merge-base=6fb37c9f7^ ... aac0e05bf`) conflicts only in `CHANGELOG.md`,
  `docs/reviews/REVIEW-LOG.md`, `docs/reviews/lessons/misc.md` and `docs/reviews/lessons/testing-qa.md`.
  Plan 001 squashed (`4263535a1^` to `8ea315052`) conflicts only in `CHANGELOG.md`, `REVIEW-LOG.md`
  and three lessons files. The HeroRace chain (1.4.x plus `a0db0558b`, then `6ae924638`, then
  `eb6d18431`, then `eb5cf0289`, simulated on trees) has no conflict in any HeroRace code or test
  file; `eb6d18431` is clean (exit 0) and `eb5cf0289` conflicts only in `docs/features/hero-race.md`.
- Blob equality: every one of the 17 existing Enlistment code and test files the series touches has
  the same blob on `origin/bannerlord-1.4.5` and on `6fb37c9f7^` (the 1.5.x parent of the series), so
  `git checkout aac0e05bf <file>` gives exactly 1.4.x plus plan 014. Only `Main/SubModule.cs` and
  `docs/features/enlistment.md` differ, and both are handled without a checkout.
- The plan 014 merge `7feeec94f` and the plan 001 merge `79cab8906` resolved only bookkeeping files
  (`git show --cc --name-only` lists CHANGELOG, REVIEW-LOG and three lessons files; on `7feeec94f` it
  also lists `Main/SubModule.cs`, which merged automatically: its combined diff has no hunk), so
  `aac0e05bf` and `8ea315052` are the code end states.
- Nothing ported exists on 1.4.x yet: `ColumnLeftSettlement`, `OnStopEnded`, `OnPartyLeftSettlement`,
  `_isNewCampaign`, "Null-first on load" and `IServiceMaintenanceService>()?.ResetSessionCaches` each
  have 0 hits on `origin/bannerlord-1.4.5` and `origin/fix/public-armory-ids-145`. The CareerSystem
  half of plan 001 is already there (`f4273639` is an ancestor of `origin/bannerlord-1.4.5`;
  `CareerPersistenceBehavior.cs:33-34` resets on `OnNewGameCreatedEvent`).
- No other WP before this one ports a commit that touches the 19 Enlistment files or the HeroRace
  and SpecialResources code files above (scan of every PORT/UNSURE row in
  `plans/_audit/2026-11-04-port148/data/classified.jsonl` against `wp_map.json`). Earlier WPs do
  touch `docs/features/enlistment.md` (WP107 `c79a58521`, WP110 `ec2a3026f`),
  `docs/features/special-resources.md` (WP108 `ef6afeb67`), `docs/features/hero-race.md` (WP107
  plan 006) and `RacePersistenceBehaviorTests.cs` (WP104 `a0db0558b`), all in other regions.

### 1.4.x excerpts (`origin/bannerlord-1.4.5`)

`Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs:115-130` (the load reset is behind the co-op
gate; a new campaign never reaches it):

```csharp
    internal void OnGameLoaded(CampaignGameStarter starter)
    {
        if (!_coopSession.IsAuthority) return;

        // (the one-line comment at :119 is omitted here)
        _maintenance.ResetSessionCaches();
        _normalizer.Normalize(_playerParty.GetMainHeroId(), CampaignTime.Now.ToDays);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        // A brand-new campaign starts with no service record. SyncData(IsLoading) has NOT
        // run here, so _justLoadedFromSave is false and clearing is correct.
        if (!_justLoadedFromSave)
            _store.Clear();
    }
```

`Main/Features/Enlistment/ServiceMaintenanceService.cs:217-241`: `ResetSessionCaches` clears five
fields, calls `_attachment.InvalidateCommanderCache()` (`:224`), `_status?.Invalidate()`,
`_army?.ResetSessionCaches()` (`ArmyMembershipAdapter.cs:147-153`: `_createdArmy = null`) and
`_reconciler?.ResetForNewSession()` (`:240`). Every callee is a field clear.

`Main/SubModule.cs:668-695` (`OnGameEnd`; `OnGameStart` follows at `:697`). The block this WP extends:

```csharp
        try
        {
            IoC.Resolve<TAOM.Adapters.IMapReachAdapter>()?.Reset();
            IoC.Resolve<Features.ArmyTargeting.ITargetScoreContextFactory>()?.Reset();
        }
        catch { /* teardown is best-effort, never break OnGameEnd */ }
```

`Main/Features/SpecialResources/SpecialResourcesBehavior.cs:102-130`: `SyncData` seeds the load from
the live singleton (`:105` `var data = _storage.GetAllData();`), and `OnNewGameCreated` (`:116`,
`private`) reads `Hero.MainHero` (`:118`) and never clears storage. The constructor takes six
arguments (`:26-40`), the same as the 1.5.x tests expect.

`Main/Features/HeroRace/RacePersistenceService.cs`: `ResetForNewCampaign` `:85-95` (clears map and
legend), `RestoreHeroRaces` `:97-105` warns on every empty map, `SyncRaceData` `:168-181` clears on
load. Calls `_heroRosterAdapter.GetAllAliveHeroRaces()` at `:70` and `:109`; the 1.4.x adapter has
only that method (`Main/Adapters/IHeroRosterAdapter.cs:19`), not 1.5.x's `GetAllHeroRaces` (#1011,
WP126). `RacePersistenceBehavior.cs:9-12` (constructor stores the service only), `:18`
(`OnNewGameCreatedEvent` calls `ResetForNewCampaign`). `Main/SubModule.cs:852-853` builds it with
`new` per campaign inside `RegisterProgressionAndIdentity`, called from `OnGameStart` (`:725`).
`git grep RacePersistenceBehavior -- Main` finds only that line, the class itself, and a comment in
`CareerPersistenceBehavior.cs:32`.

### Engine facts (installed v1.4.8, `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, read this session)

| Member | Installed file:line | Why it matters |
|---|---|---|
| `Campaign.OnInitialize()` builds `new CampaignGameStarter(GameMenuManager, ConversationManager)` | `TaleWorlds.CampaignSystem.Campaign.cs:1383` | every campaign gets a new starter |
| `base.GameManager.OnGameStart(base.CurrentGame, campaignGameStarter)` | `Campaign.cs:1391` | TAOM builds `RacePersistenceBehavior` here, before any save data loads |
| saved campaign: `GameManager.OnGameLoaded` `:1427`, `InitializeCampaignBehaviors` `:1428`, `LoadBehaviorData()` `:1429` | `Campaign.cs` | constructor reset precedes `SyncData` (#1035) |
| `DoLoadingForGameType` `:1641`: saved campaign calls `OnGameLoaded(gameStarter)` `:1670`, `OnSessionStart` `:1671`; new campaign calls `OnNewGameCreated(gameStarter)` `:1694`, `OnSessionStart` `:1695` | `Campaign.cs` | a new campaign never fires `OnGameLoaded` (`e99d705a3`) |
| `public override void OnDestroy()` | `Campaign.cs:1121` | runs after the submodules' `OnGameEnd` |
| `internal void LoadBehaviorData(CampaignBehaviorBase)`: calls `SyncData` only when the save holds the behavior's StringId (or a key containing its type name) | `TaleWorlds.CampaignSystem.CampaignBehaviorDataStore.cs:86` | a save with no record for a behavior never reaches its `SyncData` |
| `BehaviorSaveData.SyncData<T>`: save `_records.Add(key, data)` `:30` (throws on a duplicate key); load returns `false` and leaves the ref unchanged on a missing key `:38` | `CampaignBehaviorDataStore.cs:26-38` | why the load must start from a null local |
| `public interface IDataStore { bool IsSaving { get; } bool IsLoading { get; } bool SyncData<T>(string key, ref T data); }` | `TaleWorlds.CampaignSystem.IDataStore.cs` | the test fakes implement exactly this |
| `OnNewGameCreatedEvent`, `OnGameLoadedEvent`, `OnSessionLaunchedEvent`: `IMbEvent<CampaignGameStarter>` | `TaleWorlds.CampaignSystem.CampaignEvents.cs:853`, `:861`, `:849` | handler shapes used |
| `OnSettlementLeftEvent`: `IMbEvent<MobileParty, Settlement>`; `OnPartyLeftArmyEvent`: `IMbEvent<MobileParty, Army>` | `CampaignEvents.cs:815`, `:1093` | the stop-end edge |
| `OnCharacterCreationIsOverEvent`: non-generic `IMbEvent` on v1.4.8 | `CampaignEvents.cs:601` | the 1.4.x parameterless seeding handler stays (1.5.x has `MbEvent<int>`) |
| `Game.Destroy()` `:246`: `GameManager.OnGameEnd(this)` `:253`, then `GameType.OnDestroy()` `:254`, `Current = null` `:260`, `Common.MemoryCleanupGC()` `:263` | `TaleWorlds.Core.Game.cs` | `SubModule.OnGameEnd` runs before `Campaign.OnDestroy` |
| `MBGameManager.OnGameEnd(Game)` calls every submodule's `OnGameEnd` (`:175`); `OnGameStart(Game, IGameStarter)` calls every submodule's `OnGameStart` (`:164`) | `TaleWorlds.MountAndBlade.MBGameManager.cs:171-180`, `:159-164` | dispatch to `SubModule` |
| `public virtual void OnGameEnd(Game game)`; `protected internal virtual void OnGameStart(Game, IGameStarter)` | `TaleWorlds.MountAndBlade.MBSubModuleBase.cs:97`, `:36` | anchors the source test reads |
| `Hero.MainHero => CharacterObject.PlayerCharacter.HeroObject`; `PlayerCharacter => Game.Current.PlayerTroop as CharacterObject` | `TaleWorlds.CampaignSystem.Hero.cs:892`, `CharacterObject.cs:250` | throws outside a game, so the plan 001 test of a hero-free wipe fails on today's code |
| `CampaignGameStarter` fields `_gameMenuManager` `:11`, `_conversationManager` `:13`, `_campaignBehaviors` (`List<CampaignBehaviorBase>`) `:15`, `_models` (`List<GameModel>`) `:17` | `TaleWorlds.CampaignSystem.CampaignGameStarter.cs` | what a retained starter keeps alive (Part B) |

### Retention candidates found by code reading (for Part B; `origin/bannerlord-1.4.5`)

- `private CampaignGameStarter _lastSessionStarter;` on `EnlistmentBehavior.cs:29`,
  `EnlistmentContentBehavior.cs:28` and `MessengerCampaignBehavior.cs:46`, all container singletons
  (`EnlistmentIoC.cs:90`, `:96`; `MessengerIoC.cs:14`; the container is the static
  `Main/IoC.cs:77` `_container`). Each is overwritten only at the next session launch
  (`EnlistmentBehavior.cs:136-138`), never cleared at game end. The same field on
  `PlayerAllianceProposalBehavior`, `CultureConversionBehavior` and `FieldCommissionBehavior` is
  harmless: those are built with `new` per campaign (`SubModule.cs:975`, `:1262`, `:1286`).
- `CommanderLordAdapter._lastSeenMapEvent` (`Main/Adapters/CommanderLordAdapter.cs:74`), singleton
  (`EnlistmentIoC.cs:13`), never cleared (the ported plan 014 `enlistment.md` text names it as not
  reset).
- `Patch70_FiefGrantDecisionSwap._faultReportedFor` (`Main/Features/FiefGranting/Hooks/Patch70_FiefGrantDecisionSwap.cs:57`),
  a static `Campaign`, set only after a Patch70 fault.
- `MarriageClanPoolCache.PoolByCulture` (`Main/Features/MarriageAlignment/MarriageClanPoolCache.cs:25`),
  a static of `Clan` lists.
- `MobilePartyAttachmentAdapter._cachedCommanderParty` (`Main/Adapters/MobilePartyAttachmentAdapter.cs:164`):
  this WP makes `SubModule.OnGameEnd` clear it.

### Conventions that bind this change

- **ADR-007 adapters**: services never touch sealed TaleWorlds types. The ported services keep that
  (the stop-end edge passes a hero id string into `OnPartyLeftSettlement`); only behaviors (entry
  points) read `MobileParty` and `CampaignEvents`.
- **ADR-002 thin entry points** (under 150 lines, delegate to services): `EnlistmentBehavior.cs`
  ends at 149 lines and `EnlistmentMaintenanceBehavior.cs` at 120. `EnlistmentMenuBehavior.cs` (160
  lines today) and `SpecialResourcesBehavior.cs` (545 today) were already over the guideline before
  this WP; do not refactor them here (edit-scope discipline).
- **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG` outside the IoC
  registration exception. None is introduced.
- **TDD**: the failing test comes first. Steps A1, A4, A6 and A8 are RED steps; ported tests land
  before the code that makes them pass.
- **`.claude/rules/csharp-architecture.md` "Singleton Services Holding Per-Campaign State MUST Have a
  Session-Reset Story"** (line 96 on 1.4.x): every singleton holding per-campaign state needs a
  `ResetForNewSession()` and tests (`*SessionResetTests`). This WP is that rule applied.
- **D2**: none of the three features has a 1.5.x module at `e45f6df2c` (no `*Module.cs` in their
  folders; the 1.5.x `OnGameEnd` call sits in `e45f6df2c:Main/SubModule.cs:883-884`), so the
  `SubModule.cs` line is hand-written here.
- **MCM json2 rule** (rename a persisted setting, never flip its default) and
  **`.claude/rules/harmony-patches.md`** (MovementOrder postfixes go through the shared deferred
  category): not touched; no setting and no patch is added.
- **Localization**: no new player-facing string (the new HeroRace line is a log line).

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/119 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may still print; ignore it) |
| Build, errors only (RED steps) | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > /c/Users/mikew/source/repos/taom-port/119-build.log 2>&1; echo "exit=$?"; grep -oE 'error CS[0-9]+' /c/Users/mikew/source/repos/taom-port/119-build.log \| sort -u; grep -E 'error CS' /c/Users/mikew/source/repos/taom-port/119-build.log \| grep -oE '[^\\ ]+\.cs\(' \| sort -u` | as each RED step says |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0`; the full suite takes minutes, so give the call a 600000 ms timeout |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Namespace>"` | read `Failed:` and `Total:` on the summary line |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links` and `orphan_features` equal Step A0.7's values; `ai_dashes: 0` |

(In the "errors only" row the `\|` is a table escape.) The exact "Build, errors only" command:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > /c/Users/mikew/source/repos/taom-port/119-build.log 2>&1; echo "exit=$?"; grep -oE 'error CS[0-9]+' /c/Users/mikew/source/repos/taom-port/119-build.log | sort -u; grep -E 'error CS' /c/Users/mikew/source/repos/taom-port/119-build.log | grep -oE '[^\\ ]+\.cs\(' | sort -u
```

It prints the exit code, the distinct error codes, then the distinct file names that carry errors
(each followed by `(`). Both `-p:` flags are
mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate the CopyModule target on
`DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game install. Never
`./build.ps1`. Never two builds at once in this worktree. Always build before a `--no-build` test run.
No ModuleData, hook or tool file changes here, so the data, hook and tool-test gates are not needed.

## Scope

**In scope** (the only files you create or modify; 32):

- Part A, Enlistment (plan 014): the 10 files under `Main/Features/Enlistment/` and the 9 under
  `TAOM.Tests/Features/Enlistment/` that `git diff --name-only 6fb37c9f7^ aac0e05bf -- Main/Features/Enlistment TAOM.Tests/Features/Enlistment`
  prints (two of them new: `EnlistmentSessionResetTests.cs`, `EnlistmentStopEndTests.cs`), and
  `docs/features/enlistment.md`.
- Part A, single-owner (this WP edits only these anchors; the integrator resolves any merge overlap,
  because WP117 and WP118 also edit this file in parallel wave 2 lanes): `Main/SubModule.cs`, exactly
  two lines inserted in `OnGameEnd`. No other edit. Integrator note: WP117 adds its own
  `MapSessionHooks.EndSession("gameEnd")` block at the end of `OnGameEnd`. Both `OnGameEnd` additions
  (WP117's and this WP's `ResetSessionCaches` call) must survive the merge.
- Part A, SpecialResources (plan 001): `Main/Features/SpecialResources/SpecialResourcesBehavior.cs`,
  `TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs` (new),
  `docs/features/special-resources.md`.
- Part A, HeroRace: `Main/Features/HeroRace/RacePersistenceService.cs`, `IRacePersistenceService.cs`,
  `RacePersistenceBehavior.cs`; `TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs`,
  `RacePersistenceBehaviorTests.cs`, `RacePersistenceWiringTests.cs` (new); `docs/features/hero-race.md`.
- Part B: `docs/investigations/second-game-retention-2026-11.md` (new).

**Out of scope** (do not touch):

- `CHANGELOG.md` (D1); `docs/reviews/**` (the 1.5.x review records, Codex prompts, RCAs,
  `REVIEW-LOG.md` and lessons stay behind; WP142 syncs lessons); `.claude/**`, `CLAUDE.md`,
  `AGENTS.md`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md`; `docs/adrs/**`;
  `plans/README.md` (integrator).
- `Main/IoC.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`, `Directory.Build.props`,
  `Dependencies/**`: not needed (the changed services are container-resolved; SDK globbing picks up
  new files).
- Every other hunk of `6ae924638` (NRE finalizers: WP112; AutoResolve removal: WP123; career quest:
  WP129; Fire and Tribute, LastSeen, Patch123: 1.5.x-only), including
  `Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs` (WP112).
- `f981629cd` (#1011, `GetAllHeroRaces` rename and dead-hero races): WP126.
- Any other Enlistment file, in particular `EnlistmentMeritMissionBehavior.cs` and
  `EnlistmentBattleBehavior.cs` (other WPs). Never check out a directory; name every file.
- Any retention fix (clearing `_lastSessionStarter`, `_lastSeenMapEvent`, statics): a follow-up after
  Part B's procedure has run.
- The SpecialResources "no record" load gap (still open on 1.5.x at `e45f6df2c`; documented, deferred).
- MCM settings, ModuleData, save-format changes.

## Git workflow

- Create the worktree from the main checkout (Step A0.1); work and commit only there, on
  `port148/119-memory-cross-campaign-resets`. Never push, open a PR, tag, or merge into
  `bannerlord-1.4.5`. Run git only through the Bash tool (D19). Never touch the main checkout's
  working tree or its stashes (other sessions' uncommitted files live there).
- Port mechanics, instead of replaying each commit with `git cherry-pick -n`: the plan 014 and plan
  001 follow-ups carry review records, RCAs, lessons and CHANGELOG hunks, `6ae924638` carries other
  WPs' hunks, and a pick-by-pick replay would need bookkeeping cleanup after nearly every commit. A
  path-limited squash stages exactly what the replay plus the per-port drop would leave (the
  squashed simulations above prove it). Both mechanics stage the result and never create a commit:
  - **Path checkout** where the 1.4.x blob equals the 1.5.x parent blob (verified in Step A0.4):
    `git checkout <sha> <files>`. Byte-identical by construction.
  - **Path-limited apply** everywhere else: `git diff <from> <to> -- <paths> | git apply --3way`. It
    applies only the named paths' hunks, stages them, and merges against the 1.5.x preimage blob when
    the context differs. For a whole-commit diff with no bookkeeping (`eb6d18431`) it equals
    `git cherry-pick -n`. Check `echo "exit=$?"` (0) and `git diff --name-only --diff-filter=U`
    (empty), never the message text.
  - Name every file; never check out or apply a directory you did not list.
- Bookkeeping hunks of every pack commit (CHANGELOG, review records, RCAs, prompts, REVIEW-LOG,
  lessons) are never applied (D1, per-port procedure step 2).
- Stage with explicit `git add <paths>` (never `-A`, `-u` or `.`), then run `git commit -F <file>` in
  the NEXT sequential Bash call: never the same command, never a parallel sibling call. Write each
  message with the Write tool to the file named in its step (outside the worktree, so it can never be
  staged) and pass the path to git in `C:/...` form.
- Never `--no-verify`, `--amend`, `-C` or `-c`. If a hook denies a commit, STOP and report its
  message verbatim.
- Commit message rules: subject `<type>(<scope>): v2.0.28 - <description>`, at most 72 characters; a
  body for 1.4.x readers wrapped at 72 (bodies become player release notes under plan 020), no long
  dashes, no `Co-Authored-By` and no "Generated with" line; trailers `Research:`, `Not-tested:`,
  `Save-compat:` as given; one `Ported-from: <40-hex sha>` per included 1.5.x commit. The exact
  messages are in Steps A3, A5, A7, A9 and B2.

## Part A: port the three session-reset fixes

### Step A0: worktree, prerequisites, baseline

**A0.1 Worktree.**

```bash
cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/119 -b port148/119-memory-cross-campaign-resets bannerlord-1.4.5
```

**Verify**: prints `Preparing worktree (new branch 'port148/119-memory-cross-campaign-resets')`. If
the branch or the folder already exists: STOP.

**A0.2 Drift check** (the command in the header, run inside the worktree). Changes by earlier WPs
are expected in: `Main/SubModule.cs` (WP104, WP105, WP114 and others),
`TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs` and `HeroRaceWiringTests.cs` (WP104),
`Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs` (WP106),
`Main/Features/Enlistment/Hooks/EnlistmentBattleBehavior.cs` (WP110, WP112),
`Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs` (WP112), the three
feature docs (WP107, WP108, WP110), `docs/investigations/save-growth-2026-11.md` (WP114). Any other
changed path: read it, then let A0.4 and A0.5 decide.

**A0.3 Prerequisites (WP104, WP105).**

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && test -f Main/Composition/FeatureModules.cs && echo composition-ok; \
grep -cF 'public static string RepoPath(params string[] parts)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static IEnumerable<string> CSharpFiles(params string[] roots)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static bool MentionsOutsideComments(string path, string needle, bool wholeWord = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'public static readonly string[] CodeRoots = { "Main" };' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'using TAOM.Tests.Infrastructure;' TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs; \
grep -c 'GetAllHeroRaces()' Main/Adapters/IHeroRosterAdapter.cs
```

**Verify**: `composition-ok`, then `1` on each of the next five lines. The seventh line prints `1`
(or `0`: record it; Step A6 then adds that `using`). The last line prints `0` (or `1`: WP126 merged
early; record it; Step A8 then skips its adaptation). Any other value on the first six: STOP (WP104
or WP105 is not merged).

**A0.4 Enlistment blobs.**

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && \
for p in $(git diff --name-only 6fb37c9f7^ aac0e05bf -- Main/Features/Enlistment TAOM.Tests/Features/Enlistment); do \
  if git cat-file -e "6fb37c9f7^:$p" 2>/dev/null; then \
    [ "$(git rev-parse "HEAD:$p")" = "$(git rev-parse "6fb37c9f7^:$p")" ] && echo "same   $p" || echo "DRIFT  $p"; \
  else test -e "$p" && echo "EXISTS $p" || echo "new    $p"; fi; done
```

**Verify**: 17 `same` lines and 2 `new` lines (`TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs`,
`TAOM.Tests/Features/Enlistment/EnlistmentStopEndTests.cs`). Any `DRIFT` or `EXISTS`: STOP and report
the line (the path checkout would overwrite another WP's change).

**A0.5 Anchors.**

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && for a in \
 'public override void OnGameEnd(Game game)' \
 'IoC.Resolve<Features.ArmyTargeting.ITargetScoreContextFactory>()?.Reset();' \
 'protected override void OnGameStart(Game game, IGameStarter gameStarterObject)' \
 'IServiceMaintenanceService>()?.ResetSessionCaches()' \
 'campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));' \
 ; do printf '%s:%s  %s\n' "$(grep -cF "$a" Main/SubModule.cs)" "$(grep -nF "$a" Main/SubModule.cs | head -1 | cut -d: -f1)" "$a"; done; \
grep -cF 'var data = _storage.GetAllData();' Main/Features/SpecialResources/SpecialResourcesBehavior.cs; \
grep -cF 'private void OnNewGameCreated(CampaignGameStarter starter)' Main/Features/SpecialResources/SpecialResourcesBehavior.cs; \
grep -c '_isNewCampaign\|ResetForNewSession' Main/Features/HeroRace/RacePersistenceService.cs Main/Features/HeroRace/IRacePersistenceService.cs Main/Features/HeroRace/RacePersistenceBehavior.cs; \
ls TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceWiringTests.cs 2>&1 | grep -c 'No such file'; \
git grep -n 'RacePersistenceBehavior' -- Main | grep -v '^Main/Features/HeroRace/RacePersistenceBehavior.cs:'
```

**Verify**:
- `1:<a>` (OnGameEnd), `1:<b>` (ArmyTargeting line), `1:<c>` (OnGameStart) with `a < b < c`;
  `0:` for the `IServiceMaintenanceService` call; `1:` for the `RacePersistenceBehavior` line.
- `1`, then `1`.
- each of the three HeroRace files `:0`.
- `2`.
- exactly two lines: `Main/Features/CareerSystem/CareerPersistenceBehavior.cs:32:` (a comment) and
  the `Main/SubModule.cs` `AddBehavior` line.

Anything else: STOP (a port already landed, or a holder moved).

**A0.6 Baseline.** Run Restore, Build, Test. **Verify**: build exit 0; `Failed: 0`. Record `Total`,
`Passed`, `Skipped` as **T0** (the WP101 baseline was 9154 / 9151 / 3; later WPs added tests, so T0
is higher and should equal the latest count the integrator recorded in `plans/README.md`). Then run
Filtered three times, with `TAOM.Tests.Features.Enlistment`, `TAOM.Tests.Features.SpecialResources`
and `TAOM.Tests.Features.HeroRace`. Record each `Total` as **E0**, **S0**, **H0**; each must say
`Failed: 0`. Any failure: STOP.

**A0.7 Docs baseline.** Run the Docs command. Record `dead_links` and `orphan_features`.

### Step A1 (RED): Enlistment tests (plan 014)

Sources: the nine test files at `aac0e05bf` (the series end state; see Disposition).

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && \
T=$(git diff --name-only 6fb37c9f7^ aac0e05bf -- TAOM.Tests/Features/Enlistment) && echo "$T" | wc -l && \
git checkout aac0e05bf $T && git status --porcelain
```

**Verify**: `9`, then nine staged lines: seven `M ` and two `A ` (`EnlistmentSessionResetTests.cs`,
`EnlistmentStopEndTests.cs`), nothing else.

Run "Build, errors only". **Verify** (RED): exit non-zero; the error codes are a subset of `CS0122`,
`CS1061`, `CS1729`; every file named is one of `EnlistmentSessionResetTests.cs`,
`EnlistmentStopEndTests.cs`, `EnlistmentWaitMenuPresenterTests.cs`, `SettlementFollowingTests.cs`,
`ServiceMaintenanceServiceTests.cs`, `EnlistmentPumpAuthorityTests.cs`. (They name the missing
`ResetForNewSession`, `OnStopEnded`, `ColumnLeftSettlement`, `OnPartyLeftSettlement`, the private
`OnNewGameCreated`, and the new constructor arity.) An error in any other file, in `Main`, or a
green build: STOP.

### Step A2 (GREEN): Enlistment code and the game-end call

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && \
P=$(git diff --name-only 6fb37c9f7^ aac0e05bf -- Main/Features/Enlistment) && echo "$P" | wc -l && \
git checkout aac0e05bf $P && git diff --cached --name-only | wc -l
```

**Verify**: `10`, then `19`.

Then add the game-end call (`a67792c40`; the 1.5.x text is `e45f6df2c:Main/SubModule.cs:883-884`).
Read `C:\Users\mikew\source\repos\taom-port\119\Main\SubModule.cs` around `OnGameEnd`, then use the
Edit tool: old_string is the single line

```
            IoC.Resolve<Features.ArmyTargeting.ITargetScoreContextFactory>()?.Reset();
```

and new_string is that same line followed by exactly these two lines (12 spaces of indentation):

```
            IoC.Resolve<Features.ArmyTargeting.ITargetScoreContextFactory>()?.Reset();
            // Enlistment's singletons cache the commander MobileParty and an Army (field clears only).
            IoC.Resolve<Features.Enlistment.IServiceMaintenanceService>()?.ResetSessionCaches();
```

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && grep -n -B3 -A3 'IServiceMaintenanceService>()?.ResetSessionCaches();' Main/SubModule.cs; git diff --stat -- Main/SubModule.cs
```

**Verify**: the new call sits directly after the `ITargetScoreContextFactory` line, inside the `try`,
followed by `}` and `catch { /* teardown is best-effort, never break OnGameEnd */ }`; the diff stat is
`2 insertions(+)`.

Run Build: exit 0, 0 errors. Run Filtered with `TAOM.Tests.Features.Enlistment`: `Failed: 0` and
`Total` = E0 + 31. Then:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && git diff --quiet aac0e05bf -- $(git diff --name-only 6fb37c9f7^ aac0e05bf -- Main/Features/Enlistment TAOM.Tests/Features/Enlistment) && echo same
```

**Verify**: `same`. If `GameEnd_CallsTheEnlistmentSessionReset` fails, the `SubModule.cs` edit is
missing or misplaced: fix the placement, never the test (two failed attempts: STOP). Any other
failure: STOP (the transplanted files are byte-identical to the reviewed 1.5.x end state, so another
failure means the base differs from what this plan assumed).

### Step A3: Enlistment doc, then commit A

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && git diff 6fb37c9f7^ aac0e05bf -- docs/features/enlistment.md | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U; grep -c 'plan 014' docs/features/enlistment.md; grep -c '^<<<<<<<\|^>>>>>>>' docs/features/enlistment.md
```

**Verify**: `exit=0`; no unmerged path; `4`; `0`. If instead `exit=1` and the doc is listed as
unmerged: another WP edited a paragraph this series also edits. Open the file, keep both sides' text
in each conflict (the other WP's lines first, then the plan 014 lines), delete the markers,
`git add docs/features/enlistment.md`, and re-run the two `grep` checks. If one conflict is a
paragraph both sides rewrote differently: STOP.

Stage:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git add Main/SubModule.cs docs/features/enlistment.md && git status --porcelain | wc -l && git status --porcelain | grep -v '^[MA] '
```

**Verify**: `21`, and the second command prints nothing.

Write `C:\Users\mikew\source\repos\taom-port\119-msg-A.txt` with exactly:

```text
fix(enlistment): v2.0.28 - session caches reset on load, new game, exit

Enlistment keeps per-session state on objects that live as long as
the game process. Starting a second campaign without restarting the
game, or loading an earlier save, could keep the last session's
commander party and army, the town-stop timer, the shore-leave offer
and its once-a-day cooldown, the army-rhythm snapshot and the
commander-loss popup flag. A soldier could be held in a town after
his commander left, never be offered shore leave, or lose his lord
without a word.

All of that is now cleared on every load, on a new campaign and when
the game returns to the main menu. A save with no enlistment data
loads with no service record instead of the previous session's. The
shore-leave offer comes once per town stop again: the stop ends when
the commander leaves town. A discharge also re-arms the
commander-loss popup.

Research: taom-src v1.4.8 Campaign.DoLoadingForGameType (OnGameLoaded
:1670 for a save, OnNewGameCreated :1694 for a new campaign),
CampaignBehaviorDataStore.LoadBehaviorData :86, Game.Destroy
(OnGameEnd :253 before Campaign.OnDestroy :254)
Not-tested: a second campaign in one process and an earlier-save load
while enlisted (in-game checks in plans/119)
Save-compat: no save field added or changed
Ported-from: 6fb37c9f7114a6b133ab73e88edfff09bfabaafe
Ported-from: f6ac2d335fd6373eda7d8006be501ddd34279e50
Ported-from: e99d705a3f2d3ad924f200ff3bb3a7a6c90f670e
Ported-from: 74fb3d35e44f70f008d6d35069880d9319d20c04
Ported-from: b9d18458ed73ce8d4b48c86ec2ec7e4e48c8a125
Ported-from: 41754a03e8abe41f977fe224193c94f0f7f0e219
Ported-from: a67792c408042b3653f1615511ab138286ea5aae
Ported-from: fd61757bf342f67975f730f3ff39bef014afa855
Ported-from: aac0e05bff28c70cdba160656f738e77f2c7a2b7
```

Then, in the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git commit -F C:/Users/mikew/source/repos/taom-port/119-msg-A.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: one new commit with that subject; `git status --porcelain` prints nothing.

### Step A4 (RED): SpecialResources test (plan 001)

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && git checkout 8ea315052 TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs && \
sed -i 's/(v1\.5\.3 <c>BehaviorSaveData\.SyncData<\/c>)/(v1.4.8 <c>BehaviorSaveData.SyncData<\/c>)/' TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs && \
git add TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs && \
grep -c 'v1.4.8 <c>BehaviorSaveData.SyncData' TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs; \
grep -c 'v1\.5' TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs; \
grep -c '\[TestMethod\]' TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs
```

**Verify**: `1`, `0`, `5`. (The re-cite is the per-port rule: the fake mirrors installed v1.4.8
`BehaviorSaveData.SyncData`, `CampaignBehaviorDataStore.cs:26-38`, which behaves as the comment says.)

Run "Build, errors only". **Verify** (RED): exit non-zero; codes `CS0122` only; the only file named
is `SpecialResourcesBehaviorSessionResetTests.cs` (`OnNewGameCreated` is `private` today). Anything
else: STOP.

### Step A5 (GREEN): SpecialResources code and doc, then commit B

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && git diff 4263535a1^ 8ea315052 -- Main/Features/SpecialResources/SpecialResourcesBehavior.cs docs/features/special-resources.md | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U; \
grep -c 'Dictionary<string, float> data = null;' Main/Features/SpecialResources/SpecialResourcesBehavior.cs; \
grep -c 'internal void OnNewGameCreated(CampaignGameStarter starter)' Main/Features/SpecialResources/SpecialResourcesBehavior.cs; \
awk '/internal void OnNewGameCreated/,/^    }/' Main/Features/SpecialResources/SpecialResourcesBehavior.cs | grep -c 'var hero = Hero.MainHero;'; \
grep -c 'plan 001' docs/features/special-resources.md
```

**Verify**: `exit=0`; no unmerged path; `1`; `1`; `0`; `3`. The target shape (from
`8ea315052:Main/Features/SpecialResources/SpecialResourcesBehavior.cs:104-146`): `SyncData` returns
early after saving the live dictionary; the load reads into `Dictionary<string, float> data = null;`
then `_storage.RestoreData(data)`; `OnNewGameCreated` is `internal`, starts with
`_storage.RestoreData(null);`, then `_service.ResetSessionState();` and `_isFirstTickAfterLoad = true;`.
The 1.4.x-only ScreenManager subscriber block in `RegisterEvents` and the parameterless
`OnCharacterCreationIsOver` must be unchanged (`git diff --cached -- Main/Features/SpecialResources/SpecialResourcesBehavior.cs`
touches only `SyncData` and `OnNewGameCreated`). A conflict in the C# file: STOP. A conflict in the
doc: resolve as in Step A3 (keep both sides), then `git add` it.

Run Build: 0 errors. Filtered with `TAOM.Tests.Features.SpecialResources`: `Failed: 0`, `Total` =
S0 + 5. Stage:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git add Main/Features/SpecialResources/SpecialResourcesBehavior.cs TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs docs/features/special-resources.md && git status --porcelain
```

**Verify**: exactly three lines (`M ` the behavior, `A ` the test, `M ` the doc).

Write `C:\Users\mikew\source\repos\taom-port\119-msg-B.txt` with exactly:

```text
fix(specres): v2.0.28 - a new campaign no longer keeps the old balances

Special resource balances are kept by an object that lives as long as
the game process. A second campaign started without restarting the
game kept the first campaign's balances, and its first save wrote
them into the new save file.

A new campaign now empties the balances before character creation
gives the new hero a starting amount, and loading reads the saved
balances into an empty value, so a save record without them loads
empty instead of keeping the previous campaign's. Saving is unchanged.
Still open: a save made before special resources existed, loaded
after another campaign, keeps that campaign's balances (feature doc).

This finishes plan 001 on this line; its career half is f4273639.

Research: taom-src v1.4.8 CampaignBehaviorDataStore.LoadBehaviorData
:86 (SyncData only for a behavior the save holds) and
BehaviorSaveData.SyncData :26-38 (a missing key leaves the ref alone)
Not-tested: a second campaign in one process (see plans/119)
Save-compat: no save field added; the key and its format are unchanged
Ported-from: 4263535a1d60353a65827700b15d57e4c70e4fc7
Ported-from: 8ea3150522e642ca39ece0b8c4c7a1ba273a2c5e
```

NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git commit -F C:/Users/mikew/source/repos/taom-port/119-msg-B.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: the new commit; `git status --porcelain` empty.

### Step A6 (RED): HeroRace new-game log tests (`6ae924638` test hunk, `eb6d18431`)

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && \
git diff 6ae924638^ 6ae924638 -- TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs | git apply --3way; echo "exit=$?"; \
git diff eb6d18431^ eb6d18431 -- TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs | git apply --3way; echo "exit=$?"; \
git diff --name-only --diff-filter=U; git status --porcelain
```

**Verify**: `exit=0` twice; no unmerged path; two `M ` lines (the two test files). If Step A0.3
printed `0` for the `using TAOM.Tests.Infrastructure;` line: read
`C:\Users\mikew\source\repos\taom-port\119\TAOM.Tests\Features\HeroRace\RacePersistenceBehaviorTests.cs`
and, with the Edit tool, add the line `using TAOM.Tests.Infrastructure;` directly after
`using TAOM.Features.HeroRace;`, then `git add` that file.

Run Build: exit 0 (these tests compile against today's API). Run Filtered with
`TAOM.Tests.Features.HeroRace`. **Verify** (RED): `Total` = H0 + 4 and exactly one failed test,
`RestoreHeroRaces_NewCampaign_LogsInfoNotWarning` (today's code warns on a new campaign's empty map).
Any other failure, or none: STOP.

### Step A7 (GREEN): HeroRace `_isNewCampaign`, then commit C1

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && git diff 6ae924638^ 6ae924638 -- Main/Features/HeroRace/RacePersistenceService.cs | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U; grep -c '_isNewCampaign' Main/Features/HeroRace/RacePersistenceService.cs
```

**Verify**: `exit=0`; nothing unmerged; `4` (the field, `= true` in `ResetForNewCampaign`, the
`if (_isNewCampaign)` early return in `RestoreHeroRaces` with the log line
`RacePersistenceService: new campaign, no race data to restore yet.`, and `= false` on the load
branch of `SyncRaceData`). A conflict here: STOP.

Run Build: 0 errors. Filtered `TAOM.Tests.Features.HeroRace`: `Failed: 0`, `Total` = H0 + 4. Stage:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git add Main/Features/HeroRace/RacePersistenceService.cs TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs && git status --porcelain
```

**Verify**: exactly those three `M ` lines.

Write `C:\Users\mikew\source\repos\taom-port\119-msg-C1.txt` with exactly:

```text
fix(hero-race): v2.0.28 - a new game no longer warns of a pre-TAOM save

Every new campaign logged a warning that no saved race data was found
and that the save might predate TAOM. A new campaign has no race data
yet, so it now logs an information line instead. A loaded save with
no race data still warns, also when it is loaded after a new campaign
in the same session. Ported from the 1.5.x line (only the
race-persistence part of 6ae924638).

Research: taom-src v1.4.8 CampaignBehaviorDataStore.LoadBehaviorData
:86 (a save without the behavior's record gets no SyncData call)
Not-tested: the log line in a live new campaign (plans/119 checks)
Ported-from: 6ae924638ca76cd3296acc0aad70399fbfb721a3
Ported-from: eb6d1843197a1c005ce7a536a21cff8ee39d5a13
```

NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git commit -F C:/Users/mikew/source/repos/taom-port/119-msg-C1.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: the new commit; `git status --porcelain` empty.

### Step A8 (RED): #1035 tests (`eb5cf0289` test hunks)

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && git diff eb5cf0289^ eb5cf0289 -- TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs | git apply --3way; echo "exit=$?"; git checkout eb5cf0289 TAOM.Tests/Features/HeroRace/RacePersistenceWiringTests.cs; git diff --name-only --diff-filter=U; git status --porcelain; grep -n 'GetAllHeroRaces()' TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs
```

(The new wiring test comes by path checkout: it does not exist on 1.4.x, and it is the file
`eb5cf0289` created, unchanged since on 1.5.x up to `e45f6df2c`.)

**Verify**: `exit=0`; nothing unmerged; `M ` for the two test files and `A ` for
`RacePersistenceWiringTests.cs`; exactly one `GetAllHeroRaces()` line (the new test
`ResetForNewSession_AfterAPreviousCampaign_LeavesNoRacesAndWarnsOnAnEmptyRestore`).

Adaptation (only if Step A0.3's last line printed `0`, the usual case): 1.4.x has no
`IHeroRosterAdapter.GetAllHeroRaces` (#1011 is WP126's), so:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && sed -i 's/_heroRosterAdapter\.GetAllHeroRaces()\.Returns(/_heroRosterAdapter.GetAllAliveHeroRaces().Returns(/' TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs && git add TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs && grep -c 'GetAllHeroRaces()' TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs
```

**Verify**: `0`.

Run "Build, errors only". **Verify** (RED): exit non-zero; codes `CS1061` only (`ResetForNewSession`
missing); files named are a subset of `RacePersistenceBehaviorTests.cs`, `RacePersistenceServiceTests.cs`.
Anything else: STOP.

### Step A9 (GREEN): #1035 code and doc, then commit C2

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && export MSYS_NO_PATHCONV=1 && git diff eb5cf0289^ eb5cf0289 -- Main/Features/HeroRace/IRacePersistenceService.cs Main/Features/HeroRace/RacePersistenceBehavior.cs Main/Features/HeroRace/RacePersistenceService.cs | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U; \
grep -c '_isNewCampaign' Main/Features/HeroRace/RacePersistenceService.cs; grep -c '_service.ResetForNewSession();' Main/Features/HeroRace/RacePersistenceBehavior.cs; grep -c 'void ResetForNewSession();' Main/Features/HeroRace/IRacePersistenceService.cs
```

**Verify**: `exit=0`; nothing unmerged; `5`; `1`; `1`. A conflict in any of the three: STOP.

Doc (by hand; the 3-way apply of this hunk conflicts with 1.4.x's different changelog head). Read
`C:\Users\mikew\source\repos\taom-port\119\docs\features\hero-race.md`, then two Edit-tool edits:

1. In the architecture diagram (a fenced block), replace the line
   `RacePersistenceBehavior (CampaignBehaviorBase)` with these three lines (the `eb5cf0289` hunk,
   verbatim; the line that follows, `    |-> OnBeforeSave -> RacePersistenceService.CaptureHeroRaces()`,
   stays):

```
RacePersistenceBehavior (CampaignBehaviorBase, built with `new` per campaign in SubModule.OnGameStart)
    |-> constructor -> ResetForNewSession()   clears map + legend + new-campaign flag (#1035)
    |-> OnNewGameCreated -> ResetForNewCampaign()   marks the campaign new (info, not the pre-TAOM warning)
```

2. Directly under the `## Changelog` heading, insert as the new first item (replace `<YYYY-MM-DD>`
   with today's date):

```
- <YYYY-MM-DD>: #1035 (port of the 1.5.x fix), `RacePersistenceBehavior`'s constructor calls `ResetForNewSession()`, so a save with no record for the behavior (the engine then skips its `SyncData`) no longer inherits the previous campaign's race map in the same process. A new game also logs an information line instead of the pre-TAOM save warning.
```

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && grep -c '#1035' docs/features/hero-race.md; grep -c '<YYYY-MM-DD>' docs/features/hero-race.md; git diff --stat -- docs/features/hero-race.md
```

**Verify**: `2`; `0`; `4 insertions(+), 1 deletion(-)`.

Run Build: 0 errors. Filtered `TAOM.Tests.Features.HeroRace`: `Failed: 0`, `Total` = H0 + 13 (+4 from
Step A6, +9 here: one behavior test, one service test, and `RacePersistenceWiringTests` with three
methods plus one data method of four rows). Stage:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git add Main/Features/HeroRace/IRacePersistenceService.cs Main/Features/HeroRace/RacePersistenceBehavior.cs Main/Features/HeroRace/RacePersistenceService.cs TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceWiringTests.cs docs/features/hero-race.md && git status --porcelain
```

**Verify**: seven lines (six `M `, one `A ` for the wiring test).

Write `C:\Users\mikew\source\repos\taom-port\119-msg-C2.txt` with exactly:

```text
fix(hero-race): v2.0.28 - old saves no longer take the last game's races

Loading a save made before TAOM tracked races, after playing another
campaign in the same session, gave that save's heroes the races of
the previous campaign's heroes with the same ids. The game skips the
race data load for a save that holds none, so nothing cleared the old
map. The race persistence now resets itself at the start of every
campaign, before any save data loads (#1035).

Research: taom-src v1.4.8 Campaign.OnInitialize (GameManager.OnGameStart
:1391 builds the behavior before LoadBehaviorData :1429),
MBGameManager.OnGameStart :159-164
Not-tested: a pre-TAOM save loaded after another campaign in one session
Ported-from: eb5cf02898fd2ccec2e3fb0429c99168e30d2af7
```

NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git commit -F C:/Users/mikew/source/repos/taom-port/119-msg-C2.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: the new commit; `git status --porcelain` empty.

### Step A10: Part A verification

Run Build and the full Test. **Verify**: build 0 errors; `Failed: 0`; `Total` = T0 + 49 (31
Enlistment, 5 SpecialResources, 13 HeroRace); `Skipped` = T0's. Run the Docs command: `dead_links` and
`orphan_features` equal Step A0.7's values; `ai_dashes: 0`. A failure: fix within the in-scope files
(the ported files are byte-identical to reviewed 1.5.x states, so first check the steps' placements);
two failed attempts: STOP.

### Part A done criteria

- [ ] Four commits on the branch with the subjects of Steps A3, A5, A7, A9.
- [ ] Full suite `Failed: 0`, `Total` = T0 + 49.
- [ ] `git diff --quiet aac0e05bf HEAD -- $(git diff --name-only 6fb37c9f7^ aac0e05bf -- Main/Features/Enlistment TAOM.Tests/Features/Enlistment) && echo same`
      prints `same` (run with `export MSYS_NO_PATHCONV=1`).
- [ ] `grep -c 'IServiceMaintenanceService>()?.ResetSessionCaches();' Main/SubModule.cs` prints `1`.
- [ ] `git diff 8ea315052 HEAD -- TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs | grep -cE '^[-+][^-+]'`
      prints `2` (only the v1.4.8 re-cite).
- [ ] `git diff --quiet eb5cf0289 HEAD -- TAOM.Tests/Features/HeroRace/RacePersistenceWiringTests.cs && echo same` prints `same`.
- [ ] `grep -c '_isNewCampaign' Main/Features/HeroRace/RacePersistenceService.cs` prints `5`.

## Part B: the MEMORY-05 heap-snapshot procedure (new work, docs only)

Part B adds no code and no fix. The probe is WP114's (`prevCampaign=`); this doc tells the
maintainer what to capture, how to compare, and what each result means.

### Step B1: confirm the holders, then write the doc

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && \
git grep -n -F -e 'private CampaignGameStarter _lastSessionStarter;' -- Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs Main/Features/Enlistment/Hooks/EnlistmentContentBehavior.cs Main/Features/Messengers/MessengerCampaignBehavior.cs; \
git grep -n -F -e 'container.Register<EnlistmentBehavior>(Reuse.Singleton);' -e 'container.Register<EnlistmentContentBehavior>(Reuse.Singleton);' -e 'container.Register<MessengerCampaignBehavior>(Reuse.Singleton);' -e 'container.Register<ICommanderLordAdapter, CommanderLordAdapter>(Reuse.Singleton);' -- Main; \
git grep -n -F -e 'private MapEvent _lastSeenMapEvent;' -e 'private static Campaign _faultReportedFor;' -e 'private static readonly Dictionary<string, MBReadOnlyList<Clan>> PoolByCulture' -e 'private MobileParty _cachedCommanderParty;' -e 'private static IContainer _container;' -- Main; \
test -f docs/investigations/save-growth-2026-11.md && echo wp114-doc-present || echo wp114-doc-absent
```

**Verify**: three `_lastSessionStarter` lines (planning-time: `EnlistmentBehavior.cs:29`,
`EnlistmentContentBehavior.cs:28`, `MessengerCampaignBehavior.cs:46`); four registration lines
(`EnlistmentIoC.cs:90`, `:96`, `:13`; `MessengerIoC.cs:14`); five field lines (`CommanderLordAdapter.cs:74`,
`Patch70_FiefGrantDecisionSwap.cs:57`, `MarriageClanPoolCache.cs:25`, `MobilePartyAttachmentAdapter.cs:164`,
`IoC.cs:77`); then `wp114-doc-present` or `wp114-doc-absent` (record it). Line numbers may differ;
a missing line: STOP (a holder changed; report it).

Create `docs/investigations/second-game-retention-2026-11.md` with exactly the content below. Replace
the line `<WP114 BULLET>` with the first of these two lines if B1 printed `wp114-doc-present`, or with
the second if it printed `wp114-doc-absent` (copy the chosen line exactly, backticks included):

```text
- Since plan 114 Part B, the `[SaveLoad]` `GameLoaded` and `GameInitializationFinished` lines carry `prevCampaign=alive|collected gen2Since=N`: a weak reference to the campaign that ended last. `alive` with `gen2Since` above 0 means the finished `Campaign` object itself is still reachable. See [save-growth-2026-11.md](save-growth-2026-11.md).
- Once plan 114 Part B is merged, the `[SaveLoad]` `GameLoaded` and `GameInitializationFinished` lines carry `prevCampaign=alive|collected gen2Since=N`: a weak reference to the campaign that ended last. `alive` with `gen2Since` above 0 means the finished `Campaign` object itself is still reachable.
```

~~~~markdown
# What a second game in one process keeps (MEMORY-05): heap snapshot procedure

> Hand-off: read this, run the procedure once on a deployed build of the 1.4.x line, and attach the
> numbers and root paths to the MEMORY-05 issue. Trigger: the 2026-11-04 port audit, finding MEMORY-05
> (`plans/_audit/2026-11-04-port148/deepdive/memory.md`), verified in
> `plans/_audit/2026-11-04-port148/verify/V07-memory.md`. Plan 119 ported the 1.5.x session resets
> (Enlistment plan 014, SpecialResources plan 001, hero races #1035) as correctness fixes. On the 1.5.x
> line the managed retention stayed after those fixes, so no memory fix is written until a snapshot
> names the holder.

## What is known

| Measurement | Value | Source |
|---|---|---|
| Process at the main menu after a campaign, v1.4.8 | 10,273 MB private against a cold menu of about 6.6 GB | [native-commit-audit-2026-08.md](native-commit-audit-2026-08.md), run 2 |
| Second campaign in one process, v1.4.8 | settles 1.8 GB above the first (14,720 against 12,9xx MB) | same |
| Managed heap (`heapMB`) at a return to the main menu | 523 MB on entry (2026-09-12, v1.4.8); on the 1.5.x line (v1.5.3) after the resets, seven returns entered at 300 to 393 MB and two at about 100 MB; a cold menu reads 73 to 83 MB | the 523 MB row: `plans/_audit/2026-11-04-port148/deepdive/memory.md`, MEMORY-05; the other rows: V07, section 3.2 |

The native part (engine allocator pools) is probably out of TAOM's reach. The managed part means
something still references objects of the finished campaign.

## Candidate holders found by reading the code

Read on `origin/bannerlord-1.4.5` on 2026-11-04. Each is reachable from a static root: TAOM's DryIoc
container is a static field (`_container` in `Main/IoC.cs`), and its `Reuse.Singleton` services live
for the whole process.

| Holder | Where | What it keeps reachable | Cleared |
|---|---|---|---|
| `_lastSessionStarter` on three container singletons | `EnlistmentBehavior` and `EnlistmentContentBehavior` (`Main/Features/Enlistment/Hooks/`), `MessengerCampaignBehavior` (`Main/Features/Messengers/`); registered `Reuse.Singleton` in `EnlistmentIoC.cs` and `MessengerIoC.cs` | the finished campaign's `CampaignGameStarter`, which holds that campaign's menu manager, conversation manager, every campaign behavior and every game model (installed v1.4.8 `TaleWorlds.CampaignSystem.CampaignGameStarter`, fields `_gameMenuManager`, `_conversationManager`, `_campaignBehaviors`, `_models`) | only when the next campaign's session launches |
| `CommanderLordAdapter._lastSeenMapEvent` (container singleton) | `Main/Adapters/CommanderLordAdapter.cs` | one finished `MapEvent` and the parties in it | never |
| `Patch70_FiefGrantDecisionSwap._faultReportedFor` (static) | `Main/Features/FiefGranting/Hooks/` | a whole `Campaign`, but only after a Patch70 fault | at the next fault |
| `MarriageClanPoolCache.PoolByCulture` (static) | `Main/Features/MarriageAlignment/` | `Clan` lists from the last game that rolled an NPC marriage | when its stamp changes |
| `MobilePartyAttachmentAdapter._cachedCommanderParty` (container singleton) | `Main/Adapters/` | the commander's `MobileParty` | on every load, new campaign and game end since plan 119 |

`PlayerAllianceProposalBehavior`, `CultureConversionBehavior` and `FieldCommissionBehavior` also keep a
`_lastSessionStarter`, but they are built with `new` for each campaign, so they hold it only while they
are reachable themselves.

## Probes already in the build

- `[MemStation] enter screen='GauntletInitialScreen' privMB=... heapMB=...` (and the matching `exit`)
  in `Logs/taom_debug_*.log` at every main-menu entry; on by default.
<WP114 BULLET>

## Procedure

Setup:

- A deployed build of the merged 1.4.x branch (TAOM and TAOM.Dependencies from one build).
- `cheat_mode = 0` in `engine_config.txt`: with cheat mode on, opening your own inventory lists every
  item and adds gigabytes on its own (MEMORY-01). Open no inventory or party screen during the run.
- Other programs closed.
- Tools, free from Microsoft: PerfView for heap snapshots of a .NET Framework process; ProcDump and
  WinDbg with SOS for exact counts and root paths. Menu names below are from PerfView's own help and
  can differ between versions. Keep every snapshot and dump outside the repo, for example in
  `C:\taom-heap\`; a full dump is about as large as the process's private memory.

Steps, all in one game process started from the launcher:

1. At the main menu wait 60 s. Snapshot **A** (cold menu).
2. Load save S from the main menu. On the campaign map wait 60 s. Snapshot **B** (optional, in campaign).
3. Return to the main menu from the escape menu. Wait 60 s. Snapshot **C** (one finished campaign).
4. Load S again from the main menu, wait 60 s on the map, return to the main menu, wait 60 s.
   Snapshot **D** (two finished campaigns).
5. Optional, for the save-growth question (SAVE-TIME-01): load S, then load S again from the save and
   load screen over the campaign map (an in-game load), wait 60 s. Snapshot **E**; compare it with B.

With each snapshot, copy the `[MemStation]` line written when that screen opened, and the
`prevCampaign=` token if the build has it.

Taking a snapshot:

- PerfView: Memory, Take Heap Snapshot, pick the Bannerlord process, Dump GC Heap. It writes a
  `.gcDump` file of the live objects. PerfView samples a very large heap and scales the counts, so read
  its counts as estimates; whether a type is present at all is reliable.
- Exact counts and root paths: `procdump -ma <pid> C:\taom-heap\C.dmp`, open the dump in WinDbg, then
  run the commands below. If your SOS rejects `-live`, drop it and confirm each instance with
  `!gcroot`: an object with no root is garbage that has not been collected yet.

```
.loadby sos clr
!dumpheap -stat -live -type TaleWorlds.CampaignSystem.CampaignGameStarter
!dumpheap -mt <MethodTable from that line>
!gcroot <one object address from that list>
```

## How to compare

Count these types in A, C and D (and in B and E when taken): `TaleWorlds.CampaignSystem.Campaign`,
`TaleWorlds.CampaignSystem.CampaignGameStarter`, `TaleWorlds.CampaignSystem.Hero`,
`TaleWorlds.CampaignSystem.Clan`, `TaleWorlds.CampaignSystem.CharacterObject`,
`TaleWorlds.CampaignSystem.Party.MobileParty`, `TaleWorlds.CampaignSystem.Settlements.Settlement`,
`TaleWorlds.CampaignSystem.Army`, `TaleWorlds.CampaignSystem.MapEvents.MapEvent`, and every type whose
name starts with `TAOM.`.

- A holds no campaign: its counts are the floor.
- C above A for any campaign type: part of the finished campaign is still reachable at the menu. Take
  the root path of one instance of each such type (PerfView: the referrers view of that type; WinDbg:
  `!gcroot`).
- D about equal to C: one finished campaign is held and replaced at the next session. D about twice C:
  campaigns pile up.
- E holding about twice B's `Hero` count: the old campaign's heroes survive an in-game load.
- `heapMB` at C and D against A says what it costs.

## Decision table

| The root path ends in | Meaning | Next (a follow-up plan, never this doc) |
|---|---|---|
| TAOM's container, then `EnlistmentBehavior`, `EnlistmentContentBehavior` or `MessengerCampaignBehavior`, then `_lastSessionStarter` | those singletons keep the finished campaign's starter and all it holds | clear `_lastSessionStarter` at game end in the three behaviors; nothing else changes, because each campaign builds a new starter (installed v1.4.8 `Campaign.OnInitialize`) |
| `CommanderLordAdapter._lastSeenMapEvent` | one finished battle and its parties | clear it from `ServiceMaintenanceService.ResetSessionCaches` |
| `Patch70_FiefGrantDecisionSwap._faultReportedFor` | a Patch70 fault pinned a campaign | keep a weak reference or an id instead |
| `MarriageClanPoolCache.PoolByCulture` | the last game's clans | clear the pool at game end; see also the stale-clan lead in plan 114's maintenance notes |
| a static event's handler list, for example `ScreenManager.OnPushScreen` | a subscriber from the old campaign | unsubscribe it at game end |
| only TaleWorlds fields, no TAOM type on the path | engine retention | tell players to restart the game between sessions; report it to TaleWorlds |
| C has no campaign objects and `heapMB` is near A's | the managed side is clean | the rest is native allocator pools: no TAOM fix; advise a restart between sessions |
| `prevCampaign=collected` yet `heapMB` above 300 at the menu | the holder is not the campaign graph | compare the largest types between A and C (arrays, strings, UI and texture caches), not TAOM singletons (V07 section 3.3) |
~~~~

**Verify**:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && grep -c '<WP114 BULLET>' docs/investigations/second-game-retention-2026-11.md; grep -c 'prevCampaign=alive|collected' docs/investigations/second-game-retention-2026-11.md; grep -c $'\xe2\x80\x94\|\xe2\x80\x93' docs/investigations/second-game-retention-2026-11.md; python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)
```

Expected: `0`; `1`; `0` (no long dash in the doc); `dead_links` and `orphan_features` equal Step
A0.7's values, `ai_dashes: 0`.

### Step B2: commit D

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git add docs/investigations/second-game-retention-2026-11.md && git status --porcelain
```

**Verify**: exactly one `A ` line.

Write `C:\Users\mikew\source\repos\taom-port\119-msg-D.txt` with exactly:

```text
docs(memory): v2.0.28 - steps to find what a second game keeps

A second campaign in one game process starts gigabytes higher than
the first, and the .NET heap often stays four to six times its cold
size at the main menu. The session resets ported in this plan fix
correctness; on the 1.5.x line the memory stayed after them. The new
investigation doc says what to capture (heap snapshots at a cold menu
and after one and two campaigns), how to compare them, which holders
code reading already names, and what each result means for a
follow-up fix.

Not-tested: the procedure itself (maintainer run owed)
```

NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/119 && git commit -F C:/Users/mikew/source/repos/taom-port/119-msg-D.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: the new commit; `git status --porcelain` empty.

### Part B done criteria

- [ ] `test -f docs/investigations/second-game-retention-2026-11.md && echo ok` prints `ok`.
- [ ] The Docs command: `dead_links` and `orphan_features` equal Step A0.7's; `ai_dashes: 0`.
- [ ] `git diff --name-only HEAD~1..HEAD` prints only that doc.

### Step B3: review gate, final verification, hand-off

1. Run the whole-WP Done criteria below; every one must hold.
2. Review gate: run `/deep-review` on `git diff $(git merge-base HEAD bannerlord-1.4.5)..HEAD` (C#
   in many files, the single-owner `SubModule.cs`). Ask it to probe the spots under "Maintenance
   notes". Act only on findings that change runtime behaviour or break this plan; land each fix as one
   extra commit `fix(enlistment): v2.0.28 - review follow-ups` (or the matching scope; tests first;
   same commit procedure) and re-run the Done criteria. After a follow-up, four criteria cannot hold
   literally (Part B's `HEAD~1..HEAD` check, `Total` = T0 + 49, the file count `32`, and any
   byte-identical check on a file the follow-up changed): allow exactly the follow-up's own files and
   tests, and list each deviation in the hand-off. Never rewrite earlier commits. If your session
   cannot invoke skills, say so in the hand-off and the integrator runs it. Never run `/review-codex`
   or `/codex-verify` (paid; maintainer only).
3. Hand-off report (your final message): the branch; each commit's short SHA and subject; T0, E0, S0,
   H0 and the final counts; which conditional branches you took (the `using` line in Step A6, the
   `GetAllHeroRaces` adaptation in Step A8, any doc conflict resolved in Steps A3 or A5, `wp114-doc-*`
   in B1); the `/deep-review` outcome or that it could not run; the "In-game checks owed" list; and
   for the integrator: this WP's row (DONE, total test count) and the old plan 001 row in
   `plans/README.md` (DONE: CareerSystem half `f4273639`, SpecialResources half WP119); and the
   reminder that WP117's and this WP's `OnGameEnd` additions in `Main/SubModule.cs` must both survive
   the merge.

## Disposition (every commit in the pack)

Simulation re-run this session, one commit at a time onto `origin/bannerlord-1.4.5` (exit 1 means
conflicts; the paths are the ones printed):

| 1.5.x commit | Subject | Simulation | Disposition |
|---|---|---|---|
| `6fb37c9f7` | fix(enlistment): v2.0.30 - reset the dwell anchor, offer latch, rhythm | 0 (clean) | **Transplanted** (end state of the series, Steps A1, A2), commit A |
| `f6ac2d335` | fix(enlistment): v2.0.30 - ResetSessionCaches drops the session latches | 0 (clean) | **Transplanted**, commit A |
| `e99d705a3` | fix(enlistment): v2.0.30 - reset session caches on a new campaign | 1: `EnlistmentSessionResetTests.cs` (prerequisite only) | **Transplanted**, commit A |
| `74fb3d35e` | docs(enlistment): v2.0.30 - session reset now covers a new campaign | 1: `IServiceMaintenanceService.cs` (prerequisite) | **Transplanted** (comments) plus its `enlistment.md` hunks (Step A3), commit A |
| `d1221b7fe` | docs(changelog): v2.0.30 - enlistment session reset (plan 014) | 1: `CHANGELOG.md` | **Skipped**: CHANGELOG only (D1) |
| `b9d18458e` | fix(enlistment): v2.0.30 - review follow-ups for plan 014 | 1: `CHANGELOG.md`, `IServiceAttachmentService.cs`, `IServiceMaintenanceService.cs`, `ServiceAttachmentService.cs`, `ServiceMaintenanceService.cs`, `EnlistmentSessionResetTests.cs`, `enlistment.md`, `REVIEW-LOG.md`, `lessons/misc.md`, `lessons/testing-qa.md` | **Transplanted** (code, tests, doc), commit A; the review record, Codex prompt, RCA, REVIEW-LOG, lessons and CHANGELOG hunks dropped (bookkeeping; WP142 syncs lessons) |
| `41754a03e` | fix(enlistment): v2.0.30 - convergence fixes for plan 014 | 1: `CHANGELOG.md`, `IServiceMaintenanceService.cs`, `EnlistmentSessionResetTests.cs`, `enlistment.md`, the deep-review record, the RCA | **Transplanted** (comments, test, doc), commit A; records and CHANGELOG dropped |
| `a67792c40` | fix(enlistment): v2.0.30 - apply maintainer decisions for plan 014 | 1: `CHANGELOG.md`, `EnlistmentReconciler.cs`, `IServiceMaintenanceService.cs`, `EnlistmentWaitMenuPresenter.cs`, `ServiceMaintenanceService.cs`, `EnlistmentReconcilerTests.cs`, `EnlistmentSessionResetTests.cs`, `enlistment.md`, the deep-review record | **Transplanted**, commit A; its `Main/SubModule.cs` line added by hand (Step A2, single-owner); record and CHANGELOG dropped |
| `fd61757bf` | fix(enlistment): v2.0.30 - review follow-ups for plan 014 | 1: `CHANGELOG.md`, `ArmyRhythmSnapshotService.cs`, `EnlistmentReconciler.cs`, `IServiceAttachmentService.cs`, `EnlistmentWaitMenuPresenter.cs`, `ServiceMaintenanceService.cs`, `EnlistmentSessionResetTests.cs`, `SettlementFollowingTests.cs`, `enlistment.md`, `REVIEW-LOG.md`, `lessons/state-lifecycle-save.md` | **Transplanted**, commit A; records, lessons, REVIEW-LOG, CHANGELOG dropped |
| `aac0e05bf` | fix(enlistment): v2.0.30 - convergence fixes for plan 014 | 1: `EnlistmentStopEndTests.cs` (prerequisite), the decisions review record, `lessons/state-lifecycle-save.md`, the decisions RCA | **Transplanted** (the test; this commit is the code end state), commit A; records and lesson dropped |
| merge `7feeec94f` | merge(improve): v2.0.30 - plan 014 Enlistment session scope (#656) | not simulated (merges are outside the commit list) | **End-state check only**: `git show --cc` shows resolutions in CHANGELOG, REVIEW-LOG and three lessons files only (`Main/SubModule.cs` auto-merged, no combined hunk) |
| `4263535a1` | fix(specres): v2.0.30 - a new campaign no longer keeps the old balances | 1: `CHANGELOG.md` only | **Ported** (path-limited, squashed with `8ea315052`, Steps A4, A5), commit B; CHANGELOG dropped |
| `8ea315052` | fix(specres): v2.0.30 - review follow-ups for plan 001 | 1: `CHANGELOG.md`, `SpecialResourcesBehavior.cs`, `SpecialResourcesBehaviorSessionResetTests.cs` (prerequisite), `special-resources.md`, `REVIEW-LOG.md`, `lessons/misc.md`, `lessons/state-lifecycle-save.md`, `lessons/testing-qa.md` | **Ported**, commit B, with one test comment re-cited to v1.4.8; review record, prompt, RCA, REVIEW-LOG, lessons, CHANGELOG dropped |
| `0935533a6` | fix(specres): v2.0.30 - convergence fixes for plan 001 | 1: the plan 001 deep-review record and RCA | **Skipped**: it only edits review records this WP does not port |
| merge `79cab8906` | merge(improve): v2.0.30 - plan 001 SpecialResources new-campaign reset | not simulated | **End-state check only**: resolutions in bookkeeping files only |
| `6ae924638` (shared hunk) | Add tests for NRE containment finalizers and improve documentation | 1: 34 paths, none under `HeroRace` | **Ported, race-persistence hunk only** (`RacePersistenceService.cs`, `RacePersistenceServiceTests.cs`; Steps A6, A7), commit C1. Every other hunk: WP112 (NRE finalizers), WP123 (AutoResolve removal), WP129 (career quest) or 1.5.x-only (Fire and Tribute, LastSeen, Patch123) |
| `eb6d18431` | fix(hero-race): v2.0.35 - a new game no longer warns of a pre-TAOM save | 1: `RacePersistenceServiceTests.cs` (needs the `6ae924638` test hunk; clean in the chain) | **Ported** (whole commit, path-limited, Step A6), commit C1 |
| `eb5cf0289` | fix(hero-race): v2.0.35 - old saves no longer take the last game's races | 1: `RacePersistenceServiceTests.cs`, `hero-race.md` (only `hero-race.md` in the chain) | **Adapted** (Steps A8, A9), commit C2: one test line uses `GetAllAliveHeroRaces` (1.4.x lacks #1011); `hero-race.md` by hand |

Findings and related changes:

| Item | Disposition |
|---|---|
| MEMORY-05 (P2) | Correctness ports in Part A; the heap-snapshot procedure in Part B (new work); the probe (`prevCampaign=`) is WP114's Step B1 |
| `f4273639` (plan 001 CareerSystem half) | Already on 1.4.x (ancestor of `origin/bannerlord-1.4.5`); nothing to do |
| `35007dbb8` (listed in the pack as a prerequisite of `eb5cf0289` for `RepoPaths.ReadSource`) | Not ported here: `TAOM.Tests/Infrastructure/RepoPaths.cs` arrives through WP104 (Step A0.3 checks it) |
| `f981629cd` (#1011, `GetAllHeroRaces`) | WP126; it must also rename the call this WP adapted (Maintenance notes) |
| SpecialResources "no record" load gap | Deferred: still open on 1.5.x at `e45f6df2c`; the doc says so |
| Retention fixes named in Part B's table | Follow-up plan once the procedure has run |

## Test plan

| File | New tests | Step |
|---|---|---|
| `TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs` (new) | 15 | A1 |
| `TAOM.Tests/Features/Enlistment/EnlistmentStopEndTests.cs` (new) | 4 | A1 |
| `SettlementFollowingTests.cs` (+4), `EnlistmentWaitMenuPresenterTests.cs` (+3), `CommanderLossAnnouncementTests.cs` (+2), `ServiceMaintenanceServiceTests.cs` (+2), `EnlistmentContainerWiringTests.cs` (+1) | 12 | A1 |
| `TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs` (new) | 5 | A4 |
| `RacePersistenceServiceTests.cs` | 3 (`6ae924638`) + 1 (`eb6d18431`) + 1 (`eb5cf0289`) | A6, A8 |
| `RacePersistenceBehaviorTests.cs` | 1 (`eb5cf0289`; `eb6d18431` adds an assertion to an existing test) | A6, A8 |
| `TAOM.Tests/Features/HeroRace/RacePersistenceWiringTests.cs` (new) | 7 results (3 methods plus one data method with 4 rows) | A8 |

Totals: Enlistment 31, SpecialResources 5, HeroRace 13; 49. Cells covered: dwell anchor, offer latch
and rhythm snapshot reset; the one reset point resets all collaborators and the pump does not; the
load edge resets before normalizing on the host and still resets on a co-op client; a save with no
Enlistment data clears the record (host and client), a save with data keeps it; the new-campaign
edge resets with and without a prior loading `SyncData`; the game-end call is present outside
comments; the stop ends on the commander's settlement-left edge only (not another party, not a
client) and on the exit sweep, also when only the re-park fails, with a throwing subscriber
swallowed; the loss modal re-arms on reset and on discharge; SpecialResources wipes storage without
a hero, loads empty on a missing key, round-trips exactly, and saves without touching storage;
HeroRace logs information on a new campaign and warns on a loaded save without race data (also after
a new campaign, and with neither call), resets in the constructor, and is built inline exactly once.
Pattern for later session-reset tests: `EnlistmentSessionResetTests.cs`. Not unit-testable (commit
trailers say so): the engine raising the events in a live campaign, a real second campaign in one
process, and the heap effect.

## Done criteria (whole WP)

Run in the worktree after Step B2 (and again after any review follow-up commit). ALL must hold:

- [ ] Part A and Part B done criteria hold.
- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` reports `Failed: 0`
      and `Total` = T0 + 49.
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD | wc -l` prints `32`, and
      `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD | grep -cE '^(CHANGELOG\.md|docs/reviews/|Main/IoC\.cs|\.claude/)'`
      prints `0`.
- [ ] `git diff $(git merge-base HEAD bannerlord-1.4.5)..HEAD -- Main/SubModule.cs | grep -cE '^[-+][^-+]'`
      prints `2`.
- [ ] `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'`
      prints nothing (five subjects, more with review follow-ups).
- [ ] `git status --porcelain` prints nothing.

## STOP conditions

Stop and report (do not improvise) if:

- Step A0.3 fails on any of its first six lines (WP104 or WP105 not merged).
- Step A0.4 prints any `DRIFT` or `EXISTS` line.
- Step A0.5 differs from its expected output (a port already landed, `RacePersistenceBehavior` is
  constructed elsewhere or container-resolved, or the `OnGameEnd` anchor is gone or out of order).
- A RED step does not fail as described (errors in other files, other error codes, a green build, or
  in Step A6 a failure other than `RestoreHeroRaces_NewCampaign_LogsInfoNotWarning`).
- `git apply --3way` leaves a conflict in any `.cs` file, or a doc conflict that is a paragraph both
  sides rewrote differently.
- The full suite after Step A10 or B2 shows a failure that two reasonable attempts within the
  in-scope files do not fix.
- A taom-src signature differs from "Engine facts" (for example `OnSettlementLeftEvent` is no longer
  `IMbEvent<MobileParty, Settlement>`, or `Campaign.DoLoadingForGameType` calls `OnGameLoaded` for a
  new campaign): report it; do not decompile and improvise.
- A hook denies a commit (for example for lacking a `CHANGELOG.md` change: WP102 not merged). Report
  the message verbatim; never stage `CHANGELOG.md`, never bypass the hook.
- The work seems to need `Main/IoC.cs`, a csproj, `Dependencies/**`, an MCM setting, a save-format
  change, or any `SubModule.cs` edit beyond the two lines of Step A2.
- You are tempted to add a retention fix (clearing `_lastSessionStarter`, `_lastSeenMapEvent` or a
  static). That is the follow-up the Part B procedure decides; it is not this WP.
- Step B1 cannot find one of the named holders.

## In-game checks owed (maintainer)

Executors cannot run the game. On a deployed build of the merged branch (TAOM and TAOM.Dependencies
from one build). TAOM's log is `Logs/taom_debug_<yyyy-MM-dd_HH-mm-ss>.log` relative to the game
process's working directory (`Main/Core/Logging/FileLogger.cs:26`, `:40`).

1. **Enlistment, second campaign in one process.** Enlist with a lord in campaign A, exit to the main
   menu, start a new campaign B without restarting, enlist again. At the column's first town stop
   the "The column halts" offer appears and taom_debug logs
   `[Enlistment] offered shore leave on arrival at '<settlement id>'`; when the commander leaves, the
   player leaves with the column (`[EnlistDiag] EXIT: left the settlement to rejoin the column`), not
   hours later.
2. **Enlistment, earlier save.** While enlisted, save (L), play on, then load an earlier save of the
   same campaign from the in-game load screen: the next town stop offers shore leave and the exit
   follows the commander.
3. **Enlistment, once per stop.** Accept shore leave in a town, walk out with the vanilla Leave option
   after the commander leaves, come back to the same town more than a day later: the offer appears
   again.
4. **Game end.** Exit to the main menu from an enlisted campaign: the menu appears normally; no new
   exception in taom_debug or `rgl_log_*.txt`.
5. **SpecialResources.** Play campaign A (earn some of its resource), exit to the main menu, start
   campaign B with another culture: the map bar shows only B's resource at its starting amount
   (`SpecialResources: Seeded starting resource for ... at character-creation finalize` in taom_debug).
6. **HeroRace.** A new game logs `RacePersistenceService: new campaign, no race data to restore yet.`
   and no `No saved race data found` warning; loading a normal TAOM save logs
   `RacePersistenceService: Restored race for N heroes.` (optional, if a save from before race
   persistence exists: start a new game, exit, load it; its heroes keep their XML races).
7. **MEMORY-05.** Run `docs/investigations/second-game-retention-2026-11.md` once and attach the
   counts, `heapMB` values and root paths to the MEMORY-05 issue; the orchestrator writes the
   follow-up plan from its decision table.

## Maintenance notes

- **WP126** ports `f981629cd` (#1011): it renames `IHeroRosterAdapter.GetAllAliveHeroRaces` to
  `GetAllHeroRaces` and must also rename the call this WP adapted in `RacePersistenceServiceTests.cs`
  (the build breaks otherwise, so it cannot be missed). On 1.5.x `f981629cd` came before
  `6ae924638` and `eb5cf0289`; on 1.4.x it lands after them, so expect light context conflicts in
  `RacePersistenceService.cs` and its tests. **WP139** later ports `e4dbdf9c4`, `40006d078` and
  `75c0c27a3` (race check lines), which need this WP's `eb5cf0289` and WP126's `f981629cd`.
- **WP128 and WP132** (wave 4) port later 1.5.x Enlistment commits. The 19 Enlistment files here are
  byte-identical to `aac0e05bf`, so those picks meet the context they had on 1.5.x.
- **WP129** (`c96a02a4b`, `30cbcd465`, `01ca483e2`, `bc59806f0`) and **WP133** (`406dd739f`,
  `fee906345`) edit `SpecialResourcesBehavior.cs` after this WP (batch 7 note: port plan 001 first).
- **WP142** syncs the lessons plans 001 and 014 wrote on 1.5.x (`state-lifecycle-save.md`,
  `testing-qa.md`, `misc.md`); this WP leaves them behind.
- **Follow-up after Part B runs**: the leading candidate is `_lastSessionStarter` on the three
  container singletons; the fix would be a field clear at game end (a new campaign always builds a
  new starter, installed v1.4.8 `Campaign.cs:1383`). Also open: `CommanderLordAdapter._lastSeenMapEvent`,
  `BattleMeritAccumulator._pending` and `FieldDutyRuntime`'s pace estimate (named as not reset in the
  ported `enlistment.md` text), and the SpecialResources no-record load gap (the FiefGranting and
  FieldCamp `_syncedThisSession` shape is the documented fix).
- The old `plans/001-cross-campaign-singleton-resets.md` (2026-06-13) describes a pre-1.5.x shape of
  the SpecialResources fix; this WP supersedes its SpecialResources half and `f4273639` its career
  half.
- Co-op (D7): the ported Enlistment code resets on every peer and keeps normalization host-only;
  this is inert in single player and untested here.
- **What a reviewer should probe**: (1) every callee of `ResetSessionCaches` is an in-memory field
  clear (it now runs on every peer and inside `Game.Destroy`); (2) `OnGameLoaded` clears the store only
  when no loading `SyncData` ran this load (`_justLoadedFromSave` is reset later, in
  `OnSessionLaunched`); (3) `EnlistmentMaintenanceBehavior.OnPartyLeftSettlement` keeps the authority
  gate and calls `OnStopEnded` before the reconcile; (4) the SpecialResources save branch returns
  before any `RestoreData`, and `OnNewGameCreated` reads no hero; (5) the race reset runs in the
  constructor, before `LoadBehaviorData`, for every campaign; (6) the `SubModule.cs` call sits inside
  the existing best-effort `try`; (7) the one adapted test line.
