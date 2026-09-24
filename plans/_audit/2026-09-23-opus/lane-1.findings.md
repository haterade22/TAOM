# Lane 1: composition and wiring (run `2026-09-23-opus`, baseline `b2e387db`)

Scope: `Main/SubModule.cs` and `Main/IoC.cs` read at `b2e387db` via `git show` (the working tree
holds another session's edits), every `Main/Features/*/*IoC.cs`, and `IoC.Resolve` use across
`Main/`. Scratch copies and the two measuring scripts live in the session scratchpad
(`...\scratchpad\l1\methods.py`, `guarded.py`); nothing in the repo was written except this file.

Note: `triage-check.md` did not exist when this lane started (the W1b checker was still running), so
triage verdicts are cited from `triage-A/B/C.md` as they stood. The SubModule god-file finding is
already triaged STILL_VALID (triage-B L404 and L412, seed F9): this lane does not re-report it; it
supplies the measurements and the design that the plan for it needs, and reports only what is new.

## 1. SubModule.cs per-method measurements

**Method.** `git show <rev>:Main/SubModule.cs > file`, then `python methods.py file`. A method
starts at a 4-space-indented line with an access modifier and `(`, and ends at the first line that
is exactly `    }`; counts include signature and closing brace. Per-line regex hits inside each
method: `PatchCategory\(`, `\.AddBehavior\(`, `\.AddModel(<..>)?\(`, `AddTaomBehavior\(`,
`IoC\.Resolve(All)?<`, lines starting `//` (comment), blank lines. Guarded vs unguarded
`PatchCategory` comes from `guarded.py` (a brace tracker that marks blocks opened by `try`).

File totals: **2,148 lines at `b2e387db`**, **1,969 at `bannerlord-1.4.5`** (`c5b84fb4`), so the
1.5.x line carries **179 more**; 758 at the June audit commit `141b749`.

| Method | b2e387db lines | 1.4.5 lines | June lines | PatchCategory | AddBehavior | AddModel | Mission adds | IoC.Resolve lines | `//` lines | blank |
|---|---|---|---|---|---|---|---|---|---|---|
| `OnSubModuleLoad` | **517** (111-627) | 414 | 155 | 24 | 0 | 0 | 0 | 53 | 179 (35%) | 35 |
| `OnBeforeInitialModuleScreenSetAsRoot` | 142 | 142 | 46 | 1 | 0 | 0 | 0 | 8 | 78 (55%) | 9 |
| `OnGameEnd` | 28 | 28 | n/a | 0 | 0 | 0 | 0 | 3 | 11 | 2 |
| `OnGameStart` (dispatcher) | 39 | 37 | 218 | 0 | 0 | 0 | 0 | 3 | 13 | 4 |
| `OnGameLoaded` + `StampSaveLoadPhase` | 5 + 9 | 5 + 9 | n/a | 0 | 0 | 0 | 0 | 1 | 0 | 0 |
| `RegisterUiExtensions` | 71 | 71 | n/a | 0 | 0 | 0 | 0 | 4 | 24 | 5 |
| `RegisterProgressionAndIdentity` | 93 | 93 | n/a | 0 | 8 | 5 | 0 | 28 | 30 | 12 |
| `RegisterRaceAgeAndFamily` | 21 | 21 | n/a | 0 | 2 | 4 | 0 | 7 | 5 | 1 |
| `RegisterDiplomacyAndConflict` | 34 | 32 | n/a | 0 | 5 | 4 | 0 | 11 | 10 | 3 |
| `RegisterCulturalFeatModels` | 30 | 30 | n/a | 0 | 0 | 21 | 0 | 10 | 0 | 0 |
| `RegisterBattleBalanceAndTargeting` | 16 | 16 | n/a | 0 | 1 | 5 | 0 | 8 | 2 | 2 |
| `RegisterSpecialResourcesAndCareers` | 92 | 80 | n/a | 0 | 6 | 8 | 0 | 33 | 31 | 5 |
| `RegisterCustomBattleModels` | 9 | absent | n/a | 0 | 0 | 2 | 0 | 2 | 1 | 0 |
| `RegisterCampaignLifeBehaviors` | **175** | 172 | n/a | 0 | 35 | 0 | 0 | 79 | 56 | 21 |
| `OnGameInitializationFinished` | **461** (1449-1909) | 447 | 136 | 60 | 0 | 0 | 0 | 41 | **231 (50%)** | 27 |
| `OnMissionBehaviorInitialize` | 166 | 155 | 63 | 1 | 0 | 0 | 32 | 26 | 75 (45%) | 11 |
| `OnApplicationTick` | 19 | 19 | 21 | 0 | 0 | 0 | 0 | 0 | 2 | 1 |
| `OnSubModuleUnloaded` | 50 | 36 | 23 | 0 | 0 | 0 | 0 | 2 | 19 | 5 |

Totals across the file at `b2e387db`: 86 lines match `PatchCategory\(`, of which 2 are
commented-out NavalTravel lines, so **84 live apply sites** (3 of them loop variables covering 15
listed categories; `guarded.py` strips comments first), 57 `AddBehavior`, 49 `AddModel` (incl. 2 Custom Battle), 32 mission-behavior adds,
**319 lines carrying `IoC.Resolve`** (`git grep -c` on the rev), about 100 distinct
`Features.<Name>` namespaces referenced (`grep -oE "(TAOM\.)?Features\.[A-Za-z]+" | sort -u`).

**What the two long methods are made of.**

- `OnSubModuleLoad` (517): 179 comment lines, 35 blank, so about **303 code lines**. Of those: 24
  Harmony category applies (13 unguarded, 11 inside a `try`), about 59 static `Patch.Initialize(...)`
  or `*IoC.InitializeHooks(...)` handshakes (the BannerColorPersistence block alone is 22 lines,
  586-611), 18 `try` blocks with 22 `catch` clauses and 20 log calls (every guarded block logs its
  own failure), two version/engine reports (117-160), the save-definer preflight (176-185), UIExtender
  setup with co-op filtering (212-214), two hotkey registrations (233-255), and two localization
  override loaders (259-289). No `AddBehavior`/`AddModel` (those are OnGameStart).
- `OnGameInitializationFinished` (461): **231 comment lines (50%)**, 27 blank, about **203 code
  lines**: 58 live `PatchCategory` sites (the table's 60 includes the 2 commented-out NavalTravel
  lines): **50 unguarded**, 8 guarded, one of which is the nine-category preview loop, 43
  `Initialize` handshakes (the BattleLoadDiagnostics block 1767-1818 is 18 Initialize calls in a row),
  three watchdog/sampler starts, `ManualPatchApplicator.ApplyAll` (1869), and the co-op Harmony
  census plus settings fingerprint (1876-1908, the only co-op gating in the method).
- Parking switches: NavalTravel is three commented-out blocks (1038-1044, 1747-1761, and the
  model line 1044); NativeSkinFixes is a commented-out install (716-739) plus a still-live
  `NativeSkinFixesInstaller.Uninstall()` at 2123. 14 lines of commented-out code in total.
- Co-op gating in SubModule: `RegisterUiExtensions` (879-949, UI filter), the census block
  (1876-1908), and `ICoopSessionProvider` resolved into 11 behavior/model constructors (the
  behaviors gate themselves). No registration is skipped for co-op except `[CoopSuppressedUi]` UI.
- MCM-dependent registration: only two sites decide *whether* to wire from MCM at load time:
  `CrashReportSettings.Instance?.EnableCrashCapture` (195) and `EnableNativeToManagedCapture` (201).
  Everything else registers unconditionally and gates at runtime (the house convention, stated at
  1290-1310, 1967, 1971, 2000).

**Growth.** June to HEAD: `OnSubModuleLoad` 155 to 517 (3.3x), `OnGameInitializationFinished` 136
to 461 (3.4x). The 1.4.5-to-1.5.x delta is concentrated in `OnSubModuleLoad` (+103; `diff` of the
two method bodies shows 105 added and 2 removed lines: the `[Engine]` report 130-160, GlobalStrings
overrides 275-289, Patch89 map-load diagnostics 385-420, Patch90 preload guard 422-438) and
`OnSubModuleUnloaded` (+14).

## 2. IoC.Resolve by kind of containing type

**Method.** `git grep -o -E "IoC\.Resolve(All)?<" b2e387db -- 'Main/*.cs' | wc -l` gives **637
occurrences in 139 files** (June `141b749`: 259 in 70 files; the outside lead of 629 in 144 was
taken at `c79a5852` and is not reproduced here). `scratchpad\l1\classify.py` then walks each file
at the revision, skips `//` lines (628 remain), and assigns each occurrence to its nearest enclosing
`class|struct|record` declaration, classified by base list, name and path:

| Kind | Occurrences | Types |
|---|---|---|
| `SubModule` (the composition root) | 325 | 1 |
| Harmony patch or static hook (incl. `ManualPatchApplicator`, 9) | 118 | 66 |
| MissionBehavior, MissionLogic, mission view or mission mixin | 53 | 19 |
| VM, widget, Gauntlet screen, map view, UIExtender mixin | 43 | 11 |
| Console command (`Cheats/`, `DevConsole/`) | 42 | 16 |
| Engine-instantiated scene or decision objects and static helpers ("other": `TaomHowdahMachine`, `TaomHowdahStandingPoint`, `TaomMumakilPlatform`, `TaomSettlementClaimantDecision`, the four `*Combat` static classes, `TaomBTLogger`, `BoneCheck`, `TableauDiagnostics`, `TaomSettings`) | 20 | 15 |
| Engine subclasses the save system or issue manager instantiates (`CareerQuest`, six `*LotrIssue`/`*Quest`) | 12 | 7 |
| Behavior-tree nodes (Warg, Spider, `LogTask`) | 11 | 9 |
| **Service, engine, provider or store** | **4** | 4 |
| CampaignBehavior | **0** | 0 |
| GameModel | **0** | 0 |

**The outside premise does not hold.** Service locator inside services is essentially absent: the
four service-kind hits are `Main/Core/Infrastructure/Reflection/ReflectionHelper.cs:9` (a static
facade over `IReflectionService`), `Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs:65`
(a patch-side static), `Main/Features/FieldCamp/Hooks/FieldCampMenuController.cs:332`
(`_contributors ??= IoC.ResolveAll<ICampOverlayContributor>()`, lazy on purpose: the contributor
set must be complete, see `IoCRegistrationDisciplineTests`), and
`Main/Features/Warg/WargRiderHandManager.cs:14` (seed F4). No CampaignBehavior and no GameModel
resolves from the container: both are constructor injected. Everything else that resolves is a type
the engine, Harmony, UIExtenderEx or the console instantiates with no constructor TAOM controls,
which is the sanctioned boundary. Recorded under "Considered and rejected" below.

**The one boundary class TAOM constructs itself** is the mission behavior: `SubModule` news up every
one (`SubModule.cs:1957-2072`). 20 of the 24 mission-behavior adds are parameterless `new X()`
(`grep -cE 'AddTaomBehavior\(new [A-Za-z.]+\(\)\)'`), and 17 of those classes resolve their
dependencies inside that parameterless constructor (e.g. `DreadAuraMissionLogic.cs:35-42`,
`SmartCavalryAIMissionBehavior.cs:40-43`, `AdvancedCombatBehavior.cs:23-26`) or in `AfterStart`
(`FieldCommissionMissionLogic.cs:23-28`). Only `CultureDoctrineMissionLogic.cs:41-51` chains the
parameterless constructor to an injecting one. That is finding COMP-04.

**Patch-state handoff has five co-existing styles** (all in patch or hook classes): (a) `SubModule`
calls a static `Initialize(service)` (127 files in `Main/` declare `public static void Initialize(`;
`SubModule.cs:586-611` is 22 such calls in a row); (b) a feature IoC's `InitializePatchStatics`,
called once at the end of `IoC.Configure` (`IoC.cs:207-210`); (c) a feature IoC's
`InitializeHooks(...)` called from `SubModule` (`SubModule.cs:496,504,510,518,530`); (d) lazy
`_x ??= IoC.Resolve<T>()` inside the patch (59 occurrences); (e) a resolver lambda passed to
`Initialize` (`SubModule.cs:1582,1636-1637`). Lane 2 owns the patch-to-service layering question;
this lane only notes that (a), (c) and (e) are what make `SubModule` carry 319 `IoC.Resolve` lines.

## 3. Feature IoC coverage

**Method.** `git ls-tree -d --name-only b2e387db Main/Features/` (108 folders) against
`git ls-tree -r --name-only b2e387db Main/Features | grep -E "IoC\.cs$"` (94 files).

- **93 of 108 feature folders own a `*IoC.cs`** (94 files: `Enlistment/Duties/DutiesIoC.cs` is
  nested). `IoC.Configure` calls all 94 (`IoC.cs:91-199`; `grep -cE "Register\w+Feature\(container\)"`
  = 94). Core services (9) and logging (1) are the only registrations inline in `IoC.cs`
  (`IoC.cs:216-234`).
- **15 folders have no IoC class:** AdvancedStartOptions, AtmospherePersistence, BattleScenes,
  CharacterSelection, DevConsole (26 files), ElephantLike, LocalizationOverride, MapEventGuard, Mcm,
  MissionPerf, NativeSkinFixes, PartyIconScale, ReturnToArmy, SkipCampaignIntro, WeatherBoundsGuard.
  They are patch-only or static features; `SubModule` wires them directly with
  `Initialize(IoC.Resolve<IModLogger>())` plus a `PatchCategory` string.
- **The per-feature IoC class owns one registration dimension of six.** Container registration moved
  into the features; every other dimension still lives only in `SubModule`: Harmony categories (84
  live sites), campaign behaviors (57), game models (49), mission behaviors (32), hook handshakes (about
  100 `Initialize` calls), unload resets (14), plus `Main/ManualPatchApplicator.cs` (101 lines,
  manual patches for three features). The June harvest counted about 50 distinct `Features.*`
  names referenced from `SubModule`; it is about 100 at `b2e387db`.
- **Contention, measured:** since the June commit, `Main/SubModule.cs` changed in 135 commits and
  `Main/IoC.cs` in 50; of the 349 commits that touched `Main/Features/`, **131 (38%) also had to
  edit `SubModule.cs` or `IoC.cs`** (`git rev-list 141b749..b2e387db -- Main/Features`, then
  `git diff-tree --name-only` per commit). Both files are single-owner in CLAUDE.md, and both carry
  another session's uncommitted edits right now.

## 4. Tests that read SubModule.cs or IoC.cs as source text

**Method.** `git grep -l -E '"SubModule\.cs"|"IoC\.cs"|Main/SubModule\.cs"' b2e387db -- TAOM.Tests`,
then each hit read to drop files that only name the file in a message or comment
(CoopVetoClassification scans every `Main/*.cs`, not SubModule specifically; Mcm, NativeSkinFixes,
Patch72, Patch74Nameplate, RecruitGate, the two EconomyDiagnostics files, UncapturableHeroesBinding,
AssemblyRedirectList and ReflectionSiteBinding only name it). **26 test files** read `SubModule.cs`
and/or `IoC.cs` text; 9 of them read both. `IoCRegistrationDisciplineTests` and
`FieldCampWiringTests` also read feature `*IoC.cs` files.

| Family | What it asserts (`Contains` or `IndexOf` on raw text) | Files |
|---|---|---|
| Category applied | a literal `PatchCategory("PatchNN_...")` exists | Patch80, Patch82, Patch84, Patch85, Patch86, Patch87, Patch88, RefugeWiring (Patch75), FieldCampWiring (Patch74), UncapturableHeroesWiring (Patch76), HeroRaceWiring (Patch72 inside the guarded loop), SharedMovementOrderPostfix (the mission-time category plus its one-shot flag) |
| Hook initialised | `X_Patch.Initialize` text exists | Patch84, Patch85, Patch87, Patch88, BannerTripletOrdering, ExitStallDisarm |
| Behavior added | `new XBehavior(` or `AddTaomBehavior(` text exists | FiefHubCampaignBehavior, RacePersistenceBehavior, MessengerCampaignBehavior, MountDespawnWiring, RefugeWiring, FieldCampWiring, SiegeDismountWiring, SiegePropDiagnosticsWiring, WandererAllegianceWiring, AutoResolveDiagnosticsWiring, SignatureStrikesBinding |
| Feature registered in IoC.cs | `XIoC.RegisterXFeature` text exists | AutoResolveDiagnostics, RacePersistence, Messenger, MountDespawn, SettlementGuards, SiegeDismount, SiegePropDiagnostics, WandererAllegiance, UncapturableHeroes |
| Ordering | one substring's index is below another's | UncapturableHeroesWiring (Enlistment registers first in IoC.cs), ResetForUnloadSweep (resets appear after the `OnSubModuleUnloaded` index) |
| Unload reset wired | `X.ResetForUnload(` text after `OnSubModuleUnloaded` | ResetForUnloadSweep (all declarers), Patch87, Patch88, UncapturableHeroesWiring |
| Model registered | `new {ModelName}(` text exists for every GameModel subclass in the assembly | GameModelOverrideBinding |
| Other wiring | `ManualPatchApplicator.ApplyAll(_harmony)` text; `public override void OnGameEnd` text | SettlementGuardsWiring, ExitStallDisarm |

Two structural weaknesses, proven under COMP-03: the assertions cannot tell code from a comment, and
they pin the exact spelling of `SubModule`, so any refactor of the composition root (including the
fix for COMP-01) breaks tests that guard behavior which did not change.

# Findings

### [COMP-01] Guard every Harmony category apply: 64 bare `PatchCategory` calls turn one drifted binding into a failed boot or a silently half-patched session

- **Evidence**: `SubModule.cs:223,259,291,292,302,487,528,532,538,543,549,565,624`: 13 bare
  `_harmony.PatchCategory(...)` calls in `OnSubModuleLoad`. The v1.5.3 engine wraps each
  `OnSubModuleLoad` in a catch that logs and then `throw new Exception()`
  (`E:/Decompiled_Bannerlord/_categories_v1.5.3/.../TaleWorlds.MountAndBlade/Module.cs:204-220`),
  so a throw from any of them stops the game from starting with TAOM enabled, and skips the rest of
  TAOM's load (BannerColor handshakes, Patch42, the "loaded" message).
- **Evidence**: `SubModule.cs:1464-1465` sets `_gameInitPatchesApplied = true` BEFORE the batch;
  50 bare calls follow (`1510-1745`, e.g. `Patch65_LandlessCultureSpawnGuard` at 1596,
  `Patch82_MapEventObserverInvariant` at 1612, `Patch26_SpecialResources` at 1708).
  `MBGameManager.OnGameInitializationFinished` iterates the submodules with no catch
  (`.../MBGameManager.cs:110-115`), called from `Campaign.cs:1471`. A throw at, say, `Patch6` aborts
  the remaining ~48 categories, the three watchdog starts, `ManualPatchApplicator.ApplyAll` (1869)
  and the census; on the next campaign load in the same process the flag returns early at 1464, so
  that campaign runs with all of them missing and nothing logged. What the engine does with the
  first exception above `Campaign.OnInitialize` is UNVERIFIED (not traced further).
- **Evidence**: the codebase already documents this exact failure and fixes it one site at a time:
  `SubModule.cs:1467-1471` ("applied unguarded and in sequence, so the FIRST one to throw silently
  prevented every later one", 2026-07-31), `312-315` ("A rename would throw out of OnSubModuleLoad
  and take the remaining ~250 lines of module init with it"), `339-341`, `554-556`. 20 sites are
  guarded by hand, each with its own try/catch and log line; 64 are not (`guarded.py`). June: 45
  unguarded of 46; the unguarded count grew by 19 since.
- **Measurement**: `python guarded.py SubModule.head.cs` (brace tracker; a site is guarded when an
  enclosing block was opened by `try`). OnSubModuleLoad 13 bare / 11 guarded; OnGameInitializationFinished
  50 / 8; OnMissionBehaviorInitialize 1 / 0 (`1922`); OnBeforeInitialModuleScreenSetAsRoot 0 / 1.
- **Impact**: Harmony aborts a category at its first failing class (`SubModule.cs:339-341`), and a
  category throws on a renamed or removed target. The binding suite pins targets on the pinned engine,
  but a Steam force-bump under a shipped build (the GAME VERSION DRIFT banner exists for this) or
  another mod's transpiler reshaping IL reaches players first. Today the blast radius of one bad
  category is "TAOM does not boot" (13 sites) or "every later game-init patch, including the CTD
  guards Patch65/82/84, is off for the session" (50 sites). Guarded, the blast radius is one feature,
  logged.
- **Effort**: S. One private helper, 64 call sites become `TryPatch("PatchNN_...")`, and the 20
  hand-written try/catch blocks can collapse into it (net line deletion).
- **Risk**: LOW. Identical behavior when nothing throws. Two text tests spell the call with a leading
  dot (`FieldCampWiringTests.cs:191` asserts `.PatchCategory("Patch74_FieldCampNameplateIcon")`);
  the rest match `PatchCategory("...` as a substring and keep passing. A guard makes a failed crash
  guard quiet instead of fatal, so the helper must surface failures (below).
- **Confidence**: HIGH on the code shape and the engine's `OnSubModuleLoad` rethrow; MED on how often
  a category actually throws in the field.
- **Fix sketch**: `void TryPatch(string category)` wraps `PatchCategory`, logs
  `[Patch] <category> FAILED: <type>: <message>` at Error, and appends to a failure list; at the end
  of each phase, if the list is non-empty, one red `InformationMessage` names the failed categories.
  Keep the once-per-process flag (re-applying duplicates patches, `SubModule.cs:1457-1463`), which is
  now safe because the batch always completes. Add a reflection test that every
  `[HarmonyPatchCategory]` literal in the assembly is applied through the helper list (COMP-03).
- **Delta**: pre-existing (45 bare sites in June), grown (+19 since `141b749`).
- **P1**: no. Crash-class mechanism with HIGH confidence, but it needs a drifted binding to fire,
  and no field occurrence outside the documented 2026-07-31 preview-category case was found.
- **Plan candidate**: yes. Small, mechanical, clean verification (build, full suite, one forced-throw
  unit test of the helper), and it is step 0 of the composition-root design below.

### [COMP-02] Stop re-using 22 singleton CampaignBehaviors across campaigns; each one hand-rolls new-campaign detection

- **Evidence**: `SubModule.cs:1091,1156,1177,1288,1293,1310,1325,1364,1367,1387,1412-1421,1436-1437`:
  22 `campaignStarter.AddBehavior(IoC.Resolve<XBehavior>())`; every one of the 22 is registered
  `Reuse.Singleton` (e.g. `Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumIoC.cs:24`,
  `EnlistmentIoC`, `MessengerIoC`; `git grep` for each type's `Register<...>(Reuse.Singleton)`).
  The other 34 behaviors are `new XBehavior(...)` per `OnGameStart`, fresh per campaign.
- **Evidence**: the singleton instance is re-added to every campaign in the process, so its fields
  outlive the campaign. The code says so and patches around it per behavior:
  `Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs:134-141` ("Singleton behavior survives
  across campaigns within one process. On a genuinely new starter clear the store"),
  `Main/Features/Messengers/MessengerCampaignBehavior.cs:44-47,168-189` (Codex review #34: the
  prior session's `_dialogsRegistered = true` suppressed dialog registration in campaign 2),
  `Main/Features/CaravanTrade/CaravanVisitMemoryBehavior.cs:31-34,44` (clears on session launch).
  Seed F3 (the enlistment dwell anchor surviving `ResetSessionCaches`) is the same class failing in a
  singleton service.
- **Evidence**: only one type outside `SubModule` consumes a singleton behavior instance:
  `Patch34_SPInventoryVMSearchApply` resolves `InventorySearchCampaignBehavior` (per-type `git grep`
  for `Resolve<...Behavior>` and constructor parameters over `Main/` minus `SubModule.cs`).
- **Measurement**: `grep -c 'AddBehavior(IoC.Resolve'` = 22, `grep -c 'AddBehavior(new'` = 34 on
  `git show b2e387db:Main/SubModule.cs`; reuse read from each feature IoC at `b2e387db`.
- **Impact**: every stateful singleton behavior must remember to detect "new starter" and reset each
  field by hand; the two that do carry a review-found bug in their history, and a new field added
  without a reset leaks campaign A's state into campaign B within one play session (load a save,
  quit to menu, load another). The per-behavior `_lastSessionStarter` / `_justLoadedFromSave`
  machinery exists only because of the reuse.
- **Effort**: S to M. Change 21 registrations to `Reuse.Transient` (DryIoc builds a fresh behavior
  per `OnGameStart` with the same constructor injection); switch the one consumer to
  `Campaign.Current.GetCampaignBehavior<InventorySearchCampaignBehavior>()`. Deleting the reset
  scaffolding afterwards is a separate, optional step.
- **Risk**: MED. A behavior that relies on cross-campaign state on purpose would change; none was
  found, but each of the 22 needs a read before its flip. Services behind them stay singletons, so
  F3-class residue in services is unaffected (that is a separate reset contract).
- **Confidence**: MED (reuse and consumers verified by grep; no behavior-by-behavior audit of all 22
  constructors for other hidden consumers).
- **Fix sketch**: flip registration reuse feature by feature with a test per behavior that two
  `IoC.Resolve<T>()` calls return different instances; keep the existing reset code until each is
  flipped and smoke-tested with a two-campaign session.
- **Delta**: pre-existing pattern, mostly introduced: 3 singleton behavior adds at `141b749`
  (InventorySearch, EquipmentPreset, Messenger), 22 at `b2e387db` (Enlistment alone added 10).
- **P1**: no.
- **Plan candidate**: yes, folded into the composition-root migration (a module's behaviors are
  constructed per campaign by its factory), or standalone as a small change.

### [COMP-03] Replace the SubModule text-scraping wiring tests: they pass on commented-out code and pin the file's spelling

- **Evidence**: `TAOM.Tests/Migration/GameModelOverrideBindingTests.cs:55-66` marks a GameModel as
  registered when `subModule.Contains($"new {m.Name}(")`. `TaomPartyNavigationModel`
  (`Main/Features/NavalTravel/Models/TaomPartyNavigationModel.cs:28`, a `DefaultPartyNavigationModel`
  subclass, so `DiscoverGameModels` at `:153-172` finds it) is parked: its only `new
  TaomPartyNavigationModel(` in `SubModule.cs` is the comment at line 1044. The test therefore reports
  it as registered. It is parked on purpose here, but the same test would pass for any model whose
  registration line was commented out by accident.
- **Evidence**: the same shape everywhere: `FieldCampWiringTests.cs:189-191`,
  `UncapturableHeroesWiringTests.cs:90-102`, `Patch85EnlistedDetachDeferralBindingTests.cs:128-138`,
  `Patch84SiegeAftermathMenuGuardTests.cs:201-211`, `RefugeWiringTests.cs:60-73`,
  `SignatureStrikesBindingTests.cs:206-213` all `Contains` a literal on the raw file; the commented-out
  `// _harmony.PatchCategory("Patch54_NavalTravelBoatVisual");` at `SubModule.cs:1753` would satisfy a
  Patch54 "is applied" test in exactly the same way. `CoopVetoClassificationTests.cs:304-312` already
  has a `StripComments` helper; none of the 26 wiring tests use it.
- **Evidence**: 26 test files read `SubModule.cs` and/or `IoC.cs` text (section 4 table). Each pins a
  substring of the composition root, so the COMP-01 helper, a rename of `AddTaomBehavior`, or moving
  a feature's wiring out of `SubModule` fails tests whose behavior did not change;
  `UncapturableHeroesWiringTests.cs:63-70` pins registration ORDER by `IndexOf` in `IoC.cs`.
- **Measurement**: `git grep -l` for the file-name literals, then each file read (section 4). The
  parked-model pass was reasoned from the test's `Contains` and the single match of
  `new TaomPartyNavigationModel(` in `git show b2e387db:Main/SubModule.cs`; the test was not run in
  isolation (no `dotnet test` in this lane); the baseline run shows the suite green apart from two
  Armory tests, which implies this test passed.
- **Impact**: the wiring gate the tests exist to provide (a feature silently dead because one line is
  missing, the "BindingVerification" intent) has a comment-shaped hole, and the tests make the
  single-owner file harder to change safely, which is the opposite of what a composition root needs.
- **Effort**: S for the interim (route all 26 through one shared reader that strips comments, as
  `CoopVetoClassificationTests` does); M for the real fix, which is reflection over the feature-module
  list once it exists (design below).
- **Risk**: LOW. Test-only.
- **Confidence**: HIGH.
- **Fix sketch**: now: a `SourceText.ReadCode("Main","SubModule.cs")` helper that blanks comments,
  used by all 26; add an explicit parked-model allowlist to `GameModelOverrideBindingTests` so
  NavalTravel is visible as parked rather than passing by accident. Later: assert on the module list
  (`ITaomFeatureModule.Describe()` output) instead of text.
- **Delta**: pre-existing pattern, mostly introduced: 18 of the 26 files did not exist at `141b749`
  (`git cat-file -e 141b749:<path>` per file); `GameModelOverrideBindingTests` did.
- **P1**: no.
- **Plan candidate**: yes (interim S fix); the reflection version rides the composition-root plan.

### [COMP-04] Give the 17 self-resolving mission behaviors an injecting constructor; SubModule already constructs them

- **Evidence**: `SubModule.cs:1957-1970,1999-2018` add 20 mission behaviors as parameterless
  `new X()`. 17 of those resolve their own dependencies from the container in the parameterless
  constructor: `Main/Features/DreadAura/Hooks/DreadAuraMissionLogic.cs:35-42`,
  `SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs:40-43`,
  `AdvancedCombat/AdvancedCombatBehavior.cs:23-26`,
  `SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs:33-36`,
  `CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs:36-39`, and 12 more
  (`classify.py` "MissionBehavior/view" rows); `FieldCommissionMissionLogic.cs:23-28` resolves in
  `AfterStart`. Only `CultureDoctrineMissionLogic.cs:41-51` chains the parameterless constructor to an
  injecting one.
- **Evidence**: meanwhile `SubModule` itself passes resolved dependencies to 4 others
  (`MountDespawnMissionBehavior` 1973-1975, the three Enlistment mission behaviors 1979-1997), so the
  file mixes both styles within 40 lines.
- **Measurement**: `grep -cE 'AddTaomBehavior\(new [A-Za-z.]+\(\)\)'` = 20; per-class constructor
  count and `git grep -l "new <Class>(" b2e387db -- TAOM.Tests`: **1 of the 19 resolving classes is
  constructed by any test** (`SiegeDismountMissionBehavior`).
- **Impact**: the mission-side logic in these classes (DreadAura's pulse scheduling, SignatureStrikes'
  runner wiring, SmartCavalry's gate) can only be exercised with a configured global container, so it
  is not; and the dependency list of each behavior is invisible at the composition root. This is the
  real, small residue of the "service locator" lead.
- **Effort**: S per class (add the injecting constructor, keep the parameterless one chaining to it
  until the composition-root step moves construction), M for all 17.
- **Risk**: LOW. Constructor-only change; the engine never constructs these (TAOM does).
- **Confidence**: HIGH.
- **Fix sketch**: follow the `CultureDoctrineMissionLogic` shape in each class; later the feature
  module's mission factory passes the dependencies and the parameterless constructors go.
- **Delta**: pre-existing pattern; 9 of the 17 resolving classes did not exist at `141b749`
  (`git cat-file -e` per file), e.g. DreadAura, SignatureStrikes, WarRam, Mumakil, BannerBearers.
- **P1**: no.
- **Plan candidate**: no as a standalone plan (low leverage alone); yes as a sub-step of the
  composition-root migration, where each moved feature gets it for free.

### [COMP-05] Decide what the unload-reset sweep is for: the engine never reloads TAOM in-process, and if it did, three static flags would break the reload anyway

- **Evidence**: `SubModule.cs:2126-2146`: `UnpatchAll`, `IoC.Dispose`, then 14 `ResetForUnload()` calls,
  justified as protection for a "reload-in-same-process" (`2117,2121,2129-2132`). The sweep is pinned
  by `ResetForUnloadSweepTests.cs:76-94` plus per-feature asserts (Patch87, Patch88,
  UncapturableHeroes).
- **Evidence**: in the v1.5.3 dump the only caller of `OnSubModuleUnloaded` is
  `Module.FinalizeSubModulesBases` (`Module.cs:242-248`), called once from `Module.FinalizeModule`
  (`Module.cs:1296-1314`), which then terminates platform services and runs the final GC: process
  shutdown. `LoadSingleModule` (`Module.cs:251-258`) adds a new module; it does not reload one.
- **Evidence**: the three once-per-process flags `_missionTimePatchesApplied`,
  `_gameInitPatchesApplied`, `_basicTableauGuardApplied` (`SubModule.cs:107-109`) are static and
  never reset in `OnSubModuleUnloaded`. After `UnpatchAll` (2126) a hypothetical reload would return
  early at 1464, 641 and 1919 and apply none of those patches.
- **Measurement**: `grep -rn "OnSubModuleUnloaded()\|FinalizeSubModules" _categories_v1.5.3`; read
  `SubModule.cs:1449-1465,2098-2147` at `b2e387db`.
- **Impact**: either the scenario is real and the reload is broken (the flags), or it is not and 14
  reset methods, a sweep test and three per-feature asserts are carrying cost for nothing, with every
  new patch that caches a static asked to add one more. The native side was not checked, so an
  in-process reload driven from `TaleWorlds.Native.dll` is UNVERIFIED.
- **Effort**: S either way.
- **Risk**: LOW.
- **Confidence**: MED (managed-side call graph read; native caller unknown).
- **Fix sketch**: ask whether any tool (the Modding Kit editor, a test harness) reloads the module in
  one process. If not, record that in `harmony-patches.md` and stop requiring `ResetForUnload` for new
  patches (keep the existing ones, they are harmless). If yes, reset the three flags in
  `OnSubModuleUnloaded` and add them to the sweep test.
- **Delta**: pre-existing (the flags predate June); the sweep grew from 1 `ResetForUnload` call at
  `141b749` to 14.
- **P1**: no.
- **Plan candidate**: no; a one-line decision for Mike, then a small edit.

### [COMP-06] Ordering rules live only in prose comments, and two have already drifted

- **Evidence**: `IoC.cs:184-186` says FieldCamp must register after Enlistment and SupplyLines because
  "FieldCampIoC eagerly resolves ICampService"; `Main/Features/FieldCamp/FieldCampIoC.cs:23-27` says
  "deliberately NO eager Resolve here" and moved it to `InitializePatchStatics`. With lazy DryIoc
  resolution the stated reason is gone; the order constraint may no longer exist.
- **Evidence**: `SubModule.cs:257` says Patch25 "Must be first"; it is the third category applied
  (after Patch37 at 199 and Patch41 at 223) and runs after the UIExtender enable (214) and two hotkey
  registrations (235, 249). Whether any of those resolves localized text first is UNVERIFIED.
- **Evidence**: half of `OnGameInitializationFinished` is comment (231 of 461 lines), most of it the
  apply-timing argument for each category (e.g. Patch65 at 1587-1595, Patch88 at 1597-1601, Patch82
  at 1606-1609). The real constraints found in code are listed in the design section; none is
  machine-checked except the Enlistment-before-UncapturableHeroes `IndexOf` test.
- **Measurement**: `methods.py` comment count; reads of the cited lines at `b2e387db`.
- **Impact**: a maintainer who trusts the prose either preserves an order that no longer matters or
  breaks one that does; the timing rationale sits in the one file every session contends for,
  instead of on the patch class it describes.
- **Effort**: S for the two stale comments; the structural fix is the design's `ApplyPhase` on each
  category plus the module order list.
- **Risk**: LOW.
- **Confidence**: HIGH on the drift; MED on whether FieldCamp's position still matters (it also
  consumes `IEnlistmentStateQuery`, which FieldCommission registers with `IfAlreadyRegistered.Keep`,
  so Enlistment-before-FieldCommission stays real).
- **Fix sketch**: correct the two comments now; in the migration, move each category's timing
  paragraph onto its patch class as the justification for its declared phase.
- **Delta**: introduced for FieldCamp (the `IoC.cs` comment landed in `24cce287` and the eager
  resolve was removed the same day in `16a58b51`, 2026-08-22, without updating it); the Patch25 order
  predates June (the CrashReport move to the top is dated 2026-05-25 at `SubModule.cs:187`).
- **P1**: no.
- **Plan candidate**: no (fold into the composition-root plan).

## Design: feature-module composition root

This is the plan-shaped answer to the already-triaged god-file finding (triage-B L404/L412, seed F9),
not a new finding. Goal: `SubModule.cs` shrinks to a kernel of roughly 300 lines that never changes
when a feature is added, and every feature owns all six of its registration dimensions in its own
folder. Win in one sentence: a feature change stops touching the two single-owner files (38% of
feature commits today). Cost in one sentence: one interface, one base class, one ordered list file,
and about 100 small module classes that mostly forward to code that already exists.

### The contract

```csharp
// Main/Composition/ITaomFeatureModule.cs
public enum ApplyPhase { ProcessLoad, MainMenu, GameInit, FirstMission }
public enum FeatureState { Enabled, Parked }

public interface ITaomFeatureModule
{
    string Id { get; }                       // "Enlistment"; used in every log line and test message
    FeatureState State { get; }              // compile-time; never read from MCM
    string ParkedReason { get; }             // issue refs; null when Enabled

    void RegisterServices(IRegistrator r);   // phase 1: container only. IRegistrator has no Resolve,
                                             // so the eager-resolve class of bug is a compile error
    void InitializeStatics(IResolver r);     // phase 2: after EVERY module registered (today's
                                             // InitializePatchStatics / InitializeHooks / Initialize(...))
    IReadOnlyList<PatchCategoryDecl> PatchCategories { get; }      // name + ApplyPhase
    IReadOnlyList<BehaviorDecl> CampaignBehaviors { get; }          // Type + factory
    IReadOnlyList<ModelDecl> GameModels { get; }                    // slot base Type + impl Type + factory + campaign/custom
    IReadOnlyList<MissionBehaviorDecl> MissionBehaviors { get; }    // Type + factory (Mission, IResolver)
    void OnPhase(ApplyPhase phase, IResolver r) { }                 // non-patch side effects: hotkeys, watchdog Start()
    void ResetForUnload() { }                                       // only if COMP-05 keeps the sweep
}
```

A `TaomFeatureModule` abstract base returns empty lists and no-op methods, so a patch-only feature
(ReturnToArmy) is about 15 lines. Each module lives beside its feature (`Main/Features/Enlistment/EnlistmentModule.cs`)
and its `RegisterServices` is initially one line calling the existing `EnlistmentIoC.RegisterEnlistmentFeature`.
Decls are data plus a typed factory, e.g. `Model<MarriageModel, TaomMarriageModel>()` captures
`(starter, r) => starter.AddModel<MarriageModel>(r.Resolve<TaomMarriageModel>())`; the loop applies
exactly what the decl lists, so the list is the truth the tests read. Behaviors and models are
registered `Reuse.Transient` so each campaign gets fresh instances (COMP-02 disappears by
construction); services stay singletons.

The ordered list is one file, one line per module, append-mostly:

```csharp
// Main/Composition/FeatureModules.cs
internal static class FeatureModules
{
    internal static readonly ITaomFeatureModule[] All =
    {
        new CoopInteropModule(),
        new PlayerPossessionModule(),
        new HeroRaceModule(),
        // ... today's IoC.Configure order ...
        new EnlistmentModule(),       // before FieldCommission (IfAlreadyRegistered.Keep) and UncapturableHeroes
        new FieldCommissionModule(),
        new LotrIssuesModule(),       // last: SuppressAll must follow every other behavior add
    };
}
```

`SubModule` keeps a kernel that is not a feature: `IoC.Configure` (which becomes the two-phase loop),
the build-stamp and `[Engine]` reports, the save-definer preflight, the CrashReport attach (must stay
first, `SubModule.cs:187-210`), UIExtender plus the co-op UI filter (`879-949`), the once-per-process
game-init flag, the `[BattleLoad]` mission bracket (`1925-1941,2075`), `MissionDiagnosticBehavior`
last (`2024-2033`), the Harmony census last (`1871-1908`), the `OnApplicationTick` time and shader
ticks, and shutdown. Each phase method becomes: kernel lines, then
`ModuleRunner.Run(phase, FeatureModules.All, ...)`, then the kernel tail.

### Ordering constraints found in the code, and where each lands

| Constraint | Evidence | In the design |
|---|---|---|
| CrashReport patch first, before any other apply | `SubModule.cs:187-210` | kernel, before the loop |
| Patch41 MCM layout and Patch83/58/61/62/89/90 must apply in `OnSubModuleLoad`, not later | `216-222`, `294-302`, `304-315`, `337-338` | `ApplyPhase.ProcessLoad` on the category decl |
| Patch55 must apply at the main menu | `634-640` | `ApplyPhase.MainMenu` |
| Mission-time category only once `Mission.Current` exists | `1915-1923` | `ApplyPhase.FirstMission` |
| Game-init batch once per process (re-apply duplicates patches and breaks the DeliverOffSpring transpiler) | `1457-1465` | kernel flag around the `GameInit` loop |
| Patch65/Patch88 must be in the standard game-init batch, not lazier (new-game spawn path) | `1587-1601` | `ApplyPhase.GameInit`; the paragraph moves to the patch class |
| Feature IoC: FieldCommission after Enlistment (`IfAlreadyRegistered.Keep`) | `IoC.cs:180-182`, `FieldCommissionIoC.cs:31` | list order; reflection test asserts index(Enlistment) < index(FieldCommission) |
| UncapturableHeroes after Enlistment (single `IInquiryAdapter` registration) | `IoC.cs:194-199` | list order; the existing `IndexOf` test becomes a list-index test |
| Eager patch statics only after every registration | `IoC.cs:203-210`, `IoCRegistrationDisciplineTests` | phase 2 `InitializeStatics`; compile-enforced by `IRegistrator` |
| Contributor collections complete before first resolve (`ICampOverlayContributor`, `IPartySpottingContributor`) | `FieldCampIoC.cs:19-27` | same two-phase rule; `ResolveMany` order is registration order, so module order also fixes contributor order |
| One engine model per slot: MarriageModel, AgentStatCalculateModel, AgentApplyDamageModel, BattleMoraleModel, MapVisibilityModel, BattleBannerBearersModel, BattleInitializationModel | `1059-1066`, `1214-1250`, `FieldCampIoC.cs:19-20` | each slot base type may appear in exactly one module's `GameModels`; other features contribute through services or contributor seams (the existing pattern); reflection test enforces uniqueness |
| TAOM models registered in `OnGameStart` so they follow SandBox's defaults | `1031-1034`, `1233-1246` | the model loop runs in `OnGameStart`, as now |
| Custom Battle mirrors two models on `BasicGameStarter` only | `1253-1268` | `ModelDecl.Target = Custom` |
| Vanilla behavior removal before its TAOM replacement | `1006-1008` (InitialChildGeneration), `1443-1446` (LotrIssues) | inside the owning module's factory; LotrIssues last in the list |
| Player Switcher character-creation handler at priority 1100, after TAOM's 1050; equal priorities throw | `969-972`; `CharacterCreationRegistrationBehavior.cs:9` | independent of list order; a reflection test reads both constants and asserts 1100 > 1050 |
| Mission behaviors tick in reverse add order; the tree logic must be added before `AdvancedCombatBehavior` | `1952-1958` | list order (AdvancedCombat module after the BehaviorTree kernel line); asserted by index |
| `MissionDiagnosticBehavior` added last, `BattleLoadPhaseBehavior` after TAOM's adds | `2024-2055` | kernel tail |
| Harmony census after every patch | `1871-1875` | kernel tail |

Two constraints are stale prose (COMP-06): FieldCamp's position in `IoC.cs` and Patch25 "first".
Before the first gameplay feature moves, run a one-off reflection script (game assemblies loaded, as
`GameModelOverrideBindingTests` already does) that lists every engine method patched by two or more
TAOM categories; Harmony orders same-priority patches by apply order, so each such pair becomes an
explicit list-order constraint with a test.

### Co-op gating

No new gating in the loop. Today the composition root decides only one co-op thing, which UI
extensions to register (`RegisterUiExtensions`, attribute-driven, one-shot), and it stays in the
kernel. Peer authority and dedicated-server checks stay inside behaviors, reading
`ICoopSessionProvider` at runtime (the trap index line "Co-op loaded, peer authority and dedicated
server are three different questions"). A `CoopRelevance` member was considered and left out: nothing
would consume it today (simplicity criterion); add it the day the census or settings fingerprint
needs a per-module list.

### MCM-dependent registration

`State` is compile-time only. The house convention is to register unconditionally and gate at
runtime (`SubModule.cs:1290-1310`), and persisted MCM values outlive default flips (trap index
"Persisted MCM defaults"), so the loop never reads MCM. The only load-time MCM reads today,
`CrashReportSettings.EnableCrashCapture` and `EnableNativeToManagedCapture` (`195,201`), are kernel.

### Parking becomes one line

`public override FeatureState State => FeatureState.Parked;` plus `ParkedReason => "#296/#120: TAOM_Map
has no naval navmesh"`. The runner still calls `RegisterServices` for a parked module (NavalTravel's
services are registered today, `IoC.cs:112`, and tests resolve them) but skips its categories,
behaviors, models, mission behaviors and `OnPhase`. No log line (the NativeSkinFixes comment at
`730-731` rejects a per-session announcement). The three commented-out NavalTravel blocks
(`1038-1044`, `1747-1761`) and the NativeSkinFixes install block (`716-739`) become live decls in their
modules, so they compile and stay correct across engine bumps instead of rotting as comments, and
`SettingRequireRestartPostureTests`' hand-kept "parked" exceptions can be derived from
`FeatureModules.All.Where(m => m.State == Parked)`.

### Text tests become reflection tests

Six generic tests over `FeatureModules.All` replace the wiring asserts in the 26 files (the files
keep their behavior tests):

1. Every `[HarmonyPatchCategory]` literal in the TAOM assembly is declared by exactly one module (or
   by the kernel list), and every declared category exists in the assembly. This covers all 94
   categories (count from `triage-check.md:85`, `git grep -h -o 'HarmonyPatchCategory("[^"]*")'`),
   not the 12 that have a text test today.
2. Every non-abstract `GameModel` subclass is declared by exactly one module; no slot base type is
   declared twice; parked modules' models are reported as parked, not as registered (fixes the COMP-03
   false pass).
3. Every non-abstract `CampaignBehaviorBase` and TAOM mission-behavior subclass is declared by exactly
   one module, or is on a short named allowlist (behaviors the engine or another behavior adds).
4. Order assertions on list indices, one per row of the constraints table.
5. Handshake tests: build a test container with fakes, run a module's `RegisterServices` and
   `InitializeStatics`, assert the patch's ready flag (for example Patch84's `IsReady`,
   `Patch84SiegeAftermathMenuGuardTests.cs:211`), which tests behavior instead of spelling.
6. Every module whose behaviors implement `SyncData` sets `OwnsSaveData` (below).

### Failure mode when a module throws during registration

Today, measured above: a throw in `IoC.Configure` or any bare call in `OnSubModuleLoad` fails the boot
(the engine rethrows, `Module.cs:204-220`); a throw inside the `OnGameStart` helpers leaves every later
behavior and model unregistered (engine handling above that point UNVERIFIED); a throw in the game-init
batch leaves the rest of the batch off for the whole process (COMP-01); a throw in a mission
constructor skips every later TAOM mission behavior, with the bracket naming the culprit
(`1933-1941`).

Proposed: `ModuleRunner` wraps each (module, phase) call. On a throw it logs
`[Module] <Id> failed in <phase>: <type>: <message>`, marks the module Faulted, skips the module in
every later phase of the session (half-wired is worse than absent), and continues with the next module;
at the end of the phase one red `InformationMessage` lists the faulted modules. One exception, fail
closed: a module with `OwnsSaveData = true` (any behavior with a `SyncData` body) that faults in
`RegisterServices`, `InitializeStatics` or the `OnGameStart` behavior phase rethrows, because running a
campaign without the behavior that persists its data risks losing that data on the next save (whether
the engine keeps orphaned behavior data across a save is UNVERIFIED; failing closed does not depend on
the answer). Kernel steps keep today's behavior.

### Incremental migration, single-owner-safe at every step

- **Step 0** (independent, do first): COMP-01's `TryPatch` helper and COMP-03's comment-stripping
  test reader. Both are small and make every later step safer.
- **Step 1** (the only step that adds lines to `SubModule.cs` and `IoC.cs`): add the interface, the
  base class, `ModuleRunner`, an EMPTY `FeatureModules.All`, and one runner call per phase. Each call
  sits at the END of its phase's feature block and before the kernel tail; in `IoC.Configure` the
  register loop sits after the last `Register*Feature` line and the statics loop after
  `InitializePatchStatics`. With an empty list, behavior is identical; the full suite proves parity.
- **Step 2 onward** (one feature per commit, or a handful of leaf features): add `XModule.cs` in the
  feature folder and one line in `FeatureModules.cs`, then only DELETE that feature's lines from
  `SubModule.cs` and `IoC.cs`, and convert that feature's text asserts to the generic tests in the
  same commit. Moving to the end-of-phase loop changes the feature's position, so each commit checks
  the feature against the constraints table and the multi-patched-method list; leaf features with no
  entry are order-free. Suggested order: pilots with every dimension and an existing text test
  (WandererAllegiance, SiegePropDiagnostics, ReturnToArmy), then the diagnostics features (fail-open by
  design), then gameplay leaves, then the constrained cores last (Enlistment, FieldCommission,
  CareerSystem, HeroRace, the creature mounts, LotrIssues).
- **Last step**: delete the transitional "or present in SubModule text" branch of the generic tests,
  the per-feature static `Register*Feature` calls in `IoC.cs`, and `ManualPatchApplicator` (its three
  features declare their manual patches through `OnPhase(GameInit)`).

Effort: L overall, as roughly 20 small commits; each is S with a clean verification story (build,
full suite, and for gameplay features a two-campaign smoke). Risk: MED overall, concentrated in the
order-sensitive cores; LOW for the leaf features. The single-owner rule for `SubModule.cs` and
`IoC.cs` holds throughout because after step 1 those files only lose lines.

## Considered and rejected

- **Service locator inside services (the outside lead's "about 134 resolves outside the
  boundaries").** Rejected on measurement: 4 service-kind resolves of 628, none in a class named
  `*Service`; the rest are engine-, Harmony-, UIExtender- or console-instantiated types, where
  constructor injection is impossible. The real residue is COMP-04 (mission behaviors TAOM builds
  itself) and F4 (Warg, already a seed).
- **A new "service registry" or auto-discovery by reflection (scan the assembly for modules).**
  Rejected: an explicit ordered list is what the ordering constraints need, and discovery order is not
  a contract. Reflection is used only in the tests.
- **`CoopRelevance` on every module.** Left out until a consumer exists (see the design).
- **Per-frame module ticks in `OnApplicationTick`.** Two features tick today (`2078-2096`); a
  per-frame virtual loop over about 100 modules buys nothing.
- **Splitting `SubModule` into partial classes or more `Register*` helpers.** Rejected: the June
  extraction already did this for `OnGameStart` (218 lines in June, now a 39-line dispatcher plus 8
  helpers), and the file still grew from 758 to 2,148 lines, because the contention is structural
  (every feature must edit it), not a matter of method length.
- **Converting the 22 singleton behaviors in isolation without the module work.** Viable (COMP-02 is
  written so it can ship alone), but if the migration is approved it is cheaper to do per module.
- **Two temporary diagnostics wired permanently** (`TroopCountDiagnosticsBehavior`, "TEMPORARY" since
  `430baead`, 2026-07-02, `SubModule.cs:1175-1177`; Patch79 "SCAFFOLDING" since `b8ef3800`,
  2026-09-05, `459-480`). Not reported: both are cheap at runtime, and whether their questions are
  answered is Mike's call; worth one line in the next triage.

## What I did not cover

- The engine's handling of an exception thrown out of `OnGameStart`, `OnGameInitializationFinished`
  (above `Campaign.OnInitialize`) or `OnMissionBehaviorInitialize`: not traced; marked UNVERIFIED.
- Whether `OnSubModuleUnloaded` can be followed by a reload from native code (COMP-05).
- Harmony application order for methods patched by two or more TAOM categories: named as a
  pre-migration script, not run.
- A behavior-by-behavior audit of all 22 singleton behaviors' fields (COMP-02 read Messenger,
  Enlistment, CaravanVisitMemory, InventorySearch, WarOfTheRingMomentum registration).
- `Main/SubModule.cs` and `Main/IoC.cs` working-tree edits (another session's) and everything on the
  skip list; Elk and ElephantLike were read only at `b2e387db` for resolve counts, with no findings on
  them.
- No build or test run (orchestrator-owned); the COMP-03 false pass is reasoned from the test source
  and the file text, not from an isolated run.
