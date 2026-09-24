# Verify batch A-04: adversarial re-check of CORRECTNESS-07, CORRECTNESS-02, CORRECTNESS-01

Checker: fresh adversarial pass. HEAD is `4b5662b2`; `git diff --stat b2e387db 4b5662b2` touches no
`.cs`, `.csproj` or `.props` file (CHANGELOG, sounds, docs, one JSON), so every C# citation was read
via `git show b2e387db:<path>` and holds at HEAD too. Engine reads from the v1.5.3 `taom-src` cache.
No build, no test run.

## CORRECTNESS-07: session scope wired ad hoc (absolute clocks and engine handles on singletons)

**Outcome: CONFIRMED (presenter latch, rhythm cache, missing new-game reset, adapter handle
retention), with the position pin itself PLAUSIBLE (one engine link unverified) and two
corrections. Impact today: MED.**

What holds on my own re-reading:

- `EnlistmentWaitMenuPresenter.cs:64` `_lastOfferedSettlementId`, `:67` `double? _lastOfferedAtHours`,
  `:70` 24 h cooldown, `:132` `nowHours - _lastOfferedAtHours.Value < OfferCooldownHours`. Fed
  `CampaignTime.Now.ToHours` from `EnlistmentMenuBehavior.cs:75`. Registered
  `Reuse.Singleton` at `EnlistmentIoC.cs:55`. `git grep _lastOfferedAtHours` finds only the
  declaration and two writes: nothing resets it. A backwards clock (earlier save, or a new campaign
  that starts at the fixed campaign start time) makes the difference negative, so the offer is
  suppressed until the new clock passes the old stamp plus 24 h.
- Not by-design: the presenter's own comment (`:123-124`) says "a save/reload mid-stop re-asking is
  harmless and self-healing", i.e. the author expected a reload to re-arm the offer; the singleton
  does the opposite. `.claude/rules/csharp-architecture.md:96-114` makes a session-reset story
  MANDATORY for "any absolute clock, latch or shown-flag" on a singleton, so this is a rule
  violation, not a tradeoff. No test covers a backwards clock:
  `TAOM.Tests/Features/Enlistment/EnlistmentWaitMenuPresenterTests.cs:167-267` only moves time
  forward (100.0, 100.5, 130.0).
- `ServiceMaintenanceService.cs:217-241` (`ResetSessionCaches`) clears the maintenance fields, the
  attachment cache, status, army adapter and reconciler; not the presenter, not the rhythm cache.
- `ArmyRhythmSnapshotService.cs:21-22,36-39,67-70` as cited; singleton at `EnlistmentIoC.cs:75`.
- `EnlistmentBehavior.cs:115-122` calls `ResetSessionCaches` from `OnGameLoaded` only;
  `OnNewGameCreated` (`:124-130`) and `OnSessionLaunched` (`:132-146`) only clear the store.
  `EnlistmentReconciler.cs:450-455` states this in its own comment and guards its anchor with a
  backwards-clock re-anchor at `:456`.
