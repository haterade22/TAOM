# Lane 3: correctness and resilience (run `2026-09-23-opus`, baseline `b2e387db`)

Read-only audit. Every `path:line` below was read this run against `b2e387db` (working tree equals
HEAD for every path cited unless noted; skip-listed paths read via `git show b2e387db:`). Scratch
scripts live in the session scratchpad (`lane3_*.py`); none writes to the repo.

## Finding format extras

Each finding carries **Delta** (vs June commit `141b749`), **P1** and **Plan candidate** per BRIEF.md.

---

### [CORRECTNESS-01] Ratchet the suppressed nullable warnings folder by folder, starting with Arena

- **Evidence**:
  - `Main/TAOM.csproj:9` `<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625</NoWarn>`;
    `Dependencies/TAOM.Dependencies.csproj:9` suppresses the same seven plus `8374;8174`;
    `Directory.Build.props:6` `<Nullable>enable</Nullable>` (seed F10).
  - `.editorconfig` exists at the repo root (`root = true`, 342 B) and carries only whitespace and
    charset rules: no `dotnet_diagnostic.*` line anywhere (read in full). No nested `.editorconfig`
    under `Main/` (`ls Main/.editorconfig` fails).
  - `Main/TAOM.csproj:113` already references the `Nullable` 1.3.1 polyfill package, so
    `[NotNullWhen]`, `[MaybeNull]` and friends are available on net472: annotation is not blocked by
    the target framework.
  - Measured from the orchestrator's scratch log (`raw/build-nowarn.log`, `-p:NoWarn=`):
    **2,028** distinct diagnostics by message (4,060 lines, each printed twice by MSBuild), **1,890**
    distinct `(file, line, col, code)` locations; the gap is CS8618, which Roslyn reports once per
    field at the same constructor location. 2,025 are in `Main`, 3 in `Dependencies/Foundation`
    (`PatchShield.cs:313`, `SubModuleConstructionGuard.cs:248,253`).
