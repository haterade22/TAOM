# Deep review and Codex: plan 009, guarded patch-category apply (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: Plan 009, apply every Harmony patch category through one guarded helper
         (branch improve/009-guarded-patch-category-apply, 7f02fc8d..9da9b5b9)
Date: 2026-09-24

Scope:   C# (Main/PatchCategoryApplier.cs, Main/SubModule.cs), 11 test files, CHANGELOG,
         two docs. No XML, no harness files in the diff.
Waves:   Agents 1 to 6 (one wave); Codex adversarial review complete (END OF CODEX REVIEW present).

STANDARDS:     PASS: 0 violations of checks 1 to 10; 1 MEDIUM outside the diff (stale lens), 6 LOW, 3 nits
COMPATIBILITY: FAIL: 2 incompatible (DisplayMessage firing set at module load and first main menu), 3 unverified
EFFICIENCY:    PASS: 0 issues
COMPLETENESS:  INCOMPLETE: GitHub issue missing (needs Mike); lens 5 stale (fixed); harmony-patches.md:61 owed at merge
DATA FLOW:     FAIL: 2 gaps (1 HIGH, 1 LOW), 4 inconsistencies (2 MED, 2 LOW)
DESIGN:        4 KEEP proposals (3 apply, 1 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Details

Every finding below was re-read against the worktree source, the installed v1.5.3 engine
(`taom-src` cache and `ilspycmd` on `Modules/Native/.../TaleWorlds.MountAndBlade.GauntletUI.dll`)
and the Lib.Harmony 2.4.2 NuGet DLL before it was classified.

### Verification of the load-bearing engine claims

| Claim | Evidence read this review |
|---|---|
| `DisplayMessage` has no queue | `TaleWorlds.Library.InformationManager.cs:54-57`: `DisplayMessageInternal?.Invoke(message)` |
| The chat log is its only single-player subscriber and is built late | `MPChatVM.cs:545` subscribes; `GauntletUISubModule.OnBeforeInitialModuleScreenSetAsRoot` calls `GauntletChatLogView.Initialize()` (`:189`) |
| That hook runs after every `OnSubModuleLoad` | `Module.InitializeSubModuleBases` (`:201-222`) vs `SetInitialModuleScreenAsRootScreen` (`:765-774`) |
| The splash video follows, and the initial screen clears the chat log | `Module.cs:779-790`; `GauntletVideoPlaybackScreen.cs:52` `HideAllMessages()`; `GauntletInitialScreen.cs:77` `ClearAllMessages()` |
| The inquiry subscriber exists by TAOM's hook and queues | `GauntletUISubModule.cs:196-197` `_queryManager.Initialize()`; `GauntletQueryManager.Initialize` subscribes `OnShowInquiry` (`:55`) and holds `_inquiryQueue` (`:19`, `:174`) |
| Harmony category apply is not transactional and indexes the whole assembly | `Harmony.PatchCategory(Assembly,string)` (`:149-159`) runs `CreateClassProcessor(type).Patch()` per class with no catch; `BuildCategoryCache` (`:161-173`) calls `GetFromType`, which is `type.GetCustomAttributes(inherit: true)` with no catch (`HarmonyMethodExtensions:115-120`) |
| The engine throws a new exception, not a rethrow | `Module.cs:218-220`: `MBDebug.Print(text2); ...SetCrashReportCustomString(text2); throw new Exception();` |

### Findings and classification

| # | Source | Sev | Finding | Class | Action |
|---|---|---|---|---|---|
| 1 | Agent 5 #1 (HIGH), Agent 2 F1, Agent 6 KEEP 1, Codex P2 | HIGH | `ReportPatchFailures("module load")` at the end of `OnSubModuleLoad` drains the failure list into a `DisplayMessage` nobody receives, so the 28 module-load categories (Patch37, 49, 62, 63, 83, 90 among them) fail with no on-screen notice. A chat line at the first main menu would also be cleared by the initial screen after the splash video. | CONFIRMED | Fixed: the module-load report is gone; the one-shot main-menu block reports `"startup"` (module load plus Patch55) as an inquiry. RED tests `SubModuleSource_OnSubModuleLoad_DoesNotReportPatchFailures`, `SubModuleSource_MainMenuSetup_ReportsStartupFailuresInAnInquiry`. |
| 2 | Codex P3, Agent 2 F3, Agent 5 #6 | LOW | The summary says "Those fixes are off this session", but Harmony keeps the classes of a category it applied before the failing one. | CONFIRMED | Fixed: "Some fixes in those groups are off this session". RED: `TakeFailureSummary_NamesThePhaseAndEveryFailedCategoryInOrder`. |
| 3 | Agent 5 #2 | MED | "Costs one category" is false when an attribute names a type the engine no longer has: `BuildCategoryCache` throws for every call, so all 84 categories fail (and the game now starts with no category patch, where it used to refuse to start). | CONFIRMED (wording); NEEDS MIKE (product) | Wording fixed in `PatchCategoryApplier` doc comment, CHANGELOG and the lifecycle doc. Whether to refuse to load or to continue in that case is Mike's call. |
| 4 | Agent 5 #3, Agent 1 #1, Agent 4 C-2 | MED | Lens 5 (`5-data-flow.md:106-121`) greps for `_harmony.PatchCategory("<name>")` and flags its absence HIGH; the Harmony lesson's Prevent grep (`harmony-il.md:18,20`) does the same. Every future review would raise a false HIGH. | CONFIRMED | Fixed: both now name `TryPatchCategory("...")`; the lens exempts test-assembly probes and names the source gate. |
| 5 | Agent 5 #4 | LOW | `tools/triage_battle_load.py:530,1096` and `battle-load-diagnostics.md:738` send triagers to the deleted "Patch43 diagnostics failed to apply" warning. | CONFIRMED | Fixed: they name `[PatchApply] Patch43_BattleLoadDiagnostics FAILED`. `test_triage_battle_load.py`: 153 OK. |
| 6 | Agent 5 #5 | LOW | If a bare `Initialize` or `IoC.Resolve` in the game-init batch throws after a category failed, the leftovers are reported under "mission start". | CONFIRMED | Deferred: the throw path is the plan's recorded residual (bare initializers, `ManualPatchApplicator.ApplyAll`); the `[PatchApply]` log lines stay correct. |
| 7 | Agent 2 F2 | LOW | `crash-report.md:284` implied mods loading after TAOM are covered by Patch37's `OnSubModuleLoad` finalizer; no module's override is. | CONFIRMED | Fixed. |
| 8 | Agent 1 #2, #3, Agent 6 KEEP 2, Agent 4 C-6 | LOW | Comments at the Patch77, Patch61 and Patch83 sites still say a category apply can throw there. | CONFIRMED | Fixed (comment-only). |
| 9 | Agent 1 #4, Agent 2 F3 | LOW | "logs and rethrows" (SubModule comment, CHANGELOG) and "another mod reshaping IL" as a cause of an unresolved target. | CONFIRMED | Fixed. |
| 10 | Agent 1 #10, Agent 2 FU4, Agent 4 C-6 | LOW | The rewritten lifecycle row kept an em dash, "most patch categories" and stale line refs. | CONFIRMED | Fixed (`:112`, `:199`, `:207-616`, 24 call sites). |
| 11 | Agent 4 C-5 | LOW | Constructor guards untested; the three side-effect rewrites (Patch37, Patch77, preview) unpinned. | CONFIRMED | Fixed: `Constructor_WithANullApplyAction_Throws`, `Constructor_WithANullLogger_Throws`, `SubModuleSource_KeepsTheSideEffectsOfAFailedApply` (characterisation, green before and after). |
| 12 | Agent 1 #8 | NIT | Two adjacent comments say the same thing at the Harmony construction. | CONFIRMED | Merged into one. |
| 13 | Agent 1 #9 | NIT | Test ids named after the plan number. | CONFIRMED | Renamed to `UnresolvableTargetProbe`, `Test_UnresolvableTargetProbe`, `taom.tests.patchcategoryapplier`. |
| 14 | Agent 1 #6, Agent 4 C-1 | MED | No GitHub issue. | NEEDS MIKE | `/issue` is public; the in-game smoke needs an issue to carry `triage-needs-ingame`. |
| 15 | Agent 1 #5 | LOW | The notice is literal English (now also the inquiry title "TAOM" and button "OK"). | NEEDS MIKE | Plan deferred localization; `/localize` if Mike wants it translated. |
| 16 | Agent 1 #7, Agent 5 FU | LOW | CHANGELOG committed by the executor although the plan reserved it; heading date. | Orchestrator | The heading `## 2026-09-24` now matches this commit's date. The scope deviation is for the merge. |
| 17 | Agent 4 C-3 | LOW | `.claude/rules/harmony-patches.md:61` gate line owed. | Orchestrator | Left for the merge, as the plan's maintenance notes assign it. |

No finding was a false positive. Agent 3 reported no performance issue.

### Unverified (in-game smoke owed)

- **U1:** the game-initialization and mission-start red lines may fade behind a loading screen
  (a line lasts 10 s of chat-log ticks, `MPChatLineVM.cs`), and the chat-box option or
  `HideBattleUI` hides them. If the smoke shows that, the inquiry path covers them too.
- **U2:** whether the startup inquiry, shown under the splash video, is on screen when the main
  menu appears. The code path matches the `StallReportNotifier` precedent at the same hook, and the
  query manager queues inquiries.
- **U3:** players' runtime Harmony; this machine's copies are all 2.4.2.0.
- **Smoke:** locally break one `OnSubModuleLoad` target (Patch41) and one game-init target, never
  committed. Expect one `[PatchApply]` Error line for each, the startup inquiry naming the first,
  a red line naming the second, and every other category applied.

## Action items

1. Mike: file the GitHub issue (finding 14) and decide finding 3 (refuse to load, or continue with
   one notice, when Harmony's category index cannot be built).
2. Mike: accept the literal-English notice or queue `/localize` (finding 15).
3. Orchestrator at merge: `harmony-patches.md:61` gate line, the CHANGELOG placement, and the
   review-log number (sibling plan branches append entries too).
4. In-game smoke (U1, U2) before closing the issue.

## Improvements (Step 4)

**APPLIED:**
- `Main/SubModule.cs:618,638` and `ReportPatchFailures`: Agent 6 KEEP 1, subsumed by the finding 1
  fix. Its "startup" label is used; its chat-line delivery is replaced by an inquiry because the
  initial screen clears the chat log (Agent 2 F1). Proved by the two RED source tests.
- `Main/SubModule.cs` Patch77, Patch61, Patch83 comments: Agent 6 KEEP 2, comment-only, suite
  green.
- `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs`: Agent 6 KEEP 3, the premise test
  `RealHarmony_ACategoryWhoseTargetDoesNotResolve_ThrowsHarmonyException` deleted and its premise
  moved into the assertion message of the surviving real-Harmony test, which still fails if the
  throw or its cause disappears.

**NOT APPLIED:**
- Agent 1 #11 (pass a notifier `Action<string>` into the applier): Agent 6 weighed and rejected it
  on the simplicity criterion; the `persistent` branch would make the delegate two delegates.
- Codex finding 2 fix, "cover a category that partially succeeds before failing": the test would
  depend on Harmony's metadata order of classes in the test assembly, and the new wording is true
  whether or not a class applied first.
- Agent 6 alternative for finding 1 (a red chat line at the first main menu): disproved by
  `GauntletInitialScreen.OnInitialize` calling `ClearAllMessages()` after the splash video.

**FOLLOW-UP** (pre-existing code or docs, no issue filed; Mike files issues):
- `SubModule.cs:619` (now `:620`) green "TAOM loaded successfully!" has never displayed for the
  finding 1 reason (Agents 2, 5, 6). Delete or move into the main-menu block.
- Eight feature docs still spell `_harmony.PatchCategory("...")`: `worldmap-battle-scene-grid.md:206`,
  `alignment-aware-execution.md:474`, `arena.md:149`, `battle-scenes.md:3,49,100`,
  `localization-override.md:35,75,118` (its disable steps point at a literal that no longer exists),
  `settlement-nameplate-fade.md:67`, `skip-campaign-intro.md:74`, `weather-bounds-guard.md:52,83`;
  also `banner-color-persistence.md:65-66`.
- `docs/ai-includes/architecture.md:293-295` file tree omits `PatchCategoryApplier.cs`;
  `harmony-patch-registry.md:382, 497, 527, 704, 786, 902` still describe per-site try/catch guards.
- "Guarded like Patch60/61/62/63" comments at `SubModule.cs` Patch68 and the watchdog sites.
- `Patch37_CrashReport.cs:25-27,113-119`: the `MBSubModuleBase.OnSubModuleLoad` finalizer catches
  no module's throw; deletion candidate (Agent 2 FU2).
- `crash-report.md:285`: `MissionBehavior.OnMissionTick` is `public virtual` on v1.5.3, not
  abstract (Agent 2 FU3).
- `Patch65LandlessCultureSpawnGuardBindingTests.cs:21` says the category applies "at module load";
  it applies in `OnGameInitializationFinished`.
- An unknown category name is a silent no-op in Harmony, so a typo logs "applied OK"; the deferred
  COMP-03 reflection gate (every `[HarmonyPatchCategory]` applied) would catch it.
- `ManualPatchApplicator.ApplyAll` and the bare game-init initializers can still abort the batch
  (the plan's residual; finding 6 rides on it).
- Plan 009 line 838 attributes runtime Harmony to the Bannerlord.Harmony module; the dependency
  project ships it (Codex).

## Verification

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter FullyQualifiedName~PatchCategoryApplierTests`:
  9 passed before any edit; 3 failed after the RED tests were written (wording, module-load
  report, startup inquiry); 13 passed after the fix.
- Full suite after all edits: **Failed 2, Passed 10248, Skipped 2, Total 10252**. The two failures
  are the known live-Armory tests `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.
- `python -m unittest discover -s tools/tests -t . -p test_triage_battle_load.py`: 153 OK.
- Step 4.6 convergence pass: not run; this delegate cannot spawn a `deep-reviewer`. The orchestrator
  owns it.

VERDICT: READY FOR COMMIT (with the NEEDS MIKE items open and the in-game smoke owed)

## Codex review

Raw output: `docs/reviews/raw/codex-adversarial-009-guarded-patch-category-apply-2026-09-24.md`
(gitignored); prompt: `docs/reviews/codex-adversarial-009-guarded-patch-category-apply-2026-09-24.prompt.md`.
Codex read the branch through git refs, decompiled the installed engine and Harmony to stdout, and
pasted excerpts for every boundary the change relies on.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | HIGH (lens 5 rule 2c: a promised notice that cannot fire) | Yes | Reproduced from the installed engine: no subscriber at `OnSubModuleLoad`, and `TakeFailureSummary` clears the list first. Codex's fix (report at the initial-screen hook) was necessary but not sufficient: Agent 2 showed the chat line is cleared after the splash, so the fix uses an inquiry. |
| 2 | P3 | LOW | Yes | `PatchClassProcessor` installs each job's wrapper before the next class; no rollback. Wording fixed; the partial-application test was not added (see NOT APPLIED). |
| S9 | Partly confirmed (coverage limit) | LOW | Yes | The source-shape tests now pin the three side-effect branches and the report placement; lifecycle execution and message delivery stay smoke-only. |
| S10 | Confirmed (= finding 1) | HIGH | Yes | Same defect. |
| S1 to S8 | Disputed or unverified | n/a | Yes | Counts re-checked by Agent 4 and Agent 5 (84 call lines before and after, identical category multiset). |

- **Confirmed bugs:** Codex 1 and 2 (fixed).
- **False positives:** none.
- **Design questions:** none raised by Codex; finding 3 (from Agent 5) is Mike's.
- **Things Codex missed:** the chat-log clear after the splash (Agent 2), the assembly-wide category
  index failure (Agent 5; Codex quoted `BuildCategoryCache` and noted discovery is not
  category-local, but raised no finding), the stale lens 5 and lesson greps, and the Patch43 triage
  consumers.

Root cause table (Phase 3e):

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Module-load notice sent to no subscriber | Dead / no-op code | Assumed an API worked a certain way: the plan copied the "TAOM loaded successfully!" line as precedent without checking who listens | Source test keeps the report out of `OnSubModuleLoad`; lesson in `localization-ui.md` |
| 2 | Summary claims a failed group is off | Other (engine-library semantics) | Assumed `PatchCategory` is atomic | Wording test; lesson in `harmony-il.md` |

## AGENTS.md lessons (pending)

For the consolidated Phase 3h update, not applied here:

- **Bugs Codex typically misses:** a UI clear that happens between a message and the player's eyes
  (Codex traced the subscriber order but not the splash video and `ClearAllMessages`).
- **Bugs Codex typically misses:** review-harness text (lens greps, lesson Prevent lines) that a
  rename in the code makes stale.
- **What Codex does well:** stdout decompiles of the exact runtime Harmony, with the non-transactional
  class loop quoted to disprove a wording claim.