- `MobilePartyAttachmentAdapter.cs:161-164` (comment: "MUST be invalidated on discharge, session
  launch and game load"), cache test `:175-178`, write `:198`. `InvalidateCommanderCache` has exactly
  two callers (`DischargeService.cs:123`, `ServiceMaintenanceService.cs:224` via
  `ServiceAttachmentService.cs:160`), so the "session launch" leg of that comment is not wired.
  Singleton at `EnlistmentIoC.cs:14`. The pump runs the cheap tier (`ServiceMaintenanceService.cs:108`)
  before refreshing `_cachedCommanderPartyId` (`:116`), and that id is also never cleared on a new
  game.
- Engine (v1.5.3 cache): `LordPartyComponent.cs:134-138` creates `stringId + "_party_1"` through
  `MobileParty.CreateParty`, which calls `FindNextUniqueStringId` against the NEW campaign's object
  manager (`MobileParty.cs:4256-4258`), so the first lord party of a fresh campaign reproduces the
  old id. `MobileParty.IsActive` is `{ get; set; }` (`MobileParty.cs:365`), `Position` returns a
  plain field (`MobileParty.cs:1064-1068`), so reading a dead campaign's party does not throw.
- `git show b2e387db:Main/SubModule.cs:789-798` resets exactly two ArmyTargeting singletons on
  `OnGameEnd` for the retention reason quoted; `IoC.Configure` runs once (`:115`), `IoC.Dispose`
  only at `:2127`, so the container is process-lifetime.
- `CommanderLordAdapter.cs:68-74` (one retained MapEvent, accepted within a campaign),
  `ArmyMembershipAdapter.cs:19-25,147-153` (reset on load only). Both singletons
  (`EnlistmentIoC.cs:13,41`).
- `InventorySearchCampaignBehavior.cs:23,43-50,61-72`: singleton (`QuickActionsIoC.cs:15`), added via
  `IoC.Resolve` at `SubModule.cs:1288`. The engine's `SyncData<T>` on load leaves `data` untouched
  when the key is missing (`CampaignBehaviorDataStore.cs:33-38`), so the second load of a legacy save
  in one process keeps `_persistedVersion = 1` and skips the reconcile. LOW, as stated.
- Measurements reproduce: `git grep -n ResetForNewSession b2e387db -- Main` = 27 lines in 20 files;
  6 instance implementations (FiefSiegeParticipation, Camp, MapLoadHeartbeat, Refuge, SupplyOrder,
  EnlistmentReconciler; `MapLoadTracer` is static). My singleton count is 496
  (`git grep -h -o -E "Register<[^>]*>\(Reuse\.Singleton"`), within one of the lane's 495.
- Delta: `git log -S_lastOfferedAtHours` and `-S_settlementEntryHours` both give `ff47cebb`
  (2026-08-25); `-S_cachedCommanderParty;` and `-S_lastSeenMapEvent` give `a5076f4a` (2026-08-07).
  Introduced after June, as claimed.

What does not hold, corrected:

1. `RemoteFiefSettlementSwapper.cs:20` is not a steady retention path: `Restore` nulls
   `_swappedParty` at `:73`, so it holds the old party only if a `Swap` was never followed by a
   `Restore`. Drop it from the retention list or qualify it.
2. `ArmyRhythmSnapshotService.Invalidate()` (`:67-70`) has ZERO callers anywhere in `Main`
   (`git grep -n -E "_rhythm" b2e387db -- Main` shows only `GetSnapshot` at
   `EnlistmentDailyService.cs:211`, `DutyOrchestrationService.cs:75,133`). The claim "not called from
   ResetSessionCaches" understates it: it is dead API.

What stays unverified: whether any engine teardown sets the abandoned campaign's `MobileParty.IsActive`
to false (the position pin needs it to stay true). The lane already marks the pin UNVERIFIED in game;
I agree it is PLAUSIBLE, not confirmed.

## CORRECTNESS-02: count suppressed failures per catch site, put the counts in the crash bundle

**Outcome: CONFIRMED (the gap is real and the headline counts reproduce), with three corrections.
Impact today: LOW (observability; no runtime fault).**

What holds on my own re-reading:

- Raw grep at `b2e387db` reproduces exactly: `git grep -c -P 'catch\s*\(\s*(System\.)?Exception\b'`
  over `Main/*.cs` sums to 574; `git grep -c -P 'catch\s*(\{|$)'` sums to 476; catches with a `when`
  filter = 2.
- An independent classifier I wrote (`scratchpad/catch_classify.py`, comment and string stripping,
  brace matching, over a `git archive b2e387db` extract) gives: `catch (Exception)` 573 (495 logged,
  13 silent, 5 sentinel, 60 other); bare `catch` 473 (311 silent, 103 sentinel, 6 silent-flow, 51
  other, 2 rethrow); typed 16. Silent = 324, identical to the lane. My sentinel count is higher (103
  vs 89) only because my regex also accepts `return 0f;`, `string.Empty` and `Array.Empty`.
- Crash bundle contents as cited: `CrashReportService.cs:173-242` (`ComposeContext`, no fault
  counts), `:244-252` (`Safe` records collector failures only), `LogTailCollector.cs:13`
  (`DefaultTailLines = 500`), `:25-41` (taom, rgl and diag tails). No first-chance exception
  tracking anywhere (`git grep FirstChance b2e387db -- Main Dependencies` = 0), and no existing
  fault-counting type (`git grep -i "class \w*(Fault|Swallow|ErrorCount)"` finds nothing relevant).
- PatchShield's `Interlocked` counters (`Dependencies/Foundation/PatchShield.cs:90-111`) are
  printed only by `WriteSessionSummary` (`:424`), whose single caller is the `ProcessExit` lambda at
  `Dependencies/SubModule.cs:255-258`. No `Main/Features/CrashReport` file reads them (4 hits, all
  comments).
- Cited sites read and match: `GameMenuManagerSetNextMenuPatch.cs:32-40` (latch), 
  `Patch35_Formation_SetMovementOrder.cs:54-65` (latch, message says the feature is "disabled for
  the rest of this session"), `AiMilitaryBehavior_CalculateDistanceScoreForBesieging_Patch.cs:108-111`
  (empty `catch (Exception)` around the whole campaign-AI scoring postfix), `SiegeGatheringFailureInfo.cs:55-60`,
  `CampLayoutBuilder.cs:150-157`, `EnlistmentBattleBehavior.cs:238-241`,
  `Patch85_EnlistedDetachDeferral.cs:91`, `Patch35_Mission_OnTick.cs:7-12` (zero-allocation contract).
- Not by-design: fail-open is mandated for hooks, and the proposal keeps fail-open; no ADR or rule
  forbids counting.

What does not hold, corrected:

1. "38 `if (!_xxxLogged)` guards in 29 files" reproduces only with a CASE-INSENSITIVE grep
   (`git grep -i -P` gives 38 in 29; case-sensitive gives 26 in 23). Of the 38, only **14 sit
   directly inside a catch block (13 files)** by brace walk-back (`scratchpad/latch_in_catch.py`);
   the other 24 are first-occurrence or config latches with no exception (for example
   `TaomHowdahMachine.cs:68` `_firstMachineTickLogged`, `MissionDiagnosticBehavior.cs:33`,
   `ArmyTargetingService.cs:95,153`, `DeadMountDespawnService.cs:99`). The "Log-once latch (38 sites)"
   migration shape is therefore 14 sites, not 38.
2. PatchShield swallows are not invisible to a bundle: each one writes a `DiagLog` line
   (`PatchShield.cs:309`, "swallowed {Type} from a patch on {owner}.{name}") and the bundle carries
   the last 500 lines of diag.log (`CrashReportIoC.cs:37-46`, `LogTailCollector.cs:36-39`). The
   offending patch is also unpatched after the first swallow (`PatchShield.cs:313`), so repeats are
   rare. Only the running totals are missing.
3. Side observation, not part of the claim but it bears on "what reaches a bundle": the "fixed TAOM
   state record" is registered as `new TaomStateCollector()` with no providers
   (`CrashReportIoC.cs:34-35`; every constructor parameter defaults to null,
   `TaomStateCollector.cs:23-28`), so career, resources, feats, revolt and messenger fields are
   always null in a bundle today. Worth its own finding if no lane has it.

## CORRECTNESS-01: ratchet the suppressed nullable warnings folder by folder, starting with Arena

**Outcome: CONFIRMED (the suppression, the counts and the proposed mechanics all hold; the
NoWarn-over-editorconfig precedence the lane left at MED is now verified in the compiler), with
three corrections that weaken the crash-weighting argument. Impact today: LOW (a direction item;
no runtime defect).**

What holds on my own re-reading:

- `git show b2e387db:Main/TAOM.csproj:9` `<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625</NoWarn>`;
  `Dependencies/TAOM.Dependencies.csproj:9` the same seven plus `8374;8174`;
  `Directory.Build.props:6` `<Nullable>enable</Nullable>`; `Main/TAOM.csproj:113` the `Nullable`
  1.3.1 polyfill. No `TreatWarningsAsErrors`, ruleset or globalconfig anywhere
  (`git ls-tree -r` for `.editorconfig|globalconfig|.ruleset` finds only the root `.editorconfig`).
- Root `.editorconfig` (`root = true`, 22 lines, 320 B in the blob, so about 342 B on disk with CRLF)
  has no `dotnet_diagnostic` line; no nested `.editorconfig` exists.
- Counts re-derived by my own parser (`scratchpad/nowarn_check.py`) from the orchestrator's
  `scratchpad/raw/build-nowarn.log` (a `-p:NoWarn=` build of a `b2e387db` worktree): 4,060 raw
  CS86xx lines, **2,028** distinct messages, **1,890** distinct locations; per code CS8618 756,
  CS8603 408, CS8604 331, CS8625 285, CS8600 108, CS8601 78, CS8602 62 (matches `baseline.md`).
  Folder locations match the lane's table exactly: Dependencies/Foundation 3 in 2 files
  (`PatchShield.cs:313`, `SubModuleConstructionGuard.cs:248,253`), Arena 21 in 5, FieldCommission 48
  in 14, CulturalFeats 140 in 9 (131 CS8618), Enlistment 143 in 40, Adapters 216 in 46, Siege 8,
  PlayerSwitcher 6, StaleCharacterRepair 2, DreadAura 3, BanditManagement 8. The named Arena lines
  (`Patch69_TournamentEndGuard.cs:50,55,84`, `Patch69_TournamentRosterGuard.cs:72-74,109`) and
  `FieldCommissionSaveData.cs:32,33,37,39,40,41` (CS8600) plus `:43-45` (CS8604) are in the log.
- Precedence, verified rather than taken from the brief: in the SDK 10.0.401 compiler
  (`Microsoft.CodeAnalysis.CSharp.CSharpDiagnosticFilter.GetDiagnosticReport`, decompiled with
  ilspy), the editorconfig severity is consulted only when the id is NOT already in
  `specificDiagnosticOptions` (`(!isSpecified || flag)`), and `/nowarn` puts the id there as
  `Suppress`. So while the ids stay in `<NoWarn>`, a folder `.editorconfig` cannot re-enable them,
  exactly as the lane says; steps 1 and 2 (move the suppression into the root `.editorconfig` as
  `none`) are required and output-neutral; step 3 (nested `= error`) then works.
- Delta: the NoWarn line dates from the initial commit (`git log -S` gives `a2bbdc99`,
  2026-01-24). Pre-existing, seed F10, as stated.
- Not an ADR-decided tradeoff. One doc calls it deliberate:
  `docs/reviews/rca-banner-bearers-2026-07-16.md:24` ("CS8603 is deliberately suppressed
  project-wide for `Main`", used to dispute a review finding), while
  `docs/ai-includes/code-quality.md:395` lists "Ignoring nullable warnings" as Bad. The BRIEF treats
  F10 as a verified seed, not a decided tradeoff, so the finding stands; the plan should cite and
  supersede the RCA line.

What does not hold, corrected:

1. The warnings the lane uses to justify the Arena-first and FieldCommission ordering are
   annotation debt, not latent null bugs. Arena: `Patch69_TournamentEndGuard.cs:50` and
   `Patch69_TournamentRosterGuard.cs:72-74` are `ResetForUnload` assigning null to lazily resolved
   static caches (CS8625), `:55,84` are `return null` from a `[HarmonyFinalizer]` typed `Exception`
   (CS8603), and `:109` passes a possibly-null settlement to `TournamentEntrantMapper.ResolveFiller`,
   which null-checks it (`TournamentEntrantMapper.cs:57-58`). FieldCommission: the "real
   null-on-load surface" at `FieldCommissionSaveData.cs:32-45` is already handled, since all three
   importers return early on null (`FieldCommissionMeritService.cs:341,353,368`) and the comment at
   `FieldCommissionSaveData.cs:34-36` documents it. `PatchShield.cs:313` likewise passes a nullable
   `originalMethod` to a callee that checks it (`:325`). Graduating these folders would add `?`
   annotations, not catch bugs; the crash-RCA weighting predicts nothing about these specific lines.
   The ratchet's value is the null-flow signal on NEW code, which the lane also states.
2. RCA counts: `git ls-tree -r` finds **252** `rca-*.md` under `docs/reviews/` (not 253), and
   **30** name an NRE with a word-boundary regex (`NullReferenceException|\bNREs?\b`), 31 with a
   plain substring, not 26. The direction of the argument is unchanged.
3. The lane's `lane3_rca2.py` slug matching was not re-run; the per-folder "RCAs by slug" column is
   unverified by me.

## What I did not cover

- No build or test (brief). The nullable counts come from the orchestrator's existing raw log, re-parsed
  by my own script; I did not produce a fresh `-p:NoWarn=` build.
- The editorconfig precedence was read in the SDK 10.0.401 Roslyn only; which SDK TAOM builds with
  was not pinned (no `global.json`), so an older SDK's compiler was not decompiled.
- CORRECTNESS-07: whether engine teardown clears `MobileParty.IsActive` on an abandoned campaign was
  not traced (the position pin stays PLAUSIBLE); no in-game reproduction; no heap dump for retention.
  The lane's `lane3_singletons.py` 94 mutable-state count was not re-derived.
- CORRECTNESS-02: the "gameplay only" split (740 catches, 475 logged, 148 silent) and the per-folder
  table were not re-derived; my classifier matched the global split. The 38 nested logger-fallback
  count was not re-derived.
- Skip-listed working-tree files were not read; `Main/SubModule.cs` only via `git show b2e387db:`.
