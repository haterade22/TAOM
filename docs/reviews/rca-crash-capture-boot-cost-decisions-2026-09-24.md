# RCA: plan 006 maintainer decisions, crash capture (2026-09-24)

## Top-line

Branch `improve/006-crash-capture-boot-cost`, diff `70727529..8e6b0935` (the two commits that apply
decisions D3, D4, D24, D26, D43 and D47), reviewed by six `/deep-review` lenses (standards, engine,
efficiency, completeness, data flow, design) and one Codex adversarial pass (gpt-6-astra, ultra).
No HIGH finding. **One runtime defect was confirmed** (Codex P2): the bridge's off-main-thread
verdict travelled only as a best-effort write to `Exception.Data`, so an exception with a read-only
`Data` lost it and a worker-thread capture ran the Mission and Campaign collectors and the inquiry
on the worker. The rest are a test gap that let four mutations survive, and text the decisions made
stale. All are fixed on the branch (report:
`docs/reviews/deep-review-006-crash-capture-boot-cost-decisions-2026-09-24.md`). The runtime
defect and the test gap share one root: **the new capture source copied the old source's safety
mechanism, and no test observed what the service was actually told.**

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | MED | `Native2ManagedBridge.MarkIfOffMainThread` wrote `ex.Data[OffMainThreadDataKey]` inside `try { } catch { }`; the service read an absent mark as main-thread. For an exception whose `Data` is read-only (`Exception.Data` is virtual; mscorlib returns `EmptyReadOnlyDictionaryInternal` for preallocated agile exceptions) a worker capture took the full path | Safety decision in a best-effort side channel | D43 reused the AppDomain hook's mechanism (Codex #46 MED-03, 2026-05-25); the swallow was read as tolerance, not as the unsafe fallback | Verdict is now `HandleException(..., bool offMainThread)` from the bridge and the hook through one `AppDomainExceptionHook.IsOffMainThread`; the `Data` mark and `CrashReportService.IsOffMainThread` are deleted. RED test with a read-only `Data` on a worker thread. Lesson in `harmony-il.md` |
| F2 | MED | No test reached the capture path with a reachable service; mutations (bridge returns the raw exception, verdict dropped or computed late, helper hands back instead of swallowing, `Finalizer` passes id 0) and the toggle-off guard all survived | Test cannot fail | Every test ran with `IoC` unconfigured, and the record called the swallow path unreachable from a test | `RecordingCrashService` installed into the helper's cache; six mutations now each fail at least one test. Lesson in `testing-qa.md` |
| F3 | LOW | MCM hint for Enable Native-to-Managed Capture described the old six shims ("character tableau callbacks") and none of the ten combat callbacks it now governs | Stale text made false by this change | Hint lives outside the diff; the executor did not grep the toggle's hint | Hint rewritten. Lesson in `misc.md` |
| F4 | LOW | `reflection-sites.md:21` and `:113` said "six" shims; now 16 | Stale count | Repeat of the 2026-09-23 `misc.md` lesson on numbers elsewhere | Count dropped from both lines. Lesson in `misc.md` |
| F5 | LOW | The owed probe for a *dropped* callback used `OnAgentRemoved`, which D47 put back on the list, so the probe could not fail | Verification gap | Example chosen before the list changed, not re-checked | Probe now names `Agent_OnDismount`; the `OnAgentRemoved` throw is relabelled as the recipe for an off-main bundle |
| F6 | LOW | `crash-report.md:102` said the master-off path returns the raw exception and loses its throw site without PatchShield; D26 made it preserve | Stale text made false by this change | Config table not re-read after the helper change | Rewritten, and it now names the BUTR side effect (see NEEDS MIKE) |
| F7 | LOW | Tableau caller list omitted `BasicCharacterTableau` and `BrightnessDemoTableau` (installed View DLL) | Incomplete engine claim | The list came from the `taom-src` cache, which holds only types someone asked for | Code comment and doc list all six callers |
| F8 | LOW | Risks bullet for combat callbacks named only default return values; a swallow also abandons the rest of the managed method (later behaviours, `_activeAgents.Remove`, `Agent.OnRemove`) and leaves `ref`/`out` state as written | Missing limitation | D47 trace looked at reach, not at what a swallow skips | Bullet extended with the engine lines |
| F9 | LOW | "Called far more often than the per-frame screen shims" was an unmeasured comparison, and the per-call PatchShield wrapper on every shim went unmentioned | Overclaim / cost accounting | Cost paragraph costed only the bridge's null path | Comparison dropped; PatchShield's per-call `GetMethodFromHandle` named until plan 007 lands |
| F10 | LOW | The review record filed the boot-time id under D43 and the ten entries under D47; the register says the opposite, and "an unset id marks" was the executor's choice | Record mismatch | Record written from memory of the prompt, not the register rows | Rows relabelled, register ids added, executor's choice marked |
| F11 | NIT | `s_mainThreadId` was the only `s_`-prefixed static in `Main/`; the off-main test was defined twice (hook and bridge) | Convention / duplication | New code | Renamed `_mainThreadId`; one `IsOffMainThread` |
| F12 | NIT | Cap test `All_IsASmallDistinctAllowlist` could never fail on its own; the pin's name carried a count | Redundant test | `AreEquivalent` already checks counts and duplicates | Cap test folded into the pin's message; pin renamed `All_IsExactlyTheReviewedShims` |

## Root-cause pattern

F1 and F2 are one failure seen from two sides. The decision said "mark off-thread captures", and the
executor implemented the existing mark. The tests then asserted the mark on the exception, which is
the mechanism, not the outcome (what the service does). With a writable `Data` the two agree, so the
tests passed; with a read-only `Data` they part, and no test looked. F3 to F6 are the list-change
pattern already recorded on 2026-09-23 (`misc.md`), recurring for additions.

## Why each agent missed these

- **The executor (plan 006 decisions pass):** implemented D43 by copying `AppDomainExceptionHook`'s
  mark, including its swallowing catch, and wrote tests against an unreachable service, which the
  earlier record had declared the only reachable path.
- **Lens 1 (Standards):** found LOW-5 (the duplicated off-main logic) and LOW-4 (the untested
  toggle guard) but reviewed the mark's catch as a style match with the hook, not as a failure mode.
- **Lens 2 (Engine):** verified every shim, Harmony's `throw` behaviour and the premise around
  `Subscribe()`, but did not decompile `Exception.Data`; F1 needs the BCL, not the game DLLs.
- **Lens 3 (Efficiency):** out of scope for F1 and F2; it found F9's PatchShield cost.
- **Lens 4 (Completeness):** found F2 (M1, with the exact mutations) and F5, but its mutation (b)
  moved the mark rather than making the write fail.
- **Lens 5 (Data Flow):** traced the mark from writer to reader as CONNECTED (T2) and noted the
  read-only guard, but checked the happy path of the flow, not the catch's direction.
- **Lens 6 (Design):** proposed replacing the id with `TWParallel.IsMainThread()` while keeping the
  `Data` mark, so the side channel survived its proposal too.
- **Codex:** found F1, F5 and F6; it missed F2 as a defect (it listed it as a coverage limit, Known
  Suspect 9), F3 (noted, not counted), F4, F7 to F10.

## Feedback memories to codify

- Safety verdicts travel as parameters, not annotations (`lessons/harmony-il.md`).
- A lazily resolved service needs a test with it reachable (`lessons/testing-qa.md`).
- A list's membership change re-reads counts, examples and hints (`lessons/misc.md`, a repeat).