- **Crash-history weighting** (method: `lane3_rca2.py` matches each `Main/Features/<Name>` to
  `docs/reviews/rca-*.md` by filename slug, then counts files containing `NullReferenceException|NRE`;
  26 of 253 RCAs mention an NRE at all). Location counts from `lane3_nowarn.py`:

  | Folder | Warnings (locations) | Files | RCAs by slug | RCAs naming an NRE | Note |
  |---|---|---|---|---|---|
  | `Dependencies/Foundation` | 3 | 2 | 1 | 0 | PatchShield is the crash-containment layer itself |
  | `Features/Arena` | 21 | 5 | 5 | 1 (#407 tournament NRE, `rca-patch69-tournament-guard-2026-08-07.md:3`) | plus exit hang and exit AV RCAs |
  | `Features/FieldCommission` | 48 | 14 | 4 | 3 (`rca-field-commission-reset-equipments-2026-08-20.md:3` "threw `NullReferenceException` inside vanilla") | Patch71 alone carries 12 |
  | `Features/CulturalFeats` | 140 | 9 | 5 | 2 (`rca-culturefeat-partyculture-nre-2026-06-15.md:1`) | 131 of 140 are CS8618 on feat fields: low signal |
  | `Features/Enlistment` | 143 | 40 | 10 | 1 | biggest mixed-code folder, highest RCA count |
  | `Adapters` | 216 | 46 | n/a | NRE RCAs cite adapters indirectly | 90 CS8603 + 44 CS8625: the engine-null boundary |
  | `Features/Siege`, `PlayerSwitcher`, `StaleCharacterRepair`, `DreadAura`, `BanditManagement` | 8, 6, 2, 3, 8 | 1 to 3 | 1 to 4 | 1 each | cheap wins |

- **Impact**: the compiler already computes 2,028 null-flow diagnostics per build and the csproj throws
  them away. 26 RCAs record NRE crashes or NRE-class review findings (the CultureFeat party-culture
  NRE crashed the campaign map tick; #407 crashed tournaments; Patch71 reproduced a vanilla NRE). New
  code in every folder lands with no null-flow signal at all, so the backlog only grows (the digest's
  folder list includes 2026-09 folders such as `Mumakil` 16, `PreloadBodyGuard` 2, `Elk` 2).
- **Mechanism (exactly what must change)**: while the seven ids stay in `<NoWarn>`, an `.editorconfig`
  severity cannot re-enable them for a folder (the compiler's `/nowarn` wins over tree options for a
  suppressed id, as the brief states; not re-tested this run because building is out of scope). So:
  1. Delete `8600;8601;8602;8603;8604;8618;8625` from `Main/TAOM.csproj:9` (keep `$(NoWarn)`), and the
     same seven from `Dependencies/TAOM.Dependencies.csproj:9`.
  2. In the root `.editorconfig`, under `[*.cs]`, add seven lines
     `dotnet_diagnostic.CS8600.severity = none` (one per id). The build output is then unchanged:
     baseline stays at the 2 BHA warnings.
  3. Per graduating folder, add a nested `Main/Features/<Name>/.editorconfig` (no `root = true`) with
     the seven ids at `= error`. A nested file overrides the root file for every `.cs` under it, and
     the per-folder file doubles as the ownership record ("this folder is null-clean").
     Use `= warning` only for the one build in which the folder is being cleaned; do not leave a
     folder at `warning`, since 2 shipped warnings are the whole baseline and a warning tier becomes
     noise within weeks.
  4. Promotion rule: a folder moves to `error` when a scratch build with its file set to `warning`
     reports zero CS86xx for its path (count with the `lane3_nowarn.py` regex). Fixes are annotations
     (`?`, `= null!` on IoC-injected fields only where the constructor genuinely assigns later, a
     guard clause where the engine can hand back null), never `!` on an engine getter.
  5. Optional gate once three or more folders graduate: a `tools/tests` check that every
     `Main/Features/*/.editorconfig` sets all seven ids to `error`, so a folder cannot quietly
     downgrade.
- **Ordering** (crash-RCA density first, then size): `Dependencies/Foundation` (3, one afternoon, and it
  protects every patch), then **`Features/Arena` as the first `Main` folder** (21 warnings in 5 files,
  5 RCAs, one shipped NRE; the warnings sit exactly in the two guard patches that exist because of
  crashes, `Patch69_TournamentEndGuard.cs:50,55,84` and `Patch69_TournamentRosterGuard.cs:72-74,109`),
  then `Siege`, `PlayerSwitcher`, `StaleCharacterRepair`, `DreadAura`, `BanditManagement` (27 combined),
  then `FieldCommission` (48; the save-data load path at `FieldCommissionSaveData.cs:32-45` is six
  CS8600 plus three CS8604 into `Import*`, a real null-on-load surface), then `Adapters` (216; the
  engine boundary, split by adapter file), then `Enlistment` (143). `CulturalFeats` goes last despite
  2 NRE RCAs: 131 of its 140 are CS8618 on registered-feat fields, which a single `= null!` idiom or a
  `required`-style init resolves without changing any flow.
- **Effort**: S for steps 1 to 3 plus Arena; M per large folder afterwards.
- **Risk**: LOW. Steps 1 and 2 are output-neutral by construction; graduation touches annotations and
  guard clauses only. A folder at `error` can block an unrelated hotfix build; the escape is a one-line
  severity edit, which is reviewable.
- **Confidence**: HIGH on counts and the csproj mechanics; MED on NoWarn-vs-editorconfig precedence
  (taken from the brief, not re-tested; the proposal removes the dependency on it).
- **Delta**: pre-existing (seed F10). **P1**: no. **Plan candidate**: yes, the first two steps plus
  Arena are a clean, verifiable S-sized plan (build output unchanged, then zero CS86xx under Arena).

### [CORRECTNESS-02] Count suppressed failures per catch site and put the counts in the crash bundle

- **Measurement** (`lane3_catch.py` over a `git archive b2e387db Main` extract; brace-matched catch
  bodies, comments stripped; the class is the first match of: body contains `throw` = rethrow; a
  logger call (`Log*(`, `.Log*(`, `Debug.Print`, `InformationManager.`, `MBDebug.`, `Trace.Write`) =
  logged; empty = silent; only `return <literal|default|empty>;` = sentinel; `continue;`/`break;` =
  silent-flow; anything else unlogged = other). Raw grep cross-check over `Main/*.cs` at `b2e387db`:
  `git grep -c -E "catch\s*\(\s*(System\.)?Exception\b"` = **574**, `git grep -c -E "catch\s*(\{|$)"`
  = **476** (the parser finds 573 and 473; the rest are catch keywords inside comments or strings).

  | Kind | Total | logged | silent | sentinel | silent-flow | other (unlogged) | rethrow |
  |---|---|---|---|---|---|---|---|
  | `catch (Exception ...)` | 573 | 496 | 13 | 5 | 0 | 59 | 0 |
  | bare `catch` | 473 | 0 | 311 | 89 | 6 | 65 | 2 |
  | typed (IOException etc.) | 16 | 4 | 0 | 0 | 0 | 7 | 5 |

  Of the 324 silent catches, 38 are nested logger-unavailable fallbacks inside another catch
  (`lane3_nested.py`); **286 top-level silent catches in 144 files** remain. Only 2 catches in all of
  `Main` use a `when` filter. Excluding diagnostic folders (`CrashReport`, `*Diagnostics`,
  `DevConsole`), gameplay code has 740 catches: 475 logged, 148 silent, 38 sentinel, 74 other.

  | Top folders (Exception + bare) | total | logged | silent | sentinel | other |
  |---|---|---|---|---|---|
  | `Features/CrashReport` | 126 | 6 | 65 | 38 | 15 |
  | `Adapters` | 124 | 105 | 7 | 11 | 1 |
  | `Features/BattleLoadDiagnostics` | 89 | 7 | 60 | 6 | 15 |
  | `Features/HeroRace` | 47 | 15 | 11 | 6 | 15 |
  | `Main/SubModule.cs` (read at `b2e387db`) | 44 | 27 | 16 | 0 | 1 |
  | `Features/DevConsole` | 33 | 2 | 6 | 10 | 15 |
  | `Features/SaveLoadDiagnostics` | 28 | 1 | 26 | 0 | 1 |
  | `Features/ArmyTargeting` | 17 | 1 | 15 | 0 | 1 |

  Spot-reading (about 20 sites in 7 folders) says most silent catches are deliberate and correct in
  isolation: diagnostic field gathering (`ArmyTargeting/Diagnostics/SiegeGatheringFailureInfo.cs:55-86`,
  13 one-line catches), scene teardown (`FieldCamp/Visuals/CampLayoutBuilder.cs:150-157`), diagnostic
  log lines (`Enlistment/Hooks/EnlistmentBattleBehavior.cs:238-241`), logger fallbacks
  (`Enlistment/Hooks/Patch85_EnlistedDetachDeferral.cs:91`). The defect is not that they are silent but
  that nothing counts them. One gameplay site swallows a whole service call with no log:
  `ArmyTargeting/Hooks/AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:108-111`
  (campaign AI scoring; the comment says "IoC not initialized", but it also hides any service fault
  for the whole session).
- **The log-once latch**: 38 `if (!_xxxLogged)` guards in 29 files (regex
  `if\s*\(\s*!\s*_?\w*(Logged|Warned|Reported|Faulted)\w*\s*\)`), for example
  `Enlistment/Hooks/GameMenuManagerSetNextMenuPatch.cs:31-39` and
  `CompanionTactics/BattleActionBar/Hooks/Patch35_Formation_SetMovementOrder.cs:53-64`. After the first
  failure the site goes dark: a menu redirect that fails on every transition and one that failed once
  leave the same single log line.
- **What reaches a crash bundle today** (read `Main/Features/CrashReport/CrashReportService.cs:173-252`,
  `Collectors/TaomStateCollector.cs:37-49`, `Collectors/LogTailCollector.cs:13,25-41`): identity,
  modules, assemblies, Harmony correlation, campaign and mission snapshots, a fixed TAOM state record
  (career, special resources, feats, revolt tuning, messenger count), MCM, process, memory, GPU, OS,
  env vars, frame timing, the last **500 lines** each of `taom_debug.log`, the rgl log and `diag.log`,
  and `CollectorFailures`, which records only failures of the collectors themselves
  (`CrashReportService.cs:244-252`). So silent, sentinel and "other" catches never reach a bundle, and
  a latched catch reaches it only if its one line is within the last 500 log lines. PatchShield
  already keeps `Interlocked` swallow counters (`Dependencies/Foundation/PatchShield.cs:106-111`), but
  writes them only from a `ProcessExit` handler (`Dependencies/SubModule.cs:251-258`), which a CTD
  never runs, and no crash collector reads them (`git grep PatchShield Main/Features/CrashReport` finds
  comments only).
- **Impact**: crash triage starts blind on "was it degrading before it died". A service that threw and
  was swallowed for an hour (the #634 off-thread class, a stale adapter after load) leaves no count;
  the bundle cannot say "site X swallowed 41,000 exceptions this session". The first-chance exception
  cost of a site throwing per frame is invisible too.
- **Design: one helper**, `Main/Core/Diagnostics/SuppressedFaults.cs` (or `Dependencies/Foundation`, so
  PatchShield can report through it):
  - `public sealed class FaultSite { public readonly string Id; internal long Count; internal int LastTick; internal Type? FirstType; internal string? FirstMessage; }`,
    one per site: `private static readonly FaultSite Site = SuppressedFaults.Site("Enlistment.SetNextMenu");`.
    `Site(...)` appends to a lock-guarded registry list at type initialisation only.
  - `public static bool Note(FaultSite site, Exception ex)`: `var n = Interlocked.Increment(ref site.Count);`,
    stamp `LastTick = Environment.TickCount`, `Interlocked.CompareExchange` the first exception type
    and message into the site (a reference store, no allocation), return `true` when `n` is 1, 10,
    100, 1,000 and so on, so any log volume is logarithmic.
  - `public static void Swallowed(FaultSite site, Exception ex, IModLogger? logger = null)`: calls `Note`
    and on `true` logs `[Fault] {Id} x{n}: {Type}: {Message}` inside its own try/catch, which removes the
    38 nested logger-fallback catches.
  - `SuppressedFaults.Snapshot(int top = 25)` copies the sites with `Count > 0`, sorted by count. A new
    `SuppressedFaultsCollector` joins `CrashReportService.ComposeContext` (one more `Safe(...)` line and
    one `ExceptionContext` field), both renderers print it as a table, and a `[Fault] summary` line is
    written at mission end and game end (PatchShield's summary belongs there too, not only on
    `ProcessExit`).
- **Before and after, the three commonest shapes**:
  1. Log-once latch (38 sites):
     `catch (Exception ex) { if (!_faultLogged) { _faultLogged = true; try { IoC.Resolve<IModLogger>().LogError($"... {ex.Message}"); } catch { } } }`
     becomes `catch (Exception ex) { SuppressedFaults.Swallowed(Site, ex); }` and the static bool goes.
  2. Silent empty catch in gameplay code (148 sites): `catch { }` becomes
     `catch (Exception ex) { SuppressedFaults.Note(Site, ex); }` (count only, no log), still fail-open.
  3. Sentinel return (94 sites): `catch { return null; }` becomes
     `catch (Exception ex) { SuppressedFaults.Note(Site, ex); return null; }`.
  Leave alone: the collectors inside `CrashReport` (the reporter must not depend on anything that can
  fault while it runs) and `SaveLoadDiagnostics`, which has its own `LogFault` channel.
- **Allocation-free paths**: `Note` is one `Interlocked.Increment`, one int store and one compare, with
  the site in a static field: never a string key or dictionary lookup per fault. Sites where that
  matters: `Formation.SetMovementOrder` postfixes (async AI thread,
  `Patch35_Formation_SetMovementOrder.cs:39-48`), `Formation.GetOrderPositionOfUnit` (worker pool),
  `Mission.OnTick` postfixes (`Patch35_Mission_OnTick.cs:7-12` states a zero-allocation contract), the
  settlement nameplate alpha patch (about 3,000 calls/s per `PatchShield.cs:61-66`), the BT decorators
  (per agent per tick) and campaign AI scoring. `Interlocked` alone makes it safe from the off-thread
  engine callbacks named in `.claude/rules/csharp-architecture.md`.
- **Effort**: M (helper, collector, renderer rows, tests), then S per folder migrated.
- **Risk**: LOW. Fail-open behaviour is unchanged; only counting and a logarithmic log are new.
  Migration churn can collide with in-flight sessions: one folder per commit.
- **Confidence**: HIGH on counts and bundle contents; MED on the per-class split (regex classification;
  the spot-read sites all matched their class).
- **Delta**: pre-existing. **P1**: no. **Plan candidate**: yes: helper plus collector is self-contained,
  with a clean test story (one unit test per threshold, one renderer test).

### [CORRECTNESS-03] `Patch35_Mission_OnTick` is a live per-frame postfix with an empty body; a failed resolve retries every frame

- **Evidence**: `Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs:15-36`
  (read at `b2e387db`): the postfix on `Mission.OnTick` resolves settings once, checks
  `EnableFormationPresets`, then does nothing ("Body intentionally empty for now: hotkey-driven preset
  I/O moved to UI buttons", `:32-34`). Its category is applied unconditionally
  (`git show b2e387db:Main/SubModule.cs:1715`). `:27` `try { _settings = IoC.Resolve<...>(); } catch { return; }`
  leaves `_settings` null on failure, so a failing resolve throws and is swallowed on every frame.
  `git grep Patch35_Mission_OnTick b2e387db` finds only the class and docs
  (`docs/features/companion-tactics.md:132,175` still describe it as a live hot-path hook).
- **Impact**: a Harmony wrapper on `Mission.OnTick` runs every frame of every mission for no effect; if
  the resolve ever fails, it becomes a first-chance exception per frame with no log line.
- **Effort**: S. **Risk**: LOW (no callers; delete the file, its API-snapshot row and the two doc lines).
- **Confidence**: HIGH on the empty body and applied category; the wrapper's per-frame cost is not
  measured here.
- **Fix sketch**: delete the patch; add a per-tick hook when one has work to do. Deletion that holds
  parity.
- **Delta**: pre-existing (`55950378`, 2026-05-07). **P1**: no. **Plan candidate**: no; fold into a
  dead-code sweep.

### Harmony inventory (measured, feeds CORRECTNESS-04 to 06)

Method: `lane3_harmony.py` over the `b2e387db` extract; a patch method is `[HarmonyPrefix]`-style
attribute or a method named `Prefix`/`Transpiler`/`Finalizer`; bodies brace-matched.

- **Skip-capable prefixes**: 50 `bool` prefixes in 44 files; 39 contain a literal `return false` (33
  files), 5 more return a computed bool (`return !menus.EnsureMenuOpen(...)`,
  `Refuge/Hooks/RefugeEncounterPatch.cs:67`), 6 never return false (a `bool` signature with no skip
  path, e.g. `BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs:54`,
  `CustomBattles/Hooks/CustomBattleSideVM_UpdateCharacterVisual_Patch.cs:16`). So **38 files can skip
  the original**. 52 `void` prefixes.
- **Transpilers**: 11 methods in 11 files, plus one manual `harmony.Patch(transpiler:)` at
  `Main/ManualPatchApplicator.cs:69` (it applies file 3 below).
- **Finalizers**: 33 in 23 files: 9 `Patch37_CrashReport` plus 2 `Native2ManagedPatcher` (crash
  capture), 10 `SaveLoadDiagnostics` plus 2 `Patch91` (void, observe only), 2 `Patch88` and
  `Patch77` (restore state, return `__exception`), and 7 that swallow a named exception class.

| Transpiler | On a failed match | Can it emit broken IL? | Real-IL test |
|---|---|---|---|
| `BannerColorPersistence/Hooks/Banner_TryGetBannerDataFromCode_Transpiler.cs:35` | returns `instructions`, logs | no | none |
| `BannerColorPersistence/Hooks/CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs:19` | returns the list unchanged, **no log**; rewrites every `callvirt get_MapFaction` (count unchecked) | no (same stack shape: `Hero -> IFaction` both ways) | none |
| `BannerColorPersistence/Hooks/MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch.cs:41` (via `BannerColorTranspiler`) | delegated | no | yes (`TranspilerSiteBindingTests`) |
| `CastleRecruitment/Hooks/Patch42_AiHourlyTick_Transpiler.cs:26`, `Patch42_FillSettlements_Transpiler.cs:28` (via `CastleAiTranspiler.cs`) | returns the list, logs a warning at three bail points; anchor-method check within 24 instructions | no | none |
| `CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs:51` | returns `instructions`, logs | no | none |
| `Enlistment/Hooks/BehaviorComponent_OnBehaviorActivated_Transpiler.cs:58` | `replaced > 0 ? list : instructions` (`:87`), **no log** | no (call replaces call, same stack) | none |
| `MarriageAlignment/Hooks/Patch81_MarriageClanDraw.cs:35` | requires exactly 2 `Clan.get_All` hits, else logs and returns | no (`call get_All` becomes `ldarg.1; call CandidateClansFor`) | none |
| `PartyIconScale/Hooks/Patch53_PartyIconScale.cs:34`, `Patch53_PartyIconScaleHumanVisual.cs:34` (via `PartyIconScaleTranspiler`) | delegated | no | yes |
| `RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs:25` | logs and returns at both anchors | **theoretically**: the backward scan for the `ldarg.0 ... get_Race` start (`:65-83`) is unbounded, so a reshaped assert could NOP a range wider than the call's arguments. Today the assert is the method's first statement (`HeroCreator.cs:260`, v1.5.3 decompile), so the scan cannot overshoot | none |

Verdict: **no transpiler can emit broken IL against v1.5.3**; all self-bail. Two bail silently
(CampaignSceneNotification, BehaviorComponent), so an engine bump that moves their anchor shows up
only as a behaviour regression in game. The real-IL gap is triage L282 (STILL_VALID), not re-reported.

### [CORRECTNESS-04] Four of the nine crash-capture finalizers patch empty base virtuals and can never fire

- **Evidence**: `Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs` (read at `b2e387db`) puts
  finalizers on `MissionBehavior.OnMissionTick` (`:104-110`), `MBSubModuleBase.OnSubModuleLoad`
  (`:113-119`), `ScriptComponentBehavior.OnTick` (`:45-51`) and `MissionView.OnMissionScreenTick`
  (`:63-69`). In the installed v1.5.3 engine (`pwsh tools/taom-src.ps1 path <Type>`):
  `MissionBehavior.cs:150-152` `public virtual void OnMissionTick(float dt) { }`;
  `MBSubModuleBase.cs:8-10` empty `OnSubModuleLoad`; `MissionView.cs:21-23` empty
  `OnMissionScreenTick`; `ScriptComponentBehavior.cs:215-218` `OnTick` body is
  `Debug.FailedAssert("This base function should never be called.")`. Harmony rewrites the body of
  the method it is given; an override is a different method, so a throw inside any override (every
  real mission behaviour, every submodule, every mission view) never passes through these
  finalizers. They fire only when an override calls `base.X()` and the empty base throws, which it
  cannot. The file header still says "9 Harmony Finalizers on TaleWorlds lifecycle methods" (`:11`)
  and the helper comment counts "9 per-tick Harmony Finalizers" (`CrashReportService.cs:94`).
- **Impact**: coverage is overstated, not missing: override exceptions still reach the outer
  `Managed.ApplicationTick` / `Module.OnApplicationTick` / `ScreenManager` finalizers, which is also
  why no one noticed. The cost is four live wrappers (one of them on every submodule load and one on
  every base-calling `OnMissionTick`) and a design document that misleads the next person who asks
  "which frame did this crash come from": the `originatingPatchTarget` string can never be
  `MissionBehavior.OnMissionTick`.
- **Related, same file (design, not a defect)**: `CrashReportPatchHelper.HandleAndSwallow` always
  returns null ("Always swallows in v1", `CrashReportPatchHelper.cs` header). A throw that recurs every
  frame therefore truncates every frame at the same point (everything after the thrower is skipped,
  forever), and each occurrence recomputes the stack snapshot and signature and writes one
  `LogError` line (`CrashReportService.cs:101-114`); the throttle's per-signature occurrence count
  (`CrashBundleThrottle.cs:65-66`) exists but only feeds that per-occurrence line. At 60 fps that is 60
  log lines a second into the file the bundle tails. Making the suppression line logarithmic
  (occurrence 1, 10, 100, ...) is a two-line change inside the existing throttle and pairs with
  CORRECTNESS-02.
- **Effort**: S. **Risk**: LOW (delete four finalizer classes; keep the category; update the two
  comments; add a test that asserts every Patch37 target is non-virtual or sealed, so it cannot recur).
- **Confidence**: HIGH (engine bodies read from the installed DLL decompile this run).
- **Fix sketch**: delete the four; if per-behaviour attribution is wanted, patch the dispatch sites
  (`Mission.OnTick` loop at `Mission.cs:3759`) instead, which is one finalizer that sees every
  behaviour.
- **Delta**: pre-existing (`7df18ca0`, 2026-05-25). **P1**: no. **Plan candidate**: yes, small and
  testable; bundle with the logarithmic suppression line.

### [CORRECTNESS-05] Settle both analyzer warnings as false positives, and pin the one unpinned field

- **BHA0001** at `Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs:22`:
  the build log reports "Member 'InitializeLordPartyProperties' does not exist in Type
  'TaleWorlds.CampaignSystem.Party.PartyComponents.InitializationArgs'": the analyzer dropped the
  nesting. The installed v1.5.3 engine has it: `LordPartyComponent.cs:14` `public class InitializationArgs`
  nested in `LordPartyComponent`, `:29` `public void InitializeLordPartyProperties(MobileParty mobileParty, Hero owner)`,
  called from `:129`. The attribute uses `nameof(LordPartyComponent.InitializationArgs.InitializeLordPartyProperties)`,
  which only compiles if the member exists in the referenced game DLLs. A BindingVerification test
  resolves the nested target by its CLR name (`TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs:81-98`,
  `LordPartyComponent+InitializationArgs`), and the baseline trx records it **Passed**
  (`InitializeLordPartyProperties_StillResolves_OnTheNestedArgs_MobilePartyAndOwner`). Parameter
  names match the prefix (`owner`, `:27`). **Patch88 binds; not a P1.** Whether Harmony applied it in a
  running game is UNVERIFIED here (no in-game log read), but nothing in the binding differs from the
  sibling `Patch88_SpawnLordPartyScope`.
- **BHA0006** at `Main/Features/CultureDoctrine/Hooks/TeamTacticProbe.cs:40`: "Expected
  'System.Collections.Generic.List`1', actual 'List<TaleWorlds.MountAndBlade.BehaviorComponent>'". The
  analyzer compares the open generic metadata name with the constructed type. The engine field is
  `private readonly List<BehaviorComponent> _behaviors` (`FormationAI.cs:41`, v1.5.3), exactly what
  `FieldRefAccess<FormationAI, List<BehaviorComponent>>("_behaviors")` asks for, and the bind is
  fail-soft (`:38-42` returns null on failure). False positive. Gap: `CultureDoctrineBindingTests.cs`
  pins `_currentTactic` (`:122-123`) and the public `FormationAI` surface (`:210-222`) but not
  `_behaviors`, so a rename would silently blank the status line's armed rows.
- **Fix sketch**: suppress each with a scoped `#pragma warning disable BHA0001` / `BHA0006` plus a
  one-line reason at the attribute (the build then has zero warnings, so the next real BHA warning is
  visible), and add a three-line `_behaviors` binding test. Both depend on nothing else.
- **Effort**: S. **Risk**: LOW. **Confidence**: HIGH.
- **Delta**: introduced (Patch88 `6bf93e51` 2026-09-12; TeamTacticProbe `75f1880a` 2026-09-17).
  **P1**: no. **Plan candidate**: yes, as a two-commit hygiene plan: a zero-warning build is the
  precondition for noticing the next analyzer hit.

### [CORRECTNESS-06] Skip-original prefixes and swallowing finalizers: the compatibility and state risks worth acting on

- **Skip-original prefixes**: of the 38 files that can skip, most replace UI or TAOM-owned flows
  (screens, menus, custom battle VMs) where no other mod has a stake. The ones that drop vanilla side
  effects other mods observe:
  - `Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs:31` and `MakePeaceAction_ApplyInternal_Patch.cs:31`
    run at `Priority.High` and veto the action. Skipping `ApplyInternal` also skips its
    `CampaignEvents` dispatch, which is correct for a vetoed war; but in Harmony 2 every other mod's
    prefix still runs (they are not skipped), so a mod that records "war about to be declared" in its
    own prefix (the installed `Bannerlord.Diplomacy` module is on this desktop's module list) sees a
    declaration that never happens. Compatible only if such mods read `__runOriginal`. UNVERIFIED
    against any specific mod; the registry (`docs/reference/harmony-patch-registry.md`) records no
    compatibility note for either.
  - `MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs:19` replaces
    `Formation.GetOrderPositionOfUnit` (worker-pool thread, per unit): any battle-AI mod that
    transpiles or prefixes the original body loses its effect whenever TAOM's gate is on. Decided
    feature; worth one registry line naming the conflict class.
  - `Enlistment/Hooks/LordConversationsConditionPatches.cs:41-84` (four skip prefixes on one
    conversation condition) and `PlayerEncounter.DoMeeting` (two TAOM skip prefixes,
    `Refuge/Hooks/RefugeEncounterPatch.cs:41` and `SupplyLines/Hooks/SupplyCaravanEncounterPatch.cs:31`)
    already document their coexistence (`SupplyCaravanEncounterPatch.cs:24-25` doc comment).
- **Catch-to-vanilla after a partial commit** (the Patch71 RCA class,
  `rca-field-commission-reset-equipments-2026-08-20.md:16`: "the prefix's catch returned true ...
  re-created the exact torn state"): `SupplyLines/Hooks/SupplyCaravanEncounterPatch.cs:43-44` calls
  `PlayerEncounter.Finish()` then `SetMoveModeHold()` inside the try, and the catch returns `true` (`:50`), so a
  throw after `Finish()` runs vanilla `DoMeeting` on a finished encounter. `SkipCampaignIntro/Hooks/Patch58_SkipCampaignIntro.cs`
  invokes `LaunchSandboxCharacterCreation` then sets `IsLoaded` (`:67-68`), catch returns `true` (`:71-74`); a throw in between falls back to the
  vanilla intro, which pushes the video state over the just-pushed character creation state (vanilla
  `SandBoxGameManager.cs:123-146`, v1.5.3). Both need a throw from engine code that is unlikely to
  throw, so LOW; the fix is the Patch71 rule: once the prefix has committed a side effect, its catch
  must return `false`.
- **Swallowing finalizers** (7): each swallows only one class (NRE, AV, InvalidOperation or a keyed
  ArgumentNull), which follows the PatchShield rule. State risk: a Harmony finalizer wraps the whole
  patched method, including other mods' prefixes and postfixes on it, so
  `AdvancedCombat/Hooks/Agent_CheckToDropFlaggedItem_Guard_Patch.cs:96-97` (NRE, no log, per agent)
  and `ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs:47-62` (NRE,
  mid-way through a method that picks and assigns the army's gathering point) also hide other mods'
  NREs and can leave a partially executed engine method. Neither logs through a counter; both are
  natural first adopters of the CORRECTNESS-02 helper.
- **Effort**: S per item. **Risk**: LOW. **Confidence**: MED (compatibility judged from code shape; no
  third-party mod was read).
- **Delta**: pre-existing. **P1**: no. **Plan candidate**: no; route the two catch fixes into the next
  touch of each feature and the finalizer counts into CORRECTNESS-02.

### [CORRECTNESS-07] Session scope is wired ad hoc: absolute clocks and engine handles on singletons survive a load or a new campaign (the class behind seed F3)

- **Measurement** (`lane3_singletons.py`, `lane3_resetgap.py` over the `b2e387db` extract): 495
  implementation types registered `Reuse.Singleton`; 94 hold mutable instance state (a collection,
  `CampaignTime`, an engine object, or a non-readonly clock- or latch-named field), after excluding
  21 config, catalog, loader, factory and adapter names; **6** implement `ResetForNewSession`
  (`git grep -n ResetForNewSession b2e387db -- Main` = 27 lines in 20 files, features Enlistment,
  FiefGranting, FieldCamp, MapLoadDiagnostics, Refuge, SupplyLines, UncapturableHeroes). Most of the
  rest are process caches of config or static data, which is correct. The ones that hold campaign
  time or campaign objects, by hand-reading each hit:
- **Evidence, absolute campaign-hour latches (same shape as F3)**:
  - `Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs:64-67` `_lastOfferedSettlementId`
    and `private double? _lastOfferedAtHours;`, tested at `:132` as
    `nowHours - _lastOfferedAtHours.Value < OfferCooldownHours` (24 h). Registered singleton
    (`EnlistmentIoC.cs:55`). Not cleared by `ServiceMaintenanceService.ResetSessionCaches`
    (`ServiceMaintenanceService.cs:217-241`, which F3 already shows misses the dwell anchor too). After
    loading an earlier save, or starting a new campaign in the same process, `nowHours - stamp` is
    negative, so it is always below 24 and the leave-on-arrival offer is silently suppressed until the
    new clock passes the old stamp plus a day: potentially the whole remaining playthrough. Introduced
    in `ff47cebb` (2026-08-25), the same commit as F3's `_settlementEntryHours`
    (`git log -S"_settlementEntryHours"`).
  - `Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs:21-22,36-39` caches a snapshot
    keyed by `floor(nowDays * 24)`; `Invalidate()` (`:67-70`) is not called from `ResetSessionCaches`,
    so a load at the same campaign hour (the common quick-reload case) serves the pre-load snapshot
    for up to one hour. Value data only: LOW.
- **Evidence, the new-campaign path never runs the session reset**: `EnlistmentBehavior.cs:115-121`
  calls `ResetSessionCaches` from `OnGameLoaded` only; `OnNewGameCreated` (`:124-130`) clears the
  store and nothing else. The reconciler says so itself (`EnlistmentReconciler.cs:451-454`: "a brand-new
  campaign in the same process never calls it") and guards its own anchor. The attachment adapter's
  handle does not: `Main/Adapters/MobilePartyAttachmentAdapter.cs:161-164` ("Survives campaign switches
  within one process, so it MUST be invalidated on discharge, session launch and game load") is
  invalidated only by `DischargeService.cs:123` and `ResetSessionCaches`. The cache test at `:175-178`
  is `party != null && party.IsActive && party.StringId == expectedCommanderPartyId`. In the installed
  v1.5.3 engine a lord party's id is deterministic (`LordPartyComponent.cs:134-137`,
  `stringId + "_party_1"`) and `MobileParty.IsActive` is a plain auto-property (`MobileParty.cs:365`)
  that nothing clears when a campaign is abandoned. So: quit to menu while enlisted, start a new
  campaign, enlist under the same lord, and `SyncPositionCached` can hit the dead party and write
  `main.Position = party.Position` (`:198`) every pass, pinning the player to where that lord stood
  in the old campaign. UNVERIFIED in game (whether the pump reaches this before a revalidating
  lookup depends on `ServiceMaintenanceService.cs:108,116` ordering; the cached id is also stale
  there, since `_cachedCommanderPartyId` is cleared only by the same reset).
- **Evidence, dead-campaign retention**: `git show b2e387db:Main/SubModule.cs:788-798` resets two
  ArmyTargeting singletons in `OnGameEnd` because "the finalized campaign stays reachable through the
  whole of the next campaign's load". The same path exists, unhandled, through
  `Main/Adapters/CommanderLordAdapter.cs:74` (`MapEvent`; its comment at `:68-73` accepts one
  retained MapEvent within a campaign, not across one), `ArmyMembershipAdapter.cs:25` (`Army`, reset
  on load only), `MobilePartyAttachmentAdapter.cs:164` (`MobileParty`) and
  `RemoteFiefSettlementSwapper.cs:20` (`MobileParty`). Heap size retained is UNVERIFIED (no dump), but
  each is a strong reference from a process-lifetime object into the old object graph.
- **Evidence, L56 class, one more instance** (L56 itself is triaged STILL_VALID, not re-reported):
  `Main/Features/QuickActions/Hooks/InventorySearchCampaignBehavior.cs` is a singleton behavior
  (`QuickActionsIoC.cs:15`, added via `IoC.Resolve` at `SubModule.cs:1288`) whose version tag exists
  to detect legacy saves, but `SyncData` on a save lacking the key leaves the previous session's
  `_persistedVersion = 1` in place, so the legacy reconcile in `OnGameLoaded` never fires. LOW.
- **Impact**: one feature (Enlistment, 10 RCAs) owns most of the live instances; the cost is silent
  feature death after a reload (the offer modal), a possible position pin in a second campaign, and
  memory held across campaigns in a mod whose memory budget is already a tracked problem (#385).
  The architecture rule mandates the pattern, but each feature re-derives it and the gaps are in
  collaborators nobody listed.
- **Fix sketch (the class, not the instances)**: one interface, `ISessionScoped { void ResetForNewSession(); }`,
  registered with `RegisterMany` on every singleton that holds campaign time, a campaign object or
  a per-campaign latch; one lifecycle behavior (or the existing `SubModule.OnGameEnd`) resolves
  `IEnumerable<ISessionScoped>` and calls them on `OnNewGameCreated`, on `OnGameLoaded` before
  normalisers, and on game end. `ResetSessionCaches` then shrinks to the feature-local fields, and
  the two `OnGameEnd` special cases fold in. Guard it with a reflection ratchet test: every
  `Reuse.Singleton` type with an instance field of an engine type, `CampaignTime`, or a `double`/`double?`
  named `*Hours`/`*Days`/`*Stamp` must implement `ISessionScoped` or sit on a reviewed baseline list.
- **Effort**: M (interface, dispatcher, the Enlistment instances, the ratchet test with its
  baseline). **Risk**: LOW to MED: resetting too early on load could drop a value `SyncData` just
  restored, so the dispatcher must run before `SyncData` on new game and never clear persisted
  stores (only transient fields).
- **Confidence**: HIGH on the presenter latch and the missing new-game reset (code read); MED on the
  position pin (engine state after abandoning a campaign not observed).
- **Delta**: introduced (presenter `ff47cebb`, adapters `a5076f4a` 2026-08-07, both after June).
  **P1**: no (no crash or save corruption shown; the pin is recoverable by discharge).
  **Plan candidate**: yes: the Enlistment half (presenter, rhythm cache, new-game call to
  `ResetSessionCaches`, F3's anchor) is an S plan with clean tests; the interface and ratchet are a
  second M plan.

## Considered and rejected

- **SaveableTypeDefiner id collisions**: none. Bases 726900501, 601, 701, 801, 901, 1001, 1101, 1201
  (step 100); local ids 101 to 106 (`git grep AddClassDefinition` per definer); effective ids
  `base + local` are all distinct, and `SaveDefinerCollisionDetectorTests.cs` already guards the
  deliberate reuse of an upstream base.
- **`SupplyOrderService._lastFrameHours` not reset** (`SupplyOrderService.cs:54,220-222`): an
  equality skip, so a stale value costs at most one skipped frame. Not worth a line.
- **Transpilers emitting broken IL**: none can at v1.5.3 (table above). The unbounded backward scan in
  `DeliverOffSpring_RaceAssert_Patch.cs` is theoretical while the assert is the method's first
  statement.
- **"Rewrite every silent catch"**: most are correct in isolation (diagnostic gathering, teardown,
  logger fallbacks). Counting them (CORRECTNESS-02) is the win; rewriting bodies is churn.
- **Promoting all nullable ids to error at once**: 2,028 diagnostics in one change would stall every
  in-flight session; the folder ratchet gets the same end state without a flag day.
- **`TroopWeightService._healthCache` / `_lastBaseLimit` not in `ClearCache`**: both are
  `ConditionalWeakTable<PartyBase, ...>` keyed by engine objects, so they die with the campaign.
- **Patch58 double launch**: needs `CleanAndPushState` to throw after pushing; `CleanAndPushState`
  cleans the stack first, so even the fallback path recovers. LOW, folded into CORRECTNESS-06.

## What I did not cover

- No build or test run (brief). Nullable counts come from the orchestrator's scratch log; the
  NoWarn-versus-editorconfig precedence was not re-tested.
- The catch classifier is regex plus brace matching; about 20 sites were spot-read. Per-class counts
  can be off by a few percent.
- Crash-RCA weighting matches RCA filenames to feature slugs; RCAs that name a feature only in their
  body are undercounted.
- Harmony: `__runOriginal` usage by third-party mods and the actual behaviour of the installed
  `Bannerlord.Diplomacy` module were not read; mod-compatibility verdicts are from TAOM code shape.
  Whether Patch88 applied in a running game was not checked in a game log.
- The singleton scan only sees fields named with a leading underscore and registrations of the
  `Register<I, T>(Reuse.Singleton)` shapes; `RegisterDelegate` and `RegisterInstance` singletons
  and static fields in patches were not scanned. Mission-scoped state on singletons (per-mission
  latches) was not audited.
- No in-game reproduction of the enlistment position pin or the offer suppression; no heap dump for
  the retention claim.
- Skip-listed working-tree files were not read (SubModule and IoC only via `git show b2e387db:`).
