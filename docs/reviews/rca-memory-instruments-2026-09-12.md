# RCA: memory attribution instruments, deep review 2026-09-12

Scope: the probe extension (`EngineMemoryStatsReader`, `EngineMemoryStats`, `GpuMemorySplit`,
`MemoryProbeReportFormatter`), the `ProcessMemoryTokens` extraction, the two `[SaveLoad]`
campaign-launch phases wired in `SubModule.cs`, and `ScreenCloseHeapRelease`. Five agents
(standards, API compatibility, efficiency, completeness, data flow). Suite 8,740 green before the
review, the two diagnostics classes 330 green after the fixes.

## Summary

Three confirmed findings: two from the data-flow agent (fixed the same hour) and one P2 from the
Codex adversarial pass that removed a whole class. One agent false positive. The systemic lesson
is the one that keeps recurring in this repo's lifecycle work: engine behavior was asserted from a
name and from the one layer that was read, not from the call sites that actually run.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 3 | P2 (Codex) | `ScreenCloseHeapRelease` ran `Common.MemoryCleanupGC()` on `ScreenManager.OnPopScreen` for the inventory, party and character screens, justified by "the engine only collects on a full screen teardown or a native timer, never on a plain PopScreen". True of `ScreenManager`; false one layer up: those screens close through `GameStateManager.PopState`, and `GameStateManager.OnPopState` ends with `Common.MemoryCleanupGC()` (`:306`), as does `OnPushState` (`:278`). The class was a second full collection immediately before the engine's own. Worse, it inverted the measurement: the 2.6 GB that stayed between inventory opens had survived two engine collections and is rooted, so a release could never have returned it. Removed with its tests, registration and wiring; every doc that described it corrected. | missing vanilla gate, wrong layer | I grepped `MemoryCleanupGC` callers, saw `ScreenManager.DeactivateAndFinalizeAllScreens` and `GameStateManager.cs:278/306` in the same list, and read only the `ScreenManager` one because the class subscribed to a `ScreenManager` event. The five deep-review agents were briefed with my own framing ("the engine only collects on full teardown"), so the compat agent verified the members existed and the data-flow agent traced our subscription order, and neither opened the screens' close paths. Codex did, by reading `InventoryManager.CloseInventoryPresentation`, `PartyScreenHelper.CloseScreen` and `GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen` down to `PopState`. | Before adding any engine-lifecycle side effect (a GC, a cache clear, a flush) at an event, read the CALLER CHAIN of the concrete operation you are reacting to, not only the layer whose event you subscribed to, and list every place the engine already performs the same side effect in that chain. A briefing that states the conclusion ("the engine never does X") must be phrased as the question ("does the engine do X anywhere in this chain?"). Lesson appended to `docs/reviews/lessons/gamemodels-services.md`. |

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED (doc-level, code correct) | Comments, the feature doc and the CHANGELOG said `OnGameLoaded` fires for a new campaign and that both new stamps "split the stretch after `AllBehaviorDataLoaded`". On the installed v1.4.8, `Campaign.OnInitialize` calls `GameManager.OnGameLoaded` only in the `SavedCampaign` branch (`Campaign.cs:1420-1427`) and BEFORE `LoadBehaviorData`; only `OnGameInitializationFinished` (`:1452`) runs after `AllBehaviorDataLoaded`, on both paths. | lifecycle claim, engine call order | The seam was chosen from the `MBSubModuleBase` virtual's name and the runbook's need; the caller (`Campaign.OnInitialize`) was never opened. The API agent verified the SIGNATURES existed, which is the question the compat prompt asks; the ORDER question was only asked because the data-flow prompt named it explicitly. | Before wiring any `MBSubModuleBase` / `GameManager` lifecycle virtual, read its caller in `Campaign` / `Game` / `Module` and record, in the comment at the seam, WHICH `GameLoadingType`s reach it and where it sits relative to the phase it is meant to bracket. Lesson appended to `docs/reviews/lessons/state-lifecycle-save.md`. |
| 2 | LOW | `EngineMemoryStatsReader`: the "dump requested" marker was set after `Directory.CreateDirectory` and the path construction, so a throw in either left both dump fields null and the report printed no gpu-dump line at all, indistinguishable from "no dump requested". | never-fabricate, negative space | The gpu-dump fix was written against the case actually observed (the engine writes nothing) and not against the failure modes of the code around it. The formatter test covers the missing-path render; the reader is untestable (engine statics), so nothing exercised the ordering. | Marker first, then anything that can throw (`dumpMissingPath = gpuDumpDirectory` before the try). General rule already in the never-fabricate lesson: a requested-but-failed reading must be distinguishable from a never-requested one at every exit of the method, not only the exit you expected. |

Agent false positive, recorded so the next reviewer does not chase it: the API agent reported that
`ScreenCloseHeapRelease`'s XML doc names the screens under `SandBox.GauntletUI.CharacterDeveloper`.
The file mentions `SandBox.GauntletUI` once (line 48) and no sub-namespace; the code matches by bare
CLR name. Nothing to change.

## Why each agent missed finding 1

- Standards: out of scope by design (ADRs, registration, naming).
- API compatibility: asked "does the virtual exist with this signature" and answered it correctly;
  the same agent DID find the order when the data-flow prompt made it ask. The prompt, not the
  agent, decides whether call order is checked.
- Efficiency: cold path, out of scope.
- Completeness: checked that docs describe the behavior, not that the described behavior is true.
- Data flow: caught it, by opening `Campaign.cs`. This is the "open the engine consumer" rule from
  the NavalTravel RCA (`rca-navaltravel-2026-06-24.md`) applied to a lifecycle hook instead of a
  GameModel, which is why it was asked for explicitly.

## Root-cause pattern

Both findings are the same shape as earlier RCAs in this repo: a claim about engine behavior
(when a hook fires; whether an engine call produces a file) written from the API's name and
the happy path, then propagated into three documents before anyone read the call site. The
first live run of the day had already produced one instance (the `gpu dump written` line for a
file that did not exist); finding 2 is the incomplete fix of that instance. The preventive
action is not a new rule but the existing one applied one step earlier: read the caller before
writing the comment, and treat every exit of a diagnostic method as a place where "unknown" must
still be distinguishable from "not asked".

## Feedback memories to codify

None new. The lifecycle-hook lesson goes to the lessons file (below); the never-fabricate rule
already exists and this is an application of it.
