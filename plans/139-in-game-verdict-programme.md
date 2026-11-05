# Plan 139: Prove owed in-game checks from the session log, and stop new silent catches, on the 1.4.x line

> **Executor instructions**: Follow this plan step by step in the worktree named under "Git workflow".
> Run every verification command and confirm its expected result before moving on. If anything in
> "STOP conditions" occurs, stop and report; do not improvise. The plan has five Parts; land them in
> order A, B, C, D, E. One session can take Parts A and B, a second Part C, a third Parts D and E; each
> Part has its own done criteria. Do not edit `plans/README.md` (it belongs to the integrator). When
> done, put the status, commit hashes and the Step F numbers in your hand-off report.
>
> **Drift check (run first, inside Step 0)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/IoC.cs Main/SubModule.cs Main/Features/CrashReport/CrashReportService.cs Main/Features/SaveLoadDiagnostics/Hooks/SaveManager_Save_Patch.cs Main/Features/StaleCharacterRepair Main/Features/CompanionTactics/BattleActionBar Main/Features/CareerSystem Main/Features/HeroRace Main/Adapters/HeroRosterAdapter.cs Main/Adapters/IHeroRosterAdapter.cs Main/Features/CastleRecruitment Main/Features/CharacterCreation/Hooks Main/Features/ArmyTargeting/Hooks Main/Features/Enlistment/Hooks Main/Features/CompanionTactics/FormationPresets Main/Features/CompanionTactics/Roles/Hooks Main/BehaviorTreeWrapper/BehaviorTreeBannerlordWrapper.cs TAOM.Tests/Migration/IlCallScanner.cs tools/_gamedir.py .claude/skills/verify/SKILL.md .claude/skills/release/SKILL.md .claude/rules/csharp-architecture.md`
> Earlier work packages of this programme change many of these on purpose (WP104, 107, 112, 115, 118,
> 119, 126, 129, 130, 131; the table in "Current state" says what each leaves). Step 0 and each step's
> precheck are the concrete gates: STOP only when one of those fails.

## Status

- **Priority**: P2
- **Effort**: L (three sessions: Parts A+B, Part C, Parts D+E)
- **Risk**: MED (many files, four conditional feature steps, a regenerated test baseline)
- **Depends on**: WP118, WP126, WP129, WP130, WP131, WP136 merged (the map's list, plus WP136: `EntryPointLineCountGateTests`
  and its baseline, which C1's view outgrows; lanes of one wave cut their branches in parallel, so this is a
  real edge). It also reads output of earlier waves, which the wave order merges first: WP102 (generated CHANGELOG, `/verify` Step 5),
  WP104 (`RepoPaths`), WP107 (CrashReportService end state), WP112 (the #1076 `[Check]` lines),
  WP115 (`[Shutdown]` line, `tools/ingame_ledger.py`), WP119 (`ResetForNewSession`). Step 0 checks each.
- **Category**: dx / tests / diagnostics
- **Wave / lane**: wave 5, lane D
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (the outcome log is
  core logging, registered in `Main/IoC.cs` beside `IModLogger` as on 1.5.x; no feature module), D7
  (the #981 co-op manifest ships only as a test fixture; the co-op `[[human]]` item of #1011 is dropped),
  D8 (subjects carry the `<Version>` of `Main/_Module/SubModule.xml`, `v2.0.28` until the v2.4.0
  release), D9 (no CI: every gate here runs locally), D10 (an ordinary session executes this file),
  D19 (git only through the Bash tool)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. The 1.5.x ref has since moved (`31292c035` when this was written, with new
  manifests and tests for 1.5.x-only features); every 1.5.x file below is read at a fixed sha, never
  from the moving ref.
- **Issue**: port of #1069 (epic), #1070 (reader), #1071 (outcome log), #1072 (check lines), #1073
  (silent-catch ratchet), #1074 (`/verify` and `/release`); the check lines verify #970, #971, #1011,
  #918 and #1076. The orchestrator creates the 1.4.x tracking issue before implementation lands.

## Why this matters

- The 2026-11 player logs could not settle the questions that matter: the Harad first-battle freeze is
  undecidable from the logs (V02), and the save-growth holder is unknown, with "SaveShield swallowing a
  load-callback fault" one of two live hypotheses (V01). On 1.4.x, PatchShield's swallowed count reaches
  only `diag.log` (`Dependencies/Foundation/PatchShield.cs:416-422`), catch blocks in Harmony hooks drop
  exceptions with no trace, and a crash bundle carries no session totals.
- Every port in this programme ends with "In-game checks owed" for one maintainer. 1.5.x answers most
  of them from the log: `tools/ingame_verdict.py` reads a session against a manifest per issue and
  counts evidence only from a build that contains the fix, read from the `[BuildStamp]` line. The 1.4.x
  log writes that line in the same format (E3 `taom_debug_2026-11-01_20-00-00.log` line 2:
  `TAOM=v2.0.0.0 build.20260930-120651Z+8df90444cc2f1e6d83bb95d2164ab764b4fcae65`).
- After this plan: a `[Session]` summary (check, counter and swallowed totals, PatchShield's count) lands
  in `taom_debug_*.log` at each save, crash, unload, process exit and every 10 minutes; the reader
  judges #1076, #970, #971, #1011 and #918 from the log; `/verify` and `/release` report owed verdicts;
  and a ratchet test fails any new silent catch in a Hooks or Harmony patch file.

## Current state

### Files and their roles (1.4.x `origin/bannerlord-1.4.5` at `1b00d1acd` unless noted)

| File | Role and anchor |
|---|---|
| `Main/IoC.cs` (single-owner) | `RegisterLoggingServices` `:223-226` registers only `container.Register<IModLogger, FileLogger>(Reuse.Singleton);` |
| `Main/SubModule.cs` (single-owner) | `IoC.Configure();` `:114` in `OnSubModuleLoad`; `OnApplicationTick` `:1913`; `OnSubModuleUnloaded` `:1933` (WP115 adds the `[Shutdown] clean module unload` line after `base.OnSubModuleUnloaded();`) |
| `Main/Features/SaveLoadDiagnostics/Hooks/SaveManager_Save_Patch.cs` | `Prefix(string saveName)` `:17-22`, one guarded `BeginSaveAttempt` call |
| `Main/Features/CrashReport/CrashReportService.cs` | Constructor ends `CrashBundleThrottle throttle)`; throttle check `if (admission.Decision != CrashBundleDecision.WriteBundle)` `:108`; `var context = ComposeContext(` `:124`; `_logTail.Collect()` `:217`. WP107 brings the 1.5.x end state (`HandleException(..., bool offMainThread = false)`) |
| `Main/Features/CrashReport/CrashReportIoC.cs:61` | `container.Register<ICrashReportService, CrashReportService>(Reuse.Singleton);` (constructor injection, DryIoc 4.8.8 on both lines) |
| `Main/Core/Logging/IModLogger.cs` | `LogInfo/LogDebug/LogWarning/LogError(string)`, `LogFilePath`: identical on both lines |
| `Main/Core/Logging/FileLogger.cs` | Writes `Logs/taom_debug_yyyy-MM-dd_HH-mm-ss.log` (`:26-27`, `:39-40`) relative to `bin\Win64_Shipping_Client`; line format `[yyyy-MM-dd HH:mm:ss] [LEVEL] message` |
| `Main/Core/Diagnostics/BuildStampReport.cs:146` | `[BuildStamp] TAOM=<ver> TAOM.Dependencies=<ver>...`; WP115 adds `config=`/`jitOpt=` tokens after each stamp and pins the reader's pattern with a test |
| `Dependencies/Foundation/PatchShield.cs:104` | `public static long SwallowedTotal` |
| `Dependencies/Foundation/DiagLog.cs:77` | `# TAOM.Dependencies session start: yyyy-MM-dd HH:mm:ss` (the reader's diag session header) |
| `tools/_gamedir.py` | `game_dir`, `game_modules`, `ensure_exists`; no `DEFAULT_GAME_ROOT` (the reader imports it) |
| `TAOM.Tests/Migration/IlCallScanner.cs` | 166 lines, equal to `a27c6b338^`; WP118 adds `Fingerprint` (`a27c6b338`) |
| `Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs` | After WP112: the 1.5.x `de90a2451` file with `IOutcomeLog` swapped for private `Check(...)`/`Swallowed(...)` helpers over `IModLogger`; consts `ParentRepairCheckId = "taom.stale-character.parent-repair"`, `BirthCompletedCheckId = "taom.stale-character.parent-birth"`; registered by `StaleCharacterRepairIoC.cs:11` |
| `Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs` | Today attaches in `OnMissionScreenInitialize`; after WP130 (`40385aa5b` + `d96f349f1`) a `[DefaultView]` that attaches on the first Battle tick (`ShouldAttach`), stores `_movie = _layer.LoadMovie(...)`, reads `ShowBattleActionBar` |
| `Main/Features/CompanionTactics/BattleActionBar/BattleActionBarService.cs` | Equals 1.5.x `55950378a` = `40385aa5b^`; after WP130 equals `b4ad748f2^` (has `Guarded(Action record)`) |
| `Main/Features/CareerSystem/Hooks/Patch27_CareerSystem/ViewModel_ExecuteCommand_CareerScreen_Patch.cs:12-21` | `Postfix` calls `charDevVM.ExecuteDone()` then `GauntletCareerScreen.OpenCareerScreen()`; WP129 (`0761c01b7`) adds the 4-line "only click handler (#971)" comment and `TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs` |
| `Main/Features/CareerSystem/UI/GauntletCareerScreen.cs` | `OpenCareerScreen(bool switchMode = false)` `:58`; `OnInitialize` `:79` ends with `ScreenManager.TrySetFocus(_gauntletLayer);` `:101`. Openers: Patch27 (normal) and `CareerSwitchDialogueBehavior.cs:84` (switch); WP129 deletes the dead mixin opener. There is no RealmUI coupling on this line (`git grep RealmUI` is empty) |
| `Main/Features/HeroRace/RacePersistenceService.cs` | Constructor `(IHeroRosterAdapter, IRaceManager, IModLogger)`; no #644 wraith code on this line (no `INazgulRegistry`, no `keptWraiths`). After WP119 and WP126 it equals the 1.5.x `e4dbdf9c4^` file minus #644 |
| `Main/Features/CastleRecruitment/CastleRecruitmentService.cs:20` | Constructor `CastleRecruitmentService(ICastleRecruitmentSettingsProvider settings)`; no `VolunteerProductionService` (War Chronicle, 1.5.x only). WP131 adds `BlocksAiRecruitAtCastle` (`0d3f2bc88`), `Hooks/CastleAiRecruitDispatcher.cs` and `TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs` (`f83a63e57`); registered by `CastleRecruitmentIoC.cs:11` |
| `Main/Core/Validation/FiniteFloatValidator.cs:57` | `public static bool IsFiniteInRange(double value, double min, double max)` (namespace `TAOM.Core.Validation`) |
| `Main/Features/CharacterCreation/Hooks/FaceGenVM_Refresh_RaceFilter_Patch.cs` | Has `ResolveFilterService`, `ResolvePlayerSwitchSession`, `ResolveLogger`; no `ResolveFactionPresets` (FactionUI presets are 1.5.x only) |
| `.claude/skills/verify/SKILL.md` | Step 2b runs `python -m unittest discover -s tools/tests -t .`; after WP102, Step 5 is "CHANGELOG.md untouched", then `## Output Format` |
| `.claude/skills/release/SKILL.md` | After WP102: Phase 4 generates the CHANGELOG section, Phase 5 is the release note, Phase 6 commit, Phase 7 tag; WP115 adds `tools/ingame_ledger.py` and the testers' checklist (`docs/releases/vX.Y.Z-testers.md`) to Phase 5 |
| `.claude/rules/csharp-architecture.md:25-31` | "Non-Negotiable Rules" table; its last row is the `?.` for computed properties rule (`:31`) |

Silent catches: 313 C# files sit under a `Hooks` folder on `1b00d1acd` (counted with `git ls-tree`), so the
ratchet's selection is at least that large; 1.5.x asserts more than 500 selected files.

### Verified v1.4.8 engine facts (installed DLLs through `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`, unless the v1.4.8 dump is named)

- `TaleWorlds.Engine.GauntletUI.GauntletLayer`: `public GauntletMovieIdentifier LoadMovie(string movieName, ViewModel dataSource)` (`:119`); `GauntletMovieIdentifier.Movie` is `public IGauntletMovie Movie { get; set; }` (`:10`); `IGauntletMovie.IsLoaded` is `bool { get; }`. `GauntletMovie.LoadMovie` (`:91-96`) sets `IsLoaded = true` only when `WidgetFactory.GetCustomType(MovieName)` returns a prefab, so a missing prefab does not throw.
- `TaleWorlds.MountAndBlade.Mission`: `public MissionMode Mode => _missionMode;` (`:1183`); `public bool IsFieldBattle` (`:1373`); `EndMissionInternal` (`:4573-4605`) calls every listener's `OnEndMission` (`:4580`) before `CurrentState = State.Over`. `MissionScreen`'s `IMissionListener.OnEndMission` (`:4170-4185`) calls each view's `OnMissionScreenFinalize`, so `Mission.Mode` is still readable there. `TaleWorlds.Core.MissionMode` has `Battle` and `Deployment`.
- `TaleWorlds.ScreenSystem.ScreenManager`: `public static ScreenBase TopScreen { get; private set; }` (`:108`); `public static void TrySetFocus(ScreenLayer layer)` (`:837`).
- `GameStateScreenManager.CreateScreen` (v1.4.8 dump `_modules_build\Native__TaleWorlds.MountAndBlade.View.cs`, class at `:12253`): builds the screen with `Activator.CreateInstance(type, state)` (`:12343`), so a constructor throw arrives wrapped and registers nothing; `OnCreateState` registers a null listener only when `CreateScreen` returns null (`:12371-12380`).
- `TaleWorlds.CampaignSystem.Hero`: `public bool IsAlive => !IsDead;` (`:317`), `MainHero` (`:892`), `FindFirst(Func<Hero,bool>)` (`:2130`), `FindAll(Func<Hero,bool>)` (`:2140`). `Settlement.IsCastle` (`:382`).
- `CampaignEvents`: `public static IMbEvent DailyTickEvent` (`:819`); `public static IMbEvent<Hero, Settlement, Hero, CharacterObject, int> OnTroopRecruitedEvent` (`:949`). `CampaignTime.ElapsedDaysUntilNow` is `float` (`:110`); `CampaignTimeModel.CampaignStartTime` is `CampaignTime` (`:7`).
- `RecruitmentCampaignBehavior`: `RecruitVolunteersFromNotable(MobileParty, Settlement)` (`:504`) calls `GetRecruitVolunteerFromIndividual` (`:555`), which calls `ApplyInternal` (`:651-653`); `ApplyInternal` (`:606`) raises `CampaignEventDispatcher.Instance.OnTroopRecruited(side1Party.LeaderHero, ...)` (`:638`). `GarrisonRecruitmentCampaignBehavior.RepopulateVolunteerListCache(Town)` (`:124`) asks `MaximumIndexGarrisonCanRecruitFromHero` (`:133`, `:155`) and raises no recruit event.
- `Campaign.CreateCampaignEvents` (v1.4.8 dump `Campaign.cs:1002-1011`, called from `OnDataLoadFinished` at `:724`): the first daily tick after a load waits the elapsed fraction of the current day, so a save made in the first half of a day reruns that day's tick (key = the same campaign day). The #918 manifest's wording holds on v1.4.8.

### Conventions that bind this plan

- ADR-007 adapters: services never touch sealed TaleWorlds types (the castle service takes ids and bools;
  `CastleRecruitObserver` in `Hooks/` is the engine glue). ADR-002 thin entry points (under 150 lines,
  delegate to services); `BattleActionBarMissionView` already exceeds 150 lines on 1.5.x and grows by 24
  here, with the verdict logic in `BattleActionBarChecks` (see Maintenance notes; C1 gives it its row in
  WP136's entry-point baseline). ADR-003 no `#region`,
  ADR-004 no `[Obsolete]`, ADR-005 no `#if DEBUG`.
- TDD: every step lands its tests first (RED), then the code (GREEN).
- MCM json2 rule (rename a persisted setting, never flip its default): no setting is added or changed here.
- `.claude/rules/harmony-patches.md`: no new patch, category or `MovementOrder` postfix here.
- Outcome cost rule (ported in Part D): hot paths use an `OutcomeCounter`, never `Pass/Fail/Skip`, and
  `Pass/Fail/Skip` run on the main thread.

## Commands

`WT$ <command>` means one Bash call: `cd /c/Users/mikew/source/repos/taom-port/139 && <command>`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `WT$ dotnet restore TAOM.sln` | exit 0 |
| Build | `WT$ dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (BHA0001 on Patch88 is a known false positive) |
| Test | `WT$ dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` |
| Filtered | the Test command plus `--filter "FullyQualifiedName~<Class>"` (join several with a pipe inside the quotes, as each step writes them) | `Failed: 0` |
| Reader tests | `WT$ python -m unittest tools.tests.test_ingame_verdict` | `OK` |
| Tool tests | `WT$ python -m unittest discover -s tools/tests -t .` | no failure beyond the Step 0 list |
| Docs | `WT$ python tools/lint_docs.py --summary` | no new finding for the touched docs |

Both `-p:` flags are mandatory on every build and test (`Main/TAOM.csproj:133-136` does not gate the copy on
`DisableModuleCopy`). Never `./build.ps1`. Python is 3.14 on this laptop (`tomllib` needs 3.11 or later).

## Scope

**In scope** (the only files you modify):

- Part A: new `Main/Core/Logging/IOutcomeLog.cs`, `Main/Core/Logging/OutcomeLog.cs`,
  `TAOM.Tests/Core/Logging/{OutcomeLogTests,OutcomesTests,OutcomeSeamsTests}.cs`; `Main/IoC.cs` (one line,
  single-owner), `Main/SubModule.cs` (three insertions, single-owner),
  `Main/Features/SaveLoadDiagnostics/Hooks/SaveManager_Save_Patch.cs`,
  `Main/Features/CrashReport/CrashReportService.cs`, `docs/features/crash-report.md`,
  `docs/features/save-load-diagnostics.md`; `Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs`,
  `TAOM.Tests/Features/StaleCharacterRepair/{StaleCharacterParentRepairTests,StaleCharacterRepairServiceTests}.cs`.
- Part B: new `tools/ingame_verdict.py`, `tools/tests/test_ingame_verdict.py`,
  `tools/tests/fixtures/ingame_verdict/**`, `tools/ingame_checks/1076.toml`, `docs/features/ingame-verdicts.md`;
  `tools/_gamedir.py`, `docs/features/stale-character-repair.md`.
- Part C: exactly the files each step's commit line names (C1 battle action bar plus
  `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` and `TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs`, C2 career screen plus
  `TAOM.Tests/Migration/IlCallScanner.cs`, C3 hero race and the two roster adapter files, C4 castle
  recruitment with the new `CastleRecruitDay.cs` and `Hooks/CastleRecruitObserver.cs`, and each feature's doc),
  plus per step its new `tools/ingame_checks/<issue>.toml`, its fixtures, `tools/tests/test_ingame_verdict.py`
  and `docs/features/ingame-verdicts.md`.
- Part D: `.claude/skills/verify/SKILL.md`, `.claude/skills/release/SKILL.md`, `.claude/rules/csharp-architecture.md`.
- Part E: the 20 Main files listed in Step E1, new `TAOM.Tests/Infrastructure/{SilentCatchScan.cs,SilentCatchRatchetTests.cs,SilentCatchBaseline.txt}`,
  `docs/features/crash-report.md`; `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` and
  `EntryPointLineCountGateTests.cs` only if Step E1 item 4 moves a row.

**Out of scope** (do not touch): `Client/` anything, `Main/Core/Logging/ScreenFaultLog.cs` and the Reeve's
Ledger (1.5.x-only, `cb4eeaa3d` hunks dropped); `tools/ingame_checks/981.toml` (co-op, D7) and `1079.toml`
(Long Defeat, a feature) and every manifest added to 1.5.x after `e45f6df2c`; `tools/ingame_ledger.py`
(WP115's); `KingdomWarSnapshotAdapter`, `VolunteerProductionService` and every War Chronicle file;
the #644 Ringwraith code; `tools/README.md`, `docs/reference/feature-map.md`, `docs/INDEX.md`, lessons,
`CHANGELOG.md` (bookkeeping: WP142 to WP144 sync them; the rows they need are in Maintenance notes);
`Dependencies/`; `Main/TAOM.csproj` (SDK-style, new files compile without an entry); any MCM setting.

## Git workflow

- Worktree (create once, reuse if present):
  `git -C /c/Users/mikew/source/repos/TAOM worktree add /c/Users/mikew/source/repos/taom-port/139 -b port148/139-in-game-verdict-programme bannerlord-1.4.5`.
  Base: local `bannerlord-1.4.5` with every earlier WP merged. Work only there. Never write into the game
  install. At most four executor worktrees at once; never two builds in one worktree.
- Git only through the Bash tool (D19). Stage explicit paths; never `git add -A`, `git add .`,
  `git commit -a`. Never stash, `reset --hard`, pull, push, tag, open a PR or merge into `bannerlord-1.4.5`.
- Per-port procedure (binding): `git cherry-pick -n <sha>` (Part E) or a checkout/`git apply --3way` of the
  1.5.x change (Parts A to D, where a pick would conflict on files this line does not have). Never let
  `cherry-pick`, `--continue`, `-C`, `-c` or `--amend --no-edit` make a commit (`GIT_EDITOR=true` on this
  laptop commits the 1.5.x message). After staging, if `test -f "$(git rev-parse --git-path CHERRY_PICK_HEAD)"`
  succeeds, run `git cherry-pick --quit` (keeps index and worktree) before committing.
- `git apply --3way` stages what it applies and leaves conflict markers where it cannot; resolve as the
  step says, then `git add <path>`. Never leave a `<<<<<<<` line (`git diff --cached --check` prints nothing).
- Scratch output goes to `C:\Users\mikew\source\repos\taom-port\139-tmp\` (`mkdir -p` it once), never
  inside the worktree.
- Commit: write the message with the Write tool to `C:\Users\mikew\source\repos\taom-port\msg-139-<step>.txt`,
  stage explicit paths, then in the NEXT sequential Bash call
  `WT$ git commit -F C:/Users/mikew/source/repos/taom-port/msg-139-<step>.txt && git log -1 --format='%h %s'`.
  Never the same call as `git add`, never a parallel call, never PowerShell.
- Message template: subject `<type>(<scope>): v2.0.28 - <description>` (at most 72 characters; use the
  `<Version>` of `Main/_Module/SubModule.xml` if a release changed it), body wrapped at 72 for 1.4.x
  readers, no em or en dashes, no Co-Authored-By or "Generated with" line; trailers `Research:`,
  `Not-tested:`, optional `Save-compat:`/`Rejected:`, one `Ported-from: <40-hex>` per included 1.5.x
  commit. Each step gives its message.
- Build and the filtered tests after each commit; the full suite at the end of each Part.
- Before the last commit of each Part, run `/deep-review` on that Part's diff if your session offers it
  (CLAUDE.md rule for C# changes); if it does not, say so in your report.

## Steps

### Step 0: Preflight

1. Create the worktree, then:
   `WT$ git status --porcelain | wc -l; git rev-parse HEAD; echo "$BANNERLORD_GAME_DIR"; python --version; mkdir -p /c/Users/mikew/source/repos/taom-port/139-tmp`
   Expected: `0`, a sha (record `BASE`), the game path, `Python 3.1x`. An empty game path is an environment
   failure: STOP and report; do not set it yourself.
2. Prerequisites (one call):
   ```bash
   WT$ for f in Main/Core/Diagnostics/IStampClock.cs Main/Core/Diagnostics/StopwatchStampClock.cs TAOM.Tests/Features/LoadTimeStamps/FakeStampClock.cs TAOM.Tests/Infrastructure/RepoPaths.cs; do test -f $f && echo "ok $f" || echo "MISSING $f"; done; \
     grep -c '"Client"' TAOM.Tests/Infrastructure/RepoPaths.cs; \
     grep -c 'public static IEnumerable<string> Fingerprint(' TAOM.Tests/Migration/IlCallScanner.cs; \
     git diff --quiet a27c6b338 HEAD -- TAOM.Tests/Migration/IlCallScanner.cs && echo "scanner==a27c6b338" || echo "scanner differs"; \
     grep -c '\[Shutdown\] clean module unload' Main/SubModule.cs; \
     grep -c 'bool offMainThread = false' Main/Features/CrashReport/CrashReportService.cs; \
     grep -c '"taom.stale-character.parent-repair"\|"taom.stale-character.parent-birth"' Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs; \
     grep -c 'private void Check(string id, string result, string detail)' Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs; \
     git grep -l 'interface IOutcomeLog\|class OutcomeLog \|class SilentCatchScan' -- Main TAOM.Tests | wc -l; ls tools/ingame_verdict.py tools/ingame_checks 2>&1 | grep -c 'No such file'; \
     test -f tools/ingame_ledger.py && echo ledger-yes || echo ledger-no; \
     grep -c '## Step 5: CHANGELOG.md untouched' .claude/skills/verify/SKILL.md; \
     test -f TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt && echo "ok entry-point baseline" || echo "MISSING entry-point baseline"; \
     grep -c 'private const int MaxBaselineEntries = ' TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs; \
     grep -c 'BattleActionBarMissionView' TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt
   ```
   Expected, in order: four `ok` lines; `0`; `1`; `scanner==a27c6b338`; `1`; `1`; `2`; `1`; `0`; `2`;
   `ledger-yes` (`ledger-no` is not a STOP: Step D1 has a branch for it); `1`; `ok entry-point baseline`; `1`
   (WP136's gate); `0` (the view is not listed yet). Any `MISSING`, or another
   value: STOP (the Status block names the WP that owes it). Record `ledger-yes/no` for Part D.
3. Baseline: Restore, Build, Test. Record `BASE_TOTAL`, `BASE_SKIPPED` (`Failed: 0` required). Tool tests:
   record `BASE_TOOLS` (`Ran N tests`) and the names of any failing tests (`BASE_TOOL_FAILS`).
4. Run the drift check from the header and read it against the table above.

## Part A: the outcome log (Steps A1, A2)

### Step A1: Check lines, counters and the session summary (`cb4eeaa3d`, transplant)

Method: checkout of the two new files and three test files, plus the five seams by hand. A
`cherry-pick -n cb4eeaa3d` conflicts in six paths here (simulated this session onto `1b00d1acd`: the
Reeve's Ledger mixin and wiring test, `ScreenFaultLog.cs` and its tests, `SubModule.cs`, `crash-report.md`),
four of them 1.5.x-only files; the transplant is the same change without them.

1. **RED.**
   `WT$ git checkout cb4eeaa3d TAOM.Tests/Core/Logging/OutcomeLogTests.cs TAOM.Tests/Core/Logging/OutcomesTests.cs TAOM.Tests/Core/Logging/OutcomeSeamsTests.cs`
   In `OutcomeSeamsTests.cs` delete the whole test `ScreenFaultLog_ReportsEveryThrowToOutcomes` (its
   `[TestMethod]` line through its closing `    }` and the blank line after): `ScreenFaultLog` is not on
   this line. **Verify (RED)**: Build fails with CS0246/CS0103 naming `OutcomeLog`, `IOutcomeLog` or
   `Outcomes`.
2. **GREEN.**
   `WT$ git checkout cb4eeaa3d Main/Core/Logging/IOutcomeLog.cs Main/Core/Logging/OutcomeLog.cs`
   Then, with the Edit tool:
   - `Main/IoC.cs`, in `RegisterLoggingServices`, after `container.Register<IModLogger, FileLogger>(Reuse.Singleton);`
     add `        container.Register<IOutcomeLog, OutcomeLog>(Reuse.Singleton);`
   - `Main/SubModule.cs`, directly after the first `        IoC.Configure();` in `OnSubModuleLoad`:
     ```csharp

             // #1071: the [Check] and [Session] facade for static patches, and the exit summary on ProcessExit.
             try { Core.Logging.Outcomes.Initialize(IoC.Resolve<Core.Logging.IOutcomeLog>()); }
             catch { /* outcome logging must never stop the mod loading */ }
             Core.Logging.Outcomes.SummariseOnProcessExit();
     ```
     As the first statement of `OnApplicationTick(float dt)`:
     `        Core.Logging.Outcomes.Tick(); // the 10-minute [Session] heartbeat: one clock compare per tick`
     In `OnSubModuleUnloaded`, on its own line directly above the `try` line that logs
     `[Shutdown] clean module unload`:
     `        Core.Logging.Outcomes.WriteSummary("exit"); // #1071: the session summary, just before the marker; never throws`
   - `SaveManager_Save_Patch.cs`, in `Prefix`, after the `catch { /* diagnostic only */ }` line:
     `        Core.Logging.Outcomes.WriteSummary("save"); // #1071; never throws`
   - `CrashReportService.cs`: add the field `    private readonly IOutcomeLog _outcomes;` after
     `    private readonly CrashBundleThrottle _throttle;`; add `IOutcomeLog outcomes` as the last
     constructor parameter (`        CrashBundleThrottle throttle,` then `        IOutcomeLog outcomes)`);
     add `        _outcomes = outcomes;` after `        _throttle = throttle;`; and after the closing brace
     of the `if (admission.Decision != CrashBundleDecision.WriteBundle) { ... return null; }` block insert:
     ```csharp

                 // #1071: the session summary, before ComposeContext collects the log tail, so both the TAOM tail in
                 // report.txt and the bundle's taom_debug.log contain the totals. Only for an admitted crash: a crash
                 // every frame would otherwise write one per frame.
                 _outcomes.WriteSummary("crash");
     ```
     (`using TAOM.Core.Logging;` is already at the top.)
   - `docs/features/crash-report.md`: after the paragraph that starts `The very first bundle is never cooldown-gated`
     insert a blank line and the paragraph from `git show cb4eeaa3d -- docs/features/crash-report.md` that
     starts `**Session summary (#1071).**` (verbatim); as the first item under `## Changelog` add (today's date):
     ```text
     - YYYY-MM-DD: **Session summary in the bundle** (#1071, port). An admitted crash writes the `[Session] summary reason=crash` lines before the log tail is collected.
     ```
   - `docs/features/save-load-diagnostics.md`: in the `SaveBegin` row, replace the last cell text
     `Save lifecycle reset` with:
     ```text
     Save lifecycle reset. The same prefix then writes the `[Session] summary reason=save` lines (#1071) on the game thread, before serialisation
     ```
3. **Verify (GREEN)**: Build exit 0; Filtered
   `--filter "FullyQualifiedName~TAOM.Tests.Core.Logging|FullyQualifiedName~TAOM.Tests.Features.CrashReport|FullyQualifiedName~SaveLoadDiagnostics"`:
   `Failed: 0`, and `OutcomeLogTests` 17, `OutcomesTests` 6, `OutcomeSeamsTests` 5 passed.
   `WT$ git grep -c 'Outcomes\.' -- Main/SubModule.cs` prints `Main/SubModule.cs:4`.
4. **Commit** (`msg-139-A1.txt`; stage the 2 new Main files, the 3 test files, `Main/IoC.cs`,
   `Main/SubModule.cs`, `SaveManager_Save_Patch.cs`, `CrashReportService.cs`, the two docs):
   ```text
   feat(logging): v2.0.28 - check lines, counters and a session summary

   TAOM's debug log can now record that a feature did what it was built
   to do, and how a session ended, in lines a script can read.
   IOutcomeLog writes "[Check] id=<id> result=pass|fail|skip <detail>"
   once per id, result and scope, counts hot paths with no string or
   disk write, and reports a swallowed exception once per feature and
   site. A "[Session] summary" with those totals and PatchShield's
   swallowed count, which until now reached only diag.log, is written
   before each save, before a crash report collects its log tail, just
   before the [Shutdown] line at unload, at process exit, and every 10
   minutes of real time. Features move onto it in the next commits.

   Research: no engine member; PatchShield.SwallowedTotal,
    StopwatchStampClock (plan 118), DryIoc 4.8.8 on both lines
   Not-tested: the heartbeat, exit and crash summaries in a real session
   Ported-from: cb4eeaa3de5cffc961d6b287460f76d612636faf
   ```

### Step A2: The hollow-parent repair writes through the outcome log (completes WP112's `1165a00de`/`de90a2451` port)

WP112 ported the repair with private `Check`/`Swallowed` helpers over `IModLogger` because `IOutcomeLog`
was absent; restore the 1.5.x shape.

1. **RED.** `WT$ git checkout de90a2451 TAOM.Tests/Features/StaleCharacterRepair/StaleCharacterParentRepairTests.cs`
   and in `TAOM.Tests/Features/StaleCharacterRepair/StaleCharacterRepairServiceTests.cs` replace
   `_sut = new StaleCharacterRepairService(_adapter, _logger);` with
   `_sut = new StaleCharacterRepairService(_adapter, _logger, Substitute.For<IOutcomeLog>());`.
   **Verify (RED)**: Build fails (CS1729: no constructor takes 3 arguments).
2. **GREEN.** `WT$ git checkout de90a2451 Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs`, then
   `WT$ git diff HEAD -- Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs | grep -E '^\+[^+].*[^[:space:]]' | grep -v -e '_outcomes' -e 'IOutcomeLog'`
   must print nothing (every added line is the field, constructor or an `_outcomes.` call; the removed lines
   are WP112's `Check(`/`Swallowed(` calls, its two helpers and their comment). Any other added line: STOP.
3. **Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~StaleCharacterRepair|FullyQualifiedName~Patch83"`:
   `Failed: 0` and the same passed count as before this step (WP112 rewrote the assertions one for one).
   `WT$ grep -c '_outcomes\.' Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs` is at least `7`;
   `grep -c 'private void Check(' ...` prints `0`.
4. **Commit** (`msg-139-A2.txt`; the service and the two test files):
   ```text
   refactor(stale-character): v2.0.28 - write checks through IOutcomeLog

   The hollow-parent repair (#1076) wrote its [Check] and [Swallowed]
   lines through IModLogger while IOutcomeLog was missing from this
   line. It now writes them through IOutcomeLog, as on 1.5.x, so they
   count in the [Session] summary and a repeated swallow is counted
   instead of logged again. The line text is unchanged; a fail line is
   now written at INFO, like every outcome line.

   Research: none (no engine member changes)
   Not-tested: a birth with a hollow parent in a real campaign
   Ported-from: 1165a00dec70c2b37b35dce4b97fa8507f7f3272
   Ported-from: de90a24514689b1f60afdd32971ffdb6afff141e
   ```

**Part A done**: Test command `Failed: 0`, total `BASE_TOTAL + 28`.

## Part B: the verdict reader (Step B1)

### Step B1: Reader, fixtures, the #1076 manifest (`c8d69e426`, `c5705474e`, `53e13cfe3`, `95647d13b` end state)

Method: end-state transplant from `e45f6df2c` (`tools/ingame_verdict.py` there equals `95647d13b`'s). The
1.5.x commits were cherry-picked onto `1b00d1acd` this session: `c8d69e426` conflicts only in
`tools/README.md`; the three follow-ups conflict on files that do not exist here yet. Per issue, the
manifest, its fixtures and its test class travel with that issue's check lines (Part C); the reader's own
tests use fixture copies of the 1.5.x #970 and #981 manifests, so they never depend on Part C.

1. **Reader, fixtures, fixture manifests, `_gamedir`.**
   ```bash
   WT$ python -I - <<'PY'
   import subprocess
   from pathlib import Path
   TIP = "e45f6df2c"
   def show(p): return subprocess.run(["git", "show", f"{TIP}:{p}"], capture_output=True, check=True).stdout
   Path("tools/ingame_verdict.py").write_bytes(show("tools/ingame_verdict.py"))
   names = subprocess.run(["git", "ls-tree", "-r", "--name-only", TIP, "tools/tests/fixtures/ingame_verdict"],
                          capture_output=True, text=True, check=True).stdout.split()
   assert len(names) == 82, len(names)
   taken = [n for n in names if not any(s in n for s in ("/career_", "/castle_", "/race_"))]
   assert len(taken) == 59, len(taken)
   for n in taken:
       p = Path(n); p.parent.mkdir(parents=True, exist_ok=True); p.write_bytes(show(n))
   for issue, note in (("970", "The 1.4.x manifest is tools/ingame_checks/970.toml (plan 139 Part C)."),
                       ("981", "#981 (co-op server settings) is not on the 1.4.x line (D7).")):
       head = (f"# Test fixture: 1.5.x tools/ingame_checks/{issue}.toml at e45f6df2c, for the reader's own tests.\n"
               f"# Its fixed_in is the 1.5.x sha the tests assert; the fake git answers by build sha. {note}\n")
       Path(f"tools/tests/fixtures/ingame_verdict/manifests/{issue}.toml").write_text(
           head + show(f"tools/ingame_checks/{issue}.toml").decode("utf-8"), encoding="utf-8", newline="\n")
   g = Path("tools/_gamedir.py"); raw = g.read_bytes().decode("utf-8"); nl = "\r\n" if "\r\n" in raw else "\n"
   if "DEFAULT_GAME_ROOT" not in raw:
       anchor = 'MODULES_ENV_VAR = "BANNERLORD_GAME_MODULES"'
       assert raw.count(anchor) == 1
       raw = raw.replace(anchor, anchor + nl + "# The install root the tools fall back to when neither variable is set (one machine's path; see game_dir)."
                         + nl + 'DEFAULT_GAME_ROOT = r"E:\\Steam\\steamapps\\common\\Mount & Blade II Bannerlord"')
       g.write_bytes(raw.encode("utf-8"))
   print("reader + %d fixtures + 2 fixture manifests; DEFAULT_GAME_ROOT %s" % (len(taken), "present" if "DEFAULT_GAME_ROOT" in g.read_text() else "MISSING"))
   PY
   ```
   Expected: `reader + 59 fixtures + 2 fixture manifests; DEFAULT_GAME_ROOT present`. In `tools/_gamedir.py`
   the new constant line reads `DEFAULT_GAME_ROOT = r"E:\Steam\steamapps\common\Mount & Blade II Bannerlord"`
   (the 1.5.x `fd1cee10b` text; `game_dir` returns `$BANNERLORD_GAME_DIR` first, so the laptop never uses it).
2. **The test file** (the 1.5.x file minus the per-issue classes Part C re-adds; 970/981 read as fixtures;
   the literal gate reads `Main` only):
   ```bash
   WT$ python -I - <<'PY'
   import ast, subprocess
   from pathlib import Path
   src = subprocess.run(["git", "show", "e45f6df2c:tools/tests/test_ingame_verdict.py"], capture_output=True, check=True).stdout.decode("utf-8")
   lines, tree = src.split("\n"), ast.parse(src)
   def span(n): return min([n.lineno] + [d.lineno for d in getattr(n, "decorator_list", [])]), n.end_lineno
   spans = []
   for n in tree.body:
       if isinstance(n, ast.ClassDef) and n.name in {"CastleRecruit918Tests", "Issue971Tests", "HeroRace1011Tests"}:
           spans.append((n.lineno, n.end_lineno + 2))
       if isinstance(n, ast.ClassDef) and n.name == "RepoManifestGateTests":
           for i in n.body:
               if isinstance(i, ast.FunctionDef) and i.name in {"test_970_names_the_attach_and_stance_checks",
                       "test_981_holds_only_the_issue_s_server_items", "test_918_holds_only_the_ai_off_check",
                       "test_971_names_the_open_and_init_checks", "test_1011_names_two_checks_and_no_posthumous"}:
                   s, e = span(i); spans.append((s, e + 1))
   assert len(spans) == 8, spans
   out = "\n".join(l for k, l in enumerate(lines, 1) if not any(a <= k <= b for a, b in spans))
   for old, new in [('real_manifest("970.toml")', 'manifest("970.toml")'), ('real_manifest("981.toml")', 'manifest("981.toml")'),
                    ('for folder in ("Main", "Client"):', 'for folder in ("Main",):'),
                    ('# Main and Client only: a [Check] line', '# Main only: a [Check] line'),
                    ('def test_the_gate_reads_main_and_client_only(self):', 'def test_the_gate_reads_main_only(self):'),
                    ('self.assertIn(parts[0], {"Main", "Client"}, p)', 'self.assertIn(parts[0], {"Main"}, p)'),
                    ('which no .cs file under Main or Client "', 'which no .cs file under Main "'),
                    ('which the shipped #981 manifest leaves NOT REACHED', 'which the #981 fixture manifest leaves NOT REACHED'),
                    ('"""The real #981 manifest over three server boots, each its own session."""',
                     '"""The #981 fixture manifest (a 1.5.x co-op check, D7) over three server boots, each its own session."""'),
                    ('Rules 1 to 4 over the real #970 manifest', 'Rules 1 to 4 over the #970 fixture manifest')]:
       assert out.count(old) >= 1, old
       out = out.replace(old, new)
   t = ast.parse(out)
   n = sum(1 for c in t.body if isinstance(c, ast.ClassDef) for f in c.body if isinstance(f, ast.FunctionDef) and f.name.startswith("test_"))
   assert n == 100, n
   Path("tools/tests/test_ingame_verdict.py").write_text(out, encoding="utf-8", newline="\n")
   print("test file: 100 test methods; real_manifest left for:", [l.strip() for l in out.split("\n") if "real_manifest(" in l and "def " not in l])
   PY
   ```
   Expected: `test file: 100 test methods; real_manifest left for: ['self.t.real_manifest("1076.toml")']`.
3. **The #1076 manifest** (`fixed_in` is WP112's port commit, which already writes these `[Check]` lines):
   ```bash
   WT$ python -I - <<'PY'
   import subprocess
   from pathlib import Path
   def git(*a): return subprocess.run(["git", *a], capture_output=True, text=True, check=True).stdout
   # The oldest match: Step A2's commit carries the same trailer and must not be taken for WP112's port.
   sha = (git("log", "--reverse", "--format=%H", "--grep=^Ported-from: 1165a00dec70c2b37b35dce4b97fa8507f7f3272", "HEAD").split() or [""])[0]
   if not sha:
       sha = (git("log", "--reverse", "--format=%H", "-SParentRepairCheckId", "--", "Main/Features/StaleCharacterRepair/StaleCharacterRepairService.cs").split() or [""])[0]
   assert sha, "no commit on HEAD ports #1076: STOP"
   t = subprocess.run(["git", "show", "e45f6df2c:tools/ingame_checks/1076.toml"], capture_output=True, check=True).stdout.decode("utf-8")
   for old, new in [('fixed_in = "50e74c760"   # the commit before the fix: the manifest ships with the emitter, so its own sha is not known',
                     f'fixed_in = "{sha[:12]}"   # the 1.4.x port of 1165a00de and de90a2451 (plan 112), which first wrote these lines'),
                    ("; the RCA gives a way to build this state\"", "; docs/features/stale-character-repair.md describes the state\""),
                    (" id (RCA, Codex F2).", " id.")]:
       assert t.count(old) == 1, old
       t = t.replace(old, new)
   Path("tools/ingame_checks").mkdir(exist_ok=True)
   Path("tools/ingame_checks/1076.toml").write_text(t, encoding="utf-8", newline="\n")
   print("1076.toml fixed_in", sha[:12], git("log", "-1", "--format=%s", sha).strip())
   PY
   ```
   Expected: the sha and the subject of WP112's `fix(stale-character): ... repair a hollow parent before a birth` commit.
4. **Verify**: Reader tests print `OK` with `Ran 100 tests`. `WT$ python -m unittest tools.tests.test_gamedir`
   prints `OK`. `WT$ python tools/ingame_verdict.py --issue 1076; echo "exit=$?"` prints a report and
   exits `4` (NOT REACHED: no session on this laptop proves the check; any exit but 0, 1, 4 is a STOP).
5. **Docs.** `WT$ git show e45f6df2c:docs/features/ingame-verdicts.md > docs/features/ingame-verdicts.md`, then with
   the Edit tool:
   - Replace the two `Manifests on trunk: ...` lines under `### Current Values` with: ``One manifest per issue
     in `tools/ingame_checks/` (list them with `ls tools/ingame_checks`). `tools/tests/fixtures/ingame_verdict/manifests/970.toml`
     and `981.toml` are copies of the 1.5.x manifests that drive the reader's own tests; #981 (co-op server
     settings) does not apply to this line.``
   - Delete the Key Files row for `Main/Core/Logging/ScreenFaultLog.cs`. Keep the `tools/ingame_ledger.py` row
     only if Step 0 recorded `ledger-yes`.
   - Replace the bullet `- Feature emitters, each with a fake ... StaleCharacterParentRepairTests.cs.` (three lines)
     with ``- Feature emitters, each with a fake `IOutcomeLog`: `StaleCharacterParentRepairTests.cs`.``
   - `` under `Main` or `Client` holds `` becomes `` under `Main` holds ``.
   - Replace item 3 of "How to verify in game" (from its line that starts `3.` through the line that ends
     `check the newest PASS or FAIL wins.`) with ``3. `--sessions 2` for a save made in one session and loaded in the next. More sessions never hide
     a newer result: per check the newest PASS or FAIL wins. (`--ds-data` and `--ds-engine` read a dedicated
     server's logs; no manifest on this line uses them.)``
   - In "How to add a check" step 4 replace the parenthesis that names `40385aa5b` with ``(on this line
     the commit whose body carries `Ported-from:` for the 1.5.x fix, never a 1.5.x sha)``.
   - `(#1073, closed and on trunk` plus the next line's `since 2026-10-31)` become `(#1073)`.
   - Replace the three 1.5.x lines under `## Changelog` with
     ``- <today>: ported to the 1.4.x line by plan 139: `IOutcomeLog`, the reader, and check lines for the fixes this line carries (#1071 to #1074).``
   - `- **Status:** Open` becomes `- **Status:** Open (1.4.x port: plan 139)`.
   `docs/features/stale-character-repair.md` (WP112 left placeholders): for each hit of
   `WT$ grep -n 'arrives with WP139' docs/features/stale-character-repair.md`, replace
   ``(`tools/ingame_checks/1076.toml` arrives with WP139)`` with ``(`tools/ingame_checks/1076.toml`, read by
   `python tools/ingame_verdict.py --issue 1076`; see [in-game verdicts](ingame-verdicts.md))`` on the first
   hit and ``(`tools/ingame_checks/1076.toml`)`` on the others. If grep printed nothing, add that sentence
   ``The `[Check]` lines are read by `python tools/ingame_verdict.py --issue 1076` with `tools/ingame_checks/1076.toml` ([in-game verdicts](ingame-verdicts.md)).``
   as the last line before `## Changelog`. In the `## Tests` table, if no `HollowParent1076Tests` row exists,
   add after the `Patch83HooksHarmonyTests` row:
   ``| `HollowParent1076Tests` (`tools/tests/test_ingame_verdict.py`) | the real manifest against fixture logs: a finished birth, a throw, a throw on a second birth, a killed process, a healthy game |``.
   **Verify**: `WT$ grep -c 'ingame-verdicts.md' docs/features/stale-character-repair.md` at least `1`; Docs
   command: no new finding (the new doc has an inbound link, so it is not an orphan); `grep -c $'\xe2\x80\x94\|\xe2\x80\x93' docs/features/ingame-verdicts.md` prints `0`.
6. **Commit** (`msg-139-B1.txt`; stage `tools/ingame_verdict.py`, `tools/_gamedir.py`, `tools/tests/test_ingame_verdict.py`,
   `tools/tests/fixtures/ingame_verdict` (the new folder only), `tools/ingame_checks/1076.toml`, the two docs;
   then `git diff --cached --name-only | wc -l` prints `67` if `_gamedir.py` changed, else `66`):
   ```text
   feat(tools): v2.0.28 - in-game check verdicts read from session logs

   A new tool, tools/ingame_verdict.py, reads a play session's logs and
   gives each owed in-game check of an issue a verdict: PASS, FAIL or NOT
   REACHED, with up to three evidence lines per check. Each issue's checks
   live in tools/ingame_checks/<issue>.toml. Evidence counts only from a
   build whose [BuildStamp] sha contains the fix (git merge-base
   --is-ancestor), and each session's last [Session] summary is printed
   with its swallowed sites. Read-only unless --post is given.

   The first manifest is #1076 (a birth from a hollow parent). The
   reader's own tests use copies of the 1.5.x #970 and #981 manifests as
   fixtures; #981 (co-op server settings) does not apply to this line.

   Research: 1.4.x FileLogger, BuildStampReport and DiagLog write the
    formats the reader parses; E3's BuildStamp line matches its pattern
   Not-tested: a verdict over a played 1.4.x session
   Ported-from: c8d69e426bb843ce73ef300e140dd4451d56eb65
   Ported-from: c5705474ed5899a8bd5576436e572ff3614ca593
   Ported-from: 53e13cfe35d72f4253c81df7b1f7db24ededc457
   Ported-from: 95647d13b230016d28c6f51732f6d1b315b82bc5
   Ported-from: b4ad748f2dcf4946f404659871990a6e2734c1f1
   Ported-from: 30a938689bf08da25705cc7ab0a59f4735b178c9
   Ported-from: 1165a00dec70c2b37b35dce4b97fa8507f7f3272
   Ported-from: de90a24514689b1f60afdd32971ffdb6afff141e
   ```
   (The 67 files: reader, `_gamedir.py`, test file, 59 fixtures (10 synthetic manifests among them), the 2
   fixture copies, `1076.toml`, the new doc, the stale-character doc.)

**Part B done**: Tool tests: `BASE_TOOLS + 100`, no failure beyond `BASE_TOOL_FAILS`; C# total unchanged.

## Part C: check lines for the fixes this line carries (Steps C1 to C4)

Each step is independent and runs only when its base fix is on the branch; a step whose base fix is
absent is skipped and reported (orchestrator note: check lines only for ported fixes). Run first:

```bash
WT$ for s in 40385aa5bdb759eb52d92a30b3a78a194cf000e0 d96f349f1124ab474eb9b299b7c1389f6d9f8365 0761c01b75d58d46eb6f97e06d94db568ac33d53 f981629cd86f34c2ea907c768a586e224adbd4ab 0d3f2bc88e664e10d62589b114757eed2609f117; do echo "$s -> $(git log -1 --format=%h --grep="^Ported-from: $s" HEAD)"; done
```

Then the shape checks (one call):

```bash
WT$ echo C1; grep -c '\[DefaultView\]\|_movie = _layer.LoadMovie\|internal static bool ShouldAttach' Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs; grep -c 'ShowBattleActionBar' Main/Features/CompanionTactics/BattleActionBar/BattleActionBarService.cs; ls TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs; \
  echo C2; grep -c "The career button's only click handler (#971)" Main/Features/CareerSystem/Hooks/Patch27_CareerSystem/ViewModel_ExecuteCommand_CareerScreen_Patch.cs; ls TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs; \
  echo C3; grep -c 'GetAllHeroRaces();' Main/Adapters/IHeroRosterAdapter.cs; grep -c 'ResetForNewSession\|_isNewCampaign' Main/Features/HeroRace/RacePersistenceService.cs; \
  echo C4; grep -c 'bool BlocksAiRecruitAtCastle' Main/Features/CastleRecruitment/ICastleRecruitmentService.cs; ls Main/Features/CastleRecruitment/Hooks/CastleAiRecruitDispatcher.cs TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs
```

A step runs when its port commit was printed above (C1: either `40385aa5b` or `d96f349f1`) and its shape
checks hold: C1 prints at least `3` (`40385aa5b`'s view prints `4`: its summary comment also names
`[DefaultView]`), at least `1`, and the file; C2 `1` and the file; C3 `1` and at least `2`; C4 `1` and both
files. Otherwise skip the step and record why. Each step's `Ran N tests` assumes every earlier C step ran:
subtract 1, 7, 14 or 6 for a skipped C1, C2, C3 or C4.

Shared pieces every C step uses (with its own values):

- **Fixtures**: `WT$ for f in $(git ls-tree -r --name-only e45f6df2c tools/tests/fixtures/ingame_verdict | grep '/<prefix>_'); do git show "e45f6df2c:$f" > "$f"; done`
- **Re-add the Python test class and manifest-gate method** from the 1.5.x file:
  ```bash
  WT$ CLASS=<class or empty> METHOD=<method> python -I - <<'PY'
  import ast, os, subprocess
  from pathlib import Path
  P = Path("tools/tests/test_ingame_verdict.py")
  tip = subprocess.run(["git", "show", "e45f6df2c:tools/tests/test_ingame_verdict.py"], capture_output=True, check=True).stdout.decode("utf-8")
  tl, tt = tip.split("\n"), ast.parse(tip)
  def span(n): return min([n.lineno] + [d.lineno for d in getattr(n, "decorator_list", [])]), n.end_lineno
  def cls(name):
      n = next(n for n in tt.body if isinstance(n, ast.ClassDef) and n.name == name); s, e = span(n); return tl[s - 1:e]
  def meth(c, name):
      k = next(n for n in tt.body if isinstance(n, ast.ClassDef) and n.name == c)
      m = next(i for i in k.body if isinstance(i, ast.FunctionDef) and i.name == name); s, e = span(m); return tl[s - 1:e]
  CLASS, METHOD = os.environ.get("CLASS", ""), os.environ["METHOD"]
  cur = P.read_bytes().decode("utf-8").replace("\r\n", "\n")
  assert f"def {METHOD}(" not in cur and not (CLASS and f"class {CLASS}(" in cur), "already re-added"
  L = cur.split("\n")
  if CLASS:
      i = L.index("class CrashReportEchoTests(unittest.TestCase):"); L[i:i] = cls(CLASS) + ["", ""]
  j = L.index("    def test_every_fixed_in_is_a_commit_in_this_clone(self):"); L[j:j] = meth("RepoManifestGateTests", METHOD) + [""]
  out = "\n".join(L); ast.parse(out); P.write_text(out, encoding="utf-8", newline="\n")
  print("re-added", CLASS or "(method only)", METHOD)
  PY
  ```
- **The manifest**: copy `tools/ingame_checks/<issue>.toml` from `e45f6df2c` with the step's edits, `fixed_in`
  set to the first 12 hex of the base-fix port commit printed above (the reader's `git merge-base` then binds
  evidence to builds that contain the 1.4.x fix). Use the snippet below with the step's values:
  ```bash
  WT$ ISSUE=<n> FIX=<40-hex 1.5.x fix sha> python -I - <<'PY'
  import os, subprocess
  from pathlib import Path
  issue, fix = os.environ["ISSUE"], os.environ["FIX"]
  sha = subprocess.run(["git", "log", "-1", "--format=%H", f"--grep=^Ported-from: {fix}", "HEAD"], capture_output=True, text=True, check=True).stdout.strip()
  assert sha, "no port commit for " + fix
  t = subprocess.run(["git", "show", f"e45f6df2c:tools/ingame_checks/{issue}.toml"], capture_output=True, check=True).stdout.decode("utf-8")
  EDITS = {
   "970": [('# Client/Features/CompanionTactics/', '# Main/Features/CompanionTactics/'),
           ('fixed_in = "40385aa5b"   # the second of the two fix commits the issue names (d96f349f1, 40385aa5b)',
            f'fixed_in = "{sha[:12]}"   # the 1.4.x port of the #970 fix (1.5.x d96f349f1 and 40385aa5b)')],
   "971": [('fixed_in = "0761c01b7"   # named in the issue\'s fix comment', f'fixed_in = "{sha[:12]}"   # the 1.4.x port of the #971 fix (1.5.x 0761c01b7)'),
           ("writes no open line; RealmUI's map-bar tile opens it in\n# normal mode with no click, so it writes an init line without an open line. Init counts only mode=normal.",
            "writes no open line; init counts only mode=normal.")],
   "1011": [('fixed_in = "f981629cd"   # the fix the issue\'s closing comment names', f'fixed_in = "{sha[:12]}"   # the 1.4.x port of the #1011 fix (1.5.x f981629cd)'),
            (' wraiths=[0-9]+', ''),
            ("\n[[human]]\nclaim = \"co-op: after a client loads a save that restores races, its Coop_client.log has no line 'Client updated managed BasicCharacterObject.Race' (the reader cannot pass on a missing line)\"\n", "\n")],
   "918": [('fixed_in = "0d3f2bc88"   # named in the issue\'s merge comment of 2026-10-21', f'fixed_in = "{sha[:12]}"   # the 1.4.x port of the #918 fix (1.5.x 0d3f2bc88)')],
  }
  for old, new in EDITS[issue]:
      assert t.count(old) == 1, old
      t = t.replace(old, new)
  Path(f"tools/ingame_checks/{issue}.toml").write_text(t, encoding="utf-8", newline="\n")
  print(issue, "fixed_in", sha[:12], subprocess.run(["git", "log", "-1", "--format=%s", sha], capture_output=True, text=True).stdout.strip())
  PY
  ```
- **The feature-emitter bullet** of `docs/features/ingame-verdicts.md`: insert `` `<TestClass>.cs`, `` directly
  before `` `StaleCharacterParentRepairTests.cs` `` in the bullet that starts `- Feature emitters`.

### Step C1: Battle action bar attach and stance checks (#970; `b4ad748f2`, `30a938689`, `0bc2a132f`)

Simulation onto `1b00d1acd`: all three conflict, on the `Client/` view (absent here) and on files WP130 brings.
Method: the series' net diff, with the view's path rewritten from `Client/` to `Main/`.

1. **RED.**
   ```bash
   WT$ git show 0bc2a132f:TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarChecksTests.cs > TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarChecksTests.cs && git diff b4ad748f2^ 0bc2a132f -- TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarServiceTests.cs | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U
   ```
   Expected `exit=0` and no unmerged path. On a conflict, keep HEAD's lines and add the 1.5.x hunk's `+` lines
   by hand (`git show b4ad748f2 30a938689 0bc2a132f -- <file>`): the service tests gain the `_outcomes`
   field and fifth constructor argument and three tests; the input tests gain `Between` and four `View_*`
   tests and the outcome-log constructor argument. Then `CompanionTacticsWiringTests.cs`: if it exists,
   `WT$ git diff b4ad748f2^ 0bc2a132f -- TAOM.Tests/Features/CompanionTactics/CompanionTacticsWiringTests.cs | git apply --3way`;
   if it does not, create it holding only the 1.5.x test `RegisterCompanionTacticsFeature_BattleActionBarService_Resolves`
   (from `git show b4ad748f2 -- TAOM.Tests/Features/CompanionTactics/CompanionTacticsWiringTests.cs`) in a
   `[TestClass] public class CompanionTacticsWiringTests` in namespace `TAOM.Tests.Features.CompanionTactics`,
   with the usings `System.Linq`, `DryIoc`, `Microsoft.VisualStudio.TestTools.UnitTesting`, `NSubstitute`,
   `TAOM.Core.Logging`, `TAOM.Features.CompanionTactics`, `TAOM.Features.CompanionTactics.BattleActionBar`
   (the 1.5.x overlay test needs plan 022 types this line lacks; do not take it).
   **Verify (RED)**: Build fails naming `BattleActionBarChecks` and the 5-argument `BattleActionBarService`.
2. **GREEN.**
   ```bash
   WT$ git show 0bc2a132f:Main/Features/CompanionTactics/BattleActionBar/BattleActionBarChecks.cs > Main/Features/CompanionTactics/BattleActionBar/BattleActionBarChecks.cs && git diff b4ad748f2^ 0bc2a132f -- Main/Features/CompanionTactics/BattleActionBar/BattleActionBarService.cs | git apply --3way; echo "svc exit=$?"; git diff b4ad748f2^ 0bc2a132f -- Client/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs | sed 's#Client/Features/#Main/Features/#g' | git apply --3way; echo "view exit=$?"; git diff --name-only --diff-filter=U
   ```
   Expected `svc exit=0`, `view exit=0`, nothing unmerged. If the view conflicts, keep HEAD and make these
   seven edits by hand (the whole net diff): after `RefreshIntervalSec` add the comment
   `// The [Check] key (#1072): counts every mission screen of the process. Main thread only.` and
   `private static int _missionSerial;`; after the `_logger` field add `private IOutcomeLog? _outcomes;` and the
   four per-mission fields `_missionKey = ""`, `_fieldBattle`, `_attachedOnce`, `_enabledBattleTick` with their
   comment; in `OnMissionScreenInitialize` after the `IModLogger` resolve add the `_outcomes`, `_missionKey`
   and `_fieldBattle` lines; in `TryAttachLayer` after the "layer attached" log add `_attachedOnce = true;`, the
   LoadMovie comment and `BattleActionBarChecks.RecordAttach(_outcomes, _missionKey, _movie.Movie?.IsLoaded == true);`;
   in its catch add the `_outcomes?.Fail(... "reason=exception mission=" ...)` call; in `OnMissionScreenTick`
   inside `if (!_isInitialized)` add `if (Mission.Mode == MissionMode.Battle) _enabledBattleTick = true;` first;
   at the top of `OnMissionScreenFinalize` add the `ClassifyAtEnd` call and `BattleActionBarChecks.RecordEnd(_outcomes, _missionKey, end);`.
   Copy each line from `git diff b4ad748f2^ 0bc2a132f -- Client/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs`.
3. **Verify (GREEN)**: Build exit 0; Filtered `--filter "FullyQualifiedName~CompanionTactics"`: `Failed: 0`;
   `BattleActionBarChecksTests` 8 methods (14 results with data rows) pass.
4. **The entry-point baseline (ADR-002).** WP130's view is 136 lines at the class, under the limit and so not
   in WP136's baseline; the 24 added lines take it to 160. `Baseline_Regenerate_WhenAsked` cannot add the row by
   itself: it creates a missing file, but an existing one is only pruned (`live.Where(p => old.ContainsKey(p.Key))`),
   so the row goes in by script and the regeneration then rewrites the file. First the failure:
   ```bash
   WT$ dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EntryPointLineCountGateTests" > C:/Users/mikew/source/repos/taom-port/139-tmp/entry-point.txt 2>&1; echo "exit=$?"; grep -oE '[A-Za-z_.]+: [0-9]+ lines \(limit 150\)|[A-Za-z_.]+: grew to [0-9]+ lines from [0-9]+' C:/Users/mikew/source/repos/taom-port/139-tmp/entry-point.txt | sort -u
   ```
   Expected: non-zero exit and exactly one line,
   `TAOM.Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView: 160 lines (limit 150)`.
   Another class named: STOP. Another number for this class: use it below and name it in the hand-off. Then:
   ```bash
   WT$ python -I - <<'PY'
   import re
   from pathlib import Path
   KEY = "TAOM.Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView"
   text = Path(r"C:\Users\mikew\source\repos\taom-port\139-tmp\entry-point.txt").read_text(encoding="utf-8", errors="replace")
   lines = re.search(re.escape(KEY) + r": (\d+) lines \(limit 150\)", text).group(1)
   p = Path("TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt")
   raw = p.read_bytes().decode("utf-8"); nl = "\r\n" if "\r\n" in raw else "\n"
   L = [l for l in raw.split(nl) if l]
   head, rows = [l for l in L if l.startswith("#")], [l for l in L if not l.startswith("#")]
   old = len(rows)
   rows = sorted(rows + [f"{KEY} {lines}"], key=lambda r: r.split(" ")[0])
   head = [f"# Entries: {len(rows)}." if l.startswith("# Entries:") else l for l in head]
   p.write_bytes((nl.join(head + rows) + nl).encode("utf-8"))
   g = Path("TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs")
   src = g.read_bytes().decode("utf-8")
   const = f"private const int MaxBaselineEntries = {old};"
   assert src.count(const) == 1, const
   g.write_bytes(src.replace(const, f"private const int MaxBaselineEntries = {old + 1};").encode("utf-8"))
   print("MaxBaselineEntries", old, "->", old + 1, "row", KEY, lines)
   PY
   ```
   Build, then
   `WT$ TAOM_WRITE_ENTRY_POINT_BASELINE=1 dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EntryPointLineCountGateTests.Baseline_Regenerate_WhenAsked"`
   (it keeps the row, because the row is now listed).
   **Verify**: `WT$ grep -c 'BattleActionBarMissionView 160' TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt`
   prints `1`; `WT$ git diff --numstat -- TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs`
   prints `2  1` for the baseline (the new row and the `# Entries:` line) and `1  1` for the gate test; Filtered
   `--filter "FullyQualifiedName~EntryPointLineCountGateTests"`: `Failed: 0` (`Baseline_Regenerate_WhenAsked` reports
   Inconclusive without its variable).
5. **Manifest, fixtures, Python tests, docs.** The attach fixtures arrived in B1. Manifest snippet with
   `ISSUE=970 FIX=40385aa5bdb759eb52d92a30b3a78a194cf000e0` (if no `40385aa5b` port commit exists, use
   `FIX=d96f349f1124ab474eb9b299b7c1389f6d9f8365`). Re-add snippet with `CLASS= METHOD=test_970_names_the_attach_and_stance_checks`.
   `docs/features/companion-tactics.md`: replace the #970 bullet that starts `- **In-game check owed**` (it
   names `Show Battle Action Bar` and ends `shows it once.`) with the 11-line bullet that starts the same way in
   `git show 0bc2a132f:docs/features/companion-tactics.md` (verbatim); if no such bullet exists, add the new
   one after the bullet that starts `- **Known, LOW:** a sally-out sortie`. Emitter bullet: `BattleActionBarChecksTests.cs`.
   **Verify**: Reader tests `OK`, `Ran 101 tests`.
6. **Commit** (`msg-139-C1.txt`; the 3 Main files, the 4 test files, `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt`,
   `TAOM.Tests/Infrastructure/EntryPointLineCountGateTests.cs`, `tools/ingame_checks/970.toml`,
   `tools/tests/test_ingame_verdict.py`, the two docs):
   ```text
   feat(logging): v2.0.28 - battle action bar attach and stance checks

   The battle action bar (#970) writes [Check] lines, so
   python tools/ingame_verdict.py --issue 970 judges it from the log.
   taom.battle-action-bar.attach passes once per mission when the bar's
   layer is added and its movie loaded; it fails when the attach throws,
   when the movie did not load, or at mission end when a Battle-mode
   tick saw the setting on and the bar never attached; it skips with
   reason=setting-off. taom.battle-action-bar.stance passes when a click
   reaches Brace for Cavalry, Pike Wall, Testudo, Line Charge or
   Skirmish. The verdict mappings live in BattleActionBarChecks.

   The view passes ADR-002's 150-line limit for entry points at 160
   lines, as on 1.5.x, so it takes one row in the shrink-only
   EntryPointLineCountBaseline.txt and MaxBaselineEntries rises by one.
   Splitting the view is a separate refactor.

   Research: v1.4.8 GauntletLayer.LoadMovie, IGauntletMovie.IsLoaded,
    Mission.EndMissionInternal, MissionScreen.OnEndMission (taom-src)
   Not-tested: a field battle with Show Battle Action Bar on
   Ported-from: b4ad748f2dcf4946f404659871990a6e2734c1f1
   Ported-from: 30a938689bf08da25705cc7ab0a59f4735b178c9
   Ported-from: 0bc2a132fffe8daf834518dccb02d8f71a477d75
   ```

### Step C2: Career screen open and init checks (#971; `0ab3dc35a`, `4e0c2a3b0`, `2d6934006`; helpers from `92839bc52`, `5fcf8390c`)

The new test calls `IlCallScanner.ExtractOpCodes` (from SKIP-FEATURE `92839bc52`, K1-01) and
`4e0c2a3b0`'s test calls `IlCallScanner.ExtractReferencedMembers` (from co-op SKIP `5fcf8390c`, found this
session); `git diff a27c6b338 e45f6df2c -- TAOM.Tests/Migration/IlCallScanner.cs` is exactly those two
methods (49 added lines, none removed), so the scanner is lifted to its 1.5.x end state.

1. **RED.**
   ```bash
   WT$ git diff --quiet a27c6b338 HEAD -- TAOM.Tests/Migration/IlCallScanner.cs && git show e45f6df2c:TAOM.Tests/Migration/IlCallScanner.cs > TAOM.Tests/Migration/IlCallScanner.cs && git diff --numstat -- TAOM.Tests/Migration/IlCallScanner.cs; git show 2d6934006:TAOM.Tests/Features/CareerSystem/CareerScreenChecksTests.cs > TAOM.Tests/Features/CareerSystem/CareerScreenChecksTests.cs; git diff 0ab3dc35a^ 2d6934006 -- TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs | git apply --3way; echo "exit=$?"
   ```
   Expected `49  0  TAOM.Tests/Migration/IlCallScanner.cs` and `exit=0`. No numstat line means the scanner is
   not WP118's `a27c6b338` file: STOP. Then the fixtures with prefix `career` (6 files; `CareerScreenChecksTests`
   reads them). **Verify (RED)**: Build fails naming `CareerScreenChecks`.
2. **GREEN.**
   ```bash
   WT$ git show 2d6934006:Main/Features/CareerSystem/UI/CareerScreenChecks.cs > Main/Features/CareerSystem/UI/CareerScreenChecks.cs && git diff 0ab3dc35a^ 2d6934006 -- Main/Features/CareerSystem/Hooks/Patch27_CareerSystem/ViewModel_ExecuteCommand_CareerScreen_Patch.cs Main/Features/CareerSystem/UI/GauntletCareerScreen.cs | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U
   ```
   Expected `exit=0` (`0ab3dc35a`'s Main hunks merged cleanly onto `1b00d1acd` in the simulation). In
   `CareerScreenChecks.cs` replace the two comment lines that mention RealmUI with
   `    // The dialogue's career switch opens the screen in switch mode; the career button opens it in normal mode and`
   and `    // is the only opener that writes the open line.` (this line has no RealmUI coupling).
3. **Verify (GREEN)**: Build exit 0; Filtered `--filter "FullyQualifiedName~CareerButtonClickTests|FullyQualifiedName~CareerScreenChecksTests|FullyQualifiedName~IlCallScanner"`:
   `Failed: 0`, none Inconclusive (the IL tests load the game assemblies; Inconclusive means they did not
   load: environment failure, report it).
4. **Manifest, Python tests, docs.** Manifest snippet `ISSUE=971 FIX=0761c01b75d58d46eb6f97e06d94db568ac33d53`;
   re-add `CLASS=Issue971Tests METHOD=test_971_names_the_open_and_init_checks`. `docs/features/career-system.md`:
   before the first `## ` heading that follows the `## Career-Switch Picker` section, insert
   `## In-game check lines (#971)`, a blank line, and the paragraph of that section from
   `git show 2d6934006:docs/features/career-system.md` minus its last sentence (the one naming RealmUI,
   with its link). Its `GameStateScreenManager.CreateScreen` sentence holds on v1.4.8 (Current state).
   Emitter bullet: `CareerScreenChecksTests.cs`.
   **Verify**: Reader tests `OK`, `Ran 108 tests`; `WT$ grep -c RealmUI Main/Features/CareerSystem/UI/CareerScreenChecks.cs tools/ingame_checks/971.toml docs/features/career-system.md`
   prints `0` for each.
5. **Commit** (`msg-139-C2.txt`; the 3 Main files, `CareerButtonClickTests.cs`, `CareerScreenChecksTests.cs`,
   `IlCallScanner.cs`, the 6 fixtures, `971.toml`, the test file, the two docs):
   ```text
   feat(career): v2.0.28 - career screen open and init check lines

   The career button logs whether its click opened the career screen, so
   python tools/ingame_verdict.py --issue 971 judges it from the log.
   taom.career-screen.open passes when the career screen is on top after
   the click, fails with reason=not-on-top, or with the exception's type
   when the click throws (the exception still propagates).
   taom.career-screen.init passes at the end of OnInitialize with
   mode=normal or mode=switch. IlCallScanner gains ExtractOpCodes and
   ExtractReferencedMembers, which the new IL tests use.

   Research: v1.4.8 ScreenManager.TopScreen, TrySetFocus;
    GameStateScreenManager.CreateScreen uses Activator.CreateInstance
   Not-tested: the career button click in a campaign
   Ported-from: 0ab3dc35ad6e1f40a5154f5d12175841a34b4cb8
   Ported-from: 4e0c2a3b0b3cc53353a061db611392f84ff8834b
   Ported-from: 2d6934006f6e842a2706626a3f73ff8a6460494e
   Ported-from: 92839bc52da2a4d5bf2aeb6404ce415fdd24b3fe
   Ported-from: 5fcf8390c63d6955255a291ed050095066d8a862
   ```

### Step C3: Race persistence check lines (#1011; `e4dbdf9c4`, `40006d078`, `75c0c27a3`)

The 1.5.x hunks sit on the #644 Ringwraith code this line does not have. A three-way merge of the series onto
a synthetic post-WP126 file (the `e4dbdf9c4^` file minus #644, built this session) gave three conflict blocks
in `RacePersistenceService.cs` and one leftover `keptWraiths` reference; the resolutions are below.

1. **RED.** The service test file, by script (keeps this line's setup and inserts the 1.5.x tests with the
   Ringwraith test rewritten and every `wraiths=` field dropped):
   ```bash
   WT$ python -I - <<'PY'
   import re, subprocess
   from pathlib import Path
   SRC = "TAOM.Tests/Features/HeroRace/RacePersistenceServiceTests.cs"
   b = subprocess.run(["git", "show", "75c0c27a3:" + SRC], capture_output=True, check=True).stdout.decode("utf-8").split("\n")
   s = b.index("    // --- #1011 check lines (#1072): taom.race-persist.capture and taom.race-persist.restore ---")
   e = b.index("}", s); assert b[e - 1] == "    }"
   block = b[s - 1:e]
   w = block.index("    public void Restore_WraithAndRemovedRace_AreCountedNotCompared()")
   assert block[w - 1].strip() == "[TestMethod]"
   block[w - 1:block.index("    }", w) + 1] = [
       "    [TestMethod]", "    public void Restore_RemovedRace_IsCountedNotCompared()", "    {",
       '        _raceManager.GetOrderedRaceNames().Returns(new[] { "human", "dwarf", "elf", "gone" });',
       '        CaptureMap(new HeroRaceInfo("lost", 3));',
       '        _heroRosterAdapter.GetAllHeroRaces().Returns(Roster(new HeroRaceInfo("lost", 0)), Roster(new HeroRaceInfo("lost", 0)));',
       "", "        _sut.RestoreHeroRaces();", "",
       '        _outcomes.Received(1).Pass(RestoreId, "1", "restored=0 dead_restored=0 dead_seen=0 skipped=1 mismatch=0 saved=1");',
       "    }"]
   text = re.sub(r" wraiths=\d+", "", "\n".join(block))
   assert "wraith" not in text.lower() and "_nazgul" not in text and text.count("[TestMethod]") == 18
   p = Path(SRC); raw = p.read_bytes().decode("utf-8"); nl = "\r\n" if "\r\n" in raw else "\n"; L = raw.split(nl)
   def one(x):
       assert sum(1 for l in L if l == x) == 1, x
       return L.index(x)
   i = one("    private IModLogger _logger;"); L.insert(i + 1, "    private IOutcomeLog _outcomes;")
   i = one("        _logger = Substitute.For<IModLogger>();"); L.insert(i + 1, "        _outcomes = Substitute.For<IOutcomeLog>();")
   i = one("        _sut = new RacePersistenceService(_heroRosterAdapter, _raceManager, _logger);")
   L[i] = "        _sut = new RacePersistenceService(_heroRosterAdapter, _raceManager, _logger, _outcomes);"
   i = one("        var freshService = new RacePersistenceService(freshAdapter, freshRaceManager, Substitute.For<IModLogger>());")
   L[i:i + 1] = ["        var freshService = new RacePersistenceService(freshAdapter, freshRaceManager, Substitute.For<IModLogger>(),",
                 "            Substitute.For<IOutcomeLog>());"]
   i = one('        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("hero_corrupt")));')
   L[i + 1:i + 1] = ["        // 1011.toml fails a load on this text, because the [Check] line itself passes (the hero is skipped).",
                     '        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("is outside the save")));']
   end = L.index("}"); assert L[end - 1] == "    }"
   L[end:end] = text.split("\n")
   p.write_bytes(nl.join(L).encode("utf-8"))
   print("inserted 18 tests before line", end + 1)
   PY
   ```
   (Dry-run this session on `1b00d1acd`'s file with WP126's rename applied: every anchor found once.) Then
   `WT$ git diff e4dbdf9c4^ 75c0c27a3 -- TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs | git apply --3way; echo "exit=$?"`.
   On a conflict (WP104 converted both files to `RepoPaths`, so contexts can differ), keep HEAD and make the
   two changes by hand: in `RacePersistenceBehaviorTests.cs` the asserted text
   `AddNonSerializedListener(this, _service.CaptureHeroRaces)` becomes
   `AddNonSerializedListener(this, _service.CaptureHeroRacesForSave)`, and the test
   `OnSessionLaunched_DoesNotWriteTheSaveCheck` (from `git show e4dbdf9c4 -- <file>`) goes after the test that
   ends `CollectionAssert.AreEqual(new[] { "restore", "capture" }, callOrder);`; in `HeroRaceWiringTests.cs` the test
   `HeroRosterAdapter_PassesIsAlive` goes after `HeroRosterAdapter_ReadsEveryHeroCharacterThroughHeroFindAll`.
   **Verify (RED)**: Build fails (`CaptureHeroRacesForSave`, the 3-argument `HeroRaceInfo`, the 4-argument constructor).
2. **GREEN.**
   `WT$ git diff e4dbdf9c4^ 75c0c27a3 -- Main/Adapters/HeroRosterAdapter.cs Main/Adapters/IHeroRosterAdapter.cs Main/Features/HeroRace/IRacePersistenceService.cs Main/Features/HeroRace/RacePersistenceBehavior.cs Main/Features/HeroRace/RacePersistenceService.cs | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U`
   Expected: only `RacePersistenceService.cs` unmerged. Resolve its blocks:
   - constructor signature: `    public RacePersistenceService(IHeroRosterAdapter heroRosterAdapter, IRaceManager raceManager, IModLogger logger,`
     then `        IOutcomeLog outcomes)`;
   - constructor body: HEAD's three assignments plus `        _outcomes = outcomes;` (no `_nazgul`);
   - restore counters: `        var deadRestored = 0;`, `        var deadSeen = 0;`,
     `        // The race each map hero should have once the restore is done; invalid races have none.`,
     `        var expected = new Dictionary<string, int>(StringComparer.Ordinal);` (no `keptWraiths`);
   - outside the markers, replace the `counts` statement with
     `        var counts = string.Format(Invariant, "restored={0} dead_restored={1} dead_seen={2} skipped={3}",`
     and `            restoredCount, deadRestored, deadSeen, skippedInvalid);`.
   `git add` the file. **Verify**: `WT$ grep -c 'keptWraiths\|_nazgul\|INazgulRegistry\|wraiths=' Main/Features/HeroRace/RacePersistenceService.cs` prints `0`;
   `git diff --cached --check` prints nothing.
3. **Verify (GREEN)**: Build exit 0; Filtered `--filter "FullyQualifiedName~TAOM.Tests.Features.HeroRace"`: `Failed: 0`;
   `RacePersistenceServiceTests` gained 18 passing tests.
4. **Manifest, fixtures, Python tests, docs.** Fixtures with prefix `race` (12 files), then
   `WT$ sed -i 's/ wraiths=[0-9]*//' tools/tests/fixtures/ingame_verdict/race_*.txt && grep -c 'wraiths' tools/tests/fixtures/ingame_verdict/race_*.txt | grep -vc ':0$'`
   prints `0`. Manifest snippet `ISSUE=1011 FIX=f981629cd86f34c2ea907c768a586e224adbd4ab`; re-add
   `CLASS=HeroRace1011Tests METHOD=test_1011_names_two_checks_and_no_posthumous`. `docs/features/hero-race.md`:
   before the paragraph that starts `Position offsets per race are stored` insert the
   `**Check lines for the in-game verdict reader (#1072).**` paragraph from `git show 75c0c27a3:docs/features/hero-race.md`
   with `` `restored dead_restored dead_seen wraiths skipped mismatch=0 saved` `` changed to
   `` `restored dead_restored dead_seen skipped mismatch=0 saved` ``; in the component diagram replace
   `    |-> OnBeforeSave -> RacePersistenceService.CaptureHeroRaces()` with
   `    |-> OnBeforeSave -> RacePersistenceService.CaptureHeroRacesForSave()   writes taom.race-persist.capture`
   and directly after the line `    |       legend absent (pre-#330 save): legacy raw-int restore` add
   `    |       then reads every race back and writes taom.race-persist.restore`. Emitter bullet: `RacePersistenceServiceTests.cs`.
   **Verify**: Reader tests `OK`, `Ran 122 tests`.
5. **Commit** (`msg-139-C3.txt`; the 5 Main files, the 3 test files, 12 fixtures, `1011.toml`, the test file, the two docs):
   ```text
   feat(hero-race): v2.0.28 - race persistence writes check lines

   Race persistence writes check lines that show, from the log alone,
   whether dead heroes kept their race across a save and a load (#1011).
   Each save writes taom.race-persist.capture with the hero and dead
   counts (dead=0 fails: every campaign starts with a dead lord_E6_2).
   Each load writes taom.race-persist.restore after reading every race
   back: mismatch=0 passes, a new campaign or a save with no record
   skips, an empty map from the save's record fails. The hero roster
   reports IsAlive. No saved field changes.

   Research: v1.4.8 Hero.IsAlive, Hero.FindAll; lord_E6_2 is
    alive="false" in characters/heroes.xml:152-154
   Not-tested: a save in one session and its load in the next
   Save-compat: no saved field changes
   Ported-from: e4dbdf9c4f50ff3d680471602611d04abef6ac65
   Ported-from: 40006d078a5ff4098d173eb30e3697cac911e5b3
   Ported-from: 75c0c27a3d3cd0cce416de72639ccd04ffac975f
   ```

### Step C4: A daily log check for AI castle recruits (#918; `852678f86`, `da5c87c23`)

The 1.5.x service takes a `VolunteerProductionService` (War Chronicle, OUT) and the observer calls
`KingdomWarSnapshotAdapter.TryFloorElapsedDays` (War Chronicle, OUT): keep this line's constructor and carry
a local copy of the 9-line floor (orchestrator note K1). A three-way merge onto a synthetic post-WP131 file
gave two conflict blocks in the service, none in the interface or behavior; the test file is done by script.

1. **RED.**
   - New `TAOM.Tests/Features/CastleRecruitment/CastleRecruitDayTests.cs`: namespace
     `TAOM.Tests.Features.CastleRecruitment`, `[TestClass] public class CastleRecruitDayTests`, the six tests of
     `git show e45f6df2c:TAOM.Tests/Adapters/KingdomWarSnapshotAdapterTests.cs` whose names start
     `TryFloorElapsedDays_` (lines 51-94 there), with `KingdomWarSnapshotAdapter.` replaced by `CastleRecruitDay.`,
     a one-line summary ("the day key of the #918 check; cases ported from 1.5.x KingdomWarSnapshotAdapterTests"),
     usings `Microsoft.VisualStudio.TestTools.UnitTesting` and `TAOM.Features.CastleRecruitment`.
   - The service tests, by script:
     ```bash
     WT$ python -I - <<'PY'
     import subprocess
     from pathlib import Path
     SRC = "TAOM.Tests/Features/CastleRecruitment/CastleRecruitmentServiceTests.cs"
     b = subprocess.run(["git", "show", "da5c87c23:" + SRC], capture_output=True, check=True).stdout.decode("utf-8").split("\n")
     s = b.index("    // --- The #918 in-game check: the blocked counter, the recruit classifier and the daily line ---")
     e = b.index("    private static float Curve(int slot) => 0.75f * (float)System.Math.Pow(0.85, slot + 1);")
     block = b[s - 1:e]
     while block[-1].strip() == "": block.pop()
     assert not any(x in "\n".join(block) for x in ("_war", "Curve(", "VolunteerProduction")) and "\n".join(block).count("[TestMethod]") == 12
     p = Path(SRC); raw = p.read_bytes().decode("utf-8"); nl = "\r\n" if "\r\n" in raw else "\n"; L = raw.split(nl)
     def one(x):
         assert sum(1 for l in L if l == x) == 1, x
         return L.index(x)
     if "using System.Linq;" not in L: L.insert(one("using Microsoft.VisualStudio.TestTools.UnitTesting;"), "using System.Linq;")
     if "using TAOM.Core.Logging;" not in L: L.insert(one("using TAOM.Features.CastleRecruitment;"), "using TAOM.Core.Logging;")
     i = one("    private ICastleRecruitmentSettingsProvider _settings = null!;")
     L[i + 1:i + 1] = ["    private IModLogger _logger = null!;", "    private OutcomeLog _outcomes = null!;"]
     i = one("        _sut = new CastleRecruitmentService(_settings);")
     L[i:i + 1] = ["        // The real outcome log over a fake logger, so the tests read the exact [Check] lines the reader reads.",
                   "        _logger = Substitute.For<IModLogger>();", "        _outcomes = new OutcomeLog(_logger);",
                   "        _sut = new CastleRecruitmentService(_settings, _outcomes);"]
     end = L.index("}"); assert L[end - 1] == "    }"
     L[end:end] = block
     p.write_bytes(nl.join(L).encode("utf-8"))
     print("inserted 12 tests")
     PY
     ```
     (Dry-run this session on `1b00d1acd`'s file plus `0d3f2bc88`'s hunk: every anchor found once, 33 tests after.)
   - Mirror tests: `WT$ git diff 852678f86^ da5c87c23 -- TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs | git apply --3way; echo "exit=$?"`.
     On a conflict (WP131 adds a transpiler-site test to this file), keep HEAD, make sure `using TaleWorlds.CampaignSystem;`
     is present, and insert before the class's closing brace the three tests
     `ApplyInternal_StillRaisesOnTroopRecruited_InInstalledEngine`, `RecruitVolunteersFromNotable_StillReachesApplyInternal_InInstalledEngine`,
     `GarrisonAutoRecruit_UsesTheGarrisonQuery_InInstalledEngine` and the `CallsMethod` helper, from
     `git show da5c87c23:TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs` (do not add a second
     `CallsMethod` if HEAD has one with the same signature); add to the class summary the 1.5.x sentence that
     starts `For the #918 check, that recruit still reaches`.
   **Verify (RED)**: Build fails (`CastleRecruitDay`, the 2-argument constructor, `ReportCastleRecruitDay`).
2. **GREEN.** New `Main/Features/CastleRecruitment/CastleRecruitDay.cs`:
   ```csharp
   using System;
   using TAOM.Core.Validation;

   namespace TAOM.Features.CastleRecruitment;

   /// <summary>
   /// The campaign day the #918 check keys its daily line by (#1072). The same floor as 1.5.x's War Chronicle
   /// <c>KingdomWarSnapshotAdapter.TryFloorElapsedDays</c>, which is not on this line.
   /// </summary>
   internal static class CastleRecruitDay
   {
       /// <summary>
       /// Whole days, floored; false (day 0) for null, a non-finite or negative value, or one at or past 2^31.
       /// The check runs on a double, where int.MaxValue is exact: the float bound would round up to 2^31,
       /// which the cast would turn into int.MinValue.
       /// </summary>
       internal static bool TryFloorElapsedDays(float? days, out int day)
       {
           day = 0;
           if (!days.HasValue || !FiniteFloatValidator.IsFiniteInRange((double)days.Value, 0d, int.MaxValue))
               return false;
           day = (int)Math.Floor(days.Value);
           return true;
       }
   }
   ```
   Then `WT$ git diff 852678f86^ da5c87c23 -- Main/Features/CastleRecruitment/CastleRecruitmentService.cs Main/Features/CastleRecruitment/ICastleRecruitmentService.cs Main/Features/CastleRecruitment/Hooks/CastleRecruitmentBehavior.cs Main/Features/CastleRecruitment/Hooks/CastleRecruitObserver.cs | git apply --3way; echo "exit=$?"; git diff --name-only --diff-filter=U`
   Expected: only `CastleRecruitmentService.cs` unmerged. Resolve: the usings block keeps HEAD's and adds
   `using System.Globalization;` and `using TAOM.Core.Logging;` (not `TAOM.Features.TroopProgression`); the
   fields and constructor block becomes
   ```csharp
       private readonly IOutcomeLog _outcomes;
       private readonly OutcomeCounter _aiBlocked;
       private readonly OutcomeCounter _aiRecruited;

       // The #918 day tally. Main thread only: the recruit event and the daily tick run on the campaign thread.
       private long _blockedBase;
       private long _dayAiRecruits;

       public CastleRecruitmentService(ICastleRecruitmentSettingsProvider settings, IOutcomeLog outcomes)
       {
           _settings = settings;
           _outcomes = outcomes;
           _aiBlocked = outcomes.Counter(AiBlockedCounterId);
           _aiRecruited = outcomes.Counter(AiRecruitedCounterId);
   ```
   In `Hooks/CastleRecruitObserver.cs` replace `KingdomWarSnapshotAdapter.TryFloorElapsedDays(` with
   `CastleRecruitDay.TryFloorElapsedDays(`, delete `using TAOM.Adapters;`, and change `in vanilla v1.5.6 raises`
   to `in vanilla v1.4.8 raises` (verified, `RecruitmentCampaignBehavior.cs:606-638`). **Verify**:
   `WT$ grep -c 'VolunteerProductionService\|_production\|KingdomWarSnapshotAdapter\|v1.5.6' Main/Features/CastleRecruitment/CastleRecruitmentService.cs Main/Features/CastleRecruitment/Hooks/CastleRecruitObserver.cs`
   prints `0` for both; `git diff --cached --check` prints nothing.
3. **Verify (GREEN)**: Build exit 0; Filtered `--filter "FullyQualifiedName~CastleRecruit|FullyQualifiedName~TroopProgression"`:
   `Failed: 0`; the three new mirror tests pass (not Inconclusive). If a container wiring test that validates
   the castle service now fails for want of `IOutcomeLog`, add `container.RegisterInstance(Substitute.For<IOutcomeLog>());`
   beside its `IModLogger` instance (the 1.5.x pattern: `Main/IoC.cs` registers it, not the feature).
4. **Manifest, fixtures, Python tests, docs.** Fixtures prefix `castle` (5 files); manifest snippet
   `ISSUE=918 FIX=0d3f2bc88e664e10d62589b114757eed2609f117`; re-add `CLASS=CastleRecruit918Tests METHOD=test_918_holds_only_the_ai_off_check`.
   `docs/features/castle-recruitment.md`: after the How-To line that starts `**Disable AI castle recruitment but keep the player path:**`
   add the line that starts `**Check #918 from a play session:**` from `git show da5c87c23:docs/features/castle-recruitment.md`
   (verbatim; its engine claims hold on v1.4.8, see Current state); add `CastleRecruitObserver` to the
   "Boundary classes" list after `CastleAiRecruitDispatcher` (or at its end); in the `## Tests` list, add to the
   `CastleRecruitmentServiceTests` bullet the clause the 1.5.x bullet gained (`the #918 check (the blocked counter,
   the AI-recruit truth table, and every pass, fail and skip line read from a real OutcomeLog)`), and to the
   `CastleRecruitmentEngineMirrorTests` bullet the three facts the 1.5.x bullet names (it still reaches
   `ApplyInternal` through `GetRecruitVolunteerFromIndividual`, `ApplyInternal` still raises `OnTroopRecruited`,
   the garrison auto-recruit still asks `MaximumIndexGarrisonCanRecruitFromHero`); leave the test counts alone
   unless you recount them. Emitter bullet: `CastleRecruitmentServiceTests.cs`.
   **Verify**: Reader tests `OK`, `Ran 128 tests`.
5. **Commit** (`msg-139-C4.txt`; `CastleRecruitDay.cs`, the service, interface, observer, behavior, the 3 test
   files, 5 fixtures, `918.toml`, the test file, the two docs):
   ```text
   feat(recruitment): v2.0.28 - a daily log check for AI castle recruits

   With AI Recruits From Castles off, the log says at each daily tick
   whether the setting held (#918). taom.castle-recruit.ai-off, keyed by
   the campaign day, passes when no AI lord took a castle notable's
   volunteer and counts the castle rule's refusals (blocked=); it fails
   when one did, with a second line naming the castle, the lord, the
   notable and the troop; it skips with reason=toggle-on. The reader
   passes after three days that each turned an AI lord away. The day
   floor is a local copy of 1.5.x's War Chronicle helper.

   Research: v1.4.8 RecruitmentCampaignBehavior ApplyInternal raises
    OnTroopRecruited (:606-638); garrisons use their own query (:124);
    Campaign.CreateCampaignEvents reruns the loaded day only for a save
    made in its first half (dump Campaign.cs:1002-1011)
   Not-tested: three in-game days with AI castle recruiting off
   Ported-from: 852678f8600969deeb0f46627002ee7834275ecd
   Ported-from: da5c87c23b1ef61e900653b80e0fc434a7eac7b7
   ```

**Part C done**: Test command `Failed: 0`, total = the Part B total + 26 (C1) + 7 (C2) + 20 (C3) + 28 (C4) for
the steps that ran (16, 7, 20, 21 if the runner folds data rows); reader tests `Ran 128 tests` when all four ran.

## Part D: the skill hooks and rule rows (Step D1; `c5705474e`, `53e13cfe3`, `95647d13b`)

### Step D1: `/verify` and `/release` report owed verdicts

Precheck: `WT$ grep -c 'Owed in-game verdicts' .claude/skills/verify/SKILL.md; grep -c '| Outcome lines |' .claude/rules/csharp-architecture.md; grep -c 'ingame_verdict' .claude/skills/release/SKILL.md`
Expected `0 0 0`; a `1` means another WP synced that file: skip that file's edit and report it.

1. `.claude/skills/verify/SKILL.md`: directly above `## Output Format` insert:
   ````text
   ## Step 6: Owed in-game verdicts (information only)

   ```bash
   python tools/ingame_verdict.py --owed --sessions 10 2>&1
   ```

   It asks `gh` for the issues labelled `triage-needs-ingame`, reads the ten newest game sessions' logs
   against each one that has a `tools/ingame_checks/<issue>.toml`, and prints a verdict per check
   ([ingame-verdicts.md](../../../docs/features/ingame-verdicts.md)). Report each issue's verdict,
   every FAIL with its reason, the newest session's `summary` line once and each `WARNING: errors=`
   line once. Give the `no manifest:` list as a count only.

   This step never fails verify and never changes "Ready for commit". NOT REACHED (exit 4) is the
   normal state before anyone has played the build. A FAIL (exit 1) is evidence against the logged
   build named in the output; whether it touches this tree is for investigation and Mike. Exit 3 (`git`
   missing or `gh` failed; the message says which) is NOT RUN; a build sha `git` cannot compare leaves
   that session NOT REACHED. Ten sessions let a two-session check such as #1011 pass, and per check the
   newest PASS or FAIL wins. Exit 2 is a malformed manifest, which Step 2b's `test_ingame_verdict` also
   fails on. Never pass `--post` here: it comments on public issues.

   ````
   and in the report block, after the line that starts `CHANGELOG:`, add
   `In-game:    [I issues judged: P PASS, F FAIL, N NOT REACHED / NOT RUN (gh)] (information only)`.
2. `.claude/skills/release/SKILL.md`: directly above the first line that starts `Source the content from`, insert
   (with `ledger-yes` from Step 0; the 1.5.x text minus its #981 sentence):
   ```text
   First, before writing a line of either note, run
   `python tools/ingame_verdict.py --owed --sessions 10` and read its output
   ([ingame-verdicts.md](../../../docs/features/ingame-verdicts.md)). It never blocks the release. Ten
   sessions let a two-session check such as #1011 pass, and per check the newest PASS or FAIL wins. A
   FAIL (exit 1) is evidence against the logged build the output names: name it to Mike before Phase 6,
   because holding the release for it is the maintainer's call. Exit 3 (`git` missing or `gh` failed) means the
   testers' checklist says the verdicts are missing. Every owed check that is not PASS goes into the
   testers' checklist below, one line each: the issue, the check id, its verdict and its `needs` text,
   FAIL lines first. They are developer text, so they stay out of the Discord note.

   ```
   and in the sentence that writes `docs/releases/vX.Y.Z-testers.md`, after `under a first line naming the release`
   add `, followed by an "Owed checks not PASS" section with the verdict lines from the run above`.
   With `ledger-no` (no testers' checklist on this line): insert the same paragraph, but replace everything
   after `because holding the release for it is the maintainer's call.` with:
   ```text
   Exit 3 (`git` missing or `gh` failed) means the verdicts are missing:
   say so to Mike. Report every owed check that is not PASS to Mike, one line each: the issue, the check
   id, its verdict and its `needs` text, FAIL lines first. They stay out of the Discord note.
   ```
3. `.claude/rules/csharp-architecture.md`: after the table row for the `?.` for computed properties rule add the
   two rows that start `| Outcome lines |` and `| Outcome cost |`, copied from lines 37-38 of
   `git show 95647d13b:.claude/rules/csharp-architecture.md` (the `Outcome lines` row there already names
   `// swallow-ok: <reason>`); their link `../../docs/features/ingame-verdicts.md` resolves from there.
4. **Verify**: `WT$ grep -c '## Step 6: Owed in-game verdicts' .claude/skills/verify/SKILL.md; grep -c 'ingame_verdict.py --owed --sessions 10' .claude/skills/release/SKILL.md; grep -c '| Outcome' .claude/rules/csharp-architecture.md; grep -c '981\|ds-data' .claude/skills/verify/SKILL.md .claude/skills/release/SKILL.md`
   prints `1`, `1`, `2`, then `0` for both files; no long dash in the added lines
   (`git diff -U0 | grep '^+' | grep -c $'\xe2\x80\x94\|\xe2\x80\x93'` prints `0`).
5. **Commit** (`msg-139-D1.txt`; the three files):
   ```text
   feat(tools): v2.0.28 - /verify and /release report in-game verdicts

   /verify gains Step 6, which runs python tools/ingame_verdict.py --owed
   --sessions 10 and reports the verdicts as information only: NOT
   REACHED is the normal state before anyone plays, and a FAIL is shown,
   never counted against the tree. /release runs the same command before
   the notes are written, lists every owed check that is not PASS for
   the testers, and names a FAIL to Mike before the release commit. The
   C# architecture rule gains two rows on IOutcomeLog and its cost.

   Research: none (skill and rule text)
   Not-tested: a /release run with owed verdicts
   Ported-from: c5705474ed5899a8bd5576436e572ff3614ca593
   Ported-from: 53e13cfe35d72f4253c81df7b1f7db24ededc457
   Ported-from: 95647d13b230016d28c6f51732f6d1b315b82bc5
   ```

## Part E: the silent-catch ratchet (Step E1; `cd4fb3719`, `00fddd3c5`)

### Step E1: Report swallowed exceptions; pin the silent catches

Simulation onto `1b00d1acd`: `cd4fb3719` conflicts in the Patch49 army file (2 blocks), the Patch20 character
creation file (1), `FaceGenVM_Refresh_RaceFilter_Patch.cs` (1: it adds `ResolveFactionPresets`, absent here),
`crash-report.md` (2), and the two 1.5.x-only Enlistment hooks; after WP112 (which brings `0002cb561`, the
pre-image of the Patch49 and Patch20 hunks) fewer are expected. Of the other files, 7 equal `cd4fb3719^` on
`1b00d1acd`, 8 differ from it only by 1.5.x's `[ClientOnly]` line (no hunk touches it), and
`OOBOverlayService.cs` differs more but merged cleanly. `00fddd3c5`'s Main file
(`Patch35_OrderOfBattleVM_Finalize.cs`) also differs only by `[ClientOnly]`.

1. **Picks.** `WT$ git cherry-pick -n cd4fb3719; echo "exit=$?"; git diff --name-only --diff-filter=U`
   Resolve:
   - `Main/Features/Enlistment/Hooks/EncounterGameMenuOrderAttackConditionPatch.cs` and
     `MapEventHelperCanMainPartyLeaveBattlePatch.cs` (1.5.x only): `git rm -q -f <path>`.
   - `FaceGenVM_Refresh_RaceFilter_Patch.cs`: in the block that adds `ResolveFactionPresets`, keep HEAD's side
     (nothing). The file must then hold exactly three `Outcomes.Swallowed("CharacterCreation", "FaceGenVM_Refresh_RaceFilter_Patch.`
     calls (`ResolveFilterService`, `ResolvePlayerSwitchSession`, `ResolveLogger`).
   - Any other conflicted Main file (expected: none, or the Patch49 or Patch20 file): keep HEAD's text
     (WP112's v1.4.8 citations) and apply that file's `cd4fb3719` hunks by hand from
     `git show cd4fb3719 -- <file>`: each turns a `catch` into `catch (Exception ex)` with one
     `Outcomes.Swallowed("<feature>", "<Class.Method>", ex);` and keeps the fallback, or changes a comment to
     `swallow-ok: <reason>`, plus `using TAOM.Core.Logging;`. A hunk whose catch no longer exists: STOP.
   - `docs/features/crash-report.md`: `git checkout --ours docs/features/crash-report.md`, then add after the
     `**Session summary (#1071).**` paragraph the `**Swallowed exceptions and the silent catch ratchet (#1073).**`
     paragraph from `git show cd4fb3719 -- docs/features/crash-report.md`, changing its words ``of `Main` and `Client` ``
     to ``of `Main` ``; and as the first item under `## Changelog` a dated copy of the 1.5.x
     `**Swallowed exceptions reported, silent catch ratchet** (#1073)` line, marked `(#1073, port)`.
   `git add` each resolved path; then `test -f "$(git rev-parse --git-path CHERRY_PICK_HEAD)" && git cherry-pick --quit`;
   then `WT$ git cherry-pick -n 00fddd3c5; echo "exit=$?"; git diff --name-only --diff-filter=U` (expected
   `exit=0`, nothing unmerged; on a conflict in the three test files take `git show 00fddd3c5:<path>`).
2. **Adapt the tests to this line** (Edit tool, `TAOM.Tests/Infrastructure/SilentCatchRatchetTests.cs`):
   - summary: `Hooks file of Main or Client` becomes `Hooks file of Main`;
   - `SelectionPositiveControl`: `Main/Features/TroopWeight/Diagnostics/TroopRoster_SetElementNumber_Patch.cs`
     (absent here) becomes `Main/Features/CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs` (a
     `[HarmonyPatch]` class outside Hooks on this line), and `selected.Count > 500` becomes
     `selected.Count > 250` with the comment `// 1.4.x: 313 files sit under Hooks at 1b00d1acd (1.5.x asserts > 500).`;
   - delete the `[DataRow(...)]` line whose site is `FaceGenVM_Refresh_RaceFilter_Patch.ResolveFactionPresets`.
   In `TAOM.Tests/Infrastructure/SilentCatchBaseline.txt` replace everything with this header only (the
   1.5.x counts are not this line's):
   ```text
   # Silent catch blocks per file, in Harmony patch and Hooks files of Main (SilentCatchRatchetTests).
   # A catch is silent when its body neither logs, rethrows, calls Outcomes.Swallowed nor carries a
   # "// swallow-ok: <reason>" comment. Counts only fall: fix a catch, then lower its line in the same commit.
   # Format: <count> <repo-relative path>, sorted by path (ordinal), counts of 1 or more.
   # Files: 0. Silent catches: 0.
   ```
3. **Regenerate the baseline from the scanner itself.** Build, then
   `WT$ dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SilentCatchRatchetTests" > C:/Users/mikew/source/repos/taom-port/139-tmp/ratchet.txt 2>&1; echo "exit=$?"`
   Expected: non-zero exit, and only `NoFileGainsASilentCatch` failed (its message lists each file as
   `<path>: N silent, baseline 0, at lines ...`). Any other failure, `EveryCatchParses` included: STOP (the
   1.5.x rule is "fix SilentCatchScan, never the source"; that is not this plan's call). Then:
   ```bash
   WT$ python -I - <<'PY'
   import re
   from pathlib import Path
   text = Path(r"C:\Users\mikew\source\repos\taom-port\139-tmp\ratchet.txt").read_text(encoding="utf-8", errors="replace")
   rows = {m.group(1): int(m.group(2)) for m in re.finditer(r"(\S+\.cs): (\d+) silent, baseline 0, at lines", text)}
   assert rows and all(p.startswith("Main/") for p in rows), sorted(rows)[:5]
   p = Path("TAOM.Tests/Infrastructure/SilentCatchBaseline.txt")
   head = [l for l in p.read_text(encoding="utf-8").splitlines() if l.startswith("#") and not l.startswith("# Files:")]
   body = [f"{n} {path}" for path, n in sorted(rows.items())]
   p.write_text("\n".join(head + [f"# Files: {len(rows)}. Silent catches: {sum(rows.values())}."] + body) + "\n", encoding="utf-8", newline="\n")
   print(len(rows), "files,", sum(rows.values()), "silent catches")
   PY
   ```
   Record the two numbers for the commit body. Rerun the filtered command: `Failed: 0`.
4. **Verify**: Build exit 0; Filtered `--filter "FullyQualifiedName~SilentCatchRatchetTests|FullyQualifiedName~CharacterCreation|FullyQualifiedName~ArmyTargeting|FullyQualifiedName~Enlistment|FullyQualifiedName~CompanionTactics|FullyQualifiedName~HeroRace|FullyQualifiedName~BehaviorTree"`:
   `Failed: 0`; `SweptSite_CallsSwallowed_AndKeepsItsFallback` reports 16 passing rows.
   `WT$ git grep -l 'Outcomes.Swallowed(' -- Main | wc -l` is at least `13` (the 13 swept files).
   Then the entry-point gate, because these hunks add lines to Harmony patch classes: Filtered
   `--filter "FullyQualifiedName~EntryPointLineCountGateTests"`: `Failed: 0` is the expected result, and then
   nothing changes. On a failure, act on what its message says, then Build and rerun until `Failed: 0`:
   `<class>: <N> lines is below its baseline`: rerun the regeneration command of C1 item 4.
   `<class>: grew to N lines from M`: edit that row's number to N by hand (the regeneration keeps the lower
   number, so it cannot raise one). `<class>: N lines (limit 150)`: add the row and raise `MaxBaselineEntries` by
   one, as C1 item 4's script does with that class as `KEY`. Name every moved row in the hand-off.
5. **Commit** (`msg-139-E1.txt`; stage the resolved Main files by name:
   `Main/BehaviorTreeWrapper/BehaviorTreeBannerlordWrapper.cs`,
   `Main/Features/ArmyTargeting/Hooks/{AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch,Army_FindBestGatheringSettlementAndMoveTheLeader_Patch}.cs`,
   `Main/Features/CharacterCreation/Hooks/{CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch,CharacterCreationContent_SetSelectedCulture_Patch,CharacterCreationCultureStageVM_OnCultureSelection_Patch,CharacterCreationNarrativeStageView_RefreshAgentVisuals_BodySync_Patch,CharacterCreationReviewStageVM_AutoFillName_Patch,FaceGenVM_Refresh_RaceFilter_Patch}.cs`,
   `Main/Features/CompanionTactics/FormationPresets/Hooks/{Patch35_OOBUIHandler_Finalize,Patch35_OOBUIHandler_Tick,Patch35_OrderOfBattleVM_Ctor,Patch35_OrderOfBattleVM_Finalize}.cs`,
   `Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs`,
   `Main/Features/CompanionTactics/Roles/Hooks/Patch35_OOBHeroItem_RefreshValues.cs`,
   `Main/Features/Enlistment/Hooks/{GameMenuManagerSetNextMenuPatch,LordConversationsConditionPatches,MapStateEnterMenuModePatch,Patch85_EnlistedDetachDeferral}.cs`,
   `Main/Features/HeroRace/Hooks/CharacterSpawner_InitWithCharacter_Patch.cs`, the three
   `TAOM.Tests/Infrastructure/SilentCatch*` files, `docs/features/crash-report.md`, plus `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` and
   `EntryPointLineCountGateTests.cs` if item 4 moved a row; `git diff --cached --name-only | wc -l` prints `24`, or `25` or `26` with those):
   ```text
   feat(logging): v2.0.28 - report swallowed exceptions, add a ratchet

   Silent catch blocks in the enlistment lord-conversation gate,
   character creation, hero race spawning, the order of battle overlay,
   army targeting and the creature behaviour tree wrapper now write one
   [Swallowed] warning per site and a count in the [Session] summary.
   Every fallback is unchanged; catches that are silent on purpose (a
   failing log call) carry a swallow-ok comment. The behaviour tree
   wrapper's other three failure paths reach the log the same way.

   SilentCatchRatchetTests counts silent catches in every Harmony patch
   and Hooks file of Main against a per-file baseline (<F> files, <C>
   catches on this line). A new silent catch fails the tests; a fixed
   one lowers the baseline in the same commit.

   Research: none (no engine member changes)
   Not-tested: the [Swallowed] lines in a real session
   Ported-from: cd4fb371983b6250c3a58a0d9a857da1f1c15059
   Ported-from: 00fddd3c5ebfbd89c7b37363fc3aec80d36af2ec
   ```

**Part E done**: Test command `Failed: 0`, total = the Part C total + 74 (12 if data rows fold).

### Step F: Final regression and report

`WT$ git status --porcelain` prints nothing. Build exit 0. Test: `Failed: 0`, total
`BASE_TOTAL + 28 + <C steps> + 74` (Done criteria), skipped = `BASE_SKIPPED`. Tool tests: `BASE_TOOLS + 128`
(fewer by 1, 7, 14, 6 for a skipped C1, C2, C3, C4), no failure beyond `BASE_TOOL_FAILS`. Docs: no new
finding. `WT$ git log --format='%s' BASE..HEAD` lists the commits made, each with the `v2.0.28 -` label.
Hand-off report (the integrator records it; you do not edit `plans/README.md`, and no old-plan row is
affected): status per Part, each commit's hash and subject, the totals, which C steps ran or were skipped
(and why), the baseline numbers from E1, and any `/deep-review` not run.

## Disposition

| 1.5.x commit | Subject (short) | Disposition |
|---|---|---|
| `cb4eeaa3d` | check lines, counters and a session summary | **Transplanted** (A1): IOutcomeLog/OutcomeLog, 3 test files, IoC, SubModule, save, crash seams, 2 docs. Dropped: Reeve's Ledger mixin and wiring test (1.5.x-only feature), `ScreenFaultLog` hunk and tests (absent; WP140 leaves it out too), `EarlyLogBridge` placement (absent: the facade follows `IoC.Configure()`) |
| `c8d69e426` | in-game check verdicts read from session logs | **Transplanted** (B1) at the `e45f6df2c` end state. `970.toml` and `981.toml` become test fixtures (981: D7); 1.4.x manifests written per issue in Part C; `tools/README.md` row to WP142 |
| `c5705474e` | /verify and /release report in-game verdicts | **Split**: reader, tests, fixtures, feature doc in B1; skill steps and rule rows in D1 (the /release text rewritten for this line's phases) |
| `53e13cfe3` | verdict summaries skip [Swallowed] lines | **Split** like `c5705474e`; its #981 re-run note dropped (D7) |
| `95647d13b` | tighten the in-game verdict reader and its docs | **Split** like `c5705474e`; #981 dedicated-server folder text dropped (D7) |
| `b4ad748f2` | battle action bar attach and stance checks | **Ported** (C1, net diff, view path `Client/` to `Main/`); fixtures and generic tests in B1; manifest gate scans `Main` only |
| `30a938689` | fail #970 attach check on an unregistered bar | **Ported** with `b4ad748f2` (C1) |
| `0bc2a132f` | pin the battle action bar attach verdicts | **Ported** with `b4ad748f2` (C1) |
| `852678f86` | a daily log check for AI castle recruits | **Adapted** (C4): no `VolunteerProductionService` parameter, local `CastleRecruitDay` floor (K1), `VolunteerProductionWiringTests` hunk dropped (file is 1.5.x-only) |
| `da5c87c23` | castle check wording matches the engine | **Ported** with `852678f86` (C4); its "first half of the day" claim re-checked on v1.4.8 (holds) |
| `0ab3dc35a` | career screen open and init check lines | **Ported** (C2) after WP129's `0761c01b7` |
| `4e0c2a3b0` | pin the career open check's type test and fail | **Ported** (C2); needs `ExtractReferencedMembers` (lifted from `5fcf8390c`) |
| `2d6934006` | correct career check wording, pin init detail | **Adapted** (C2): RealmUI wording dropped (no RealmUI coupling on this line); the CreateScreen sentence kept (holds on v1.4.8) |
| `e4dbdf9c4` | race persistence writes check lines | **Adapted** (C3): no #644 (no `INazgulRegistry`, no `wraiths=` field) |
| `40006d078` | a save with no race record skips its check | **Ported** with `e4dbdf9c4` (C3) |
| `75c0c27a3` | dead-hero checks fail on a lost roster | **Adapted** with `e4dbdf9c4` (C3): fixtures lose `wraiths=` |
| `cd4fb3719` | report swallowed exceptions, add a ratchet | **Ported** (E1, cherry-pick -n): two 1.5.x-only Enlistment hooks and `ResolveFactionPresets` dropped; baseline regenerated on this line; selection control adapted |
| `00fddd3c5` | tighten the silent catch scan, report one more | **Ported** (E1) |
| `92839bc52` (shared hunk, K1-01) | siege troop picker (SKIP-FEATURE) | **Hunk only** (C2): `IlCallScanner.ExtractOpCodes` |
| `5fcf8390c` (shared hunk, found here) | co-op server (SKIP-FEATURE) | **Hunk only** (C2): `IlCallScanner.ExtractReferencedMembers`, which `4e0c2a3b0`'s test calls |
| `1165a00de`, `de90a2451` (WP112 handoff) | hollow parent repair | **Completed** (A2 IOutcomeLog shape; B1 `1076.toml`, `HollowParent1076Tests`, fixtures, doc mentions) |
| `d13b88784`, `d8b950950`, `7ae4b8323`, `20af1716e`, `66dbb07d8`, `0add6d5c7`, `588d1c88e` (merges) | branch merges | Their resolutions are inside the `e45f6df2c` end state B1 and the C steps read |
| `981.toml`, `1079.toml`, later 1.5.x manifests | co-op server settings, Long Defeat, newer features | **Not ported** (D7; features) |

## Test plan

- C# results added: A1 +28, C1 +26, C2 +7, C3 +20, C4 +28, E1 +74; total +183 (+104 if the runner folds data
  rows; MSTest 3.1.1 normally lists each row). Python: `test_ingame_verdict.py` 100 after B1, 128 after C4.
- Patterns: `StaleCharacterParentRepairTests` (fake `IOutcomeLog`), `CareerScreenChecksTests` (real `OutcomeLog`
  over a fake `IModLogger`, its rendered lines checked against the Python fixtures).
- Not unit-testable (`Not-tested:`): the seams in a live session, the battle view in a field battle, the career
  click, a save/load pair, three days of castle recruits, the `[Swallowed]` lines.

## Done criteria

Part A: the A1 and A2 verifies hold; `git grep -c 'container.Register<IOutcomeLog, OutcomeLog>(Reuse.Singleton);' -- Main/IoC.cs` is 1; Test total `BASE_TOTAL + 28`, `Failed: 0`.
Part B: `tools/ingame_verdict.py`, `tools/ingame_checks/1076.toml`, `docs/features/ingame-verdicts.md` exist; reader tests `Ran 100 tests` `OK`; `python tools/ingame_verdict.py --issue 1076` exits 4 or 0.
Part C: for each step that ran, its manifest exists, `RepoManifestGateTests` passes (every named `[Check]` id is a literal in `Main`, every `fixed_in` is a commit here), and its C# filter shows `Failed: 0`; `EntryPointLineCountGateTests` shows `Failed: 0`; `git grep -n 'RealmUI\|wraiths=\|keptWraiths\|KingdomWarSnapshotAdapter\|VolunteerProductionService' -- Main/Features/CareerSystem Main/Features/HeroRace Main/Features/CastleRecruitment tools/ingame_checks` prints nothing.
Part D: the D1 verify holds.
Part E: `SilentCatchRatchetTests` all pass; the baseline header totals match its lines (`BaselineIsWellFormed`).
All: Step F holds; `git status --porcelain` is empty; every commit body has its `Ported-from:` lines and no long dash.

## STOP conditions

- Step 0: a missing prerequisite (stamp clock, `FakeStampClock`, `RepoPaths` with `Main` only,
  `IlCallScanner.Fingerprint`, the `[Shutdown]` line, WP112's #1076 ids and `Check` helper, WP107's
  `offMainThread` parameter, WP136's entry-point gate and baseline), the view already in that baseline, or `IOutcomeLog`, `SilentCatchScan` or `tools/ingame_verdict.py` already exists.
- C1: the entry-point gate names a class other than `BattleActionBarMissionView`. A2: an added service line that is not an `_outcomes`/`IOutcomeLog` line. B1: fixture counts other than
  82/59, a test file not at 100 methods, or no commit that ports #1076. C2: `IlCallScanner.cs` differs from
  `a27c6b338` before the lift. C3/C4: a conflict outside the blocks the step describes, or a test that needs
  `INazgulRegistry` or `VolunteerProductionService`.
- E1: a conflicted Main file whose catch no longer exists; any ratchet failure other than
  `NoFileGainsASilentCatch` while regenerating (`EveryCatchParses` included); a baseline path outside `Main/`;
  250 or fewer selected files.
- An engine member behaves or reads differently from "Verified v1.4.8 engine facts": report it; never
  decompile and improvise.
- A verification fails twice after a reasonable fix, or a conflict appears in a path the step does not list.

## In-game checks owed (maintainer; a build deployed with `./build.ps1` from a clean tree that contains this plan's commits)

| Check | What to do | Where to look | Expect |
|---|---|---|---|
| Session summary | Play 12 minutes, save, quit to desktop | `<game>\bin\Win64_Shipping_Client\Logs\taom_debug_*.log` | `[Session] summary reason=heartbeat` after about 10 minutes, `reason=save` just before the save's lines, `reason=exit` just before `[Shutdown] clean module unload`; `shield_swallowed=` present |
| Crash summary | Any captured crash (or the dev crash trigger) | the bundle's `report.txt` and `taom_debug.log` | `[Session] summary reason=crash` before the log tail |
| #970 | Show Battle Action Bar on; field battle; Begin battle; Esc; click a stance | `python tools/ingame_verdict.py --issue 970` | PASS for attach and stance |
| #971 | Character screen, career button | `--issue 971` | PASS for open and init |
| #1011 | Save a campaign; next session load it | `--issue 1011 --sessions 2` | PASS for capture (`dead=` above 0) and restore |
| #918 | Castle Recruitment on, AI Recruits From Castles off; fresh launch, one load, three days | `--issue 918` | PASS (three days with `blocked=` 1 or more) |
| #1076 | Only on a game with a hollow parent | `--issue 1076` | NOT REACHED is normal in a healthy game |
| Swallowed sites | Normal play | the log | at most one `[Swallowed]` WARNING per site, repeats counted in `[Session] swallowed` lines; no behaviour change |
| `/verify` Step 6 | Run `/verify` after a session | its report | an `In-game:` line; nothing blocks |

Never pass `--post` without the maintainer's word: it comments on public issues shared with the 1.5.x line.

## Maintainer steps

- Create the 1.4.x tracking issue (orchestrator) before the first commit lands.
- Play the sessions above; a FAIL is a finding for `/investigate`, not a reason to edit a manifest.
- Hand WP142 these bookkeeping rows (not added here, by the per-port procedure): `tools/README.md` row for
  `ingame_verdict.py` (1.5.x text at `e45f6df2c` minus its Coop and `--ds-*` clauses), `docs/reference/feature-map.md`
  row for `docs/features/ingame-verdicts.md`, and its `docs/INDEX.md` entry.
- If your session could not run `/deep-review`, run it on each Part's diff (probe: the SubModule seams and
  their order, the CrashReportService insertion point, the C3 and C4 conflict resolutions, the baseline).

## Maintenance notes

- **Creates for later WPs**: `IOutcomeLog`/`OutcomeLog`/`OutcomeCounter`/`Outcomes`/`SessionHeartbeat`
  (`Main/Core/Logging/`); `tools/ingame_verdict.py`, `tools/ingame_checks/`, the fixture folder and
  `test_ingame_verdict.py`; `_gamedir.DEFAULT_GAME_ROOT`; `BattleActionBarChecks`, `CareerScreenChecks`,
  `CastleRecruitObserver`, `CastleRecruitDay`; `IRacePersistenceService.CaptureHeroRacesForSave`,
  `HeroRaceInfo.IsAlive`; `IlCallScanner.ExtractOpCodes`/`ExtractReferencedMembers`; the ratchet trio.
  A later port that adds a check line ships its manifest (with a 1.4.x `fixed_in`), fixtures and test class
  together, as Part C does; a later port that adds a catch in a Hooks file must report it or mark it
  `// swallow-ok:`, or lower/raise the baseline in the same commit.
- **WP143** (rules sync) must keep the two `Outcome` rows and `/verify` Step 6; **WP142** adds the bookkeeping
  rows above. A 1.5.x manifest edit after `e45f6df2c` is not reflected in the fixture copies of 970/981 (test-only).
- `BattleActionBarMissionView.cs` is over the 150-line entry-point guideline on 1.5.x too; its decisions live
  in `BattleActionBarChecks`. Splitting it is a separate refactor; C1 lists it in `EntryPointLineCountBaseline.txt`,
  and the split lowers or removes that row.
- The reader's Coop and dedicated-server log support is dormant on this line (no manifest names those kinds).
- The 1.4.x `fixed_in` values are this line's port commits; if the integrator ever squash-merges, rerun the
  manifest snippets so `fixed_in` names a commit on `bannerlord-1.4.5` (`test_every_fixed_in_is_a_commit_in_this_clone` fails otherwise).
- The `IOutcomeLog` summary comment cites a `csharp-architecture.md` section that 1.4.x gains only with WP143.
- Deferred: `ScreenFaultLog` (arrives with no port), the #644 wraith count, co-op `[[human]]` items, 1.5.x
  manifests for features this line lacks.
