# RCA — tournament-exit-hang changeset (deep review 2026-07-06 + round 2 2026-07-09/10)

> **Round 2 (added 2026-07-10):** Patch60 relocated but did not eliminate the hang — see "Round 2: the real sink" below. The round-1 sections stand as the incident record of the review cycle; the round-2 section records the measured root cause and the lessons about why two multi-agent static-analysis rounds bounded it wrong.

**Scope:** exit-phase diagnostics (BattleLoadDiagnostics extension, 6 hooks) + `Patch60_TournamentExitMovieRelease` (issue #331). 5-agent deep review: Standards PASS, Efficiency PASS, API Compatibility 20/20 + 6/6 semantic claims PASS, Completeness COMPLETE (1 disputed note), Data Flow **2 confirmed findings**. Both fixed in-session; suite 4136 green after fixes.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | Exit window (`_exitWindowActive`) could leak: opened by `Mission.EndMission` for ANY mission, but every closer was campaign-path-only (`FirstMapTick` needs `MapState`; `ResetLifecycle`'s sole caller is `PlayerEncounter_Start_Patch`). A custom-battle exit or chained mission left it latched → the next campaign `MapState.OnActivate` emitted a spurious `MapResumed`/`FirstMapTick` pair with a huge `t=+` — misleading forensics from a forensics feature. The feature doc asserted the opposite ("custom battle → menu can't leak a stale window"). | State/lifecycle (latch opener/closer coverage) | The window design + doc claim were written from the happy path (campaign mission → map). The doc's safety claim cited `ResetLifecycle` without tracing its callers (one caller, campaign-only). Author reviewed per-file; the lifecycle spans hooks + service + engine callers. | Fixed: `ExitBegin` is now campaign-gated (`Campaign.Current != null`, mirroring `MissionState_OpenNew_Patch`); `LogMissionInitialize` unconditionally closes a stale window (chained missions); doc corrected + residual quit-to-menu case documented as a known limitation. Rule: for every latch, enumerate every OPEN path and verify a CLOSE exists on each; never write a doc safety claim without citing the caller trace. |
| 2 | MED | `ResetLifecycle` and `LogFirstMapTick` gated the `_exitWindowActive = false` state transition behind `IsEnabled` — toggling the MCM master switch off mid-window latched the window permanently; re-enabling later produced the same spurious stamp pair. | State/lifecycle (toggle gating) | The `if (!IsEnabled) return;` early-out was copied uniformly from the sibling logging methods. Right for pure logging, wrong for state transitions — the sibling-template trap (harness-facts "mirror the sibling's FULL convention set") in reverse: conventions must be *classified* before copying, not copied wholesale. | Fixed: state transitions are unconditional; only logging is gated. 3 regression tests pin it (`ResetLifecycle_WhenDisabledMidWindow_StillClosesExitWindow`, `LogFirstMapTick_WhenDisabledMidWindow_ClosesExitWindowSilently`, `LogMissionInitialize_ClosesStaleExitWindow`). Rule → LESSONS-LEARNED (below). |
| 3 | — | DISPUTED (Completeness agent): "`ExitStateFinalizeDone` enum position out of sequence." Refuted — `ClearUnreferencedResources` runs *nested inside* `MissionState.OnFinalize` (engine decompile + the 2026-07-06 session log: seq 4→7 = StateFinalizeBegin +78ms, ResourceClearBegin +79ms, ResourceClearDone +276ms, StateFinalizeDone +276ms). Enum order exactly matches runtime order. | — | The enum comment said "mirrors the engine's teardown flow" without stating the nesting, inviting a sequential reading. | Comment now states the nesting explicitly ("not a typo"). |
| 4 | P2 (Codex) | The finding-2 fix was incomplete: the service-level window closes became unconditional, but `PlayerEncounter_Start_Patch` and `Mission_Initialize_BattleLoad_Patch` early-out on `!svc.IsEnabled` BEFORE calling `ResetLifecycle`/`LogMissionInitialize` — so with the toggle off, the closers still never ran. Toggle-off during a chained-mission exit could still latch the window; re-enabling before the next map tick could stamp a stale pair. | State/lifecycle (fix applied at one layer of a two-layer gate) | The deep-review fix was implemented and verified at the SERVICE layer; nobody re-audited the CALLERS' gates. The 3 regression tests call the service directly, structurally blind to hook-level early-outs. | Fixed: both hooks now invoke the state-closing service method before their `IsEnabled` gate (the service self-gates its logging). Rule sharpened in LESSONS-LEARNED: "unconditional" must be verified at the OUTERMOST gate — grep every caller of the fixed method for guards that re-condition it. |

## Root-cause pattern

Both confirmed findings are the same class: **a diagnostics latch whose state machine was only correct on the path the author was staring at** (campaign tournament exit). The observation-state-machine rule (`.claude/rules/harmony-patches.md` "Static State Machines") covers sentinel collisions but not *closer-path coverage* or *toggle-gated transitions* — this RCA extends the latch discipline: (a) every opener path needs a closer path, enumerated; (b) toggles gate I/O, never state transitions.

## Why each agent missed / caught these

- **Standards, Efficiency, API-Compat, Completeness:** per-file scopes; the latch lifecycle spans 6 hooks + the service + engine caller topology. Correctly out of their lanes.
- **Data Flow (Agent 5):** caught both — by tracing `ResetLifecycle`'s callers and walking the toggle-off-mid-window sequence. This is the third consecutive review where Agent 5 caught what the other four structurally cannot; the agent-5-is-highest-value claim in the skill doc keeps proving out.

## Feedback memories to codify

One durable lesson appended to `docs/reviews/LESSONS-LEARNED.md` (State/Lifecycle/Save): diagnostics latches — unconditional transitions, enumerated closers.

---

## Round 2: the real sink (2026-07-09/10)

**Symptom recurrence:** with Patch60 deployed, the hang persisted — but MOVED into `EndMissionInternal`'s view-finalize loop, exactly where Patch60 relocated the movie release (`ReleaseMovie=104,482ms` and `108,866ms` on the 2026-07-09 repros; `RemoveLayer=0ms`). The gen0 GC delta was **+8,276 in all three measured hangs** (2026-07-06 Edoras 461 agents; 2026-07-09 Minas Morgul 4 agents; 2026-07-09 arena_empire 745 agents) — a deterministic fixed workload intrinsic to releasing the Tournament movie, independent of location, agents, and town.

**How it was named:** static analysis was exhausted (22+ agents over two rounds had bounded every generic widget-release path and reality contradicted the bounds), so an in-process `ExitStallSampler` captured the frozen MAIN thread's managed stack at +8/+20/+45s into the stall. One repro sufficed:

```
PatchShield.ShieldFinalizerVoid                       ← executing per call
WidgetTemplate.OnRelease_Patch2   (×16 recursion)     ← engine template-tree release
WidgetPrefab.OnRelease → GauntletMovie.Release → Patch60.Postfix
```
Sample #2 caught `MethodBase.GetMethodFromHandle` + RuntimeType-cache insert under `Monitor.Enter` inside `WidgetFactory.IsCustomType_Patch2` — Harmony patch-invocation overhead paying reflection per call.

**Three-factor root cause (each harmless alone):**
1. **Engine:** `WidgetTemplate.CreateWidgets` appends child template subtrees to `_customTypeChildren` on every instantiation; the tournament UI re-instantiates bracket custom types (Round/Match/Team/Participant) per refresh, so `OnRelease` recurses ~10^6 nodes (fixed per tournament → the invariant gen0 delta).
2. **UIExtenderEx (legitimate):** prefixes `WidgetFactory.IsCustomType` and blank-transpiles `WidgetTemplate.OnRelease` (the de-inlining trick) — making both "patched methods."
3. **PatchShield (TAOM.Dependencies):** `Install()` stacks a `__originalMethod`-binding Harmony finalizer on EVERY patched method in the process — Harmony's generated wrapper then executes `GetMethodFromHandle` + try/catch per invocation (~50µs). ~10^6 × ~50µs ≈ 107s.

**Fix:** `PatchShield.ExcludedTargetNamespacePrefixes` — never shield `TaleWorlds.GauntletUI`/`TaleWorlds.TwoDimension` targets. Measured result: exit 105-109s → **9.5s** (`ReleaseMovie=8,822ms`, gen0 +3). Residual = UIExtenderEx's legitimate prefix wrapper at ~10^6 calls; accepted per simplicity criterion. Patch60 stays (real leak, cost-neutral relocation, and its `ReleaseMovie=Nms` stamp is the permanent regression canary). Sampler kept as standing diagnostics, thresholds raised to +15/+30/+60s above the known-good residual.

### Round-2 findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|------------|-------------------|
| 5 | HIGH | PatchShield's blanket `__originalMethod` finalizer turned a milliseconds-scale engine UI teardown into a 107s frozen exit — a ~1000× hot-path amplifier on any frequently-called patched method. | Hot-path (per-call patch overhead) | PatchShield was ported (DR3, 2026-05-27) as a crash-tolerance net; nobody costed a Harmony finalizer's per-call wrapper (`__originalMethod` ⇒ `GetMethodFromHandle` every invocation) against high-frequency targets. The C#-side hot-path rules cover TAOM's own patches, not infrastructure that patches EVERYTHING. | Namespace exclusion shipped; LESSONS-LEARNED rule: any blanket-patching infrastructure must cost its per-call overhead × the hottest conceivable target, and hot engine layers (UI, per-frame) are excluded by default. |
| 6 | — | (process) Round-1's relocation fix shipped on the premise "release while renderer alive = milliseconds" — an ASSUMED cost, contradicted by the first post-fix repro. Two adversarial static rounds refuted the true mechanism because refuters bounded loops with assumed counts (widgets ~10^3, "scopes small") instead of measured ones. | Evidence discipline | An arithmetic refutation reads as rigorous but is only as strong as its inputs; nobody demanded a measured count. And the decisive fingerprint — identical +8,276 gen0 across differing sessions = fixed workload — was in hand on day one and not exploited. | LESSONS-LEARNED: (a) relocation fixes require a measured cost budget before shipping; (b) an arithmetic refutation must cite measured, not assumed, counts; (c) check GC-delta invariance across incidents early — it discriminates fixed-workload from scaling mechanisms; (d) when statics and reality disagree, sample the live stack — one repro ended a 3-round investigation. |

### Why the reviews missed round 2

Both multi-agent rounds and Codex audited the CHANGESET and the engine's generic release path. The sink lived in the interaction of two components outside the changeset (PatchShield × UIExtenderEx) with an engine data-structure growth pattern (`_customTypeChildren` accumulation) no agent decompiled because no reviewed file referenced it. Change-scoped review structurally cannot reach cross-component interaction costs — only measurement (stack sampling) could, which is why the sampler is now standing diagnostics.

### Round-2 deep-review findings (2026-07-10, post-fix pass)

Standards/Efficiency/Completeness/Data-Flow: PASS with zero gaps (the round-1 latch classes explicitly re-verified as not reintroduced). The Compat agent confirmed 4/4 items empirically (live CLR tests of Suspend/Resume edge cases incl. suspension of a thread blocked in native code) and produced two findings, both fixed in-session:

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|------------|-------------------|
| 7 | LOW | `ExitStallSampler`'s reflection-invoked `StackTrace(Thread,bool)` was justified by a FALSE comment ("hidden from the reference assemblies") — the ctor is present, obsolete-as-warning. The original compile failure was a wrong NAMED ARGUMENT (`fNeedFileInfo:` vs the real `needFileInfo`), misdiagnosed as a missing ctor. | Evidence discipline (misdiagnosed compile error) | CS1503 "cannot convert Thread to int" reads like overload-absence; named-argument mismatch silently excludes the intended overload from resolution. The workaround worked, so the wrong explanation was never re-tested. | Fixed: direct `new StackTrace(thread, needFileInfo: false)` under the existing pragma; reflection machinery deleted. Rule of thumb: before writing a "the API is missing" comment, re-try the call with positional arguments — a named-arg typo produces the same error class. |
| 8 | MED | The exclusion list stopped at the measured incident: TAOM's own `Patch38_SettlementNameplateFade` target (`SettlementNameplateWidget.DetermineTargetAlphaValue`, ~3000 calls/sec on the campaign map, namespace `TaleWorlds.MountAndBlade.GauntletUI.Widgets.*`) still carried the shield finalizer — the same per-call tax silently costing campaign-map frame time every session. | Hot-path (fix scoped to the incident, not the class) | The exclusion was derived from the sampled namespaces only; nobody swept the OTHER shielded targets for call frequency — the same scope-one-narrower-than-the-bug pattern the NaN-gate history warns about. | Fixed: `TaleWorlds.MountAndBlade.GauntletUI` added to `ExcludedTargetNamespacePrefixes`. Next candidate flagged for future sampling: UIExtenderEx's `TaleWorlds.Library.ViewModel` patches (per-VM-construction, warm not hot — left shielded deliberately). |

### Round-2 Codex adversarial pass (review 73, 2026-07-10): 0 P1 / 2 P2 / 4 P3 — all addressed

| # | Sev | Finding | Resolution |
|---|-----|---------|------------|
| 9 | P2 | `ExitStallSampler.Poll` had no reentrancy guard — `System.Threading.Timer` callbacks overlap when a tick outlives the period (a blocked capture), racing `++_samplesTaken` and interleaving Suspend/Resume pairs on the main thread. | Fixed: `Interlocked.Exchange` `_pollActive` guard; overlapping ticks skip. Why missed: the watchdog sibling (copied pattern) never blocks in its callback so it never needed one — the sibling-template trap again: the copied convention was safe in the sibling for a reason that didn't transfer. |
| 10 | P2 | The sampler — the only diagnostics component that suspends the main thread — rode the master toggle; users couldn't keep diagnostics while opting out of thread suspension. | Fixed: independent MCM toggle "Enable Exit Stall Sampler" (default ON, honest hint about the suspend-mid-GC risk) + `ExitStallSamplerEnabled` provider gate. |
| 11 | P3 | The capture's `catch` logged while the main thread was still suspended — allocating inside the exact window the class header calls the deadlock risk. | Fixed: exception captured, Resume in `finally`, log after resume; comment now forbids any allocation between Suspend and Resume beyond the walk. |
| 12 | P3 | No runtime invariant that `OnGameInitializationFinished` stays on the tick thread across engine bumps. | Deferred with rationale (simplicity criterion): documented in the SubModule comment + re-verify-on-engine-bump note; a wrong-thread walk degrades to a harmless wrong-stack sample. |
| 13 | P3 | Sampler header comment still said +8/+20/+45s after the threshold retune. | Fixed. |
| 14 | P3 | Feature doc still described the deleted reflection-invoked ctor path. | Fixed alongside finding 7's code change; doc now also documents the reentrancy guard + independent toggle. |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/arena.md](../features/arena.md)

<!-- backlinks-end -->
