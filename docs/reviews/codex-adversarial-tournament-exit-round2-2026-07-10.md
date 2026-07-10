OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019f4d16-3f6b-77e3-b745-fb66a654cd3b
--------
user
# Adversarial review: tournament-exit-hang ROUND 2 (#331) -- ExitStallSampler + PatchShield hot-target exclusion

You are reviewing the ROUND-2 changeset that actually fixed the 104-109s tournament-exit freeze (measured post-fix: 9.5s). Round 1 (Patch60 movie-release relocation + exit-phase diagnostics) was reviewed separately (review 72); do NOT re-review it except where round-2 touches it. Be adversarial: try to break the sampler's threading, the PatchShield exclusion's safety, and the latch pairing.

## Context

Root cause (measured via in-process stack sampling, both prior static-analysis rounds had bounded it wrong): the engine's tournament UI accumulates WidgetTemplate._customTypeChildren per round-refresh into a ~10^6-call OnRelease recursion; UIExtenderEx legitimately patches WidgetFactory.IsCustomType (prefix) and WidgetTemplate.OnRelease (blank transpiler); TAOM.Dependencies' PatchShield stacked a __originalMethod-binding Harmony finalizer on EVERY patched method in the process, so Harmony's wrapper paid MethodBase.GetMethodFromHandle + try/catch per call (~50us). ~10^6 x ~50us = ~107s frozen exit with an invariant +8,276 gen0 GC delta. Fix: PatchShield never shields TaleWorlds.GauntletUI / TaleWorlds.TwoDimension / TaleWorlds.MountAndBlade.GauntletUI targets. Measured result: exit 9.5s, ReleaseMovie=8,822ms, gen0 delta +3.

## READ FIRST

- docs/reviews/rca-tournament-exit-hang-2026-07-06.md (round-2 section + round-2 deep-review findings table -- findings 5-8 are known and fixed; verify the fixes, do not re-report)
- docs/features/battle-load-diagnostics.md (exit lifecycle + sampler sections)
- docs/migration/dr3-maintenance.md (PatchShield section incl. the hot-target exclusion note)
- CHANGELOG.md top entry (2026-07-10)

## FILES TO REVIEW

- Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs (NEW -- Timer poll + Thread.Suspend stack capture)
- Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs (ExitWindowOpenedUtcTicks latch + CloseExitWindow choke point)
- Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsService.cs (new property)
- Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs (sampler singleton)
- Main/Features/BattleLoadDiagnostics/Hooks/PlayerEncounter_Start_Patch.cs + Mission_Initialize_BattleLoad_Patch.cs (closers moved before IsEnabled hook gates -- round-1 Codex P2 follow-through)
- Main/Features/Arena/Hooks/Patch60_TournamentExitMovieRelease.cs (stopwatch stamps in the Postfix)
- Dependencies/Foundation/PatchShield.cs (ExcludedTargetNamespacePrefixes + IsExcludedTarget + the Install-loop skip)
- Main/SubModule.cs -- the sampler wiring lines near "_harmony.PatchCategory(\"Patch43_BattleLoadDiagnostics\")" (SetMainThread + Start inside the _gameInitPatchesApplied one-shot)
- Tests: TAOM.Tests/Features/BattleLoadDiagnostics/{ExitStallSamplerTests,BattleLoadDiagnosticsServiceTests}.cs

## KNOWN SUSPECTS (CONFIRM or DISPUTE with code evidence)

S1. ExitStallSampler.CaptureMainThreadStack: Thread.Suspend from a ThreadPool timer thread, then `new StackTrace(thread, false)` which ALLOCATES while the target is suspended. If the main thread is suspended mid-GC (it allocates heavily during some stalls), the sampler's allocation blocks on GC -> Resume never runs -> BOTH threads wedged = permanent freeze worse than the bug. The class doc calls this an accepted dev-machine risk. Adversarial questions: (a) is the risk window actually small given the sampler only runs while the exit window is open? (b) is there a cheap hardening (e.g., GC.RegisterForFullGCNotification? TryStartNoGCRegion? pre-building the StackTrace ctor args array outside the suspend window is already done -- what else allocates between Suspend and Resume)? (c) should the sampler be MCM-gated separately instead of riding the master diagnostics toggle?

S2. The Timer callback (Poll) can overlap itself if a tick takes >1s (System.Threading.Timer semantics) -- e.g., a capture that blocks. Two concurrent Polls could double-increment _samplesTaken (it's `++_samplesTaken`, not Interlocked) and double-Suspend the main thread (Suspend of an already-suspended thread is a no-op per the compat review, but the SECOND Resume call... trace it: double-suspend no-op means suspend-count stays 1? or 2? -- on .NET Framework, Thread.Suspend on an already-user-suspended thread is documented no-throw; does Resume then leave it suspended?). Decompile/reason carefully and give a verdict + minimal fix (e.g., an Interlocked reentrancy guard on Poll).

S3. PatchShield exclusion: `_shielded.Add(method)` marks excluded methods as handled. If PatchShield.Install runs BEFORE UIExtenderEx applies its prefab patches (install ordering: AliasStubSubModule ctor / Dependencies OnSubModuleLoad / OnGameInitializationFinished re-pass), the first pass never sees those methods (not yet patched) -- fine. But could an EARLIER shield pass have already attached finalizers to UI-layer methods patched by SOME OTHER mod before the exclusion existed at that pass? (No -- exclusion is code, not state.) Real question: are there install-order windows where a UI-namespace method gets shielded because IsExcludedTarget's DeclaringType is null (e.g., global methods / dynamic methods in GetAllPatchedMethods)? The catch returns false (fail-open = shield it). Is fail-open the right polarity HERE -- an unreadable DeclaringType on a UI-hot method would re-create the bug class. Assess.

S4. The ticks latch: `ExitWindowOpenedUtcTicks` uses Interlocked, `_exitWindowActive` is a separate volatile bool -- the pair is written non-atomically (bool first, ticks second in LogExitBegin; bool first, ticks second in CloseExitWindow). The sampler keys ONLY on ticks; the exit-phase hooks key ONLY on the bool. Is there any observable tear where one observer sees an open window and the other sees closed in a way that matters? (Poll thread vs main thread; assess each interleaving; expected verdict: benign, but prove it.)

S5. Patch60 Postfix stopwatch: added between ReleaseMovie and RemoveLayer and a LogInfo after -- any way the added code changes behavior on the failure path (exception between the calls now skips the log but the catch still fires)? Trivial; confirm.

S6. SubModule wiring: SetMainThread captures Thread.CurrentThread inside OnGameInitializationFinished, which per the compat review runs on the application-tick thread. If a future engine version moves game-init to a loader thread, the sampler would suspend the WRONG thread. Is there a cheap invariant check (e.g., also verify the thread matches the one that ticks MapState later)? Assess severity of leaving it as-is (LOW acceptable?).

## REQUIRED SECTIONS

1. VANILLA/RUNTIME EVIDENCE -- paste decompiled/verified evidence for the suspects (Harmony 2.4.2 GetAllPatchedMethods semantics, Thread.Suspend/Resume suspend-count behavior on net472, Timer reentrancy).
2. THREADING ANALYSIS -- the sampler's full interleaving matrix (Poll vs main thread vs window closers).
3. EXCLUSION SAFETY -- what loses shield protection under the three prefixes; any mod-ecosystem scenario where that matters for TAOM (total conversion).
4. FINDINGS OR OBSERVATIONS -- numbered, severity P1/P2/P3, file:line, code evidence, concrete failure scenario. "No findings" for empty sections -- do not pad.

## QUALITY GATES

- Verify each finding against the actual source; cite file:line.
- Do not re-report RCA findings 5-8 (already fixed) -- verify their fixes instead.
- Distinguish accepted-documented risks (sampler suspend-mid-GC) from new defects; only escalate an accepted risk if you find its probability/impact was materially underestimated.

## Prior review lessons

SUCCESSES: outermost-gate caller audits; empirical CLR verification; decompiling vendored DLLs; disproving seeded suspects with evidence.
FAILURES: assuming counts instead of measuring; flagging vanilla-matching code as bugs; skipping hard sections.

Output your review to stdout (redirected to docs/reviews/codex-adversarial-tournament-exit-round2-2026-07-10.md).

ERROR: The process "76680" not found.
codex
I’ll review this as a verifier only: first I’m reading the round-2 docs and touched files, then I’ll trace the runtime claims against local source/decompiled dependencies before deciding whether any findings survive.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Content -Path docs/reviews/rca-tournament-exit-hang-2026-07-06.md -TotalCount 260' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Content -Path CHANGELOG.md -TotalCount 140' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Content -Path docs/migration/dr3-maintenance.md -TotalCount 240' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Content -Path docs/features/battle-load-diagnostics.md -TotalCount 260' in C:\Users\mikew\source\repos\TAOM
 succeeded in 823ms:
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

 succeeded in 836ms:
# DR3 — Dependency Maintenance Guide

**When to use:** Bannerlord ships a new minor/patch version. BUTR releases new versions of Harmony, UIExtenderEx, ButterLib, MCM, or MBOptionScreen. Microsoft.Extensions or Serilog needs a security update. A user reports a third-party mod is greyed out / silently breaking.

This guide explains exactly which files to update, how to verify changes, and how to read the defensive infrastructure's diagnostic output.

## Architecture summary

`TAOM.Dependencies` module bundles the entire BUTR + supporting stack PLUS 4 stub alias modules PLUS a runtime defensive shield layer. **End-user impact: the launcher needs ONLY `TAOM` + `TAOM.Dependencies` + the 4 auto-ticked stubs** — no external `Bannerlord.Harmony` / `.ButterLib` / `.UIExtenderEx` / `.MBOptionScreen` modules required, and third-party mods that depend on those standard IDs are toggleable + runtime-error-tolerant.

The dependencies fall into three categories:

### Category 1 — NuGet packages (deploy automatically on build)

These are pulled via `<PackageReference>` in `Dependencies/TAOM.Dependencies.csproj`:

| Package | Version pin | What it provides |
|---|---|---|
| `Lib.Harmony` | `2.4.2` | `0Harmony.dll` (Harmony 2.x + MonoMod + Cecil + Iced ILRepack'd) |
| `Bannerlord.UIExtenderEx` | `2.13.1` | `Bannerlord.UIExtenderEx.dll` |
| `Bannerlord.MCM` | `5.11.4` | `MCMv5.dll` (the MCM API — settings attributes, base classes) |
| `System.Runtime.CompilerServices.Unsafe` | `6.0.0` | `System.Runtime.CompilerServices.Unsafe.dll` (Harmony dep) |
| `Harmony.Extensions` | `3.2.0.77` | Source-only extension methods |
| `BUTR.Harmony.Analyzer` | `1.0.1.50` | Roslyn analyzer (compile-time only) |
| `Bannerlord.BuildResources` | `1.1.0.129` | MSBuild tasks for module deployment |

**Update procedure:**
1. Open `Dependencies/TAOM.Dependencies.csproj` in an editor.
2. Bump the `Version=` attribute of the relevant `<PackageReference>`.
3. **For `Lib.Harmony` / `Bannerlord.UIExtenderEx` / `Bannerlord.MCM` bumps**: ALSO bump the matching stub `<Version>` in `Stubs/Bannerlord.Harmony/_Module/SubModule.xml` (or `.UIExtenderEx`, `.MBOptionScreen` — note: MCM's stub is `Bannerlord.MBOptionScreen`). Third-party mods may pin the stub via `<DependedModuleMetadata version="..."/>`; drift here silently fails BLSE-enforced version checks. See "Stub modules" section below.
4. Run `dotnet restore Dependencies/TAOM.Dependencies.csproj`.
5. Run `./build.ps1 -RunTests` — must pass.
6. Run smoke test (see "Verification" below).

**Build prerequisite:** Bannerlord must be CLOSED during `./build.ps1`. The MSBuild `PostBuildCopyToModules` step deploys DLLs (including `0Harmony.dll`) directly into the game install — if Bannerlord is running, those files are file-locked and the build fails with `UnauthorizedAccessException`. Close the game, retry build.

### Category 2 — Bundled BUTR runtime DLLs (manually copied from Steam Workshop)

These DLLs are NOT on NuGet. They're distributed as Bannerlord modules via Steam Workshop or NexusMods. We bundle them in `Dependencies/_Module/bin/Win64_Shipping_Client/`:

| Module / DLL | Source (Steam Workshop ID) | Where to copy from |
|---|---|---|
| `Bannerlord.ButterLib.dll` | `2859232415` | `E:\Steam\steamapps\workshop\content\261550\2859232415\bin\Win64_Shipping_Client\` |
| `Bannerlord.ButterLib.Implementation.1.4.0.dll` | `2859232415` | same as above |
| `Bannerlord.ButterLib.Implementation.1.4.1.dll` | `2859232415` | same as above |
| `Bannerlord.MBOptionScreen.v1.4.0.dll` | `2859238197` | `E:\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\` |
| `Bannerlord.MBOptionScreen.v1.4.1.dll` | `2859238197` | same as above |
| `Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll` | `2859238197` | same as above |
| `MCM.UI.Adapter.MCMv5.dll` | `2859238197` | same as above |

**Steam Workshop folder mapping (Bannerlord app ID = 261550):**
- `2859188632` — Bannerlord.Harmony (we DON'T bundle this DLL — we use Lib.Harmony NuGet instead)
- `2859222409` — Bannerlord.UIExtenderEx (we DON'T bundle this DLL — we use Bannerlord.UIExtenderEx NuGet)
- `2859232415` — Bannerlord.ButterLib (BUNDLED — no NuGet equivalent for runtime)
- `2859238197` — Bannerlord.MBOptionScreen (MCM screen UI — BUNDLED)

**Update procedure when Bannerlord ships new minor version (e.g., 1.5.0):**

1. **Confirm BUTR has shipped matching versions.** Steam Workshop auto-updates the BUTR modules when they ship 1.5.x-compatible builds. Check the Workshop folders for new `Implementation.1.5.x.dll` and `MBOptionScreen.v1.5.x.dll` files.
2. **Copy the new versioned DLLs** into `Dependencies/_Module/bin/Win64_Shipping_Client/`:
   ```pwsh
   Copy-Item "E:\Steam\steamapps\workshop\content\261550\2859232415\bin\Win64_Shipping_Client\Bannerlord.ButterLib.Implementation.1.5.*.dll" `
             -Destination "Dependencies\_Module\bin\Win64_Shipping_Client\"
   Copy-Item "E:\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\Bannerlord.MBOptionScreen.v1.5.*.dll" `
             -Destination "Dependencies\_Module\bin\Win64_Shipping_Client\"
   ```
3. **Also copy the loader + adapter** (these typically don't change per game version, but verify):
   ```pwsh
   Copy-Item "E:\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\Bannerlord.ModuleLoader.Bannerlord.MBOptionScreen.dll" `
             -Destination "Dependencies\_Module\bin\Win64_Shipping_Client\" -Force
   Copy-Item "E:\Steam\steamapps\workshop\content\261550\2859238197\bin\Win64_Shipping_Client\MCM.UI.Adapter.MCMv5.dll" `
             -Destination "Dependencies\_Module\bin\Win64_Shipping_Client\" -Force
   ```
4. **Update Main/_Module/SubModule.xml** — bump `<DependedModuleMetadata id="Native" version="v1.5.0.*" />` to the new version.
5. **Re-evaluate any older Implementation.1.4.x.dll files** — you can leave them for fallback compatibility OR delete them to slim the deployment. If deleting, also remove the version from `.gitignore` exception list.
6. **Rebuild + test:** `./build.ps1 -RunTests`. Run smoke test.

### Category 3 — Microsoft.Extensions + Serilog runtime DLLs (bundled with ButterLib, same source as Category 2)

These ship alongside ButterLib in its Steam Workshop folder:

| DLL | Purpose |
|---|---|
| `Microsoft.Bcl.HashCode.dll` | HashCode polyfill for net472 |
| `Microsoft.Extensions.DependencyInjection.dll` + `.Abstractions.dll` | DI container for ButterLib |
| `Microsoft.Extensions.Logging.dll` + `.Abstractions.dll` | Logging abstractions |
| `Microsoft.Extensions.Options.dll`, `.Primitives.dll` | Configuration system |
| `Serilog.dll`, `Serilog.Extensions.Logging.dll`, `Serilog.Sinks.File.dll` | ButterLib's structured logging backend |
| `System.Buffers.dll`, `System.Memory.dll`, `System.Numerics.Vectors.dll`, `System.Collections.Immutable.dll`, `System.Reflection.Metadata.dll` | .NET runtime polyfills for net472 |

**Update procedure:** copied alongside ButterLib in the Category 2 step above. Their versions are pinned by ButterLib's distribution; we don't manage them independently.

### Category 4 — Main module vendored DLLs (Warg AI + native skin fixes)

A separate pool of vendored DLLs lives in `Main/_Module/bin/Win64_Shipping_Client/` (NOT `Dependencies/_Module/bin/`). These ship with the **TAOM module itself**, not with `TAOM.Dependencies`. They were previously gitignored — `chore(build)` commit `c4231c8` (2026-05-23) added a `.gitignore` allowlist mirroring the Category 2 pattern.

| DLL | Origin | Why bundled |
|---|---|---|
| `MinHook.x64.dll` | [TsudaKageyu/MinHook](https://github.com/TsudaKageyu/minhook), MIT — third-party native hook lib | Runtime dep of `TAOM.NativeSkinFixes.dll` |
| `TAOM.NativeSkinFixes.dll` | **TAOM-owned C++** — source vendored at `Dependencies/NativeSkinFixes.NativeHooks/` (in-repo since 2026-05-26) | TAOM's own native plugin for covers_head morph fix + hair/beard cloth simulation. See [`docs/features/native-skin-fixes.md`](../features/native-skin-fixes.md). |

> `BehaviorTrees.dll` + `BehaviorTreeWrapper.dll` were removed from this table on 2026-05-24 — both libraries were decompiled (no upstream source repo) and inlined as TAOM source at `Main/BehaviorTrees/` + `Main/BehaviorTreeWrapper/`. They compile into `TAOM.dll` now. RCA: [docs/reviews/rca-looter-battle-nre-2026-05-24.md](../reviews/rca-looter-battle-nre-2026-05-24.md).

**Update procedure (MinHook):** stable third-party binary; only update when the upstream releases a new build. Drop the new `.dll` into `Main/_Module/bin/Win64_Shipping_Client/`, run `./build.ps1 -RunTests`, smoke test, commit.

**Update procedure (TAOM.NativeSkinFixes):** the C++ source lives in-repo at `Dependencies/NativeSkinFixes.NativeHooks/`. Workflow per change:

1. Edit the C++ source under `Dependencies/NativeSkinFixes.NativeHooks/` (hook bodies in `*Hook.cpp`, byte patterns in `Signatures.h`). The `.vcxproj` is standalone — NOT in `TAOM.sln` — to keep MSVC off the critical path for teammates / CI building `TAOM.dll` only.
2. Run `pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1` to rebuild. Output writes directly into `Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll` (and `MinHook.x64.dll` is copied via post-build step).
3. Run `./build.ps1` — `Bannerlord.BuildResources` will deploy the new DLL into the game install on every dotnet build automatically.
4. `git add Main/_Module/bin/Win64_Shipping_Client/TAOM.NativeSkinFixes.dll && git commit` — the `.gitignore` allowlist explicitly permits this binary, but `git add` is still required (it's not auto-staged by `dotnet build`).

**Important:** `MCMv5.dll` is NOT in this folder. MCMv5 is provided by `TAOM.Dependencies` (`Bannerlord.MBOptionScreen*.dll` + `MCM.UI.Adapter.MCMv5.dll`) + the `Bannerlord.MCM` NuGet (compile-time). The vestigial `<Reference Include="MCMv5">` block was removed from `Main/TAOM.csproj` in commit `c4231c8`. Do not re-add it.

**Allowlist gotcha:** The `.gitignore` allowlist is explicit — only the 4 DLLs above are un-ignored. If you add a new vendored DLL to this folder, you MUST also add a `!Main/_Module/bin/Win64_Shipping_Client/<name>.dll` line, or git will silently keep ignoring it. `TAOM.dll` + `TAOM.pdb` stay ignored by design (build outputs regenerated by `dotnet build`).

## Verification (smoke test after any dependency update)

1. **Close Bannerlord** if running (DLLs are file-locked).
2. **Rebuild:** `./build.ps1 -RunTests`
   - Both build and test must be green.
3. **Verify TAOM.Dependencies bin deployment:**
   ```pwsh
   Get-ChildItem "$env:BANNERLORD_GAME_DIR\Modules\TAOM.Dependencies\bin\Win64_Shipping_Client\" | Select-Object Name, LastWriteTime
   ```
   - Should show ~25 DLLs with timestamps matching the build.
4. **Launch Bannerlord** via Steam.
5. **Check the launcher's enabled modules:** Only `TAOM` + `TAOM.Dependencies` (plus Native/SandBox/SandBoxCore/CustomBattle) should be required. `Bannerlord.Harmony`, `Bannerlord.UIExtenderEx`, `Bannerlord.ButterLib`, `Bannerlord.MBOptionScreen` should NOT be required.
6. **Confirm in-game:**
   - Mod loads (no "TAOM.TAOM submodule could not be loaded" warning).
   - Options screen has a **Mod Options** tab.
   - Mod Options tab lists TAOM's settings (BattleBalance, RevoltTuning, etc.).
   - Click into a setting category and verify values render correctly.
   - Change a value, exit Options, re-enter — value persists.

If any of these fail, the most common causes are:
- Workshop folder didn't have the expected version (Bannerlord 1.4.5 has BUTR 1.4.1 builds; if you're on 1.5.0 and BUTR hasn't shipped 1.5.x yet, MCM/ButterLib won't load).
- DLL got corrupted on copy (re-copy from Workshop).
- SubModule.xml version constraint doesn't match installed Bannerlord (check `Main/_Module/SubModule.xml`'s `DependedModuleMetadata id="Native" version=` line).

## Common scenarios

### Scenario A: Bannerlord ships 1.4.6 (minor patch within 1.4 line)

Most likely: nothing needs to change. BUTR 1.4.1 builds typically work across all 1.4.x patches. Just bump `Main/_Module/SubModule.xml`'s Native version constraint to `v1.4.6.*` and run the smoke test.

### Scenario B: Bannerlord ships 1.5.0 (new minor version)

1. Wait for BUTR to ship matching builds (check NexusMods or BUTR Discord).
2. Steam Workshop auto-updates the BUTR modules — verify new DLLs appear in `261550/<module-id>/bin/`.
3. Copy `Bannerlord.ButterLib.Implementation.1.5.0.dll` + `Bannerlord.MBOptionScreen.v1.5.0.dll` into our bin/.
4. Bump SubModule.xml Native version constraint.
5. Test + commit.

### Scenario C: Security patch in Microsoft.Extensions

1. ButterLib's Workshop release will ship updated Microsoft.Extensions DLLs.
2. Re-copy ALL the Microsoft.Extensions.* and Serilog.* DLLs from the ButterLib Workshop folder.
3. No NuGet bump needed (we don't reference these as packages — only as bundled DLLs).
4. Test + commit.

### Scenario D: BUTR releases new MCM or ButterLib major version (e.g., MCM v6)

This is a larger change. New major versions can:
- Change SubModule class names (e.g., `MCM.MCMv6SubModule` instead of `MCM.MCMSubModule`).
- Change runtime dependencies (e.g., require new Microsoft.Extensions version).
- Add new SubModule classes (e.g., new screen renderer).

Process:
1. Read the BUTR release notes for the new version.
2. Read the new `SubModule.xml` from the BUTR module's release.
3. Update `Dependencies/_Module/SubModule.xml` to match (add/rename/remove `<SubModule>` entries).
4. Bump NuGet version pins in `Dependencies/TAOM.Dependencies.csproj` if the new version is on NuGet.
5. Copy the new bundled DLLs from Workshop.
6. Test thoroughly — major versions often have breaking API changes that ripple into TAOM source.

### Scenario E: We need a Lib.Harmony version bump (e.g., security patch in 0Harmony)

1. Update `Dependencies/TAOM.Dependencies.csproj`'s `<PackageReference Include="Lib.Harmony" Version="X.Y.Z" />`.
2. Update `Main/TAOM.csproj`'s `<PackageReference Include="Lib.Harmony" Version="X.Y.Z" IncludeAssets="compile" />` (must match).
3. `dotnet restore`.
4. Build + test.

## Risk scenarios + mitigations

### Stub modules (third-party-mod compatibility)

To preserve compatibility with third-party Bannerlord mods that declare `<DependedModule Id="Bannerlord.Harmony"/>` (or `.ButterLib` / `.UIExtenderEx` / `.MBOptionScreen`) in their `SubModule.xml`, TAOM.Dependencies ships **four passive stub modules** at the standard BUTR IDs.

Each stub is a single ~20-line `SubModule.xml` at:
- `Modules/Bannerlord.Harmony/_Module/SubModule.xml` — `<Id value="Bannerlord.Harmony"/>` v2.4.2
- `Modules/Bannerlord.UIExtenderEx/_Module/SubModule.xml` — `<Id value="Bannerlord.UIExtenderEx"/>` v2.13.1
- `Modules/Bannerlord.ButterLib/_Module/SubModule.xml` — `<Id value="Bannerlord.ButterLib"/>` v2.10.4
- `Modules/Bannerlord.MBOptionScreen/_Module/SubModule.xml` — `<Id value="Bannerlord.MBOptionScreen"/>` v5.11.4

Source files live in `Stubs/<ID>/_Module/SubModule.xml` and are deployed by the `DeployTAOMDependenciesStubs` MSBuild target in `Dependencies/TAOM.Dependencies.csproj` (fires `AfterTargets="PostBuildCopyToModules"`).

Each stub:
- Declares the standard BUTR `<Id>` so the vanilla launcher's `AreAllDependenciesOfModulePresent` check passes.
- Has `<SubModules />` empty — no DLLs load from the stub, so no duplicate `0Harmony.dll` / `Bannerlord.ButterLib.dll` enters the AppDomain.
- Declares `<DependedModule Id="TAOM.Dependencies"/>` with `<DependedModuleMetadata id="TAOM.Dependencies" order="LoadBeforeThis"/>` so the real DLLs are loaded by TAOM.Dependencies BEFORE any third-party mod tries to consume them.
- Uses `<DefaultModule value="true"/>` so the vanilla launcher auto-ticks the stub on first launch. Without this flag, the launcher's first-launch enablement logic (`item.IsSelected = item.IsNative || ((item.IsRequiredOfficial || item.IsDefault) && AreAllDependenciesOfModulePresent(item))` in `LauncherModsVM.cs:~350`) leaves the stub unchecked — and while the launcher's dep-presence check is file-on-disk only (doesn't require the stub to be ticked), users perceive un-ticked stubs as "deps missing" and may not realize they need to manually tick four placeholder entries. Auto-enable is the BetaDeps-community convention for stub modules.

**Maintenance rule:** when a `PackageReference` version in `Dependencies/TAOM.Dependencies.csproj` changes (or a vendored BUTR DLL is updated), the corresponding stub's `<Version>` must be bumped to match. Third-party mods with strict version constraints via `<DependedModuleMetadata>` will see the stub's version as the authoritative answer for "what version of Harmony is available?".

**Maintenance rule (v99 strategy, BetaDeps parity, DR3 Phase 4 — 2026-05-25):** stub `<Version>` values use the `vX.Y.99.0` pattern, not exact-match to the shipped DLL version. Third-party mods often declare `<DependedModuleMetadata version="vX.Y.x"/>` as a minimum-version check; v99 satisfies any reasonable v2.4.x / v2.13.x / v2.10.x / v5.11.x lower-bound without claiming a major-version jump. When a `PackageReference` BUMPS its minor (e.g., Lib.Harmony 2.4.x → 2.5.x), bump the matching stub to the new minor's `.99.0` (Harmony 2.5.99.0). The vanilla launcher does not enforce these constraints at all, but BLSE-enforced minimum-version checks rely on the stub version being the answer to "what version is available?".

**Red `(!)` icon on third-party mods:** this is the launcher's `IsDangerous` flag (`LauncherModuleVM.cs:280-282`) fired by TaleWorlds's `LauncherDLLData` code-verification system whenever an unsigned/third-party DLL is detected. It is a permanent warning tooltip ("Couldn't verify some or all of the code included in this module") and is **independent of toggleability** — every non-Bannerlord mod gets it. Do not mistake it for a missing-dep error. The two phenomena (`IsDisabled` = greyed/un-toggleable from missing deps, vs `IsDangerous` = red icon from unsigned code) are separate concepts in the launcher source.

### Defensive infrastructure (DR3 Phase 4 — BetaDeps parity)

TAOM.Dependencies ships 11 classes under `Dependencies/Foundation/` that catch third-party mod runtime errors and let the game keep running. Adopted from BetaDeps v0.7.5.1 (Nexus 11274) via clean-room rewrite under MIT — see `Dependencies/_Module/THIRD-PARTY-LICENSES.txt`.

**Components installed in stub-ctor (early phase, before any third-party mod):**
- `IncompatibleModDetector` — writes `session-launching.marker` at startup, deletes on main-menu reach. If marker survives to next launch, previous session crashed pre-menu; diffs modlist against `last-good-modlist.txt` to identify newly-added likely-culprit mods. **Detection only**, no XML mutation of LauncherData.xml.
- `CollectAssemblyTypesShim` — wraps `Assembly.GetTypes()` + `.GetExportedTypes()` with a Finalizer that catches `ReflectionTypeLoadException` and returns the partial type list (`ex.Types.Where(t => t != null)`). Prevents cascade failures.
- `SubModuleConstructionGuard` — Harmony Finalizer on `MBSubModuleBase` ctors. Swallows third-party SubModule ctor exceptions, logs culprit, lets launcher continue. Refuses to shield TAOM-owned SubModules.

**Components installed in TAOM.Dependencies/SubModule.OnSubModuleLoad (late phase):**
- `PatchShield` — **biggest user-value**. Iterates every Harmony-patched method in the AppDomain, attaches a Finalizer to each non-TAOM patch. Catches the trinity (`MissingMethodException`, `MissingFieldException`, `TypeLoadException`) — the canonical errors when a mod was compiled against an old Bannerlord version. Auto-unpatches the offending owner's prefixes/postfixes/transpilers from the failing target. Re-run in `OnGameInitializationFinished` to catch late-registered patches. **Hot-target exclusion (2026-07-10, #331):** targets in `TaleWorlds.GauntletUI`/`TaleWorlds.TwoDimension`/`TaleWorlds.MountAndBlade.GauntletUI` are NEVER shielded (`ExcludedTargetNamespacePrefixes`) — the shield finalizer binds `__originalMethod`, so Harmony's wrapper pays `GetMethodFromHandle` + try/catch per CALL (~50µs); stacked on UIExtenderEx's prefab-system patches (`WidgetFactory.IsCustomType`, `WidgetTemplate.OnRelease`) it turned the tournament UI's ~10^6-call template release into a measured 104-109s frozen exit. Shield value in that layer is nil (its only patcher is BUTR's own UIExtenderEx). Before shielding any new layer, cost the wrapper against the hottest conceivable target — see LESSONS-LEARNED "Blanket-patching infrastructure".
- `SaveShield` — targeted Finalizer on 10 specific TaleWorlds save/load/mission methods. Catches `DuplicateKey` + other save failures; stack-walks to attribute culprit assembly. Writes records to `failed-mods-catalog.txt` for user diagnosis.

**Diagnostic logs (always written if at all possible):**

| File | Purpose |
|---|---|
| `<game>/Modules/TAOM.Dependencies/diag.log` | Append-only runtime event log (DiagLog). All shield activity, AssemblyResolve redirects, version probe results, crash-loop detection. Inspect first when diagnosing any incident. |
| `<game>/Modules/TAOM.Dependencies/failed-mods-catalog.txt` | One line per (culprit-mod, exception-type, owner-method) that a shield swallowed. Format: `<UTC> | <culprit> | <category> | <ExceptionType> | <owner method> | <message head>`. |
| `<game>/Modules/TAOM.Dependencies/session-launching.marker` | Crash-loop sentinel. Created at SubModule construction; deleted on `OnGameInitializationFinished` (main menu reached). Survival to next launch = previous session crashed pre-menu. |
| `<game>/Modules/TAOM.Dependencies/last-good-modlist.txt` | Snapshot of enabled modules at last main-menu reach. Used by `IncompatibleModDetector` to diff against current modlist for culprit identification. |

**Opt-out flags (place an empty file at the path to activate):**

| Flag file (path: `<game>/Modules/TAOM.Dependencies/<name>`) | Effect |
|---|---|
| `patchshield-disabled.flag` | Skip `PatchShield.Install` entirely. Use when diagnosing whether a crash is masked by PatchShield vs an actual problem in TAOM. |
| `saveshield-swallow-disabled.flag` | Install SaveShield BUT re-throw exceptions instead of swallowing. Use when investigating which save/mission method is failing. |

(No opt-out for `SubModuleConstructionGuard` / `CollectAssemblyTypesShim` / `IncompatibleModDetector` — these are detection-only or have no destructive effect.)


 succeeded in 863ms:
# Battle Load Diagnostics

## Overview

`BattleLoadDiagnostics` phase-stamps the **entire attack → battle-playable lifecycle** to the TAOM debug log (`Logs/taom_debug_*.log`) and runs a background-thread **stall watchdog**. When a battle gets stuck on the loading screen (the intermittent infinite-load hang), the **last line written before the freeze names the stuck phase** — and for the equipment phase, the exact agent and the item whose collision mesh (`bo_` / `shield_body_name`) is missing.

## Why This Exists

Users report that entering a battle *sometimes* hangs forever on the loading screen — **no crash, no stack trace**, the battle never initializes. It is intermittent, happens on user machines, and cannot be reproduced locally. A hang ≠ a crash: a crash throws (and TAOM's `CrashReport` feature already captures it); a hang means the **main thread is blocked**, so nothing is thrown and the existing crash pipeline never fires. The existing scene-reference audits (`audit_battle_scenes.py`, `audit_scene_names.py`) only catch *crashes* from missing scene folders, not this hang.

The leading hypothesis (historically the cause) is a missing `bo_` collision mesh on a weapon/shield in `LOTRLOME_Armory`: the engine stalls resolving the absent mesh while spawning an agent that equips that item. The engine even logs this itself — `rgl_log_errors_*.txt` contains `get_object failed for body: bo_X` (see the companion tool [mesh-ref-validation.md](mesh-ref-validation.md)). But the hang could also be scene-side. This feature is **cause-agnostic**: it localizes *any* battle-load hang by phase, so the next user report comes with a log that points at the culprit instead of a shrug.

## Architecture

### The six lifecycle phases

Each phase is a thin Harmony hook (or `MissionLogic`) that delegates one call to `IBattleLoadDiagnosticsService`, which writes a consistent line:

```
[BattleLoad] seq=NN t=+1234ms phase=<PhaseName> <detail>
```

`seq` is a monotonic counter (`Interlocked.Increment`); `t=+Nms` is `Stopwatch` elapsed since the encounter began. A large gap between two consecutive `seq` lines is the stall location.

| # | Phase | Hook | TaleWorlds seam (v1.4.6) |
|---|-------|------|--------------------------|
| 1 | `EncounterStart` | `PlayerEncounter_Start_Patch` (Postfix) | `PlayerEncounter.Start()` — resets the lifecycle clock |
| 2 | `MissionOpenNew` | `MissionState_OpenNew_Patch` (Prefix) | `MissionState.OpenNew(string, MissionInitializerRecord, …)` — logs scene + attacker/defender/sizes/side from `PlayerEncounter.Current` |
| 3 | `BattleSceneSelected` | `BattleSceneSelection_Patch` (Postfix) | `DefaultSceneModel.GetBattleSceneForMapPatch(MapPatchData, bool)` — logs `mapIndex → sceneId` |
| 4 | `MissionInitialize` | `Mission_Initialize_BattleLoad_Patch` (Prefix) | `Mission.Initialize` (private) — opens the loading window |
| 5 | `AgentEquipBegin` / `AgentEquipOk` | `Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch` (Prefix + Postfix) | `Agent.EquipItemsFromSpawnEquipment(bool,bool,bool,int)` — **the money hook** |
| 6 | `BattlePlayable` | `BattleLoadPhaseBehavior : MissionLogic` (first `OnMissionTick`) | closes the loading window — load succeeded |

All hooks share the Harmony category `Patch43_BattleLoadDiagnostics`. Phases 4 and 5 coexist with the pre-existing prefixes on the same methods (`Patch16_AtmospherePersistence` on `Mission.Initialize`, `Patch23_BannerColorPersistence` on `EquipItemsFromSpawnEquipment`) — Harmony runs all of them.

### The money hook (phase 5)

The prefix builds an `EquipmentSnapshot` (via `IEquipmentSnapshotAdapter`, reading `Agent.SpawnEquipment` — the *full* `Equipment` incl. armor + horse, NOT `Agent.Equipment` which is weapons-only) and logs the full loadout **before** the engine equips the agent. The postfix logs `AgentEquipOk` only **after** the engine returns. So:

- **`AgentEquipBegin` with a matching `AgentEquipOk`** → that agent equipped fine.
- **`AgentEquipBegin` with NO matching `AgentEquipOk` (log ends here)** → the freeze is inside that agent's equipment spawn, and the dumped slots name the suspect — look for `bo=<null>` / `shieldBo=<null>`.

`FileLogger` flushes every line on a background writer thread (50 ms poll), so the begin line is on disk within ~50 ms even though the main thread is frozen.

### The loading window + stall watchdog

`BattleLoadLoadingWindow` is a static `volatile` latch: opened at `Mission.Initialize` (phase 4), closed at the first `OnMissionTick` (phase 6) or mission end. Phase-5 per-agent logging is gated on it, so **reinforcement waves after the battle is playable are not logged** (the symptom is the initial load only) — keeping the hot path a two-bool no-op outside the load window.

`BattleLoadStallWatchdog` runs on a **thread-pool `Timer`** (5 s poll) — it *must* be off the main thread, because a hang freezes the main thread and a main-thread timer could never fire. When the window has been open longer than the threshold (default 300 s / 5 min), it:

1. **Guaranteed:** writes `[BattleLoad] WATCHDOG STILL LOADING after Ns — last <CurrentStatusLine>` via `IModLogger` (thread-safe queue).
2. **Best-effort:** calls `ICrashReportService.HandleException(new BattleLoadStallException(...), "BattleLoadStallWatchdog")` to produce a full crash-bundle ZIP so the user can ship the log in one action. (Some collectors read live mission state from the background thread while the main thread is frozen and may return partial data — acceptable; the marker + flushed phase log are the primary signal.)

The pure decision `BattleLoadStallWatchdog.ShouldFire(windowOpen, elapsed, threshold, alreadyFired)` is unit-tested; the timer/CrashReport plumbing is not (game-only).

**Precompile suppression.** The watchdog honors a static `SuppressStallDetection` flag (`BattleLoadStallWatchdog.cs:38`): `Poll` early-returns while it is set (line 67), because a shader-precompile walk intentionally drives multi-minute cold-cache loads that would otherwise trip the 300 s threshold and emit a spurious crash bundle (false-positive found in a user's cold run, 2026-06-18). The flag is raised for the duration of a precompile walk; see [shader-precompilation.md](shader-precompilation.md).

### Scope: instruments ALL mission loads, by design

`Mission.Initialize` is the universal mission-setup path, so the loading window (and thus the watchdog + phase-5 logging) opens for **every** mission — field battle, siege, arena, town/conversation tableau, hideout — not only battles. **This is intentional.** Gating to battles would require detecting mission type at `Mission.Initialize` prefix time, and if that detection were unreliable at the moment of an *early* freeze, the gate would suppress the exact data we're hunting. For a diagnostic, a false-negative (missing the hang) is far worse than a false-positive (an extra bundle on a slow non-battle load). The watchdog marker embeds the scene name (`last phase=MissionInitialize scene='battle_terrain_b'` vs `scene='town_ES2'`), so a fired bundle self-identifies whether it was a battle or a town/arena load. Net effect: the tool catches *any* mission-load hang, which is strictly more coverage than the battle-only ask. (Deep-review 2026-06-01 MEDIUM finding — resolved as intentional scope; see `docs/reviews/rca-battle-load-diagnostics-2026-06-01.md`.)

### The mission-EXIT lifecycle (issue #331)

The load phases above answer "where did the *entry* hang?". The exit phases answer the mirror question — motivated by a user report of a **30 s–2 min constant hang exiting any tournament** (practice fights and field battles exit normally), which no static analysis could localize. Same line format, same `Patch43_BattleLoadDiagnostics` category, same master toggle; `LogExitBegin` restarts the seq counter + stopwatch so an exit reads as its own `seq=1..N` run.

| # | Phase | Hook | TaleWorlds seam (v1.4.6) |
|---|-------|------|--------------------------|
| 1 | `ExitBegin` | `Mission_EndMission_ExitPhase_Patch` (Postfix) | `Mission.EndMission()` — sets state `EndingNextFrame`; stamps mission/scene, `agents=<active>/<all>`, GC counts + heap |
| 2 | `ExitTeardownBegin` / `ExitTeardownDone` | `Mission_EndMissionInternal_ExitPhase_Patch` (Prefix + Postfix) | `Mission.EndMissionInternal()` (private) — behaviors' `OnEndMission*`, agent `OnRemove`/`OnDelete`, `FreeResources` + native `FinalizeMission` |
| 3 | `ExitStateFinalizeBegin` / `ExitStateFinalizeDone` | `MissionState_OnFinalize_ExitPhase_Patch` (Prefix + Postfix) | `MissionState.OnFinalize()` — wraps `Mission.OnMissionStateFinalize` (behavior removal + resource clear) |
| 4 | `ExitResourceClearBegin` / `ExitResourceClearDone` | `Mission_ClearUnreferencedResources_ExitPhase_Patch` (Prefix + Postfix) | `Mission.ClearUnreferencedResources(bool)` — `Common.MemoryCleanupGC()` (forced full GC) + native GPU `ClearResources` when `forceClearGPUResources` |
| 5 | `MapResumed` | `MapState_OnActivate_ExitPhase_Patch` (Postfix) | `MapState.OnActivate()` — loading screen over; stamps GC delta + `isSaving` (`SaveHandler.IsSaving`) |
| 6 | `FirstMapTick` | `MapState_OnTick_ExitPhase_Patch` (Postfix, one-shot) | `MapState.OnTick(float)` — menu/VM re-init done; **closes the exit window** |

**Exit-window gating.** `ExitBegin` opens a window (`IsExitWindowActive`); every other exit phase is silent outside it. This keeps the probes inert where their targets also fire elsewhere: `ClearUnreferencedResources` runs at mission *load*, `MapState.OnActivate` fires at campaign start/load, and `MapState.OnTick` runs **every map frame forever** (its postfix is a two-read early-out when the window is closed, per the hot-path rule). The window is **campaign-scoped**: `ExitBegin` opens only when `Campaign.Current != null` (custom battles have no `MapState` to complete the lifecycle, so opening there would leak the window). Closers, all **unconditional state transitions independent of the master toggle** (a mid-window toggle-off gates only the logging, never the close — deep-review data-flow finding 2026-07-06): `FirstMapTick` (normal path), the next `ResetLifecycle` (next campaign encounter), and the next `Mission.Initialize` (chained mission without map activation). `Mission.EndMission` re-invocation for the same mission is deduped by identity hash so the stopwatch is never restarted mid-exit. **Known limitation:** quitting to the main menu from *inside* a mission and then loading a campaign in the same process can emit one stale `MapResumed`/`FirstMapTick` pair with an implausibly large `t=+` value (self-heals immediately; cosmetic, and the huge timestamp self-identifies as stale).

**Reading an exit log:** the dominant gap names the sink — `ExitTeardownBegin→Done` = managed teardown / native finalize; `ExitResourceClearBegin→Done` = mission-end full GC / GPU clear (compare the `gc=`/`heapMB=` stamps on `ExitBegin` vs `MapResumed`); `MapResumed→FirstMapTick` = campaign/UI resume; `isSaving=True` = an autosave inside the window.

### The exit-stall stack sampler (`ExitStallSampler`, #331 round 2)

Phase gaps say *where* time went; only a stack says *what the frozen thread was doing*. `ExitStallSampler` is the watchdog's exit-side sibling: a background `Timer` polls the service's `ExitWindowOpenedUtcTicks` latch (nonzero exactly while the exit window is open; maintained by the same unconditional closers), and when a stall crosses **+15s/+30s/+60s** it suspends the MAIN thread (captured as `Thread.CurrentThread` in `SubModule.OnGameInitializationFinished`), walks it with the reflection-invoked `StackTrace(Thread, bool)` constructor (present in the net472 runtime, hidden from the reference assemblies), resumes, and logs the frames as `[ExitStall] sample#N` lines. Three samples of a deterministic stall show whether the top frames are stationary (one loop) or phased. Thresholds sit above the healthy tournament exit (~9.5s residual, measured 2026-07-10) so normal exits never log a false sample. Known accepted risk (documented in the class): suspending a thread mid-GC and allocating before resume can deadlock the sampler — acceptable for dev-machine diagnostics on reproducible stalls. **This sampler named the #331 round-2 sink in a single repro** (`PatchShield.ShieldFinalizerVoid` atop the `WidgetTemplate.OnRelease` recursion) after two multi-agent static rounds had bounded it wrong — see the RCA round-2 section and LESSONS-LEARNED "sample the live stack".

## Configuration

MCM page **"TAOM — Battle Load Diagnostics"** (`BattleLoadDiagnosticsSettings`, auto-registered by MCM). Defaults are the "diagnose now" posture — everything ON.

| Setting | Default | Effect |
|---------|---------|--------|
| `EnableBattleLoadDiagnostics` | `true` | Master toggle. Off → every hook is an early-out no-op. |
| `EnableStallWatchdog` | `true` | Background stall detector. |
| `EnableStallWatchdogBundle` | `true` | Also write a crash-bundle ZIP on stall (needs Crash Report capture on). |
| `StallWatchdogSeconds` | `300` | Seconds of load before flagging a stall (range 10–600; NaN/range-guarded in the provider). Default is 5 min because large custom siege scenes (e.g. Minas Tirith) legitimately take minutes to load on first entry; 45 s false-positived on them. |

`Reuse.Singleton` — the provider is a process singleton, but `IsEnabled` reads the MCM value live on each access, so an in-game toggle takes effect immediately. Every gate (the Mission.Initialize prefix, the watchdog poll, the behavior-add) reads through this one provider, so they stay consistent with each other at any instant.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsService.cs` / `BattleLoadDiagnosticsService.cs` | Phase-marker API; owns the stopwatch + seq counter + line format; swallows all exceptions |
| `Main/Features/BattleLoadDiagnostics/IEquipmentDumpFormatter.cs` / `EquipmentDumpFormatter.cs` | Pure `EquipmentSnapshot → log lines` (the `bo=`/`shieldBo=` tokens) |
| `Main/Features/BattleLoadDiagnostics/BattleLoadLoadingWindow.cs` | Static volatile open/closed latch + `OpenedAtUtc` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs` | Background `Timer` + pure `ShouldFire` predicate; triggers the bundle |
| `Main/Features/BattleLoadDiagnostics/BattleLoadStallException.cs` | Synthetic exception for the watchdog's bundle call (never thrown into the game) |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` + `…SettingsProvider.cs` | MCM page + the interface-wrapped provider |
| `Main/Features/BattleLoadDiagnostics/Domain/*` | `EquipmentSnapshot`, `EquipmentSlotSnapshot`, `BattleLoadPhase` DTOs |
| `Main/Features/BattleLoadDiagnostics/Hooks/*` | The 6 load-phase hooks + `BattleLoadPhaseBehavior` + the 6 exit-phase hooks (`*_ExitPhase_Patch`, issue #331) |
| `Main/Adapters/IEquipmentSnapshotAdapter.cs` / `EquipmentSnapshotAdapter.cs` | ADR-007 boundary: `Agent`/`Equipment`/`ItemObject` → `EquipmentSnapshot` |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs` | DryIoc registrations |

Wiring: `Main/IoC.cs` (registration), `Main/SubModule.cs` (`OnGameInitializationFinished` applies `Patch43` + starts the watchdog; `OnMissionBehaviorInitialize` adds `BattleLoadPhaseBehavior`).

## Dependencies

- `TAOM.Core.Logging.IModLogger` / `FileLogger` (the log sink; its per-line background flush is what makes the hang survivable).
- `TAOM.Features.CrashReport.ICrashReportService` (optional — the watchdog's bundle trigger).
- `TAOM.Core.Validation.FiniteFloatValidator` (watchdog threshold guard).
- MCM (`AttributeGlobalSettings`).

## Tests

`TAOM.Tests/Features/BattleLoadDiagnostics/` (50 tests, all green — 13 cover the exit-phase lifecycle: window open/close gating, seq restart, GC/isSaving line tokens, silent-outside-window, plus 3 review-hardening regressions pinning that window-close state transitions run even when the master toggle is off and that `Mission.Initialize` closes a stale window):

- `EquipmentDumpFormatterTests` — null/empty snapshots, `shieldBo=<null>` token on missing collision mesh, id/kind inclusion, one-line-per-slot.
- `BattleLoadLoadingWindowTests` — open/close/`OpenedAtUtc` transitions.
- `BattleLoadStallWatchdogTests` — `ShouldFire` at/above/below threshold, already-fired, window-closed.
- `BattleLoadDiagnosticsServiceTests` — disabled = no writes, scene/index/summary in markers, formatter delegation, begin-before-body ordering, status-line update, and **every phase method swallows a throwing logger** (the feature must never crash the game).
- `BattleLoadStallMarkerTests` — `Format`/`Parse` round-trip (scene + UTC + **absolute** log path), write→consume→delete lifecycle, consume-once, `ClearInflight`, missing-directory creation, and a locked/undeletable marker still surfacing its parsed info (parse-before-delete).

Hooks and the `MissionLogic` are game-only (ADR-008) and verified in-game.

### Reaching the dev: the stall marker + next-session notice

A hang freezes the **main thread**, so no in-the-moment dialog can render and the player force-quits — meaning the on-disk log + watchdog bundle never reach us. `IBattleLoadStallMarker` (`BattleLoadStallMarker`) closes that gap, mirroring `Dependencies/Foundation/IncompatibleModDetector`'s marker pattern:

- **phase 4** (`Mission.Initialize` prefix) writes `Logs/battle-load-inflight.marker` (scene + UTC + the current `taom_debug` log path);
- **phase 6 / mission end** (`BattleLoadPhaseBehavior`) deletes it once the load reaches a tick;
- the **next session's main menu** (`SubModule.OnBeforeInitialModuleScreenSetAsRoot`) calls `TryConsumeStaleMarker()` — a *surviving* marker means the previous load never finished, so `StallReportNotifier` shows a soft `ShowInquiry` ("last battle load may not have finished") with an **Open log folder** button pointing at the prior session's log.

This complements the watchdog: the watchdog fires for a player who **waits** past the threshold; the marker catches the (more common) player who **force-quits** the hang long before that. The marker lives in `Logs/` alongside `taom_debug_*.log` and the crash bundle, so one folder has everything. Wording is soft because a benign Alt-F4 during a load also leaves a marker — a low-harm false positive.

## How-To

### Triage a user's log automatically (equipment vs code)

Instead of reading the log by hand, run `tools/triage_battle_load.py` — it parses the `[BattleLoad]` lifecycle and prints a one-line **VERDICT** + the suspect agent/item/mesh:

```bash
# verdict from the log alone
python tools/triage_battle_load.py <taom_debug_*.log>
# authoritative: add the player's engine log to CONFIRM a missing mesh
python tools/triage_battle_load.py <taom_debug_*.log> --rgl-log <rgl_log_errors_*.txt>
# or hand it the whole crash bundle (it extracts both logs)
python tools/triage_battle_load.py --bundle <taom_crash_*.zip>
```

Verdicts: `EQUIPMENT` (ends at `AgentEquipBegin`, names the stuck agent's items), `EQUIPMENT_CONFIRMED` (+ the rgl_log's `get_object failed for body:` matches a suspect — reuses `validate_mesh_refs.parse_rgl_text`), `POST_EQUIP` (equipped fine, froze before playable → not equipment), `SCENE` / `PRE_SCENE` (froze during/before scene load → code), `COMPLETED`, `UNKNOWN` (diagnostics were off). Exit code is 1 for any diagnosed hang, 0 for COMPLETED/UNKNOWN, 2 for a bad path. Tests: `tools/tests/test_triage_battle_load.py`. The player-facing collection path (which files to ask for) is `.github/ISSUE_TEMPLATE/battle-load-hang.md`.

### Read a hang log

Open the user's `Modules/.../Logs/taom_debug_<timestamp>.log` and find the last `[BattleLoad]` line:

- ends at `phase=AgentEquipBegin agent#57 …` (no `AgentEquipOk`) → equipment hang; the indented `slot=… bo=<null>/shieldBo=<null>` lines name the item. Cross-check with `python tools/validate_mesh_refs.py` and the troop rosters.
- ends at `phase=BattleSceneSelected` (no `MissionInitialize`) → scene-load hang, not equipment.
- a `WATCHDOG STILL LOADING after Ns — last phase=…` line → the watchdog fired; the `last phase` is the freeze point, and a `taom_crash_*.zip` bundle was written alongside.
- ends at `phase=BattlePlayable` → the load completed; the hang is elsewhere.

### Add a new lifecycle phase

Add a value to `BattleLoadPhase`, a method to `IBattleLoadDiagnosticsService`, a thin hook in `Hooks/` with `[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]`, and an `Initialize(...)` call in `SubModule.OnGameInitializationFinished`. Keep the hook thin and exception-swallowing.

## Performance

- Outside the loading window, the phase-5 prefix is a two-bool read (`IsEnabled && IsOpen`) and returns. Inside, it does ~12 resident-property slot reads + one DTO alloc per spawning agent, only until the first tick.
- Master toggle off → every hook early-outs immediately.
- The watchdog is one thread-pool timer ticking every 5 s; negligible.
- `seq` uses `Interlocked` and the status line is a `volatile` reference, so the off-thread watchdog reads are torn-free.

## Related

- [mesh-ref-validation.md](mesh-ref-validation.md) — the companion tool that confirms/eliminates the missing-`bo_`-mesh hypothesis offline + via `rgl_log`.
- [mission-diagnostic.md](mission-diagnostic.md) — sibling diagnostic that dumps `MissionBehaviors`/`MissionLogics` on first tick (shares the same log file).
- [crash-report.md](crash-report.md) — the bundle pipeline the watchdog reuses.

## Changelog

- 2026-06-17 — Added the `IBattleLoadStallMarker` / next-session notice: phase 4 writes `Logs/battle-load-inflight.marker`, a surviving marker on next launch surfaces a soft `StallReportNotifier` inquiry with an Open-log-folder button (plus a `battle-load-hang.md` issue template).
- 2026-06-17 — Added `tools/triage_battle_load.py`, which parses the `[BattleLoad]` lifecycle and prints a one-line EQUIPMENT / EQUIPMENT_CONFIRMED / POST_EQUIP / SCENE / PRE_SCENE verdict naming the stuck agent/item/mesh.
- 2026-06-17 — Fixed a startup CTD: `BattleLoadStallMarker`'s second public ctor made DryIoc throw `UnableToSelectSinglePublicConstructorFromMultiple`; the test-seam ctor was made `internal`, leaving one public ctor.
- 2026-06-01 — Introduced the `BattleLoadDiagnostics` feature (`Patch43`): phase-stamps the full attack→battle-playable lifecycle across 6 markers, dumps per-agent equipment with `bo=`/`shieldBo=` mesh names, and runs the background stall watchdog (CrashReport bundle on stall).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/arena.md](./arena.md)
- [docs/features/atmosphere-persistence.md](./atmosphere-persistence.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->

 succeeded in 872ms:
# CHANGELOG — TAOM (Tales From the Age of Men)

## 2026-07-10

### feat(career-system): all 49 career ability icons + compact battle HUD (#101)

- **Icons:** every enabled career now has a 256x256 ability icon in a unified "named effect-icon"
  style — the ability's effect/emblem as a gritty painterly oil painting with the ability name
  hand-lettered across the bottom (Poisoned Blades = venom-slick crossed scimitars, Soul Drain =
  souls spiraling into a shadow hand, Warcry of Eorl = the sounding horn + white-horse banner, …).
  Art user-generated in Midjourney from per-ability prompts (faction palette + grounded-LOTR VFX
  policy: no wild-fantasy glow; overt effects only for the Dol Guldur sorcery set); downscaled
  Lanczos to 256 and baked into the `ui_taom_career_system` atlas (49/49 rects pixel-verified,
  manifest + atlas + `_tex.tpac` chain regenerated in order, install↔repo synced byte-identical).
- **Battle HUD** (`GUI/PreFabs/CareerSystem/AbilityHUD.xml`): panel 220x132 → 130x166, icon
  64 → 110, career-name line and black backdrop removed — the icon, "Press V" ready text, and the
  charge bar now float directly on the battle view. The VM's `AbilityName` property is now unbound
  (dead binding, candidate for later cleanup).
- **Rename:** `cave_troll_master` ability "Troll Frenzy" → "Gundabad Berserker"
  (`taom_career_strings.xml`, `taom_career_choices.xml`, the disabled template block — 16
  occurrences). The 12 `Languages/*/std_taom_career_strings_*.xml` files still carry the old
  translated name for those 8 string ids until the next `/localize` run.
- **Docs:** `career-system.md` (icon how-to rewritten: bake required, `sprite=` attr is dead,
  house style recorded) + `gui-sprite-system.md` (Sprites-Needed row closed; two empirical bake
  lessons: a repo→install deploy can silently clobber a fresh CLI bake — always
  `sync_sprite_bake.ps1` immediately; an editor pass can rebuild only the tpac without re-packing —
  mtime-check the manifest/atlas/tpac trio).

Not-tested: career-screen render of the new icons (battle HUD render verified in-game via
screenshot; the career screen resolves the identical sprite id).

### fix(dependencies): tournament-exit hang round 2 — PatchShield must never shield the Gauntlet UI layer (#331, the REAL fix)

- **Round-2 evidence (post-Patch60):** the ~107s stall MOVED with the relocated movie release into `EndMissionInternal` (2026-07-09 logs: `ReleaseMovie=104,482ms` / `108,866ms`; `RemoveLayer=0ms`), with the gen0 GC delta **+8,276 in all three measured hangs** across different towns and 4-745 agents — a deterministic fixed workload intrinsic to releasing the Tournament movie. Round-1's static arithmetic (widget counts, O(1) scans) was built on assumed counts and wrong.
- **Measured, not modeled:** new `ExitStallSampler` (`Main/Features/BattleLoadDiagnostics/`) — background thread that photographs the MAIN thread's managed stack at +15/+30/+60s into any exit stall (armed by the exit window's new `ExitWindowOpenedUtcTicks`; `Thread.Suspend` + the obsolete-as-warning `StackTrace(Thread,bool)` ctor, net472). First repro named the sink in one shot: `PatchShield.ShieldFinalizerVoid` atop a 16-deep `WidgetTemplate.OnRelease_Patch2` recursion; the second sample caught `MethodBase.GetMethodFromHandle` inside `WidgetFactory.IsCustomType_Patch2`.
- **Three-factor root cause, each harmless alone:** (1) the engine's tournament UI re-instantiates bracket templates per round, accumulating `WidgetTemplate._customTypeChildren` into a ~10^6-call release recursion (fixed per tournament — hence the invariant gen0 delta); (2) UIExtenderEx legitimately patches `WidgetFactory.IsCustomType` (prefix) and blank-transpiles `WidgetTemplate.OnRelease`; (3) TAOM.Dependencies' **PatchShield** stacks a `__originalMethod`-binding Harmony finalizer on EVERY patched method in the process — Harmony's wrapper then pays `GetMethodFromHandle` + try/catch per call (~50µs). ~10^6 × ~50µs ≈ 107s of frozen exit.
- **Fix:** `PatchShield.Install` now skips targets in `TaleWorlds.GauntletUI`/`TaleWorlds.TwoDimension`/`TaleWorlds.MountAndBlade.GauntletUI` namespaces (`ExcludedTargetNamespacePrefixes`) — the UI layer is per-widget-recursion hot and shield value there is nil. **Measured result: tournament exit 105-109s → 9.5s** (`ReleaseMovie=8,822ms`, gen0 delta +3). The residual ~9s is UIExtenderEx's legitimate prefix wrapper at ~10^6 calls — normal loading-screen territory, not worth patching third-party internals (simplicity criterion). The third prefix came out of the round-2 deep review's compat agent: TAOM's own Patch38 nameplate-fade target (~3000 calls/sec on the campaign map) was silently paying the same shield tax every frame.
- Patch60 (round 1) stays: the leak it fixes is real and its relocation is cost-neutral; its new per-exit `ReleaseMovie=Nms` stamp is the permanent regression canary. Sampler thresholds raised to +15/+30/+60s (above the known-good residual) and kept as standing diagnostics.
- Suite 4177 green (+12 tests: sampler schedule, exit-window ticks lifecycle, toggle/closer regressions).

Research: WidgetTemplate.CreateWidgets/OnRelease + WidgetFactory.IsCustomType (installed 1.4.6), Bannerlord.UIExtenderEx WidgetFactoryManager.Patch (vendored DLL decompile), PatchShield.Install/ShieldFinalizerVoid
Save-compat: none — UI teardown + diagnostics only.

## 2026-07-09

### balance(special-resources): raise all caps 400–600 → 10000, zero the starting amounts

- **Why:** the 2000-cost Mûmakil (`harad_mumakil_rider`, `recruit_cost="2000"`) was permanently
  unrecruitable — every resource capped at 400–600, and a creature is charged in the *recruiting
  player's* resolved resource (War Spoils for Mordor/Isengard/Gundabad/Dol Guldur players, War Drums
  for Harad/Aserai players), both far under 2000. Raising all 11 caps to 10000 makes the Mûmakil — and
  any future high-cost special creature/elite — affordable in every faction.
- **Also:** `starting_amount` set to 0 on all 11 resources — heroes now begin with an empty reserve and
  earn from scratch (was 20–40).
- **Data-only:** `special_resources_config.xml`. The `cap` flows `SpecialResourceConfigProvider` →
  `SpecialResource.Cap` → the `Math.Min(current + amount, Cap)` earning clamp and the `… / Cap` map-bar
  display; no C# change. Config is singleton-cached, so a **full game restart** (not a save reload) is
  needed to pick up the new values.
- **Files:** `Main/_Module/ModuleData/special_resources/special_resources_config.xml`,
  `docs/features/special-resources.md`.

Save-compat: none — the raised cap only relaxes the ceiling (existing balances ≤500 round-trip unchanged
and may now grow toward 10000); `starting_amount` affects only fresh hero seeding, so no saved balance is
retroactively zeroed.

## 2026-07-08

### chore(docs): enforce config-example + version-marker consistency (prevent doc drift)

- **Why:** the v1.4.7 deep-review found the banner-color feature doc still advertised the old
  `EnableLayerLimitTranspiler: true` default after the flip — a silent doc-vs-code drift (docs aren't
  compiled or tested). Rather than just fix the one doc, make the whole class a hard gate.
- **Two new `tools/lint_docs.py` checks:** (1) **config-example drift** — a `docs/features/*.md`
  `json` example whose values disagree with the shipped `Main/_Module/ModuleData/**/*.json` config it
  mirrors (compares shared keys only, so partial examples are fine; also flags a doc key the shipped
  config no longer has); (2) **version mismatch** — CLAUDE.md's "Target: Bannerlord X" line(s) or an
  API-snapshot header that disagrees with `.claude/pinned-game-version.txt`. Historical docs
  (migration/archive/rca-/codex-*) are exempt, reusing the existing stale-version exemption set.
- **Enforcement:** `.claude/hooks/check-doc-config-drift.sh` (PreToolUse Bash) runs
  `lint_docs.py --fail-on-drift` and **hard-blocks `git commit`** when a relevant file is staged and
  drift/mismatch is found. Fail-open per the TAOM hook rule (no python / linter crash / nothing
  relevant staged never blocks). **Wiring into `.claude/settings.json` is pending — the
  config-protection guardrail blocks settings edits without an explicit OK (the hook is dormant until
  registered).**
- **Drift found + fixed by the new checks:** `docs/features/war-of-the-ring.md` config example
  (`triggerDay` 1→2/14, testMode days) was out of sync with the shipped `war_of_the_ring.json`;
  `docs/ai-includes/agent-operating-manual.md` + `docs/features/bannerlord-together-compat.md` still
  named v1.4.5 as the *current* target. All fixed; `config_drift` + `version_mismatch` now 0.
- **Also made version-labels self-updating** so this class stops recurring: `tools/snapshot_api_surface.ps1`
  and the `taom-src` skill now derive the version from `Version.xml`/auto-detection instead of a
  hardcoded string.
- **Tests:** `tools/tests/test_lint_docs.py` — 14 unit tests (value mismatch, partial-example OK,
  extra/removed key, non-JSON skip, BOM config, historical exemption, version consistency, v-prefix).
- **Files:** `tools/lint_docs.py`, `.claude/hooks/check-doc-config-drift.sh`, `tools/tests/test_lint_docs.py`,
  `.claude/skills/lint-docs/SKILL.md`, CLAUDE.md hooks table, the doc fixes above. RCA:
  `docs/reviews/rca-v1.4.7-bump-2026-07-08.md`.

Save-compat: none — docs, tooling, and a commit-gate hook only.

### chore(engine): bump to Bannerlord v1.4.7 + impact analysis

- **Bump:** Steam auto-updated the installed shipping client v1.4.6 → **v1.4.7** (base game + War Sails). Handled via the
  `/engine-bump` offline pipeline: preserved the v1.4.6 decompile baseline (`_shipping_build_v1.4.6` + `_categories_v1.4.6`),
  regenerated the category tree + dual-build + `_manifest.json` to v1.4.7, MD5-diffed the blast radius (**10 assemblies
  changed**, none added/removed), bumped `.claude/pinned-game-version.txt`.
- **Compatibility:** `BindingVerification` gate **green (50/50)** — every Harmony target, GameModel override, and reflection
  site still resolves against v1.4.7. Creature/scene parity clean (`audit_mount_parity`, `audit_action_set_parity` 0 gaps,
  `audit_battle_scenes` all 256 indices). API snapshot regenerated + reproducible; the generator now version-stamps from
  `Version.xml` so its header no longer goes stale. Full impact matrix: `docs/migration/v1.4.7-impact.md`.
- **Patch15_BannerLayerLimit disabled** — v1.4.7 "made the banner layers unlimited in the banner reader"
  (`Banner.TryGetBannerDataFromCode` no longer has the `RemoveRange`/32-cap), so the transpiler is a no-op that logged
  `RemoveRange not found` every load. Flipped `EnableLayerLimitTranspiler` false in BOTH `BannerColorConfig.cs` and the
  shipped `banner_color_config.json` (JSON overrides the C# default) + added an early quiet-return guard so a disabled
  transpiler no longer logs the warning (the warning fired before the flag was consulted). Kept, not deleted. 3 tests flipped.
- **Patch49_ArmyGatheringNreGuard kept** — the v1.4.7 "null reference in AI behaviour" fix is a different site; the
  decompile confirms the guarded `Army.FindBestGatheringSettlementAndMoveTheLeader` derefs (`Army.cs:726` / `:659`) are
  still unguarded in v1.4.7, so the crash guard remains load-bearing (comment refreshed).
- **Unaffected (verified):** save-metadata stamp (Patch61 already upserts), attacking-a-raiding-party + village-no-militia
  crashes (different sites), `.sack` shader bloat (no TAOM workaround), cloth-sim crash (NativeSkinFixes parked).
- **Owed:** in-game control battles (vanilla → creatures charge/melee, Messenger conversation exit, SmartCavalry charge,
  >32-layer banner) — the only checks an offline session can't run.

Save-compat: none — decompile/docs/config-default changes only; no save-serialized state touched.

### chore(rendering): disable NativeSkinFixes by default (parked at the wiring level)

- **Change:** the three native MinHook detours (covers_head hand-morph freeze + hair/beard cloth physics) are
  now OFF by default. The install call in `SubModule.OnBeforeInitialModuleScreenSetAsRoot` is commented out, so
  the hooks never load and engine rendering is vanilla for everyone — regardless of any persisted MCM value.
- **Why the wiring-level park, not just a default flip:** the install gate reads `TaomSettings.Instance.EnableNativeSkinFixes`,
  and MCM persists a user's saved value over the compiled default. Flipping the default alone would leave the feature
  ON for any machine that already saved the toggle ON (the NavalTravel-park rationale). The compiled MCM default is
  also set to `false` and the hint rewritten to note the parked state.
- **Files:** `Main/SubModule.cs` (install branch commented out + `RE-ENABLE` breadcrumb), `Main/Features/TaomSettings.cs`
  (`EnableNativeSkinFixes` default `true`→`false`, hint), `TAOM.Tests/.../NativeSkinFixesInstallerTests.cs` (pinning
  test flipped to assert the `false` default), `docs/features/native-skin-fixes.md` (parked status).
- **Reversible:** the native DLL + C++ source stay in place; RE-ENABLE = uncomment the install branch + flip the default.

Save-compat: no save impact — the change only governs whether native hooks install at boot.


codex
The RCA says the round-2 deep-review fixes should already be in place: direct `StackTrace(Thread, needFileInfo: false)`, the `TaleWorlds.MountAndBlade.GauntletUI` exclusion, and hook-level closers before toggle gates. I’m checking the implementation against that, then I’ll go to runtime/decompiled evidence for the threading questions.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsService.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 472ms:
   1: using System;
   2: using System.Diagnostics;
   3: using System.Text;
   4: using System.Threading;
   5: using TAOM.Core.Logging;
   6: 
   7: namespace TAOM.Features.BattleLoadDiagnostics;
   8: 
   9: // Exit-stall stack sampler (#331 round 2). The ~107s tournament-exit stall freezes the MAIN
  10: // thread inside Mission.EndMissionInternal, so — like BattleLoadStallWatchdog — only a
  11: // background thread can observe it. While the diagnostics exit window is open (opened by
  12: // LogExitBegin, closed by FirstMapTick/ResetLifecycle/MissionInitialize), this samples the
  13: // main thread's managed stack at +8s/+20s/+45s and logs the frames. Three samples of a
  14: // deterministic stall name the hot method (a loop shows identical top frames each time).
  15: //
  16: // Thread.Suspend/StackTrace(Thread) are obsolete-but-functional on net472; this is the
  17: // standard in-process diagnostic sampling pattern. Known residual risk: suspending the main
  18: // thread mid-GC and then allocating can deadlock the sampler before Resume — acceptable for
  19: // a dev-machine diagnostic on a 100%-reproducible stall (worst case: kill + retry). The
  20: // whole capture is try/catch'd with Resume in finally, and the sampler only ever runs while
  21: // the exit window is open (never during normal play).
  22: public sealed class ExitStallSampler : IDisposable
  23: {
  24:     private const string Tag = "[ExitStall]";
  25:     private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
  26: 
  27:     // The known-good tournament exit is ~9.5s (the residual UIExtenderEx-wrapper cost of
  28:     // the engine's template-tree release, measured 2026-07-10 post-fix) — thresholds sit
  29:     // ABOVE it so healthy exits never log a false stall sample, while a regression toward
  30:     // the fixed ~107s class gets three stacks.
  31:     internal static readonly double[] SampleThresholdsSeconds = { 15.0, 30.0, 60.0 };
  32: 
  33:     private readonly IModLogger _logger;
  34:     private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
  35:     private readonly IBattleLoadDiagnosticsService _service;
  36: 
  37:     private Timer? _timer;
  38:     private Thread? _mainThread;
  39:     private long _lastWindowTicks;
  40:     private int _samplesTaken;
  41: 
  42:     public ExitStallSampler(
  43:         IModLogger logger,
  44:         IBattleLoadDiagnosticsSettingsProvider settings,
  45:         IBattleLoadDiagnosticsService service)
  46:     {
  47:         _logger = logger;
  48:         _settings = settings;
  49:         _service = service;
  50:     }
  51: 
  52:     /// <summary>Must be called from the game's main thread (SubModule lifecycle methods are).</summary>
  53:     public void SetMainThread(Thread mainThread) => _mainThread = mainThread;
  54: 
  55:     public void Start()
  56:     {
  57:         if (_timer != null) return;
  58:         _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
  59:     }
  60: 
  61:     // Pure schedule decision: one sample per poll, each waits for its own threshold.
  62:     public static bool ShouldSample(double elapsedSeconds, int samplesTaken)
  63:         => samplesTaken < SampleThresholdsSeconds.Length
  64:            && elapsedSeconds >= SampleThresholdsSeconds[samplesTaken];
  65: 
  66:     private void Poll()
  67:     {
  68:         try
  69:         {
  70:             if (!_settings.IsEnabled) return;
  71: 
  72:             long windowTicks = _service.ExitWindowOpenedUtcTicks;
  73:             if (windowTicks == 0L)
  74:             {
  75:                 _samplesTaken = 0;
  76:                 _lastWindowTicks = 0L;
  77:                 return;
  78:             }
  79: 
  80:             if (windowTicks != _lastWindowTicks)
  81:             {
  82:                 _lastWindowTicks = windowTicks;
  83:                 _samplesTaken = 0;
  84:             }
  85: 
  86:             double elapsed = (DateTime.UtcNow - new DateTime(windowTicks, DateTimeKind.Utc)).TotalSeconds;
  87:             if (!ShouldSample(elapsed, _samplesTaken)) return;
  88: 
  89:             int sampleIndex = ++_samplesTaken;
  90:             CaptureMainThreadStack(sampleIndex, elapsed);
  91:         }
  92:         catch (Exception ex)
  93:         {
  94:             try { _logger.LogWarning($"{Tag} poll failed: {ex.GetType().Name}: {ex.Message}"); }
  95:             catch { /* never propagate from a timer callback */ }
  96:         }
  97:     }
  98: 
  99:     private void CaptureMainThreadStack(int sampleIndex, double elapsedSeconds)
 100:     {
 101:         var thread = _mainThread;
 102:         if (thread == null || !thread.IsAlive)
 103:         {
 104:             _logger.LogWarning($"{Tag} sample#{sampleIndex} skipped — main thread not set/alive");
 105:             return;
 106:         }
 107: 
 108:         StackTrace? stack = null;
 109:         // Thread.Suspend/Resume + StackTrace(Thread,bool) are obsolete-as-WARNING on net472
 110:         // (present in both the reference assemblies and the runtime — verified empirically,
 111:         // round-2 compat review) — the canonical in-process sampling pattern for a stalled
 112:         // thread, acceptable for this diagnostics-only path.
 113: #pragma warning disable CS0618
 114:         thread.Suspend();
 115:         try
 116:         {
 117:             stack = new StackTrace(thread, needFileInfo: false);
 118:         }
 119:         catch (Exception ex)
 120:         {
 121:             _logger.LogWarning($"{Tag} sample#{sampleIndex} capture failed: {ex.GetType().Name}: {ex.Message}");
 122:         }
 123:         finally
 124:         {
 125:             try { thread.Resume(); }
 126:             catch { /* resume must never throw out */ }
 127:         }
 128: #pragma warning restore CS0618
 129: 
 130:         if (stack == null) return;
 131: 
 132:         // Format AFTER resume — keep the suspended window as small as possible.
 133:         var sb = new StringBuilder(2048);
 134:         sb.Append($"{Tag} sample#{sampleIndex} at +{elapsedSeconds:F0}s into exit stall — main thread ({stack.FrameCount} frames):");
 135:         for (int i = 0; i < stack.FrameCount; i++)
 136:         {
 137:             var method = stack.GetFrame(i)?.GetMethod();
 138:             sb.Append("\n    at ");
 139:             sb.Append(method == null ? "<unknown>" : $"{method.DeclaringType?.FullName}.{method.Name}");
 140:         }
 141:         _logger.LogError(sb.ToString());
 142:     }
 143: 
 144:     public void Dispose()
 145:     {
 146:         _timer?.Dispose();
 147:         _timer = null;
 148:     }
 149: }

 succeeded in 471ms:
   1: using System;
   2: using System.Diagnostics;
   3: using System.Threading;
   4: using TAOM.Core.Logging;
   5: using TAOM.Features.BattleLoadDiagnostics.Domain;
   6: 
   7: namespace TAOM.Features.BattleLoadDiagnostics;
   8: 
   9: public sealed class BattleLoadDiagnosticsService : IBattleLoadDiagnosticsService
  10: {
  11:     private const string Tag = "[BattleLoad]";
  12: 
  13:     private readonly IModLogger _logger;
  14:     private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
  15:     private readonly IEquipmentDumpFormatter _formatter;
  16: 
  17:     private readonly Stopwatch _stopwatch = new Stopwatch();
  18:     private int _seq;
  19:     private volatile string _currentStatusLine = "phase=<none>";
  20:     private volatile bool _exitWindowActive;
  21:     private long _exitWindowOpenedUtcTicks; // 0 = closed; read via Interlocked (feeds ExitStallSampler)
  22: 
  23:     public BattleLoadDiagnosticsService(
  24:         IModLogger logger,
  25:         IBattleLoadDiagnosticsSettingsProvider settings,
  26:         IEquipmentDumpFormatter formatter)
  27:     {
  28:         _logger = logger;
  29:         _settings = settings;
  30:         _formatter = formatter;
  31:     }
  32: 
  33:     public bool IsEnabled => _settings.IsEnabled;
  34:     public string CurrentStatusLine => _currentStatusLine;
  35: 
  36:     public void ResetLifecycle()
  37:     {
  38:         // Window state transitions are UNCONDITIONAL — a stale exit window must close even
  39:         // while the master toggle is off, or a mid-window toggle-off latches it and the next
  40:         // map activation emits spurious Exit* lines (deep-review data-flow finding, 2026-07-06).
  41:         CloseExitWindow();
  42:         if (!IsEnabled) return;
  43:         try
  44:         {
  45:             Interlocked.Exchange(ref _seq, 0);
  46:             _stopwatch.Restart();
  47:         }
  48:         catch (Exception ex) { SafeWarn("ResetLifecycle", ex); }
  49:     }
  50: 
  51:     public void LogEncounterStart(int mainPartySize)
  52:     {
  53:         if (!IsEnabled) return;
  54:         // Encounter is the lifecycle origin — make sure the clock is running even if a
  55:         // mission opened without PlayerEncounter.Start (e.g. arena/custom paths).
  56:         try { if (!_stopwatch.IsRunning) _stopwatch.Restart(); } catch { /* clock best-effort */ }
  57:         Emit(BattleLoadPhase.EncounterStart, $"mainPartySize={mainPartySize}");
  58:     }
  59: 
  60:     public void LogMissionOpenNew(string missionName, string sceneName, string? encounterSummary)
  61:     {
  62:         if (!IsEnabled) return;
  63:         var detail = $"mission='{missionName}' scene='{sceneName}'";
  64:         if (!string.IsNullOrEmpty(encounterSummary)) detail += " " + encounterSummary;
  65:         Emit(BattleLoadPhase.MissionOpenNew, detail);
  66:     }
  67: 
  68:     public void LogBattleSceneSelected(int mapIndex, string sceneId, bool isNaval)
  69:     {
  70:         if (!IsEnabled) return;
  71:         Emit(BattleLoadPhase.BattleSceneSelected, $"mapIndex={mapIndex} sceneId='{sceneId}' naval={isNaval}");
  72:     }
  73: 
  74:     public void LogMissionInitialize(string sceneName)
  75:     {
  76:         // A mission starting means any still-open exit window is stale (chained mission
  77:         // without map activation) — close it unconditionally before entry-phase logging.
  78:         CloseExitWindow();
  79:         if (!IsEnabled) return;
  80:         Emit(BattleLoadPhase.MissionInitialize, $"scene='{sceneName}'");
  81:     }
  82: 
  83:     public void LogAgentEquipBegin(EquipmentSnapshot snapshot)
  84:     {
  85:         if (!IsEnabled) return;
  86:         if (snapshot == null) return;
  87:         try
  88:         {
  89:             Emit(BattleLoadPhase.AgentEquipBegin,
  90:                 $"agent#{snapshot.AgentIndex} '{snapshot.AgentName}' char='{snapshot.CharacterId}' culture='{snapshot.CultureId}' slots={snapshot.Slots?.Count ?? 0}");
  91: 
  92:             var lines = _formatter.Format(snapshot);
  93:             if (lines != null)
  94:             {
  95:                 foreach (var line in lines)
  96:                     _logger.LogDebug($"{Tag}   {line}");
  97:             }
  98:         }
  99:         catch (Exception ex) { SafeWarn("LogAgentEquipBegin", ex); }
 100:     }
 101: 
 102:     public void LogAgentEquipOk(int agentIndex, string agentName)
 103:     {
 104:         if (!IsEnabled) return;
 105:         Emit(BattleLoadPhase.AgentEquipOk, $"agent#{agentIndex} '{agentName}'");
 106:     }
 107: 
 108:     public void LogBattlePlayable(string sceneName, int agentCount)
 109:     {
 110:         if (!IsEnabled) return;
 111:         Emit(BattleLoadPhase.BattlePlayable, $"scene='{sceneName}' agents={agentCount}");
 112:     }
 113: 
 114:     // ---- Mission-exit lifecycle (issue #331) ----
 115: 
 116:     public bool IsExitWindowActive => _exitWindowActive;
 117: 
 118:     public long ExitWindowOpenedUtcTicks => Interlocked.Read(ref _exitWindowOpenedUtcTicks);
 119: 
 120:     public void LogExitBegin(string missionName, string sceneName, int agentCount, int allAgentCount)
 121:     {
 122:         if (!IsEnabled) return;
 123:         try
 124:         {
 125:             Interlocked.Exchange(ref _seq, 0);
 126:             _stopwatch.Restart();
 127:             _exitWindowActive = true;
 128:             Interlocked.Exchange(ref _exitWindowOpenedUtcTicks, DateTime.UtcNow.Ticks);
 129:             Emit(BattleLoadPhase.ExitBegin,
 130:                 $"mission='{missionName}' scene='{sceneName}' agents={agentCount}/{allAgentCount} {GcStats()}");
 131:         }
 132:         catch (Exception ex) { SafeWarn("LogExitBegin", ex); }
 133:     }
 134: 
 135:     public void LogExitTeardownBegin()
 136:     {
 137:         if (!IsExitPhaseLoggable()) return;
 138:         Emit(BattleLoadPhase.ExitTeardownBegin, string.Empty);
 139:     }
 140: 
 141:     public void LogExitTeardownDone()
 142:     {
 143:         if (!IsExitPhaseLoggable()) return;
 144:         Emit(BattleLoadPhase.ExitTeardownDone, string.Empty);
 145:     }
 146: 
 147:     public void LogExitStateFinalizeBegin()
 148:     {
 149:         if (!IsExitPhaseLoggable()) return;
 150:         Emit(BattleLoadPhase.ExitStateFinalizeBegin, string.Empty);
 151:     }
 152: 
 153:     public void LogExitStateFinalizeDone()
 154:     {
 155:         if (!IsExitPhaseLoggable()) return;
 156:         Emit(BattleLoadPhase.ExitStateFinalizeDone, string.Empty);
 157:     }
 158: 
 159:     public void LogExitResourceClearBegin(bool forceClearGpuResources)
 160:     {
 161:         if (!IsExitPhaseLoggable()) return;
 162:         Emit(BattleLoadPhase.ExitResourceClearBegin, $"forceClearGpu={forceClearGpuResources}");
 163:     }
 164: 
 165:     public void LogExitResourceClearDone()
 166:     {
 167:         if (!IsExitPhaseLoggable()) return;
 168:         Emit(BattleLoadPhase.ExitResourceClearDone, string.Empty);
 169:     }
 170: 
 171:     public void LogMapResumed(bool isSaving)
 172:     {
 173:         if (!IsExitPhaseLoggable()) return;
 174:         Emit(BattleLoadPhase.MapResumed, $"isSaving={isSaving} {GcStats()}");
 175:     }
 176: 
 177:     public void LogFirstMapTick(bool isSaving)
 178:     {
 179:         if (IsExitPhaseLoggable())
 180:             Emit(BattleLoadPhase.FirstMapTick, $"isSaving={isSaving}");
 181:         // Close unconditionally — the hook only calls this while the window is open, and a
 182:         // mid-window toggle-off must not latch the window (only the LOGGING is gated).
 183:         CloseExitWindow();
 184:     }
 185: 
 186:     private bool IsExitPhaseLoggable() => IsEnabled && _exitWindowActive;
 187: 
 188:     private void CloseExitWindow()
 189:     {
 190:         _exitWindowActive = false;
 191:         Interlocked.Exchange(ref _exitWindowOpenedUtcTicks, 0L);
 192:     }
 193: 
 194:     // gen0/gen1/gen2 collection counts + managed heap size. Deltas between ExitBegin and
 195:     // MapResumed expose a mission-end full GC (Common.MemoryCleanupGC) as the time sink.
 196:     private static string GcStats()
 197:     {
 198:         try
 199:         {
 200:             long heapMb = GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024);
 201:             return $"gc={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} heapMB={heapMb}";
 202:         }
 203:         catch { return "gc=<unavailable>"; }
 204:     }
 205: 
 206:     // Single choke point: increment the sequence, stamp elapsed ms, update the status line
 207:     // for the watchdog, and write the marker. The status line is updated BEFORE the
 208:     // (potentially throwing) log write so the watchdog sees the latest phase even if the
 209:     // sink hiccups.
 210:     private void Emit(BattleLoadPhase phase, string detail)
 211:     {
 212:         try
 213:         {
 214:             int seq = Interlocked.Increment(ref _seq);
 215:             long ms = 0;
 216:             try { ms = _stopwatch.ElapsedMilliseconds; } catch { /* clock best-effort */ }
 217: 
 218:             _currentStatusLine = $"phase={phase} seq={seq} {detail}";
 219:             _logger.LogInfo($"{Tag} seq={seq} t=+{ms}ms phase={phase} {detail}");
 220:         }
 221:         catch (Exception ex) { SafeWarn("Emit", ex); }
 222:     }
 223: 
 224:     private void SafeWarn(string where, Exception ex)
 225:     {
 226:         try { _logger.LogWarning($"{Tag} {where} failed: {ex.GetType().Name}: {ex.Message}"); }
 227:         catch { /* the diagnostic must never propagate */ }
 228:     }
 229: }

 succeeded in 446ms:
   1: using TAOM.Features.BattleLoadDiagnostics.Domain;
   2: 
   3: namespace TAOM.Features.BattleLoadDiagnostics;
   4: 
   5: // Phase-marker capture surface for the battle-load lifecycle. Each thin hook delegates a
   6: // single call here; the service owns the sequence counter + stopwatch and the consistent
   7: // "[BattleLoad] seq=.. t=+..ms phase=.." line format. Every method swallows its own
   8: // exceptions — a diagnostic feature must never cause a crash or a hang.
   9: public interface IBattleLoadDiagnosticsService
  10: {
  11:     bool IsEnabled { get; }
  12: 
  13:     // The most recent phase marker as a compact string (e.g. "phase=AgentEquipBegin
  14:     // seq=57 agent#57 ..."). Read by the stall watchdog (off-thread) for its STILL-LOADING
  15:     // line so the bundle/marker names the exact phase that froze.
  16:     string CurrentStatusLine { get; }
  17: 
  18:     // Phase 1 — restart the stopwatch + sequence counter at the start of a new encounter.
  19:     void ResetLifecycle();
  20:     void LogEncounterStart(int mainPartySize);
  21: 
  22:     // Phase 2 — mission opening; encounterSummary is built by the hook from the sealed
  23:     // PlayerEncounter (or null) so this stays free of TaleWorlds types.
  24:     void LogMissionOpenNew(string missionName, string sceneName, string? encounterSummary);
  25: 
  26:     // Phase 3 — battle-terrain scene chosen for the world-map tile.
  27:     void LogBattleSceneSelected(int mapIndex, string sceneId, bool isNaval);
  28: 
  29:     // Phase 4 — Mission.Initialize entered.
  30:     void LogMissionInitialize(string sceneName);
  31: 
  32:     // Phase 5 — per-agent equipment spawn. Begin is written (and flushed) BEFORE the
  33:     // engine equips the agent; Ok only after it returns. A begin with no matching Ok = the
  34:     // freeze, and the dumped loadout names the suspect item.
  35:     void LogAgentEquipBegin(EquipmentSnapshot snapshot);
  36:     void LogAgentEquipOk(int agentIndex, string agentName);
  37: 
  38:     // Phase 6 — first OnMissionTick reached: the battle is playable (load succeeded).
  39:     void LogBattlePlayable(string sceneName, int agentCount);
  40: 
  41:     // ---- Mission-EXIT lifecycle (issue #331 — localize the tournament-exit hang) ----
  42:     // ExitBegin opens an "exit window", restarts the stopwatch + sequence counter, and
  43:     // stamps GC/heap stats. Every other exit phase is silent unless the window is open,
  44:     // so probes on methods that also fire at load time (ClearUnreferencedResources) or on
  45:     // every map frame (MapState.OnTick) stay inert outside a mission exit. The window
  46:     // closes at FirstMapTick or on the next ResetLifecycle.
  47:     bool IsExitWindowActive { get; }
  48: 
  49:     /// <summary>UTC ticks when the exit window opened; 0 while closed. Feeds the exit-stall
  50:     /// stack sampler (#331 round 2) the same way BattleLoadLoadingWindow feeds the watchdog.</summary>
  51:     long ExitWindowOpenedUtcTicks { get; }
  52: 
  53:     void LogExitBegin(string missionName, string sceneName, int agentCount, int allAgentCount);
  54:     void LogExitTeardownBegin();
  55:     void LogExitTeardownDone();
  56:     void LogExitStateFinalizeBegin();
  57:     void LogExitStateFinalizeDone();
  58:     void LogExitResourceClearBegin(bool forceClearGpuResources);
  59:     void LogExitResourceClearDone();
  60:     void LogMapResumed(bool isSaving);
  61:     void LogFirstMapTick(bool isSaving);
  62: }

 succeeded in 469ms:
   1: using DryIoc;
   2: using TAOM.Adapters;
   3: 
   4: namespace TAOM.Features.BattleLoadDiagnostics;
   5: 
   6: public static class BattleLoadDiagnosticsIoC
   7: {
   8:     public static void RegisterBattleLoadDiagnosticsFeature(IContainer container)
   9:     {
  10:         container.Register<IBattleLoadDiagnosticsSettingsProvider, BattleLoadDiagnosticsSettingsProvider>(Reuse.Singleton);
  11:         container.Register<IEquipmentDumpFormatter, EquipmentDumpFormatter>(Reuse.Singleton);
  12:         container.Register<IBattleLoadDiagnosticsService, BattleLoadDiagnosticsService>(Reuse.Singleton);
  13:         container.Register<IEquipmentSnapshotAdapter, EquipmentSnapshotAdapter>(Reuse.Singleton);
  14:         container.Register<IBattleLoadStallMarker, BattleLoadStallMarker>(Reuse.Singleton);
  15:         container.Register<BattleLoadStallWatchdog>(Reuse.Singleton);
  16:         container.Register<ExitStallSampler>(Reuse.Singleton);
  17:     }
  18: }

codex
The sampler implementation does include the direct constructor fix, but I also see the class header still mentions the old +8/+20/+45 thresholds while the code uses +15/+30/+60. I’ll keep that as a possible LOW/doc observation only if there are no higher-impact issues.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'Main/Features/BattleLoadDiagnostics/Hooks/PlayerEncounter_Start_Patch.cs','Main/Features/BattleLoadDiagnostics/Hooks/Mission_Initialize_BattleLoad_Patch.cs','Main/Features/Arena/Hooks/Patch60_TournamentExitMovieRelease.cs','Dependencies/Foundation/PatchShield.cs','Main/SubModule.cs'); foreach("'$p in $files){ $i=0; Write-Output "--- $p"; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'TAOM.Tests/Features/BattleLoadDiagnostics/ExitStallSamplerTests.cs','TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsServiceTests.cs'); foreach("'$p in $files){ $i=0; Write-Output "--- $p"; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg -n "ExcludedTargetNamespacePrefixes|IsExcludedTarget|GetAllPatchedMethods|ShieldFinalizerVoid|_shielded|PatchShield" Dependencies/Foundation/PatchShield.cs Dependencies -g"*.cs"' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg -n \"ExitWindowOpenedUtcTicks|ExitStallSampler|CloseExitWindow|LogExitBegin|IsExitWindowActive|Patch43_BattleLoadDiagnostics|SetMainThread|Start\\(\\)\" Main TAOM.Tests Dependencies -g\"*.cs\"" in C:\Users\mikew\source\repos\TAOM
 succeeded in 593ms:
--- TAOM.Tests/Features/BattleLoadDiagnostics/ExitStallSamplerTests.cs
   1: using Microsoft.VisualStudio.TestTools.UnitTesting;
   2: using TAOM.Features.BattleLoadDiagnostics;
   3: 
   4: namespace TAOM.Tests.Features.BattleLoadDiagnostics;
   5: 
   6: /// <summary>
   7: /// Pure decision logic of the exit-stall stack sampler (#331 round 2). The threading +
   8: /// Thread.Suspend capture is game-only per ADR-008; the schedule decision is what unit
   9: /// tests can pin: samples fire once each, in order, at their thresholds.
  10: /// </summary>
  11: [TestClass]
  12: public class ExitStallSamplerTests
  13: {
  14:     [TestMethod]
  15:     public void ShouldSample_BeforeFirstThreshold_ReturnsFalse()
  16:     {
  17:         // 14.9s < the 15s first threshold — a healthy ~9.5s tournament exit (the measured
  18:         // post-fix residual) must never produce a false stall sample.
  19:         Assert.IsFalse(ExitStallSampler.ShouldSample(elapsedSeconds: 14.9, samplesTaken: 0));
  20:     }
  21: 
  22:     [TestMethod]
  23:     public void ShouldSample_AtEachThreshold_FiresInOrder()
  24:     {
  25:         Assert.IsTrue(ExitStallSampler.ShouldSample(15.0, 0));
  26:         Assert.IsFalse(ExitStallSampler.ShouldSample(15.0, 1), "second sample must wait for its own threshold");
  27:         Assert.IsTrue(ExitStallSampler.ShouldSample(30.0, 1));
  28:         Assert.IsFalse(ExitStallSampler.ShouldSample(30.0, 2), "third sample must wait for its own threshold");
  29:         Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 2));
  30:     }
  31: 
  32:     [TestMethod]
  33:     public void ShouldSample_AllSamplesTaken_ReturnsFalse()
  34:     {
  35:         Assert.IsFalse(ExitStallSampler.ShouldSample(999.0, 3));
  36:     }
  37: 
  38:     [TestMethod]
  39:     public void ShouldSample_LateArm_CatchesUpOneAtATime()
  40:     {
  41:         // Poll granularity means elapsed may already be past several thresholds; each poll
  42:         // still takes exactly one sample so the samples stay distinct in time.
  43:         Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 0));
  44:         Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 1));
  45:         Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 2));
  46:         Assert.IsFalse(ExitStallSampler.ShouldSample(60.0, 3));
  47:     }
  48: }
--- TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsServiceTests.cs
   1: using System;
   2: using System.Collections.Generic;
   3: using Microsoft.VisualStudio.TestTools.UnitTesting;
   4: using NSubstitute;
   5: using TAOM.Core.Logging;
   6: using TAOM.Features.BattleLoadDiagnostics;
   7: using TAOM.Features.BattleLoadDiagnostics.Domain;
   8: 
   9: namespace TAOM.Tests.Features.BattleLoadDiagnostics;
  10: 
  11: [TestClass]
  12: public class BattleLoadDiagnosticsServiceTests
  13: {
  14:     private IModLogger _logger;
  15:     private IBattleLoadDiagnosticsSettingsProvider _settings;
  16:     private IEquipmentDumpFormatter _formatter;
  17:     private BattleLoadDiagnosticsService _sut;
  18: 
  19:     [TestInitialize]
  20:     public void Setup()
  21:     {
  22:         _logger = Substitute.For<IModLogger>();
  23:         _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
  24:         _formatter = Substitute.For<IEquipmentDumpFormatter>();
  25:         _formatter.Format(Arg.Any<EquipmentSnapshot>()).Returns(new List<string>());
  26:         _settings.IsEnabled.Returns(true);
  27:         _sut = new BattleLoadDiagnosticsService(_logger, _settings, _formatter);
  28:     }
  29: 
  30:     private static EquipmentSnapshot Snap() =>
  31:         new EquipmentSnapshot(7, "Orc", "mordor_orc", "mordor",
  32:             new[] { new EquipmentSlotSnapshot("Weapon1", "shield", null, null, null, null, "Shield") });
  33: 
  34:     [TestMethod]
  35:     public void IsEnabled_ReflectsSettings()
  36:     {
  37:         _settings.IsEnabled.Returns(false);
  38:         Assert.IsFalse(_sut.IsEnabled);
  39:     }
  40: 
  41:     [TestMethod]
  42:     public void LogEncounterStart_WhenDisabled_WritesNothing()
  43:     {
  44:         _settings.IsEnabled.Returns(false);
  45:         _sut.LogEncounterStart(10);
  46:         _logger.DidNotReceive().LogInfo(Arg.Any<string>());
  47:     }
  48: 
  49:     [TestMethod]
  50:     public void LogMissionOpenNew_IncludesSceneNameAndSummary()
  51:     {
  52:         _sut.LogMissionOpenNew("Battle", "battle_terrain_a", "side=Attacker");
  53:         _logger.Received().LogInfo(Arg.Is<string>(s =>
  54:             s.Contains("battle_terrain_a") && s.Contains("side=Attacker") && s.Contains("MissionOpenNew")));
  55:     }
  56: 
  57:     [TestMethod]
  58:     public void LogBattleSceneSelected_IncludesMapIndexAndSceneId()
  59:     {
  60:         _sut.LogBattleSceneSelected(42, "battle_terrain_b", false);
  61:         _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("mapIndex=42") && s.Contains("battle_terrain_b")));
  62:     }
  63: 
  64:     [TestMethod]
  65:     public void LogAgentEquipBegin_WhenDisabled_WritesNothing()
  66:     {
  67:         _settings.IsEnabled.Returns(false);
  68:         _sut.LogAgentEquipBegin(Snap());
  69:         _logger.DidNotReceive().LogInfo(Arg.Any<string>());
  70:         _formatter.DidNotReceive().Format(Arg.Any<EquipmentSnapshot>());
  71:     }
  72: 
  73:     [TestMethod]
  74:     public void LogAgentEquipBegin_DelegatesBodyToFormatter()
  75:     {
  76:         _formatter.Format(Arg.Any<EquipmentSnapshot>()).Returns(new List<string> { "slot=Weapon1 ..." });
  77:         var snap = Snap();
  78: 
  79:         _sut.LogAgentEquipBegin(snap);
  80: 
  81:         _formatter.Received(1).Format(snap);
  82:         _logger.Received().LogDebug(Arg.Is<string>(s => s.Contains("slot=Weapon1")));
  83:     }
  84: 
  85:     [TestMethod]
  86:     public void LogAgentEquipBegin_WritesBeginLineBeforeBody()
  87:     {
  88:         _formatter.Format(Arg.Any<EquipmentSnapshot>()).Returns(new List<string> { "BODYLINE" });
  89: 
  90:         _sut.LogAgentEquipBegin(Snap());
  91: 
  92:         Received.InOrder(() =>
  93:         {
  94:             _logger.LogInfo(Arg.Is<string>(s => s.Contains("AgentEquipBegin")));
  95:             _logger.LogDebug(Arg.Is<string>(s => s.Contains("BODYLINE")));
  96:         });
  97:     }
  98: 
  99:     [TestMethod]
 100:     public void CurrentStatusLine_UpdatesAfterPhaseMarker()
 101:     {
 102:         _sut.LogMissionInitialize("scene_x");
 103:         StringAssert.Contains(_sut.CurrentStatusLine, "MissionInitialize");
 104:     }
 105: 
 106:     [TestMethod]
 107:     public void Emit_IncludesSeqAndElapsedAndTagTokens()
 108:     {
 109:         _sut.LogEncounterStart(5);
 110:         _logger.Received().LogInfo(Arg.Is<string>(s =>
 111:             s.Contains("seq=") && s.Contains("t=+") && s.Contains("[BattleLoad]")));
 112:     }
 113: 
 114:     [TestMethod]
 115:     public void AllPhaseMethods_WhenLoggerThrows_DoNotPropagate()
 116:     {
 117:         _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
 118:         _logger.When(l => l.LogDebug(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
 119:         _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
 120:         _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
 121: 
 122:         // None of these may propagate — a diagnostic feature must never crash the game.
 123:         _sut.ResetLifecycle();
 124:         _sut.LogEncounterStart(1);
 125:         _sut.LogMissionOpenNew("m", "s", "x");
 126:         _sut.LogBattleSceneSelected(1, "s", false);
 127:         _sut.LogMissionInitialize("s");
 128:         _sut.LogAgentEquipBegin(Snap());
 129:         _sut.LogAgentEquipOk(1, "a");
 130:         _sut.LogBattlePlayable("s", 5);
 131:         _sut.LogExitBegin("TournamentFight", "arena_x", 20, 230);
 132:         _sut.LogExitTeardownBegin();
 133:         _sut.LogExitTeardownDone();
 134:         _sut.LogExitStateFinalizeBegin();
 135:         _sut.LogExitResourceClearBegin(true);
 136:         _sut.LogExitResourceClearDone();
 137:         _sut.LogExitStateFinalizeDone();
 138:         _sut.LogMapResumed(false);
 139:         _sut.LogFirstMapTick(false);
 140:     }
 141: 
 142:     // ---- Mission-exit phase lifecycle (issue #331 — tournament-exit hang localization) ----
 143: 
 144:     [TestMethod]
 145:     public void LogExitBegin_WhenEnabled_EmitsMissionSceneAgentsAndGcStats()
 146:     {
 147:         _sut.LogExitBegin("TournamentFight", "arena_sturgia_a", 24, 234);
 148:         _logger.Received().LogInfo(Arg.Is<string>(s =>
 149:             s.Contains("ExitBegin") && s.Contains("mission='TournamentFight'") &&
 150:             s.Contains("scene='arena_sturgia_a'") && s.Contains("agents=24/234") &&
 151:             s.Contains("gc=") && s.Contains("heapMB=")));
 152:     }
 153: 
 154:     [TestMethod]
 155:     public void LogExitBegin_WhenDisabled_WritesNothingAndWindowStaysClosed()
 156:     {
 157:         _settings.IsEnabled.Returns(false);
 158:         _sut.LogExitBegin("m", "s", 1, 1);
 159:         _logger.DidNotReceive().LogInfo(Arg.Any<string>());
 160:         Assert.IsFalse(_sut.IsExitWindowActive);
 161:     }
 162: 
 163:     [TestMethod]
 164:     public void LogExitBegin_OpensExitWindow()
 165:     {
 166:         _sut.LogExitBegin("m", "s", 1, 1);
 167:         Assert.IsTrue(_sut.IsExitWindowActive);
 168:     }
 169: 
 170:     [TestMethod]
 171:     public void LogExitBegin_RestartsSequenceCounter()
 172:     {
 173:         _sut.LogEncounterStart(3);
 174:         _sut.LogMissionInitialize("s");
 175: 
 176:         _sut.LogExitBegin("m", "s", 1, 1);
 177: 
 178:         _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("seq=1") && s.Contains("ExitBegin")));
 179:     }
 180: 
 181:     [TestMethod]
 182:     public void ExitPhases_BeforeExitBegin_WriteNothing()
 183:     {
 184:         _sut.LogExitTeardownBegin();
 185:         _sut.LogExitTeardownDone();
 186:         _sut.LogExitStateFinalizeBegin();
 187:         _sut.LogExitResourceClearBegin(false);
 188:         _sut.LogExitResourceClearDone();
 189:         _sut.LogExitStateFinalizeDone();
 190:         _sut.LogMapResumed(false);
 191:         _sut.LogFirstMapTick(false);
 192:         _logger.DidNotReceive().LogInfo(Arg.Any<string>());
 193:     }
 194: 
 195:     [TestMethod]
 196:     public void ExitPhases_AfterExitBegin_EmitInOrder()
 197:     {
 198:         _sut.LogExitBegin("m", "s", 1, 1);
 199:         _sut.LogExitTeardownBegin();
 200:         _sut.LogExitTeardownDone();
 201: 
 202:         Received.InOrder(() =>
 203:         {
 204:             _logger.LogInfo(Arg.Is<string>(s => s.Contains("ExitBegin")));
 205:             _logger.LogInfo(Arg.Is<string>(s => s.Contains("ExitTeardownBegin")));
 206:             _logger.LogInfo(Arg.Is<string>(s => s.Contains("ExitTeardownDone")));
 207:         });
 208:     }
 209: 
 210:     [TestMethod]
 211:     public void LogExitResourceClearBegin_InWindow_IncludesForceFlag()
 212:     {
 213:         _sut.LogExitBegin("m", "s", 1, 1);
 214:         _sut.LogExitResourceClearBegin(true);
 215:         _logger.Received().LogInfo(Arg.Is<string>(s =>
 216:             s.Contains("ExitResourceClearBegin") && s.Contains("forceClearGpu=True")));
 217:     }
 218: 
 219:     [TestMethod]
 220:     public void LogMapResumed_InWindow_IncludesGcStatsAndSavingFlag()
 221:     {
 222:         _sut.LogExitBegin("m", "s", 1, 1);
 223:         _sut.LogMapResumed(true);
 224:         _logger.Received().LogInfo(Arg.Is<string>(s =>
 225:             s.Contains("MapResumed") && s.Contains("isSaving=True") &&
 226:             s.Contains("gc=") && s.Contains("heapMB=")));
 227:     }
 228: 
 229:     [TestMethod]
 230:     public void LogFirstMapTick_ClosesExitWindow()
 231:     {
 232:         _sut.LogExitBegin("m", "s", 1, 1);
 233:         _sut.LogFirstMapTick(false);
 234: 
 235:         Assert.IsFalse(_sut.IsExitWindowActive);
 236:         _logger.ClearReceivedCalls();
 237:         _sut.LogMapResumed(false); // window closed — must be silent
 238:         _logger.DidNotReceive().LogInfo(Arg.Any<string>());
 239:     }
 240: 
 241:     [TestMethod]
 242:     public void ResetLifecycle_ClosesExitWindow()
 243:     {
 244:         _sut.LogExitBegin("m", "s", 1, 1);
 245:         _sut.ResetLifecycle();
 246:         Assert.IsFalse(_sut.IsExitWindowActive);
 247:     }
 248: 
 249:     // Window state transitions must be independent of the master toggle — a mid-window
 250:     // toggle-off must never latch the window open (deep-review data-flow finding, 2026-07-06).
 251: 
 252:     [TestMethod]
 253:     public void ResetLifecycle_WhenDisabledMidWindow_StillClosesExitWindow()
 254:     {
 255:         _sut.LogExitBegin("m", "s", 1, 1);
 256:         _settings.IsEnabled.Returns(false);
 257: 
 258:         _sut.ResetLifecycle();
 259: 
 260:         Assert.IsFalse(_sut.IsExitWindowActive);
 261:     }
 262: 
 263:     [TestMethod]
 264:     public void LogFirstMapTick_WhenDisabledMidWindow_ClosesExitWindowSilently()
 265:     {
 266:         _sut.LogExitBegin("m", "s", 1, 1);
 267:         _settings.IsEnabled.Returns(false);
 268:         _logger.ClearReceivedCalls();
 269: 
 270:         _sut.LogFirstMapTick(false);
 271: 
 272:         Assert.IsFalse(_sut.IsExitWindowActive);
 273:         _logger.DidNotReceive().LogInfo(Arg.Any<string>());
 274:     }
 275: 
 276:     [TestMethod]
 277:     public void LogMissionInitialize_ClosesStaleExitWindow()
 278:     {
 279:         // Chained mission without map activation: exit window from mission A is stale
 280:         // the moment mission B starts initializing.
 281:         _sut.LogExitBegin("m", "s", 1, 1);
 282: 
 283:         _sut.LogMissionInitialize("next_scene");
 284: 
 285:         Assert.IsFalse(_sut.IsExitWindowActive);
 286:     }
 287: 
 288:     // ---- ExitWindowOpenedUtcTicks (feeds ExitStallSampler, #331 round 2) ----
 289:     // The ticks latch must mirror the bool exactly: nonzero only while the window is open,
 290:     // cleared by every closer (incl. the unconditional-close paths).
 291: 
 292:     [TestMethod]
 293:     public void LogExitBegin_SetsExitWindowOpenedTicks()
 294:     {
 295:         _sut.LogExitBegin("m", "s", 1, 1);
 296:         Assert.AreNotEqual(0L, _sut.ExitWindowOpenedUtcTicks);
 297:     }
 298: 
 299:     [TestMethod]
 300:     public void LogFirstMapTick_ClearsExitWindowOpenedTicks()
 301:     {
 302:         _sut.LogExitBegin("m", "s", 1, 1);
 303:         _sut.LogFirstMapTick(false);
 304:         Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks);
 305:     }
 306: 
 307:     [TestMethod]
 308:     public void ResetLifecycle_WhenDisabledMidWindow_ClearsExitWindowOpenedTicks()
 309:     {
 310:         _sut.LogExitBegin("m", "s", 1, 1);
 311:         _settings.IsEnabled.Returns(false);
 312:         _sut.ResetLifecycle();
 313:         Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks);
 314:     }
 315: 
 316:     [TestMethod]
 317:     public void LogMissionInitialize_ClearsExitWindowOpenedTicks()
 318:     {
 319:         _sut.LogExitBegin("m", "s", 1, 1);
 320:         _sut.LogMissionInitialize("next_scene");
 321:         Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks);
 322:     }
 323: }

 succeeded in 713ms:
--- Main/Features/BattleLoadDiagnostics/Hooks/PlayerEncounter_Start_Patch.cs
   1: using HarmonyLib;
   2: using TaleWorlds.CampaignSystem.Encounters;
   3: using TaleWorlds.CampaignSystem.Party;
   4: 
   5: namespace TAOM.Features.BattleLoadDiagnostics.Hooks;
   6: 
   7: // Phase 1 — encounter initiated on the world map. Resets the lifecycle clock and writes
   8: // the first marker. Rich attacker/defender data isn't populated at Start() yet, so the
   9: // detailed vs-line is emitted at Phase 2 (MissionState.OpenNew) instead.
  10: [HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.Start))]
  11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
  12: public static class PlayerEncounter_Start_Patch
  13: {
  14:     private static IBattleLoadDiagnosticsService? _service;
  15: 
  16:     public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
  17: 
  18:     [HarmonyPostfix]
  19:     public static void Postfix()
  20:     {
  21:         var svc = _service;
  22:         if (svc == null) return;
  23: 
  24:         // ResetLifecycle runs even while the master toggle is off: its stale-exit-window
  25:         // close is an unconditional state transition (the clock/seq work inside self-gates
  26:         // on IsEnabled). A hook-level IsEnabled gate here bypassed the closer — a toggle-off
  27:         // mid-exit-window could leave the window latched (Codex review 2026-07-06, P2).
  28:         try { svc.ResetLifecycle(); }
  29:         catch { /* diagnostic only — never break encounter start */ }
  30: 
  31:         if (!svc.IsEnabled) return;
  32:         try
  33:         {
  34:             int size = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
  35:             svc.LogEncounterStart(size);
  36:         }
  37:         catch { /* diagnostic only — never break encounter start */ }
  38:     }
  39: }
--- Main/Features/BattleLoadDiagnostics/Hooks/Mission_Initialize_BattleLoad_Patch.cs
   1: using HarmonyLib;
   2: using TaleWorlds.MountAndBlade;
   3: 
   4: namespace TAOM.Features.BattleLoadDiagnostics.Hooks;
   5: 
   6: // Phase 4 — Mission.Initialize entered. Opens the loading window (which gates phase-5
   7: // per-agent logging and arms the stall watchdog) and writes the marker. A second prefix
   8: // on Mission.Initialize coexists fine with Patch16_AtmospherePersistence.
   9: [HarmonyPatch(typeof(Mission), "Initialize")]
  10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
  11: public static class Mission_Initialize_BattleLoad_Patch
  12: {
  13:     private static IBattleLoadDiagnosticsService? _service;
  14:     private static IBattleLoadStallMarker? _stallMarker;
  15: 
  16:     public static void Initialize(IBattleLoadDiagnosticsService service, IBattleLoadStallMarker stallMarker)
  17:     {
  18:         _service = service;
  19:         _stallMarker = stallMarker;
  20:     }
  21: 
  22:     [HarmonyPrefix]
  23:     public static void Prefix(Mission __instance)
  24:     {
  25:         var svc = _service;
  26:         if (svc == null) return;
  27: 
  28:         var scene = __instance?.SceneName ?? "<null>";
  29:         if (svc.IsEnabled)
  30:         {
  31:             // Open the window only when enabled — the watchdog and phase-5 are both gated
  32:             // on it, and phase 6 closes it on the first playable tick.
  33:             BattleLoadLoadingWindow.Enter();
  34:             // Write the inflight marker: if this load hangs and the player force-quits, the
  35:             // surviving marker triggers a "send your log" notice on the next main menu.
  36:             try { _stallMarker?.MarkInflight(scene); } catch { /* diagnostic only */ }
  37:         }
  38: 
  39:         // Called even while the master toggle is off: LogMissionInitialize's stale-exit-window
  40:         // close is an unconditional state transition (the phase logging inside self-gates on
  41:         // IsEnabled). A hook-level IsEnabled gate here bypassed the closer — a toggle-off
  42:         // mid-exit-window could leave the window latched (Codex review 2026-07-06, P2).
  43:         try { svc.LogMissionInitialize(scene); }
  44:         catch { /* diagnostic only */ }
  45:     }
  46: }
--- Main/Features/Arena/Hooks/Patch60_TournamentExitMovieRelease.cs
   1: using System;
   2: using System.Reflection;
   3: using HarmonyLib;
   4: using SandBox.GauntletUI.Missions;
   5: using TaleWorlds.Engine.GauntletUI;
   6: using TAOM.Core.Logging;
   7: 
   8: namespace TAOM.Features.Arena.Hooks;
   9: 
  10: // Issue #331 — tournament exits hang the loading screen ~30s-2min (measured 108s) while every
  11: // other mission exit is instant.
  12: //
  13: // Root cause (engine defect, verified against installed 1.4.6): MissionGauntletTournamentView.
  14: // OnMissionScreenFinalize nulls _gauntletMovie/_gauntletLayer WITHOUT ReleaseMovie/RemoveLayer
  15: // (the arena practice view releases both properly at the same hook). The leaked 'Tournament'
  16: // movie — the only mission UI holding live item-tableau/character-tableau widgets (prize item,
  17: // per-round weapon icons, winner panel), with a prize render request typically still in flight
  18: // at exit — is then torn down inside ScreenBase.HandleFinalize's layer loop, after the mission
  19: // frame pump is dead under the exit loading screen, where that teardown stalls ~108s instead of
  20: // the milliseconds it costs while the mission is alive.
  21: //
  22: // Fix: replicate the practice view's release sequence at the identical lifecycle point
  23: // (IMissionListener.OnEndMission, mission renderer still alive). The original body must run
  24: // first — it drops focus and finalizes the VM, and it nulls the private fields — so a Prefix
  25: // captures the layer/movie into __state and a Postfix releases them. Worst case on any failure
  26: // is today's vanilla leak (fail-safe). Reflection (not FieldRefAccess) keeps the drift-guard
  27: // test surface free of module types, per the Patch58 MethodInfo precedent; this path runs once
  28: // per tournament exit, so per-call GetValue cost is irrelevant.
  29: [HarmonyPatch(typeof(MissionGauntletTournamentView), nameof(MissionGauntletTournamentView.OnMissionScreenFinalize))]
  30: [HarmonyPatchCategory("Patch60_TournamentExitMovieRelease")]
  31: public static class Patch60_TournamentExitMovieRelease
  32: {
  33:     // internal (not private) so Patch60TournamentExitMovieReleaseTests can drift-guard the
  34:     // bindings against the installed engine — a field rename silently disables the fix.
  35:     internal static readonly FieldInfo? LayerField =
  36:         AccessTools.Field(typeof(MissionGauntletTournamentView), "_gauntletLayer");
  37:     internal static readonly FieldInfo? MovieField =
  38:         AccessTools.Field(typeof(MissionGauntletTournamentView), "_gauntletMovie");
  39: 
  40:     private static IModLogger? _logger;
  41: 
  42:     public static void Initialize(IModLogger logger) => _logger = logger;
  43: 
  44:     [HarmonyPrefix]
  45:     public static void Prefix(
  46:         MissionGauntletTournamentView __instance,
  47:         out ValueTuple<GauntletLayer?, GauntletMovieIdentifier?> __state)
  48:     {
  49:         __state = default;
  50:         try
  51:         {
  52:             if (LayerField == null || MovieField == null) return;
  53:             __state = new ValueTuple<GauntletLayer?, GauntletMovieIdentifier?>(
  54:                 LayerField.GetValue(__instance) as GauntletLayer,
  55:                 MovieField.GetValue(__instance) as GauntletMovieIdentifier);
  56:         }
  57:         catch (Exception ex)
  58:         {
  59:             _logger?.LogWarning($"[Arena] Patch60 capture failed — tournament UI will leak (vanilla behavior): {ex.GetType().Name}: {ex.Message}");
  60:         }
  61:     }
  62: 
  63:     [HarmonyPostfix]
  64:     public static void Postfix(
  65:         MissionGauntletTournamentView __instance,
  66:         ValueTuple<GauntletLayer?, GauntletMovieIdentifier?> __state)
  67:     {
  68:         var (layer, movie) = __state;
  69:         if (layer == null || movie == null) return;
  70:         try
  71:         {
  72:             // Practice-view parity: the original body already finalized the VM and dropped
  73:             // focus; ReleaseMovie is idempotence-guarded (Contains + IsReleased) and RemoveLayer
  74:             // finalizes the layer now, while the mission renderer still services tableau work.
  75:             // Timed per call (#331 round 2): the ~107s exit stall moved WITH this release, so
  76:             // these stamps split the cost between ReleaseMovie and RemoveLayer for the RCA.
  77:             var sw = System.Diagnostics.Stopwatch.StartNew();
  78:             layer.ReleaseMovie(movie);
  79:             long releaseMs = sw.ElapsedMilliseconds;
  80: 
  81:             var screen = __instance.MissionScreen;
  82:             if (screen != null && screen.HasLayer(layer))
  83:                 screen.RemoveLayer(layer);
  84: 
  85:             _logger?.LogInfo($"[Arena] Patch60 tournament UI released: ReleaseMovie={releaseMs}ms RemoveLayer={sw.ElapsedMilliseconds - releaseMs}ms");
  86:         }
  87:         catch (Exception ex)
  88:         {
  89:             _logger?.LogWarning($"[Arena] Patch60 release failed — tournament UI leaked this exit (vanilla behavior): {ex.GetType().Name}: {ex.Message}");
  90:         }
  91:     }
  92: }
--- Dependencies/Foundation/PatchShield.cs
   1: using System;
   2: using System.Collections.Generic;
   3: using System.IO;
   4: using System.Linq;
   5: using System.Reflection;
   6: using System.Threading;
   7: using HarmonyLib;
   8: 
   9: namespace TAOM.Dependencies.Foundation;
  10: 
  11: /// <summary>
  12: /// Wraps every Harmony-patched method in the AppDomain with a Finalizer that catches
  13: /// the trinity of "mod compiled against an old Bannerlord version" exceptions:
  14: /// <c>MissingMethodException</c>, <c>MissingFieldException</c>, <c>TypeLoadException</c>.
  15: /// On catch, logs the failure, increments per-category counters, and removes the
  16: /// offending owner's prefixes/postfixes/transpilers from this method via
  17: /// <see cref="Harmony.Unpatch(MethodBase, HarmonyPatchType, string)"/>. The patched
  18: /// method continues running uncaught from the user's perspective — the game keeps going.
  19: ///
  20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
  21: /// This is the single highest-leverage component in BetaDeps's "every BUTR-dependent mod
  22: /// works even when broken" promise.
  23: ///
  24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
  25: /// TAOM.Dependencies module directory to skip install. Useful for diagnosing whether
  26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
  27: ///
  28: /// Install timing: should run AFTER all other mods have applied their Harmony patches
  29: /// — i.e., late in the load lifecycle, NOT in SubModule ctors. See SubModule.cs
  30: /// OnSubModuleLoad or OnBeforeInitialModuleScreenSetAsRoot.
  31: /// </summary>
  32: public static class PatchShield
  33: {
  34:     private const string Tag = "PatchShield";
  35:     private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
  36:     private const string DisableFlagName = "patchshield-disabled.flag";
  37: 
  38:     private static readonly HashSet<MethodBase> _shielded = new();
  39:     private static readonly HashSet<string> _unpatched = new();
  40:     private static readonly object _lock = new();
  41: 
  42:     // Codex review 2026-05-27 S1 (HIGH): expanded from "TAOM" prefix only to full
  43:     // infrastructure-owner allowlist. Vendored BUTR/MCM Harmony IDs ("Bannerlord.ButterLib.SaveSystem",
  44:     // "MCM.UI.Adapter.MCMv5", etc.) do NOT start with "TAOM" — the prior filter would have
  45:     // unpatched the entire BUTR stack on the first MissingMethodException, breaking
  46:     // every dependent mod. This list mirrors the vendored DLLs in
  47:     // Dependencies/_Module/bin/Win64_Shipping_Client/ + Lib.Harmony's own runtime types.
  48:     private static readonly string[] ProtectedOwnerPrefixes =
  49:     {
  50:         "TAOM",
  51:         "Bannerlord.ButterLib",
  52:         "butterlib.",
  53:         "Bannerlord.UIExtenderEx",
  54:         "Bannerlord.MBOptionScreen",
  55:         "Bannerlord.ModuleLoader",
  56:         "Bannerlord.MCM",
  57:         "bannerlord.mcm.",
  58:         "MCM",
  59:         "MCMv5",
  60:         "MCM.UI.Adapter",
  61:         "BUTR.",
  62:         "HarmonyLib.",
  63:         "0Harmony",
  64:     };
  65: 
  66:     private static bool IsProtectedOwner(string owner)
  67:     {
  68:         if (string.IsNullOrEmpty(owner)) return false;
  69:         foreach (var prefix in ProtectedOwnerPrefixes)
  70:         {
  71:             if (owner.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
  72:                 return true;
  73:         }
  74:         return false;
  75:     }
  76: 
  77:     // Issue #331 round 2 (2026-07-09, measured): NEVER shield the Gauntlet/2D UI layer.
  78:     // A shield finalizer binds __originalMethod, so Harmony's generated wrapper pays a
  79:     // MethodBase.GetMethodFromHandle + try/catch on EVERY CALL (~50µs). The Gauntlet
  80:     // prefab system contains per-widget-recursion methods that UIExtenderEx patches
  81:     // (WidgetFactory.IsCustomType prefix, WidgetTemplate.OnRelease blank-transpiler);
  82:     // a tournament's accumulated template tree calls them ~2 MILLION times at release,
  83:     // so the shield tax amplified a milliseconds-scale teardown into a measured 104-109s
  84:     // frozen exit (+8,276 gen0 GCs, invariant across sessions — stack-sampled proof in
  85:     // docs/reviews/rca-tournament-exit-hang-2026-07-06.md round 2). Shield value there
  86:     // is nil anyway: the only patcher of that layer is BUTR's own UIExtenderEx.
  87:     private static readonly string[] ExcludedTargetNamespacePrefixes =
  88:     {
  89:         "TaleWorlds.GauntletUI",
  90:         "TaleWorlds.TwoDimension",
  91:         // Round-2 compat review (2026-07-10): TAOM's own Patch38 target
  92:         // (SettlementNameplateWidget.DetermineTargetAlphaValue, ~3000 calls/sec on the
  93:         // campaign map) lives here and was silently paying the shield tax every frame.
  94:         // Same rationale as above: hot widget/view layer, shield value nil.
  95:         "TaleWorlds.MountAndBlade.GauntletUI",
  96:     };
  97: 
  98:     private static bool IsExcludedTarget(MethodBase method)
  99:     {
 100:         try
 101:         {
 102:             var ns = method.DeclaringType?.Namespace ?? string.Empty;
 103:             foreach (var prefix in ExcludedTargetNamespacePrefixes)
 104:             {
 105:                 if (ns.StartsWith(prefix, StringComparison.Ordinal))
 106:                     return true;
 107:             }
 108:         }
 109:         catch { /* fail open — an unreadable type just gets shielded as before */ }
 110:         return false;
 111:     }
 112: 
 113:     private static readonly Dictionary<string, int> _ownerCounts =
 114:         new(StringComparer.OrdinalIgnoreCase);
 115:     private static readonly object _ownerLock = new();
 116: 
 117:     private static long _swallowedMissingMethod;
 118:     private static long _swallowedMissingField;
 119:     private static long _swallowedTypeLoad;
 120:     private static long _swallowedOther;
 121: 
 122:     public static int ShieldedCount { get { lock (_lock) return _shielded.Count; } }
 123:     public static int UnpatchedCount { get { lock (_lock) return _unpatched.Count; } }
 124:     public static long SwallowedMissingMethod => Interlocked.Read(ref _swallowedMissingMethod);
 125:     public static long SwallowedMissingField => Interlocked.Read(ref _swallowedMissingField);
 126:     public static long SwallowedTypeLoad => Interlocked.Read(ref _swallowedTypeLoad);
 127:     public static long SwallowedOther => Interlocked.Read(ref _swallowedOther);
 128:     public static long SwallowedTotal => SwallowedMissingMethod + SwallowedMissingField + SwallowedTypeLoad + SwallowedOther;
 129: 
 130:     public static bool IsDisabled()
 131:     {
 132:         try
 133:         {
 134:             var dir = RuntimeLog.ModuleDir;
 135:             if (string.IsNullOrEmpty(dir)) return false;
 136:             return File.Exists(Path.Combine(dir, DisableFlagName));
 137:         }
 138:         catch { return false; }
 139:     }
 140: 
 141:     /// <summary>
 142:     /// Installs the shield: iterates all currently-patched methods, attaches a
 143:     /// Finalizer to each. Idempotent — methods already shielded are skipped.
 144:     /// Safe to call multiple times to "shield-pass" new patches added by mods
 145:     /// that load after our first install (call from a late lifecycle hook).
 146:     /// </summary>
 147:     public static void Install()
 148:     {
 149:         if (IsDisabled())
 150:         {
 151:             DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
 152:             return;
 153:         }
 154: 
 155:         try
 156:         {
 157:             var harmony = new Harmony(HarmonyId);
 158:             var voidFinalizer = typeof(PatchShield).GetMethod(
 159:                 nameof(ShieldFinalizerVoid),
 160:                 BindingFlags.Static | BindingFlags.NonPublic);
 161:             var resultFinalizer = typeof(PatchShield).GetMethod(
 162:                 nameof(ShieldFinalizerWithResult),
 163:                 BindingFlags.Static | BindingFlags.NonPublic);
 164:             if (voidFinalizer == null || resultFinalizer == null)
 165:             {
 166:                 DiagLog.Log(Tag, "could not resolve shield finalizer methods; aborting install");
 167:                 return;
 168:             }
 169: 
 170:             List<MethodBase> patched;
 171:             try
 172:             {
 173:                 patched = Harmony.GetAllPatchedMethods().ToList();
 174:             }
 175:             catch (Exception ex)
 176:             {
 177:                 DiagLog.LogCaught(Tag, "GetAllPatchedMethods", ex);
 178:                 return;
 179:             }
 180: 
 181:             int added = 0, skipped = 0, alreadyShielded = 0;
 182:             lock (_lock)
 183:             {
 184:                 foreach (var method in patched)
 185:                 {
 186:                     if (method == null) { skipped++; continue; }
 187:                     if (_shielded.Contains(method)) { alreadyShielded++; continue; }
 188: 
 189:                     // Don't shield our own methods.
 190:                     try
 191:                     {
 192:                         var declAsm = method.DeclaringType?.Assembly.GetName().Name ?? string.Empty;
 193:                         if (declAsm.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
 194:                         {
 195:                             _shielded.Add(method);
 196:                             skipped++;
 197:                             continue;
 198:                         }
 199:                     }
 200:                     catch { }
 201: 
 202:                     // Never shield hot UI-layer targets — a per-call __originalMethod
 203:                     // finalizer on the Gauntlet prefab system froze tournament exits for
 204:                     // ~107s (#331 round 2). See ExcludedTargetNamespacePrefixes.
 205:                     if (IsExcludedTarget(method))
 206:                     {
 207:                         _shielded.Add(method);
 208:                         skipped++;
 209:                         continue;
 210:                     }
 211: 
 212:                     try
 213:                     {
 214:                         bool isVoid = true;
 215:                         if (method is MethodInfo mi) isVoid = mi.ReturnType == typeof(void);
 216:                         var finalizer = isVoid ? voidFinalizer : resultFinalizer;
 217:                         harmony.Patch(method, prefix: null, postfix: null, transpiler: null,
 218:                             finalizer: new HarmonyMethod(finalizer));
 219:                         _shielded.Add(method);
 220:                         added++;
 221:                     }
 222:                     catch (Exception ex)
 223:                     {
 224:                         skipped++;
 225:                         DiagLog.LogCaught(Tag, $"shielding {method.DeclaringType?.FullName}.{method.Name}", ex);
 226:                     }
 227:                 }
 228:             }
 229: 
 230:             if (added > 0 || alreadyShielded == 0)
 231:             {
 232:                 DiagLog.Log(Tag, $"shield pass: +{added} new, {alreadyShielded} already-shielded, {skipped} skipped (total: {_shielded.Count})");
 233:             }
 234:         }
 235:         catch (Exception ex)
 236:         {
 237:             DiagLog.LogCaught(Tag, "Install", ex);
 238:         }
 239:     }
 240: 
 241:     /// <summary>
 242:     /// Finalizer for void-return methods. Catches the swallow-trinity and returns
 243:     /// silently to suppress the exception; non-matching exceptions are re-thrown by
 244:     /// returning the original exception (Harmony Finalizer convention).
 245:     /// </summary>
 246:     private static Exception? ShieldFinalizerVoid(MethodBase __originalMethod, Exception __exception)
 247:     {
 248:         return ShouldSwallow(__originalMethod, __exception, out var unwrapped) ? null : unwrapped;
 249:     }
 250: 
 251:     /// <summary>
 252:     /// Finalizer for return-value methods. Same swallow behavior; the patched method
 253:     /// returns its zero/default value when we swallow because we don't have access
 254:     /// to <c>__result</c> in a Finalizer (Harmony quirk). Acceptable trade-off:
 255:     /// the caller gets a "stub" return value, which is far better than a crash.
 256:     /// </summary>
 257:     private static Exception? ShieldFinalizerWithResult(MethodBase __originalMethod, Exception __exception)
 258:     {
 259:         return ShouldSwallow(__originalMethod, __exception, out var unwrapped) ? null : unwrapped;
 260:     }
 261: 
 262:     private static bool ShouldSwallow(MethodBase originalMethod, Exception exception, out Exception unwrapped)
 263:     {
 264:         unwrapped = exception;
 265:         if (exception == null) return false;
 266: 
 267:         // Unwrap TargetInvocationException to get at the real reason.
 268:         var ex = exception;
 269:         while (ex is TargetInvocationException && ex.InnerException != null)
 270:             ex = ex.InnerException;
 271:         unwrapped = ex;
 272: 
 273:         if (ex is MissingMethodException || ex is MissingFieldException || ex is TypeLoadException)
 274:         {
 275:             if (ex is MissingMethodException) Interlocked.Increment(ref _swallowedMissingMethod);
 276:             else if (ex is MissingFieldException) Interlocked.Increment(ref _swallowedMissingField);
 277:             else Interlocked.Increment(ref _swallowedTypeLoad);
 278: 
 279:             try
 280:             {
 281:                 var owner = originalMethod?.DeclaringType?.FullName ?? "?";
 282:                 var name = originalMethod?.Name ?? "?";
 283:                 DiagLog.Log(Tag, $"swallowed {ex.GetType().Name} from a patch on {owner}.{name}: {ex.Message}");
 284:             }
 285:             catch { }
 286: 
 287:             TryUnpatchOffendingPatches(originalMethod, ex);
 288:             return true;
 289:         }
 290: 
 291:         // Codex A2 LOW fix 2026-05-27: do NOT increment _swallowedOther here — this
 292:         // path RETHROWS the exception. The counter previously misled WriteSessionSummary
 293:         // into reporting rethrown exceptions as swallowed.
 294:         return false;
 295:     }
 296: 
 297:     private static void TryUnpatchOffendingPatches(MethodBase originalMethod, Exception ex)
 298:     {
 299:         if (originalMethod == null) return;
 300: 
 301:         // Codex A3 LOW fix 2026-05-27: overload-safe dedupe key. Was
 302:         // <DeclaringType>::<methodName> — overloaded methods shared a key, so the
 303:         // second overload's failure would skip cleanup. Now uses
 304:         // <Module.ModuleVersionId>:<MetadataToken> which is unique per method handle.
 305:         string targetKey;
 306:         try
 307:         {
 308:             targetKey = $"{originalMethod.Module.ModuleVersionId}:{originalMethod.MetadataToken}";
 309:         }
 310:         catch
 311:         {
 312:             // Fallback if Module/MetadataToken unavailable for this method handle.
 313:             try { targetKey = originalMethod.ToString(); }
 314:             catch { return; }
 315:         }
 316: 
 317:         lock (_lock)
 318:         {
 319:             if (_unpatched.Contains(targetKey)) return;  // already cleaned
 320:             _unpatched.Add(targetKey);
 321:         }
 322: 
 323:         try
 324:         {
 325:             var patches = Harmony.GetPatchInfo(originalMethod);
 326:             if (patches == null) return;
 327: 
 328:             var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 329:             foreach (var p in patches.Prefixes) if (p != null) owners.Add(p.owner ?? string.Empty);
 330:             foreach (var p in patches.Postfixes) if (p != null) owners.Add(p.owner ?? string.Empty);
 331:             foreach (var p in patches.Transpilers) if (p != null) owners.Add(p.owner ?? string.Empty);
 332:             foreach (var p in patches.Finalizers) if (p != null) owners.Add(p.owner ?? string.Empty);
 333: 
 334:             var harmony = new Harmony(HarmonyId);
 335:             foreach (var owner in owners)
 336:             {
 337:                 if (string.IsNullOrEmpty(owner) || owner == HarmonyId) continue;
 338: 
 339:                 // Refuse to unpatch protected infrastructure owners (Codex S1 HIGH fix
 340:                 // 2026-05-27). Filter now covers TAOM + vendored BUTR/MCM/Harmony.
 341:                 if (IsProtectedOwner(owner))
 342:                 {
 343:                     DiagLog.Log(Tag, $"refusing to unpatch protected owner '{owner}' on {targetKey}");
 344:                     continue;
 345:                 }
 346: 
 347:                 try
 348:                 {
 349:                     harmony.Unpatch(originalMethod, HarmonyPatchType.Prefix, owner);
 350:                     harmony.Unpatch(originalMethod, HarmonyPatchType.Postfix, owner);
 351:                     harmony.Unpatch(originalMethod, HarmonyPatchType.Transpiler, owner);
 352:                     DiagLog.Log(Tag, $"unpatched owner '{owner}' on {targetKey}");
 353: 
 354:                     lock (_ownerLock)
 355:                     {
 356:                         _ownerCounts.TryGetValue(owner, out var count);
 357:                         _ownerCounts[owner] = count + 1;
 358:                     }
 359:                 }
 360:                 catch (Exception unpatchEx)
 361:                 {
 362:                     DiagLog.LogCaught(Tag, $"Unpatch owner='{owner}' on {targetKey}", unpatchEx);
 363:                 }
 364:             }
 365:         }
 366:         catch (Exception ex2)
 367:         {
 368:             DiagLog.LogCaught(Tag, $"TryUnpatchOffendingPatches({targetKey})", ex2);
 369:         }
 370:     }
 371: 
 372:     /// <summary>
 373:     /// Writes a one-line summary of swallow stats. Wire to AppDomain.ProcessExit.
 374:     /// </summary>
 375:     public static void WriteSessionSummary()
 376:     {
 377:         try
 378:         {
 379:             string topOwner = "(none)";
 380:             lock (_ownerLock)
 381:             {
 382:                 if (_ownerCounts.Count > 0)
 383:                 {
 384:                     var top = _ownerCounts.OrderByDescending(k => k.Value).First();
 385:                     topOwner = $"{top.Key} ({top.Value})";
 386:                 }
 387:             }
 388:             DiagLog.Log(Tag,
 389:                 $"SESSION SUMMARY: shielded {ShieldedCount} method(s), unpatched {UnpatchedCount} target(s), " +
 390:                 $"swallowed {SwallowedTotal} exception(s) " +
 391:                 $"(MissingMethod {SwallowedMissingMethod}, MissingField {SwallowedMissingField}, " +
 392:                 $"TypeLoad {SwallowedTypeLoad}, other {SwallowedOther}). " +
 393:                 $"Top unpatched owner: {topOwner}.");
 394:         }
 395:         catch (Exception ex)
 396:         {
 397:             DiagLog.LogCaught(Tag, "WriteSessionSummary", ex);
 398:         }
 399:     }
 400: }
--- Main/SubModule.cs
   1: using Bannerlord.UIExtenderEx;
   2: using HarmonyLib;
   3: using TaleWorlds.CampaignSystem;
   4: using TaleWorlds.Core;
   5: using TaleWorlds.Engine;
   6: using TaleWorlds.Library;
   7: using TaleWorlds.Localization;
   8: using TaleWorlds.MountAndBlade;
   9: using TaleWorlds.MountAndBlade.ComponentInterfaces;
  10: using TAOM.Features;
  11: using TAOM.Features.BannerInjection;
  12: using TAOM.Features.HeroRace;
  13: using TAOM.Core.Infrastructure;
  14: using TAOM.Core.Logging;
  15: using TAOM.Features.CharacterCreation;
  16: using TAOM.Features.FactionMap;
  17: using TAOM.Features.InitialChildGeneration;
  18: using TAOM.Adapters;
  19: using TAOM.Features.Diplomacy;
  20: using TAOM.Features.Diplomacy.Hooks;
  21: using TAOM.Features.Diplomacy.Models;
  22: using TAOM.Features.Execution;
  23: using TAOM.Features.Execution.Hooks;
  24: using TAOM.Features.Execution.Models;
  25: using TAOM.Features.RaceAge;
  26: using TAOM.Features.RaceAge.Models;
  27: using TAOM.Features.StartupResources;
  28: using TAOM.Features.NamedCompanions;
  29: using TAOM.Features.TroopProgression;
  30: using TAOM.Features.TroopWeight;
  31: using TAOM.Features.TroopWeight.Diagnostics;
  32: using TAOM.Features.TroopWeight.Hooks;
  33: using TAOM.Features.AtmospherePersistence.Hooks;
  34: using TAOM.Features.TroopProgression.Models;
  35: using TAOM.Features.AdvancedCombat;
  36: using TAOM.Features.CulturalFeats.Models;
  37: using TAOM.Features.NavalTravel;
  38: using TAOM.Features.NavalTravel.Models;
  39: using TAOM.Features.NazgulFamily;
  40: using TAOM.Features.NazgulFamily.Models;
  41: using TAOM.Features.CustomBattles;
  42: using TAOM.Features.CustomBattles.Hooks;
  43: using TAOM.Features.Warg;
  44: using TAOM.Features.Spider;
  45: using TAOM.Features.BattleBalance;
  46: using TAOM.Features.BattleBalance.Models;
  47: using TAOM.Features.Arena.Models;
  48: using TAOM.Features.Encyclopedia;
  49: using TAOM.Features.Encyclopedia.Models;
  50: using TAOM.Features.MainMenuCustomizer;
  51: using TAOM.Features.NativeSkinFixes;
  52: using TAOM.Features.ShaderPrecompilation;
  53: using TAOM.Features.Siege;
  54: using TAOM.Features.Siege.Models;
  55: using TAOM.Features.ArmyTargeting;
  56: using TAOM.Features.ArmyTargeting.Models;
  57: using TAOM.Features.TimeAcceleration;
  58: using TAOM.Features.BannerColorPersistence;
  59: using TAOM.Features.BannerColorPersistence.Hooks;
  60: using TAOM.Features.LocalizationOverride;
  61: using TAOM.Features.LocalizationOverride.Hooks;
  62: using TAOM.Features.SpecialResources;
  63: using TAOM.Features.SpecialResources.Hooks;
  64: using TAOM.Features.CareerSystem;
  65: using TAOM.Features.CareerSystem.Models;
  66: using TAOM.Features.CombatMechanics.Models;
  67: using TAOM.Features.SettlementGuards;
  68: using TAOM.Features.SettlementGuards.Hooks;
  69: using TAOM.Features.RevoltTuning;
  70: using TAOM.Features.SettlementEconomy;
  71: using TAOM.Features.SettlementEconomy.Models;
  72: using TAOM.Features.SettlementFood;
  73: using TAOM.Features.SettlementFood.Models;
  74: using TAOM.Features.BanditManagement;
  75: using TAOM.Features.BanditManagement.Models;
  76: using TAOM.Features.CastleRecruitment;
  77: using TAOM.Features.CastleRecruitment.Hooks;
  78: using TAOM.Features.SiegeDismount.Hooks;
  79: using TAOM.Features.MixedFormations.Hooks;
  80: using TAOM.Features.SmartCavalryAI.Hooks;
  81: using TAOM.Features.FiefManagement;
  82: using TAOM.Features.FiefManagement.Hooks;
  83: using TAOM.Features.SettlementNameplateFade;
  84: using TAOM.Features.SettlementNameplateFade.Hooks;
  85: using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
  86: using BehaviorTreeWrapper;
  87: using TaleWorlds.CampaignSystem.CampaignBehaviors;
  88: 
  89: namespace TAOM;
  90: 
  91: public class SubModule : MBSubModuleBase
  92: {
  93:     private Harmony _harmony;
  94:     private UIExtender? _uiExtender;
  95:     private ITimeAccelerationService? _timeAccelerationService;
  96:     private static float _shaderTickAccumulator;
  97:     private static ShaderPrecompileRunner _shaderRunner;
  98:     private static bool _missionTimePatchesApplied;
  99:     private static bool _gameInitPatchesApplied;
 100:     private static bool _basicTableauGuardApplied;
 101: 
 102:     protected override void OnSubModuleLoad()
 103:     {
 104:         base.OnSubModuleLoad();
 105: 
 106:         IoC.Configure();
 107: 
 108:         // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY
 109:         // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
 110:         // (UIExtender init, time-acceleration resolve, downstream PatchCategory calls).
 111:         // Previous order left lines 88-107 uncatchable. The only unavoidable blind spot
 112:         // is the IoC.Configure() call itself — if THAT throws, the entire feature is
 113:         // unreachable. Split CrashReport bootstrap doesn't fix this without re-implementing
 114:         // a manual DI container; accept and document the residual.
 115:         _harmony = new Harmony("com.taom.mod");
 116:         if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
 117:         {
 118:             try
 119:             {
 120:                 _harmony.PatchCategory("Patch37_CrashReport");
 121:                 IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
 122:                 if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
 123:                 {
 124:                     IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
 125:                 }
 126:             }
 127:             catch (System.Exception ex)
 128:             {
 129:                 IoC.Resolve<IModLogger>().LogError($"[CrashReport] init failed: {ex.GetType().Name}: {ex.Message}");
 130:             }
 131:         }
 132: 
 133:         _uiExtender = UIExtender.Create("TAOM");
 134:         _uiExtender.Register(typeof(SubModule).Assembly);
 135:         _uiExtender.Enable();
 136: 
 137:         // Patch41_McmLayoutFix — flip MCM's embedded options-screen prefabs from VerticalBottomToTop
 138:         // to VerticalTopToBottom (v1.4.0 layout regression). MCM's prefabs are embedded in
 139:         // Bannerlord.MBOptionScreen and load via WidgetFactoryManager.CreateAndRegister, which bypasses
 140:         // UIExtenderEx's [PrefabExtension] hook — so this is a Harmony Postfix, not a PrefabExtension.
 141:         // MUST be applied here in OnSubModuleLoad: MCM's ResourceInjector.Inject() runs at
 142:         // OnBeforeInitialModuleScreenSetAsRoot (after every module's OnSubModuleLoad), so the Postfix
 143:         // must already be attached when MCM calls CreateAndRegister.
 144:         _harmony.PatchCategory("Patch41_McmLayoutFix");
 145: 
 146:         _timeAccelerationService = IoC.Resolve<ITimeAccelerationService>();
 147: 
 148:         // Must be first — intercepts GetLocalizedText before any game texts are resolved.
 149:         // Loads English string overrides from taom_module_strings.xml (removes hardcoded "The" articles).
 150:         _harmony.PatchCategory("Patch25_LocalizationOverride");
 151:         var pathService0 = IoC.Resolve<IPathService>();
 152:         var logger0 = IoC.Resolve<IModLogger>();
 153:         var xmlPath = System.IO.Path.Combine(pathService0.ModuleDataPath, "taom_module_strings.xml");
 154:         try
 155:         {
 156:             var overrides = LocalizationOverrideLoader.ParseOverridesFromFile(xmlPath);
 157:             foreach (var kvp in overrides)
 158:                 MBTextManager_GetLocalizedText_Patch.RegisterOverride(kvp.Key, kvp.Value);
 159:             logger0.LogInfo($"[LocalizationOverride] Registered {overrides.Count} English string overrides");
 160:         }
 161:         catch (System.Exception ex)
 162:         {
 163:             logger0.LogError($"[LocalizationOverride] Failed to load overrides: {ex.Message}");
 164:         }
 165: 
 166:         _harmony.PatchCategory("Patch18_CulturalFeats");
 167:         _harmony.PatchCategory("Patch19_CustomBattles");
 168: 
 169:         // Patch58_SkipCampaignIntro — Prefix on SandBoxGameManager.OnLoadFinished that skips the vanilla
 170:         // SandBox campaign intro video on a NEW game (mirrors the engine's own IsDevelopmentMode no-video
 171:         // bypass), dropping straight into character creation; save-loads run vanilla untouched. Applied here
 172:         // in OnSubModuleLoad (process-static one-shot) — NOT the late OnGameInitializationFinished batch —
 173:         // because the target fires during the new-game load sequence (after campaign init but before
 174:         // character creation), so the patch must already be attached before any new game can start. Any
 175:         // binding failure inside the prefix falls back to the vanilla video. See docs/features/skip-campaign-intro.md.
 176:         Features.SkipCampaignIntro.Hooks.Patch58_SkipCampaignIntro.Initialize(IoC.Resolve<IModLogger>());
 177:         _harmony.PatchCategory("Patch58_SkipCampaignIntro");
 178: 
 179:         // Patch61_SaveLoadDiagnostics — always-on [SaveLoad] lifecycle logging for the "corrupted
 180:         // save" investigation. The engine swallows the real exception behind the generic
 181:         // "A problem occured while trying to load the saved game." dialog (LoadContext.Load catches
 182:         // and prints only ex.Message), so interior Finalizers stamp the actual failing type/SaveId
 183:         // to taom_debug, and save-side hooks catch bad WRITES (the #292 class) at write time.
 184:         // All Finalizers are VOID (true rethrow, stack preserved, structurally can't swallow) at
 185:         // Priority.First — SaveShield (TAOM.Dependencies) finalizes 4 overlapping methods at
 186:         // default priority and SWALLOWS; ours must observe the exception first (review 2026-07-07
 187:         // HIGH). Applied here in OnSubModuleLoad like Patch58: loads are triggered from the main
 188:         // menu, before any game init — the late batch would miss the first load. Each
 189:         // reflection-target hook (internal engine types) gets its OWN category: Harmony aborts a
 190:         // category on the first failing class, so per-hook categories keep one drifted internal
 191:         // type from killing its siblings. Diagnostics must never break startup: every category in
 192:         // its own try/catch, fail = vanilla.
 193:         try
 194:         {
 195:             var saveLoadDiagnostics = IoC.Resolve<Features.SaveLoadDiagnostics.ISaveLoadDiagnosticsService>();
 196:             var saveLoadLogger = IoC.Resolve<IModLogger>();
 197:             Features.SaveLoadDiagnostics.Hooks.SandBoxSaveHelper_TryLoadSave_Patch.Initialize(saveLoadDiagnostics);
 198:             Features.SaveLoadDiagnostics.Hooks.MBSaveLoad_LoadSaveGameData_Patch.Initialize(saveLoadDiagnostics);
 199:             Features.SaveLoadDiagnostics.Hooks.SaveManager_Load_Patch.Initialize(saveLoadDiagnostics);
 200:             Features.SaveLoadDiagnostics.Hooks.LoadContext_CreateLoadData_Patch.Initialize(saveLoadDiagnostics);
 201:             Features.SaveLoadDiagnostics.Hooks.ObjectHeaderLoadData_CreateObject_Patch.Initialize(saveLoadDiagnostics);
 202:             Features.SaveLoadDiagnostics.Hooks.ContainerHeaderLoadData_GetObjectTypeDefinition_Patch.Initialize(saveLoadDiagnostics);
 203:             Features.SaveLoadDiagnostics.Hooks.HeaderLoadData_Readers_Patch.Initialize(saveLoadDiagnostics);
 204:             Features.SaveLoadDiagnostics.Hooks.LoadResult_InitializeCallbacks_Patch.Initialize(saveLoadDiagnostics);
 205:             Features.SaveLoadDiagnostics.Hooks.CampaignBehaviorManager_LoadBehaviorData_Patch.Initialize(saveLoadDiagnostics);
 206:             Features.SaveLoadDiagnostics.Hooks.SaveManager_Save_Patch.Initialize(saveLoadDiagnostics);
 207:             Features.SaveLoadDiagnostics.Hooks.FileDriver_Save_Patch.Initialize(saveLoadDiagnostics);
 208:             Features.SaveLoadDiagnostics.Hooks.SaveOutput_PrintStatus_Patch.Initialize(saveLoadDiagnostics);
 209:             Features.SaveLoadDiagnostics.Hooks.ContainerLoadData_Fill_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);
 210:             Features.SaveLoadDiagnostics.Hooks.CampaignBehaviorDataStore_LoadBehaviorData_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);
 211:             Features.SaveLoadDiagnostics.Hooks.ArchiveDeserializer_LoadFrom_Patch.Initialize(saveLoadDiagnostics, saveLoadLogger);
 212:             _harmony.PatchCategory("Patch61_SaveLoadDiagnostics");
 213:             foreach (var category in new[]
 214:             {
 215:                 "Patch61_SaveLoadDiagnostics_ContainerFill",
 216:                 "Patch61_SaveLoadDiagnostics_BehaviorData",
 217:                 "Patch61_SaveLoadDiagnostics_ArchiveParse",
 218:             })
 219:             {
 220:                 try
 221:                 {
 222:                     _harmony.PatchCategory(category);
 223:                 }
 224:                 catch (System.Exception ex)
 225:                 {
 226:                     saveLoadLogger.LogWarning($"[SaveLoad] {category} not applied (engine drift?): {ex.GetType().Name}: {ex.Message}");
 227:                 }
 228:             }
 229:         }
 230:         catch (System.Exception ex)
 231:         {
 232:             IoC.Resolve<IModLogger>().LogError($"[SaveLoad] init failed — save/load diagnostics inactive: {ex.GetType().Name}: {ex.Message}");
 233:         }
 234:         // Patch0_BattleScenes: loads TAOM's sp_battle_scenes.xml (full 0-255 map_indices coverage) so the
 235:         // TAOM_Map Main_map grid's extended indices (158-255) resolve to real battle terrains instead of
 236:         // FailedAsserting against vanilla's 1-157 table. Re-enabled 2026-06-01 (TAOM_Map ships Main_map +
 237:         // the extended XML exists; 3 patch targets verified against installed 1.4.5). In-game grid validation
 238:         // pending the worldmap_battle_scene_grid re-author. See docs/reference/worldmap-battle-scene-grid.md.
 239:         _harmony.PatchCategory("Patch0_BattleScenes");
 240:         // Remaining patches applied in OnGameInitializationFinished — View assembly must be initialized first
 241: 
 242:         var pathService = IoC.Resolve<IPathService>();
 243:         var logger = IoC.Resolve<IModLogger>();
 244:         FactionMapPaths.Initialize(pathService.ModuleRootPath, logger);
 245: 
 246:         var allianceHook = IoC.Resolve<IOnAllianceAction>();
 247:         var peaceHook = IoC.Resolve<IOnPeaceAction>();
 248:         DiplomacyIoC.InitializeHooks(allianceHook, peaceHook);
 249:         AllianceCampaignBehavior_EndAlliance_Patch.Initialize(logger);
 250:         AllianceCampaignBehavior_StartAlliance_Patch.Initialize(logger);
 251:         AllianceCampaignBehavior_AddAllianceDecision_Patch.Initialize(logger);
 252:         DeclareWarAction_ApplyInternal_Patch.Initialize(logger);
 253:         MakePeaceAction_ApplyInternal_Patch.Initialize(logger);
 254: 
 255:         var executionHook = IoC.Resolve<IOnExecutionAction>();
 256:         ExecutionIoC.InitializeHooks(executionHook);
 257: 
 258:         TroopWeightIoC.InitializeHooks(
 259:             IoC.Resolve<IOnPartyBaseNumberOfAllMembers>(),
 260:             IoC.Resolve<IOnPartyBaseNumberOfRegularMembers>(),
 261:             IoC.Resolve<IOnRecruitmentVMRefreshPartyProperties>(),
 262:             IoC.Resolve<IOnPartyVMPopulatePartyListLabel>(),
 263:             IoC.Resolve<IOnPartyUpgraderUpgradeReadyTroops>(),
 264:             IoC.Resolve<TroopWeightDisplayHook>());
 265: 
 266:         CustomBattlesIoC.InitializeHooks(
 267:             IoC.Resolve<IOnGetCustomBattleCommanders>(),
 268:             IoC.Resolve<IOnGetCustomBattleFactions>(),
 269:             IoC.Resolve<IOnGetDefaultTroopOfFormation>(),
 270:             IoC.Resolve<ISideCommanderFilter>(),
 271:             logger);
 272: 
 273:         _harmony.PatchCategory("Patch21_ShaderPrecompilation");
 274:         _shaderRunner = IoC.Resolve<ShaderPrecompileRunner>();
 275:         ShaderPrecompilationIoC.InitializeHooks(logger, _shaderRunner);
 276: 
 277:         _harmony.PatchCategory("Patch22_ArmyTargeting");
 278:         // Patch49: Finalizer guarding vanilla Army.FindBestGatheringSettlementAndMoveTheLeader,
 279:         // which NREs (Army.cs:726 settlement.GatePosition / 659 Kingdom.Settlements, v1.4.6) when a
 280:         // besieger army can't resolve a gathering fortification — a map-tick CTD on siege start.
 281:         // No TAOM patch is on the stack; aggressive Patch22 targeting just makes it more reachable.
 282:         // Crash report 2026-06-17. See the patch's doc-comment.
 283:         _harmony.PatchCategory("Patch49_ArmyGatheringNreGuard");
 284:         // Patch59: CaravanTrade — four postfixes on CaravansCampaignBehavior private methods
 285:         // (war gate, destination re-weight, range envelope, budget-factor floor) so AI/player caravans
 286:         // range past the local town cluster instead of shuttling. Campaign-behavior target, so applied
 287:         // in this campaign-phase block alongside the other AI patches.
 288:         _harmony.PatchCategory("Patch59_CaravanTrade");
 289:         _harmony.PatchCategory("Patch30_MixedFormations");
 290:         // Patch_MissionTime_SetMovementOrder (shared by Patch31_SmartCavalryAI +
 291:         // Patch35_CompanionTactics' Formation.SetMovementOrder hook) is applied in
 292:         // OnMissionBehaviorInitialize — MovementOrder.cctor reads Mission.Current.CurrentTime,
 293:         // which is null during OnSubModuleLoad and would crash JIT prep with NRE.
 294: 
 295:         var bannerColorConfig = IoC.Resolve<IBannerColorConfigProvider>();
 296:         var bannerColorService = IoC.Resolve<IBannerColorService>();
 297:         var bannerHeroAdapter = IoC.Resolve<IBannerHeroAdapter>();
 298: 
 299:         Banner_TryGetBannerDataFromCode_Transpiler.Initialize(bannerColorConfig, logger);
 300:         Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(bannerColorService);
 301:         Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(logger);
 302:         Clan_UpdateBannerColor_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 303:         Banner_GetFirstIconColor_Patch.Initialize(bannerColorService);
 304:         BannerEditorView_OnTick_Patch.Initialize(bannerColorService, logger);
 305:         CampaignUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 306:         SandBoxUIHelper_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 307:         SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 308:         PartyVM_RefreshCurrentCharacterInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 309:         HeroViewModel_FillFrom_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 310:         PartyCharacterVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 311:         ClanPartyItemVM_GetCharacterCode_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 312:         CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Initialize(bannerColorService);
 313:         var agentColorStore = IoC.Resolve<IAgentColorStore>();
 314:         Mission_SpawnAgent_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
 315:         Agent_EquipItemsFromSpawnEquipment_Patch.Initialize(bannerColorService, bannerHeroAdapter, agentColorStore);
 316:         AgentVisuals_Create_Patch.Initialize(bannerColorService);
 317:         MapConversationTableau_SpawnOpponentLeader_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 318:         MapConversationTableau_SpawnOpponentBodyguard_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 319:         MobilePartyVisual_AddCharacterToPartyIcon_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 320:         OrderOfBattleHeroItemVM_RefreshInformation_Patch.Initialize(bannerColorService, bannerHeroAdapter);
 321: 
 322:         Mission_Initialize_Patch.Initialize(logger);
 323: 
 324:         // Patch42_CastleRecruitment — castle notable recruitment. Targets RecruitmentCampaignBehavior
 325:         // + AiVisitSettlementBehavior (both in TaleWorlds.CampaignSystem, no View/Mission.cctor
 326:         // dependency, safe in OnSubModuleLoad). The transpilers swap the AI IsCastle gate to a runtime
 327:         // toggle; the postfix invokes the private CheckRecruiting for castles. All fail-safe.
 328:         var castleRecruitmentSettings = IoC.Resolve<ICastleRecruitmentSettingsProvider>();
 329:         CastleAiToggle.Initialize(castleRecruitmentSettings);
 330:         Patch42_AiHourlyTick_Transpiler.Initialize(logger);
 331:         Patch42_FillSettlements_Transpiler.Initialize(logger);
 332:         Patch42_HourlyTickParty_Postfix.Initialize(castleRecruitmentSettings, logger);
 333:         _harmony.PatchCategory("Patch42_CastleRecruitment");
 334: 
 335:         InformationManager.DisplayMessage(new InformationMessage("TAOM loaded successfully!", Colors.Green));
 336:     }
 337: 
 338:     protected override void OnBeforeInitialModuleScreenSetAsRoot()
 339:     {
 340:         base.OnBeforeInitialModuleScreenSetAsRoot();
 341:         IoC.Resolve<IMainMenuCustomizerService>().CustomizeMenu();
 342: 
 343:         // Patch55_BasicTableauRaceGuard — MUST be applied HERE, not in OnGameInitializationFinished.
 344:         // The Save/Load hero preview (BasicCharacterTableau) renders on the COLD main menu, before any
 345:         // game-init callback fires. The sibling CharacterTableau patches live in Patch2_RefreshTableau,
 346:         // applied in OnGameInitializationFinished (campaign init) — too late to guard the save-list CTD
 347:         // (Codex C1, issue #299). By here, IoC.Configure() (OnSubModuleLoad) has already set the guard,
 348:         // and the initial module screen has not been pushed yet, so the prefix attaches before the save
 349:         // list can render. Process-static one-shot; fail-open (a missing guard is no worse than vanilla).
 350:         if (!_basicTableauGuardApplied)
 351:         {
 352:             _basicTableauGuardApplied = true;
 353:             try
 354:             {
 355:                 _harmony.PatchCategory("Patch55_BasicTableauRaceGuard");
 356:             }
 357:             catch (System.Exception ex)
 358:             {
 359:                 IoC.Resolve<IModLogger>().LogError($"[HeroRace] Patch55_BasicTableauRaceGuard apply failed: {ex.GetType().Name}: {ex.Message}");
 360:             }
 361:         }
 362: 
 363:         // BattleLoadDiagnostics collection: a battle/scene load that hung last session left
 364:         // an inflight marker (phase-4 wrote it; phase-6/end never ran to clear it). If it
 365:         // survived to this main menu, the previous load never finished — surface a notice so
 366:         // the player knows to send the diagnostic log. See docs/features/battle-load-diagnostics.md.
 367:         try
 368:         {
 369:             var stallMarker = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>();
 370:             var stale = stallMarker?.TryConsumeStaleMarker();
 371:             if (stale != null)
 372:                 Features.BattleLoadDiagnostics.StallReportNotifier.Notify(stale);
 373:         }
 374:         catch { /* never block the main menu over a diagnostic */ }
 375: 
 376:         // NativeSkinFixes — three native MinHook detours that fix engine bugs
 377:         // TaleWorlds won't: covers_head morph freeze, hair cloth orphan, beard
 378:         // cloth orphan. Loads TAOM.NativeSkinFixes.dll from Main/_Module/bin
 379:         // and pattern-scans TaleWorlds.Native.dll for the hook targets at
 380:         // install time. See docs/features/native-skin-fixes.md.
 381:         //
 382:         // PARKED 2026-07-08 (user decision) — DISABLED at the wiring level. The
 383:         // install call below is commented out so the native hooks NEVER load,
 384:         // regardless of any persisted MCM "Native Skin Fixes → Enable Native
 385:         // Skin Fixes" value (MCM persists a saved value over the compiled
 386:         // default, so flipping the default alone would not stop machines that
 387:         // already saved it ON). Engine rendering is vanilla for everyone.
 388:         // RE-ENABLE: uncomment the install branch below AND flip the MCM default
 389:         // (TaomSettings.EnableNativeSkinFixes) back to true.
 390:         IoC.Resolve<IModLogger>().LogInfo(
 391:             "[NativeSkinFixes] parked (disabled at the wiring level) — engine rendering is vanilla");
 392:         // bool nsfEnabled = false;
 393:         // try { nsfEnabled = TaomSettings.Instance?.EnableNativeSkinFixes == true; }
 394:         // catch { /* MCM not ready — fail closed */ }
 395:         // if (nsfEnabled)
 396:         //     NativeSkinFixesInstaller.Install(IoC.Resolve<IModLogger>());
 397:         // else
 398:         //     IoC.Resolve<IModLogger>().LogInfo(
 399:         //         "[NativeSkinFixes] disabled (MCM 'Enable Native Skin Fixes' is off) — engine rendering is vanilla");
 400: 
 401:         // Pre-compile Shaders — RE-ENABLED 2026-06-17 (issue #287). Walks the all-characters battle
 402:         // (character/equipment shaders) then each TAOM battle scene (terrain + forced-atmosphere
 403:         // shaders — the d3dcompiler battle-load CTD class). Drives ShaderPrecompileRunner; progress
 404:         // shows on the loading screen + a 1 Hz status toast. See docs/features/shader-precompilation.md.
 405:         if (Module.CurrentModule.GetInitialStateOptionWithId("TaomPrecompileShaders") == null)
 406:         {
 407:             Module.CurrentModule.AddInitialStateOption(new InitialStateOption(
 408:                 id:                  "TaomPrecompileShaders",
 409:                 name:                new TextObject("{=taom_precompile_shaders}Pre-compile Shaders"),
 410:                 orderIndex:          100,
 411:                 action:              () => InformationManager.ShowInquiry(new InquiryData(
 412:                     "Shader Pre-compilation",
 413:                     "Loads a battle with all TAOM troops, then walks each TAOM battle scene, to " +
 414:                     "pre-compile every shader the game would otherwise compile mid-battle.\n\n" +
 415:                     "THIS TAKES A LONG TIME (1-2 hours+). Leave it running — progress shows on the " +
 416:                     "loading screen and as a status line. One-time process; it eliminates in-game " +
 417:                     "stutter and the intermittent battle-load crash/hang.\n\n" +
 418:                     "When you see 'Shader pre-compilation COMPLETE', you can play.",
 419:                     true, true, "Start", "Cancel",
 420:                     () =>
 421:                     {
 422:                         _shaderTickAccumulator = 0f;
 423:                         _shaderRunner?.Begin();
 424:                     },
 425:                     () => InformationManager.HideInquiry())),
 426:                 isDisabledAndReason: () => (false, new TextObject("")),
 427:                 enabledHint:         new TextObject("{=taom_precompile_hint}Pre-compiles shaders to eliminate in-game stutter + the battle-load crash. Run once after installing TAOM."),
 428:                 // Hidden live when the MCM master toggle is off (no relaunch needed). Defaults to shown
 429:                 // if settings aren't resolvable yet. The "Include Scene Passes" toggle is read inside Begin().
 430:                 isHidden:            () => !(Features.TaomSettings.Instance?.EnableShaderPrecompilation ?? true)));
 431:         }
 432:     }
 433: 
 434:     protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
 435:     {
 436:         base.OnGameStart(game, gameStarterObject);
 437: 
 438:         // Session-level diagnostic snapshot: OS / CLR / mod list / mod-stack
 439:         // assembly versions / campaign context. Runs once per session and is
 440:         // idempotent so OnGameStart on save-load doesn't spam.
 441:         try
 442:         {
 443:             IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>()?.LogSessionSnapshot();
 444:         }
 445:         catch { /* diagnostic is best-effort, never break OnGameStart */ }
 446: 
 447:         if (gameStarterObject is CampaignGameStarter campaignStarter)
 448:         {
 449:             // Registration order is preserved exactly from the pre-extraction inline block:
 450:             // vanilla-behavior removals precede their TAOM replacements, and the LotrIssues suppression
 451:             // must run inside this OnGameStart (after Sandbox registered its behaviors) — so the
 452:             // groups below are invoked in the original statement order.
 453:             // Phase 9b #173 — careerPassives resolved once for the whole CulturalFeats + CareerSystem
 454:             // + TroopProgression model registration block. Replaces all CareerPassiveHelper static
 455:             // calls with instance-injected ICareerPassiveService.
 456:             var careerPassives = IoC.Resolve<TAOM.Features.CareerSystem.ICareerPassiveService>();
 457:             // Hoisted: TaomVolunteerModel consumes ICulturalFeatsService for the village
 458:             // volunteer-respawn-rate feats (Dunland/Gundabad/Dol Guldur/Mordor); the cultural-feat
 459:             // model group reuses this same reference.
 460:             var culturalFeats = IoC.Resolve<TAOM.Features.CulturalFeats.ICulturalFeatsService>();
 461: 
 462:             RegisterProgressionAndIdentity(campaignStarter, careerPassives, culturalFeats);
 463:             RegisterRaceAgeAndFamily(campaignStarter);
 464:             RegisterDiplomacyAndConflict(campaignStarter);
 465:             RegisterCulturalFeatModels(campaignStarter, culturalFeats, careerPassives);
 466:             RegisterBattleBalanceAndTargeting(campaignStarter);
 467:             RegisterSpecialResourcesAndCareers(campaignStarter, careerPassives);
 468:             RegisterCampaignLifeBehaviors(campaignStarter);
 469:         }
 470:     }
 471: 
 472:     // Character identity, creation, and troop-progression registrations (ADR-002 extraction of the
 473:     // former OnGameStart inline block — bodies are verbatim, order unchanged).
 474:     private static void RegisterProgressionAndIdentity(
 475:         CampaignGameStarter campaignStarter,
 476:         ICareerPassiveService careerPassives,
 477:         TAOM.Features.CulturalFeats.ICulturalFeatsService culturalFeats)
 478:     {
 479:         var racePersistenceService = IoC.Resolve<IRacePersistenceService>();
 480:         campaignStarter.AddBehavior(new RacePersistenceBehavior(racePersistenceService));
 481: 
 482:         var bannerInjectionService = IoC.Resolve<IBannerInjectionService>();
 483:         var bannerExclusionService = IoC.Resolve<IBannerExclusionService>();
 484:         campaignStarter.AddBehavior(new BannerInjectionBehavior(bannerInjectionService, bannerExclusionService));
 485: 
 486:         var ccContentService = IoC.Resolve<ICharacterCreationContentService>();
 487:         var ccLogger = IoC.Resolve<IModLogger>();
 488:         campaignStarter.AddBehavior(new CharacterCreationRegistrationBehavior(ccContentService, ccLogger));
 489: 
 490:         campaignStarter.RemoveBehaviors<InitialChildGenerationCampaignBehavior>();
 491:         var childGenService = IoC.Resolve<IInitialChildGenerationService>();
 492:         campaignStarter.AddBehavior(new TaomInitialChildGenerationBehavior(childGenService));
 493: 
 494:         var costService = IoC.Resolve<ITroopCostService>();
 495:         // Phase 9b #180 / partial #148 — IWageModifierService extraction. Hoists garrison-wage
 496:         // feat loop + Mordor/Gundabad/Umbar party-wage feats + Rohan mounted-wage scaling +
 497:         // recruitment-cost feats out of the model body, satisfying gamemodels.md rule 4.
 498:         var wageModifiers = IoC.Resolve<IWageModifierService>();
 499:         var volunteerService = IoC.Resolve<IVolunteerTierService>();
 500:         var recruitmentService = IoC.Resolve<IVolunteerRecruitmentService>();
 501:         var volunteerContextAdapter = IoC.Resolve<IVolunteerContextAdapter>();
 502:         var recruitmentAlignment = IoC.Resolve<TAOM.Features.AlignmentRecruitment.IRecruitmentAlignmentService>();
 503:         campaignStarter.AddModel(new TaomCharacterStatsModel());
 504:         campaignStarter.AddModel(new TaomPartyWageModel(costService, careerPassives, wageModifiers));
 505:         campaignStarter.AddModel(new TaomVolunteerModel(volunteerService, recruitmentService, volunteerContextAdapter, culturalFeats, recruitmentAlignment));
 506: 
 507:         // NavalTravel — PARKED 2026-06-26: TAOM_Map's navmesh isn't set up to take advantage of naval
 508:         // travel (no naval region navmesh → AI can't route at sea; #296/#120), so the feature is disabled
 509:         // at the wiring level — registering nothing keeps vanilla DefaultPartyNavigationModel + vanilla
 510:         // navmesh regardless of any persisted MCM toggle. All code/tests/fixes are preserved for re-enable.
 511:         // RE-ENABLE: uncomment this model registration + the Patch54/Patch57 blocks in
 512:         // OnGameInitializationFinished, and flip the `enabled` defaults back to true.
 513:         // campaignStarter.AddModel(new TaomPartyNavigationModel(IoC.Resolve<INavalTravelService>(), IoC.Resolve<IModLogger>()));
 514:     }
 515: 
 516:     // Race-appropriate aging/pregnancy/hero-creation + the Ringwraith family block.
 517:     private static void RegisterRaceAgeAndFamily(CampaignGameStarter campaignStarter)
 518:     {
 519:         var raceAgeService = IoC.Resolve<IRaceAgeService>();
 520:         var heroAgeAdapter = IoC.Resolve<IHeroAgeAdapter>();
 521:         var raceAgeLogger = IoC.Resolve<IModLogger>();
 522:         campaignStarter.AddBehavior(new RaceAgeBehavior(raceAgeService, heroAgeAdapter, raceAgeLogger));
 523:         campaignStarter.AddModel(new TaomAgeModel(raceAgeService));
 524:         campaignStarter.AddModel(new TaomPregnancyModel(raceAgeService));
 525:         campaignStarter.AddModel(new TaomHeroCreationModel());
 526: 
 527:         // Ringwraiths (Witch-King + Nazgûl) take no spouse/parents/children: block their marriage
 528:         // (so no spouse ⇒ no children) + a defensive clear-on-load for pre-feature saves.
 529:         var nazgulRegistry = IoC.Resolve<INazgulRegistry>();
 530:         campaignStarter.AddModel(new TaomMarriageModel(nazgulRegistry));
 531:         campaignStarter.AddBehavior(new NazgulFamilyBehavior(nazgulRegistry, IoC.Resolve<IModLogger>()));
 532:     }
 533: 
 534:     // Diplomacy / War of the Ring / siege defense / execution-relation registrations.
 535:     private static void RegisterDiplomacyAndConflict(CampaignGameStarter campaignStarter)
 536:     {
 537:         var diplomacyService = IoC.Resolve<IDiplomacyService>();
 538:         var wotrService = IoC.Resolve<IWarOfTheRingService>();
 539:         var diplomacyLogger = IoC.Resolve<IModLogger>();
 540:         campaignStarter.AddBehavior(new DiplomacyBehavior(diplomacyService, diplomacyLogger));
 541:         campaignStarter.AddBehavior(new PlayerAllianceProposalBehavior(diplomacyService, diplomacyLogger));
 542:         campaignStarter.AddModel(new TaomAllianceModel(diplomacyService));
 543:         campaignStarter.AddModel(new TaomKingdomDecisionPermissionModel(diplomacyService, wotrService));
 544:         campaignStarter.AddModel(new TaomDiplomacyModel(wotrService));
 545: 
 546:         var wotrLogger = IoC.Resolve<IModLogger>();
 547:         campaignStarter.AddBehavior(new WarOfTheRingBehavior(wotrService, wotrLogger));
 548:         // WotR Momentum #327 — Evil-vs-Good progress tracking + victory; behavior is a
 549:         // Reuse.Singleton (it carries the state store's persistence dict).
 550:         campaignStarter.AddBehavior(IoC.Resolve<Features.WarOfTheRingMomentum.WarOfTheRingMomentumBehavior>());
 551: 
 552:         var siegeDefenseService = IoC.Resolve<ISiegeDefenseService>();
 553:         var siegeDefenseLogger = IoC.Resolve<IModLogger>();
 554:         campaignStarter.AddBehavior(new SiegeDefenseBehavior(siegeDefenseService, siegeDefenseLogger));
 555:         campaignStarter.AddModel(new TaomSiegeEventModel(IoC.Resolve<ISiegeEngineAvailabilityService>()));
 556: 
 557:         var executionRelationService = IoC.Resolve<IExecutionRelationService>();
 558:         var playerContext = IoC.Resolve<IPlayerContextAdapter>();
 559:         campaignStarter.AddModel(new TaomExecutionRelationModel(executionRelationService, playerContext));
 560:     }
 561: 
 562:     // Cultural feat models — Phase 9b #144/#176: dispatch logic extracted to
 563:     // ICulturalFeatsService. Each model is a thin boundary that converts
 564:     // CultureObject → ICultureFeatAdapter and delegates (gamemodels.md rule 4).
 565:     // `culturalFeats` is passed in (hoisted resolve, shared with TaomVolunteerModel).
 566:     private static void RegisterCulturalFeatModels(
 567:         CampaignGameStarter campaignStarter,
 568:         TAOM.Features.CulturalFeats.ICulturalFeatsService culturalFeats,
 569:         ICareerPassiveService careerPassives)
 570:     {
 571:         campaignStarter.AddModel(new TaomArmyManagementModel(culturalFeats));
 572:         campaignStarter.AddModel(new TaomPartySpeedModel(culturalFeats, careerPassives));
 573:         campaignStarter.AddModel(new TaomSettlementProsperityModel(culturalFeats));
 574:         campaignStarter.AddModel(new TaomSettlementMilitiaModel(culturalFeats));
 575:         campaignStarter.AddModel(new TaomBuildingConstructionModel(culturalFeats));
 576:         campaignStarter.AddModel(new TaomVillageProductionModel(culturalFeats));
 577:         campaignStarter.AddModel(new TaomCaravanModel(culturalFeats, IoC.Resolve<TAOM.Features.CaravanTrade.ICaravanTradeService>()));
 578:         campaignStarter.AddModel(new TaomBattleRewardModel(culturalFeats, careerPassives));
 579:         campaignStarter.AddModel(new TaomTournamentModel(IoC.Resolve<TAOM.Features.Arena.ITournamentService>()));
 580:         campaignStarter.AddModel(new TaomPartyTroopUpgradeModel(culturalFeats, careerPassives));
 581:         campaignStarter.AddModel(new TaomPartySizeModel(culturalFeats, careerPassives));
 582:         campaignStarter.AddModel(new TaomFoodConsumptionModel(culturalFeats));
 583:         campaignStarter.AddModel(new TaomSettlementLoyaltyModel(culturalFeats, IoC.Resolve<IRevoltTuningConfigProvider>()));
 584:         campaignStarter.AddModel(new TaomSettlementFoodModel(IoC.Resolve<ISettlementFoodService>(), IoC.Resolve<ISettlementFoodConfigProvider>()));
 585:         campaignStarter.AddModel(new TaomSettlementEconomyModel(IoC.Resolve<ISettlementEconomyService>(), IoC.Resolve<ISettlementEconomyConfigProvider>()));
 586:         campaignStarter.AddModel(new TaomBanditDensityModel(IoC.Resolve<IBanditScalingService>()));
 587:         campaignStarter.AddModel(new TaomPartyMoraleModel(culturalFeats, careerPassives));
 588:         campaignStarter.AddModel(new TaomSmithingModel(culturalFeats, careerPassives));
 589:         campaignStarter.AddModel(new TaomClanFinanceModel(culturalFeats));
 590:         campaignStarter.AddModel(new TaomRaidModel(culturalFeats, careerPassives));
 591:         campaignStarter.AddModel(new TaomNotableSpawnModel(culturalFeats));
 592:     }
 593: 
 594:     // Battle-balance / encyclopedia-visibility / army-targeting model registrations.
 595:     private static void RegisterBattleBalanceAndTargeting(CampaignGameStarter campaignStarter)
 596:     {
 597:         var battleBalanceSettings = IoC.Resolve<IBattleBalanceSettingsProvider>();
 598:         var battleBalanceConfig = IoC.Resolve<IBattleBalanceConfigProvider>();
 599:         campaignStarter.AddModel(new TaomMilitaryPowerModel(battleBalanceSettings, battleBalanceConfig));
 600:         campaignStarter.AddModel(new TaomCombatSimulationModel(battleBalanceSettings));
 601:         campaignStarter.AddModel(new TaomPartyHealingModel(battleBalanceSettings, battleBalanceConfig, IoC.Resolve<ICareerPassiveService>()));
 602: 
 603:         campaignStarter.AddModel(new TaomInformationRestrictionModel(IoC.Resolve<IEncyclopediaSettingsProvider>()));
 604: 
 605:         var armyTargetingService = IoC.Resolve<IArmyTargetingService>();
 606:         campaignStarter.AddModel(new TaomTargetScoreModel(armyTargetingService));
 607:     }
 608: 
 609:     // Special-resource economy + the career system (behaviors, quests, and career GameModels).
 610:     private static void RegisterSpecialResourcesAndCareers(
 611:         CampaignGameStarter campaignStarter,
 612:         ICareerPassiveService careerPassives)
 613:     {
 614:         var specialResourceService = IoC.Resolve<ISpecialResourceService>();
 615:         var specialResourceStorage = IoC.Resolve<ISpecialResourceStorageService>();
 616:         var specialResourceConfig = IoC.Resolve<ISpecialResourceConfigProvider>();
 617:         var specialResourceLogger = IoC.Resolve<IModLogger>();
 618:         var specialResourceBehavior = new SpecialResourcesBehavior(
 619:             specialResourceService, specialResourceStorage, specialResourceConfig, specialResourceLogger);
 620:         campaignStarter.AddBehavior(specialResourceBehavior);
 621:         PartyScreenLogic_AddCommand_Patch.SetBehavior(specialResourceBehavior);
 622: 
 623:         // TEMPORARY: troop-count diagnostic (special-currency undercount investigation). Dumps the
 624:         // main party's raw + weighted counts to the log on party-screen open. Remove with the behavior.
 625:         campaignStarter.AddBehavior(IoC.Resolve<TroopCountDiagnosticsBehavior>());
 626: 
 627:         var careerDataService = IoC.Resolve<ICareerDataService>();
 628:         var careerRegistry = IoC.Resolve<ICareerRegistry>();
 629:         var careerPassiveService = IoC.Resolve<ICareerPassiveService>();
 630:         var careerLogger = IoC.Resolve<IModLogger>();
 631:         campaignStarter.AddBehavior(new CareerPersistenceBehavior(careerDataService, careerLogger));
 632:         var careerCreationHandler = IoC.Resolve<ICareerCreationHandler>();
 633:         var careerAbilityServiceForBehavior = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
 634:         campaignStarter.AddBehavior(new CareerCampaignBehavior(
 635:             careerDataService, careerRegistry, careerPassiveService, careerCreationHandler, careerAbilityServiceForBehavior, careerLogger));
 636: 
 637:         var careerAdapterFactory = IoC.Resolve<ICareerHeroAdapterFactory>();
 638:         // CareerSwitchDialogueBehavior used to take ICareerSwitchService too; that dependency
 639:         // moved to GauntletCareerScreen.OnChooseSwitchTarget (Codex Review #32 cleanup).
 640:         campaignStarter.AddBehavior(new CareerSwitchDialogueBehavior(
 641:             careerDataService, careerRegistry, careerAdapterFactory, careerLogger));
 642: 
 643:         // Career-tied quest system (Phase 6) — offers/starts tier quests; CareerQuest : QuestBase
 644:         // is registered for saving by the auto-discovered CareerQuestSaveableTypeDefiner.
 645:         var careerQuestService = IoC.Resolve<Features.CareerSystem.ICareerQuestService>();
 646:         campaignStarter.AddBehavior(new Features.CareerSystem.Quests.CareerQuestCampaignBehavior(
 647:             careerDataService, careerQuestService, careerLogger));
 648: 
 649:         // Career system GameModels — reuse the hoisted careerPassives resolve.
 650:         // Phase 9b #142 — agent-stat extraction: TaomAgentStatCalculateModel /
 651:         // TaomAgentApplyDamageModel now delegate UpdateAgentStats + damage-amp/red +
 652:         // shrug-off logic to ICareerAgentStatService (gamemodels.md rule 4).
 653:         var careerAgentStat = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>();
 654:         campaignStarter.AddModel(new TaomMapVisibilityModel(careerPassives));
 655:         campaignStarter.AddModel(new TaomInventoryCapacityModel(careerPassives));
 656:         var elephantAttackService = IoC.Resolve<Features.Elephant.IElephantAttackService>();
 657:         var spiderAttackService = IoC.Resolve<ISpiderAttackService>();
 658:         var mumakilAttackService = IoC.Resolve<Features.Mumakil.IMumakilAttackService>();
 659:         campaignStarter.AddModel<AgentStatCalculateModel>(new TaomAgentStatCalculateModel(careerAgentStat, elephantAttackService, spiderAttackService, mumakilAttackService));
 660:         // CombatMechanics (2026-07-02): TaomCombatMechanicsModel DERIVES from the (now abstract)
 661:         // TaomAgentApplyDamageModel — one AgentApplyDamageModel slot, career passives via
 662:         // inheritance + the combat feel pack on top (docs/features/combat-mechanics.md).
 663:         campaignStarter.AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(
 664:             careerAgentStat,
 665:             IoC.Resolve<Features.CombatMechanics.ICrushThroughService>(),
 666:             IoC.Resolve<Features.CombatMechanics.IChargeKnockdownService>(),
 667:             IoC.Resolve<Features.CombatMechanics.ICreatureCombatService>(),
 668:             IoC.Resolve<Features.CombatMechanics.IShieldPenetrationService>(),
 669:             IoC.Resolve<Features.CombatMechanics.ICombatMechanicsConfigProvider>(),
 670:             IoC.Resolve<Features.CombatMechanics.ICombatMechanicsSettingsProvider>()));
 671:         campaignStarter.AddModel(new TaomClanTierModel(careerPassiveService));
 672:     }
 673: 
 674:     // Campaign-life behaviors: startup resources, companions, inventory/equipment QoL, fief +
 675:     // formation tooling, messengers, marketplace, castle recruitment, alignment systems, culture
 676:     // conversion, and the LOTR issue takeover (suppression stays inside OnGameStart, last in order).
 677:     private static void RegisterCampaignLifeBehaviors(CampaignGameStarter campaignStarter)
 678:     {
 679:         var goldService = IoC.Resolve<IStartupGoldService>();
 680:         var influenceService = IoC.Resolve<IStartupInfluenceService>();
 681:         var startupLogger = IoC.Resolve<IModLogger>();
 682:         campaignStarter.AddBehavior(new StartupResourcesBehavior(goldService, influenceService, startupLogger));
 683: 
 684:         var namedCompanionService = IoC.Resolve<INamedCompanionService>();
 685:         campaignStarter.AddBehavior(new NamedCompanionBehavior(namedCompanionService));
 686: 
 687:         // QuickActions: per-save inventory-search-box persistence (SyncData round-trips
 688:         // even when EnableInventorySearch is OFF — disabled = inert, not absent).
 689:         campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.QuickActions.Hooks.InventorySearchCampaignBehavior>());
 690: 
 691:         // EquipPresets: per-save preset persistence + orphan pruning. Unconditional registration
 692:         // so the SyncData round-trip preserves presets even when EnableEquipmentPresets is OFF
 693:         // (the MCM hint promises "existing presets are inert (preserved in save)").
 694:         campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.EquipPresets.Hooks.EquipmentPresetCampaignBehavior>());
 695: 
 696:         // FiefManagement (Patch36) — register UNCONDITIONALLY so the menu is always present
 697:         // and the EnableFiefManagement MCM toggle takes effect immediately at runtime.
 698:         campaignStarter.AddBehavior(new FiefHubCampaignBehavior(
 699:             IoC.Resolve<IFiefHubMenuPresenter>(),
 700:             IoC.Resolve<IFiefManagementSettingsProvider>()));
 701: 
 702:         // CompanionTactics (Patch35) — FormationPresets persistence behavior. Registered
 703:         // unconditionally so SyncData round-trips even when EnableFormationPresets is OFF.
 704:         campaignStarter.AddBehavior(new Features.CompanionTactics.FormationPresets.Hooks.FormationPresetCampaignBehavior(
 705:             IoC.Resolve<Features.CompanionTactics.FormationPresets.IFormationPresetService>(),
 706:             IoC.Resolve<IModLogger>()));
 707: 
 708:         // Messengers — paid messenger dispatch + dialog hooks + per-save SyncData persistence.
 709:         // Registered unconditionally so saves round-trip pending messengers even when
 710:         // EnableMessengers is OFF (disabled = inert, not absent).
 711:         campaignStarter.AddBehavior(IoC.Resolve<TAOM.Features.Messengers.MessengerCampaignBehavior>());
 712: 
 713:         // CultureMarketplace (#207) — daily injection of LOTRLOME items into town markets
 714:         // keyed by owner culture. No SyncData (stock lives in vanilla Settlement.ItemRoster).
 715:         campaignStarter.AddBehavior(new Features.CultureMarketplace.CultureMarketplaceBehavior(
 716:             IoC.Resolve<Features.CultureMarketplace.ICultureItemPoolService>(),
 717:             IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceInjectionService>(),
 718:             IoC.Resolve<Features.CultureMarketplace.ICultureMarketplaceMaintenanceService>(),
 719:             IoC.Resolve<ITownRosterAdapter>(),
 720:             IoC.Resolve<Features.CultureMarketplace.Domain.MarketplaceTuning>(),
 721:             IoC.Resolve<IModLogger>()));
 722: 
 723:         // CastleRecruitment (Patch42) — castle notable population + maintenance + volunteer fill +
 724:         // player "Recruit troops" castle menu + issue/quest suppression for castle notables.
 725:         // Registered unconditionally so the MCM master toggle takes effect at runtime.
 726:         campaignStarter.AddBehavior(new CastleRecruitmentBehavior(
 727:             IoC.Resolve<ICastleRecruitmentService>(),
 728:             IoC.Resolve<IModLogger>()));
 729: 
 730:         // AlignmentDesertion — opposed-alignment troops (Free vs Evil) desert daily from mobile
 731:         // parties and garrisons. Registered unconditionally so the MCM master toggle takes effect
 732:         // at runtime; stateless (no SyncData). Reuses the Execution IAlignmentService.
 733:         campaignStarter.AddBehavior(new Features.AlignmentDesertion.Hooks.AlignmentDesertionBehavior(
 734:             IoC.Resolve<Features.AlignmentDesertion.IAlignmentDesertionService>(),
 735:             IoC.Resolve<IModLogger>()));
 736: 
 737:         // EliteEmissary — buy a faction's elite troops for its special resource at key settlements.
 738:         // Registered unconditionally so the MCM master toggle takes effect at runtime; stateless (no SyncData).
 739:         campaignStarter.AddBehavior(new Features.EliteEmissary.Hooks.EliteEmissaryBehavior(
 740:             IoC.Resolve<Features.EliteEmissary.IEliteEmissaryService>(),
 741:             IoC.Resolve<Features.EliteEmissary.IEliteEmissarySettingsProvider>(),
 742:             IoC.Resolve<Features.EliteEmissary.IEliteEmissaryConfigProvider>(),
 743:             IoC.Resolve<ISettlementOwnerAdapter>(),
 744:             IoC.Resolve<IModLogger>()));
 745: 
 746:         // CultureConversion — conquered cross-culture fiefs gradually adopt the new owner's culture
 747:         // (troops, militia, identity). Registered unconditionally so SyncData round-trips conversion
 748:         // records and completed overrides re-apply on load even when the MCM toggle is off.
 749:         campaignStarter.AddBehavior(new Features.CultureConversion.Hooks.CultureConversionBehavior(
 750:             IoC.Resolve<Features.CultureConversion.ICultureConversionService>(),
 751:             IoC.Resolve<Features.CultureConversion.ICultureConversionStore>(),
 752:             IoC.Resolve<IModLogger>()));
 753: 
 754:         // LotrIssues — suppress ALL 43 vanilla procedural issue behaviors (Sandbox registered them
 755:         // before this OnGameStart) and register the single LOTR custom-issue dispatcher in their
 756:         // place. New-campaign feature: a pre-suppression save keeps in-flight vanilla issues until
 757:         // they resolve, since their behaviors are only absent for newly-started campaigns here.
 758:         Features.LotrIssues.LotrIssueSuppression.SuppressAll(campaignStarter, IoC.Resolve<IModLogger>());
 759:         campaignStarter.AddBehavior(new Features.LotrIssues.LotrIssuesCampaignBehavior(
 760:             IoC.Resolve<Features.LotrIssues.ILotrIssueService>(),
 761:             IoC.Resolve<IModLogger>()));
 762:     }
 763: 
 764:     public override void OnGameInitializationFinished(Game game)
 765:     {
 766:         base.OnGameInitializationFinished(game);
 767: 
 768:         // Harmony patches are process-global (applied to methods, persist across games). Apply this
 769:         // whole per-game-init patch block ONCE per process — re-applying on a 2nd game init duplicates
 770:         // every prefix/postfix, restarts the BattleLoad watchdog, and CRASHES the non-idempotent
 771:         // DeliverOffSpring transpiler (chained twice, it can't find its already-NOPped anchor). The
 772:         // shader-precompile walk starts N custom games in one process and tripped exactly this on item 2;
 773:         // a player loading a 2nd campaign/custom-battle in one session hits the same crash.
 774:         // Mirrors _missionTimePatchesApplied in OnMissionBehaviorInitialize.
 775:         if (_gameInitPatchesApplied) return;
 776:         _gameInitPatchesApplied = true;
 777: 
 778:         _harmony.PatchCategory("Patch1_FirstTimeInit");
 779:         _harmony.PatchCategory("Patch2_RefreshTableau");
 780:         _harmony.PatchCategory("Patch3_SetRace");
 781:         _harmony.PatchCategory("Patch4_CharacterSpawner");
 782:         _harmony.PatchCategory("Patch5_FaceGen");
 783:         _harmony.PatchCategory("Late_Transpiler");
 784:         _harmony.PatchCategory("Late_ActionSetOverride");
 785:         _harmony.PatchCategory("Patch6_BannerEditor");
 786:         _harmony.PatchCategory("Patch7_FactionMap");
 787:         _harmony.PatchCategory("Patch9_RaceFilter");
 788:         _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
 789:         _harmony.PatchCategory("Patch8_SiegeCampGuard");
 790:         _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
 791:         _harmony.PatchCategory("Patch11_Diplomacy");
 792:         _harmony.PatchCategory("Patch12_WarOfTheRing");
 793: 
 794:         _harmony.PatchCategory("Patch14_Execution");
 795:         _harmony.PatchCategory("Patch15_BannerLayerLimit");
 796:         _harmony.PatchCategory("Patch16_AtmospherePersistence");
 797:         _harmony.PatchCategory("Patch17_TroopWeight");
 798:         _harmony.PatchCategory("Patch23_BannerColorPersistence");
 799:         _harmony.PatchCategory("Patch24_BannerDriftGuard");
 800:         _harmony.PatchCategory("Patch39_BanditPartySize");
 801:         _harmony.PatchCategory("Patch40_HideoutDescription");
 802:         _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
 803:         // Patch47 RE-ENABLED 2026-06-12 after full exoneration: its 06-12 morning indictment
 804:         // ("post-sever tick AV") was actually the CanAttack charge crash at set_attack_entity
 805:         // (0x6BAB4E), which fired with AND without Patch47 and is fixed in data (LOTRLOME
 806:         // monster Flags). Patch47's own job verified working: severed riders die clean on-foot
 807:         // deaths (act_death_by_arrow class) instead of AVing in the native mounted-death path —
 808:         // which 1.4.6 still does on melee deaths (Die-path AV reading float-bits-as-index from
 809:         // a corrupted action record, debugger-proven 06-12). See docs/features/spider.md.
 810:         Features.Spider.Hooks.Agent_Die_SpiderDismount_Patch.Initialize();
 811:         _harmony.PatchCategory("Patch47_SpiderDeathDismount");
 812: 
 813:         // Patch48: the non-lethal sibling of Patch47. A CanDismount melee hit on a mounted Spider Rider AVs in
 814:         // native HandleBlowAux (reading 0x3) — the same broken non-vanilla mounted-dismount path Patch47 routes
 815:         // around on death. Strips CanDismount for spider riders so the native dismount never fires (the rider
 816:         // stays on the locked mount; damage still applies). Debugger-proven 2026-06-15. See docs/features/spider.md.
 817:         _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");
 818: 
 819:         // Patch50: Finalizer swallowing a vanilla NRE in Agent.CheckToDropFlaggedItem (Agent.cs:3595),
 820:         // reached via the shared synthetic-bite path (CustomAttacksUtils.TakeDamage → RegisterBlow →
 821:         // OnAgentHit → affectedAgent.CheckToDropFlaggedItem) when a warg bites another warg (mount
 822:         // victim with a null wielded Item). Already caught by WargAttackService, but swallowing lets
 823:         // OnAgentHit finish and stops the log spam. Crash report 2026-06-17. See the patch doc-comment.
 824:         _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");
 825: 
 826:         // Patch56_SceneNotificationVisualGuard: Finalizer swallowing a managed NRE in
 827:         // PopupSceneSpawnPoint.InitializeWithAgentVisuals, reached via GauntletSceneNotification.OpenScene
 828:         // when the become-king (or sibling) cinematic builds a character whose human AgentVisuals yields
 829:         // null — the engine derefs the human visual without a null guard (it guards only the mount). The
 830:         // finalizer aborts the cinematic cleanly (HideSceneNotification) so cinematics that CAN render
 831:         // still play. Fourth raw custom-race/visual render path (after Patch55). Crash reports
 832:         // 2026-06-24/25 (become ruler of empire_w/gondor). See the patch doc-comment.
 833:         _harmony.PatchCategory("Patch56_SceneNotificationVisualGuard");
 834: 
 835:         // Patch13_RaceAge — noise reduction (NOT a crash fix). NOPs the harmless
 836:         // mother.Race == father.Race SilentAssert in DeliverOffSpring that fires on every
 837:         // mixed-race birth (normal in TAOM). Stops the debugger break + debug-log spam.
 838:         _harmony.PatchCategory("Patch13_RaceAge");
 839: 
 840:         var resourceHook = IoC.Resolve<IOnPartyUpgradeResourceCheck>();
 841:         var specResLogger = IoC.Resolve<IModLogger>();
 842:         PartyCharacterVM_InitializeUpgrades_Patch.Initialize(resourceHook, specResLogger);
 843:         PartyScreenLogic_UpgradeTroop_Patch.Initialize(resourceHook, specResLogger);
 844:         PartyScreenLogic_AddCommand_Patch.Initialize(resourceHook, specResLogger);
 845:         RecruitmentVM_RecruitGate_Patch.Initialize(IoC.Resolve<IOnRecruitmentResourceGate>(), specResLogger);
 846:         _harmony.PatchCategory("Patch26_SpecialResources");
 847:         _harmony.PatchCategory("Patch51_RecruitmentResourceGate");
 848:         _harmony.PatchCategory("Patch27_CareerSystem");
 849:         _harmony.PatchCategory("Patch29_CCBodyProperties");
 850:         _harmony.PatchCategory("Patch44_CCNameAutofill");
 851:         _harmony.PatchCategory("Patch33_EquipPresets");
 852:         _harmony.PatchCategory("Patch34_QuickActions");
 853:         _harmony.PatchCategory("Patch35_CompanionTactics");
 854:         _harmony.PatchCategory("Patch36_FiefManagement");
 855:         SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());
 856:         _harmony.PatchCategory("Patch38_SettlementNameplateFade");
 857: 
 858:         // Patch53_PartyIconScale — transpiler that rewrites the two hardcoded 0.3f campaign-map scale
 859:         // literals in MobilePartyVisual.AddCharacterToPartyIcon (leader figure + its mount) into a call
 860:         // to PartyIconScaleConfig.GetScale(), so both honour the MCM "Map Figure Scale" slider
 861:         // (default 0.15 = half vanilla). See docs/features/party-icon-scale.md.
 862:         Features.PartyIconScale.Hooks.Patch53_PartyIconScale.Initialize(IoC.Resolve<IModLogger>());
 863:         _harmony.PatchCategory("Patch53_PartyIconScale");
 864: 
 865:         // NavalTravel PARKED 2026-06-26 (#296/#120) — see the model-registration comment in OnGameStart.
 866:         // Patch54 (boat visual) + Patch57 (at-sea native-AV crash guard) are only meaningful while a party
 867:         // can be at sea, which only the (now-unregistered) TaomPartyNavigationModel enables — so neither is
 868:         // applied while the feature is parked. RE-ENABLE: uncomment both blocks with the model registration.
 869:         // Patch54_NavalTravelBoatVisual — render an at-sea party as a boat (base game renders no ship at sea).
 870:         // Features.NavalTravel.Hooks.Patch54_NavalTravelBoatVisual.Initialize(IoC.Resolve<Features.NavalTravel.INavalTravelService>(), IoC.Resolve<IModLogger>());
 871:         // _harmony.PatchCategory("Patch54_NavalTravelBoatVisual");
 872:         //
 873:         // Patch57_NavalAtSeaLandRescueGuard — prevent the native AV CTD on the hourly AI tick (the vanilla
 874:         // AIMoveToNearestLandBehavior's native cross-region pathfind AVs on TAOM_Map's missing naval navmesh,
 875:         // #120). Only fires for an at-sea party, which can't happen while the model is unregistered.
 876:         // var navalRescueLogger = IoC.Resolve<IModLogger>();
 877:         // Features.NavalTravel.Hooks.Patch57_NavalAtSeaLandRescueGuard.Initialize(IoC.Resolve<Features.NavalTravel.INavalTravelService>(), navalRescueLogger);
 878:         // try { _harmony.PatchCategory("Patch57_NavalAtSeaLandRescueGuard"); }
 879:         // catch (System.Exception ex) { navalRescueLogger.LogWarning($"[NavalTravel] Patch57 at-sea rescue guard failed to apply: {ex.Message}"); }
 880: 
 881:         // BattleLoadDiagnostics — phase-stamp the attack->battle-playable lifecycle so an
 882:         // intermittent battle-load hang leaves a log whose last line names the stuck phase
 883:         // (and, for the equipment phase, the agent + the item whose bo_ collision mesh is
 884:         // missing). The background stall watchdog auto-triggers a crash bundle on a freeze.
 885:         var battleLoadSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
 886:         var equipSnapshotAdapter = IoC.Resolve<IEquipmentSnapshotAdapter>();
 887:         var battleLoadStallMarker = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>();
 888:         Features.BattleLoadDiagnostics.Hooks.PlayerEncounter_Start_Patch.Initialize(battleLoadSvc);
 889:         Features.BattleLoadDiagnostics.Hooks.MissionState_OpenNew_Patch.Initialize(battleLoadSvc);
 890:         Features.BattleLoadDiagnostics.Hooks.BattleSceneSelection_Patch.Initialize(battleLoadSvc);
 891:         Features.BattleLoadDiagnostics.Hooks.Mission_Initialize_BattleLoad_Patch.Initialize(battleLoadSvc, battleLoadStallMarker);
 892:         Features.BattleLoadDiagnostics.Hooks.Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.Initialize(battleLoadSvc, equipSnapshotAdapter);
 893:         // Exit-phase probes (issue #331 — 30s-2min hang exiting tournaments): stamp the
 894:         // mission end -> map resume window so the dominant phase gap names the time sink.
 895:         Features.BattleLoadDiagnostics.Hooks.Mission_EndMission_ExitPhase_Patch.Initialize(battleLoadSvc);
 896:         Features.BattleLoadDiagnostics.Hooks.Mission_EndMissionInternal_ExitPhase_Patch.Initialize(battleLoadSvc);
 897:         Features.BattleLoadDiagnostics.Hooks.Mission_ClearUnreferencedResources_ExitPhase_Patch.Initialize(battleLoadSvc);
 898:         Features.BattleLoadDiagnostics.Hooks.MissionState_OnFinalize_ExitPhase_Patch.Initialize(battleLoadSvc);
 899:         Features.BattleLoadDiagnostics.Hooks.MapState_OnActivate_ExitPhase_Patch.Initialize(battleLoadSvc);
 900:         Features.BattleLoadDiagnostics.Hooks.MapState_OnTick_ExitPhase_Patch.Initialize(battleLoadSvc);
 901:         _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
 902:         IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();
 903: 
 904:         // Exit-stall stack sampler (#331 round 2): OnGameInitializationFinished runs on the
 905:         // game's main thread — the same thread the tournament-exit stall freezes — so this
 906:         // is a valid capture point for the sampler's main-thread reference.
 907:         var exitStallSampler = IoC.Resolve<Features.BattleLoadDiagnostics.ExitStallSampler>();
 908:         exitStallSampler.SetMainThread(System.Threading.Thread.CurrentThread);
 909:         exitStallSampler.Start();
 910: 
 911:         // Patch60 — release the tournament UI movie/layer at OnEndMission time. The engine's
 912:         // MissionGauntletTournamentView leaks both (nulls without release, unlike the practice
 913:         // view), deferring the Tournament-movie teardown into ScreenBase.HandleFinalize under
 914:         // the exit loading screen, where an in-flight prize tableau render stalls it ~108s (#331).
 915:         var tournamentExitLogger = IoC.Resolve<IModLogger>();
 916:         Features.Arena.Hooks.Patch60_TournamentExitMovieRelease.Initialize(tournamentExitLogger);
 917:         try { _harmony.PatchCategory("Patch60_TournamentExitMovieRelease"); }
 918:         catch (System.Exception ex) { tournamentExitLogger.LogWarning($"[Arena] Patch60 tournament-exit movie release failed to apply: {ex.Message}"); }
 919: 
 920:         // Manual patches for PRIVATE engine methods (AccessTools-resolved targets; can't use
 921:         // [HarmonyPatch] attribute binding + PatchCategory). Extracted verbatim to
 922:         // ManualPatchApplicator (ADR-002); apply order unchanged, each fail-safes with a warning.
 923:         ManualPatchApplicator.ApplyAll(_harmony);
 924:     }
 925: 
 926:     public override void OnMissionBehaviorInitialize(Mission mission)
 927:     {
 928:         base.OnMissionBehaviorInitialize(mission);
 929: 
 930:         // Apply Formation.SetMovementOrder patches (Patch31_SmartCavalryAI + Patch35
 931:         // CancelStanceOnMove) only once Mission.Current is non-null — MovementOrder's
 932:         // type initializer constructs static fields whose ctor reads
 933:         // Mission.Current.CurrentTime. Applying earlier crashes JIT prep with NRE.
 934:         if (!_missionTimePatchesApplied)
 935:         {
 936:             _missionTimePatchesApplied = true;
 937:             _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
 938:         }
 939: 
 940:         mission.AddMissionBehavior(new AdvancedCombatBehavior());
 941:         mission.AddMissionBehavior(new BehaviorTreeMissionLogic());
 942:         mission.AddMissionBehavior(new AutonomousMovementPlayerController());
 943:         mission.AddMissionBehavior(new WargMissionBehavior());
 944:         mission.AddMissionBehavior(new SpiderMissionBehavior());
 945:         mission.AddMissionBehavior(new Features.Elephant.ElephantMissionBehavior());
 946:         mission.AddMissionBehavior(new Features.Mumakil.MumakilMissionBehavior());
 947:         mission.AddMissionBehavior(new SiegeDismountMissionBehavior());
 948:         mission.AddMissionBehavior(new MixedFormationsMissionBehavior());
 949:         mission.AddMissionBehavior(new SmartCavalryAIMissionBehavior());
 950:         mission.AddMissionBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());
 951: 
 952:         var colorStore = IoC.Resolve<IAgentColorStore>();
 953:         if (colorStore != null)
 954:             mission.AddMissionBehavior(new AgentColorStoreCleanupBehavior(colorStore));
 955: 
 956:         // MissionDiagnostic: added LAST so it sees all behaviors added by TAOM AND
 957:         // every other mod in the load chain. Dumps MissionBehaviors + MissionLogics
 958:         // on first OnMissionTick to taom_debug_*.log so user-uploaded crash logs
 959:         // contain enough data to identify mod-conflict bugs (BehaviorType=Logic +
 960:         // !MissionLogic null-cast offenders) and action-set anomalies.
 961:         var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
 962:         var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
 963:         var diagLogger = IoC.Resolve<IModLogger>();
 964:         if (diagSvc != null && raceMgr != null && diagLogger != null)
 965:             mission.AddMissionBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));
 966: 
 967:         // BattleLoadDiagnostics phase-6: "battle playable" marker on first tick + closes
 968:         // the loading window so the stall watchdog stands down and phase-5 stops logging.
 969:         var battleLoadDiagSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
 970:         if (battleLoadDiagSvc != null && battleLoadDiagSvc.IsEnabled)
 971:             mission.AddMissionBehavior(new Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior(
 972:                 battleLoadDiagSvc, IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>()));
 973: 
 974:         // Dev-trigger behavior watches the CrashReport MCM toggle and throws a tagged
 975:         // TaomDevTriggerException on the next OnMissionTick when the player flips
 976:         // "Throw On Next Mission Tick". QA only — no-op in normal play.
 977:         mission.AddMissionBehavior(new Features.CrashReport.DevTriggers.CrashReportDevTriggerMissionBehavior());
 978: 
 979:         var careerAbilityService = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
 980:         if (careerAbilityService != null && Campaign.Current != null)
 981:         {
 982:             mission.AddMissionBehavior(new Features.CareerSystem.CareerPerkMissionBehavior(
 983:                 IoC.Resolve<ICareerDataService>(),
 984:                 careerAbilityService,
 985:                 IoC.Resolve<Features.CareerSystem.Abilities.IAbilityActivationController>(),
 986:                 IoC.Resolve<Features.CareerSystem.UI.IAbilityHudController>(),
 987:                 IoC.Resolve<Features.CareerSystem.Abilities.IAbilityEffectExecutor>(),
 988:                 IoC.Resolve<Features.CareerSystem.ICareerPassiveService>(),
 989:                 IoC.Resolve<IModLogger>()));
 990:         }
 991:     }
 992: 
 993:     protected override void OnApplicationTick(float dt)
 994:     {
 995:         _timeAccelerationService?.OnTick();
 996: 
 997:         // Shader pre-compilation walk: tick the runner every frame (responsive state transitions),
 998:         // and surface its status as a 1 Hz toast when a loading screen isn't already showing it.
 999:         var runner = _shaderRunner;
1000:         if (runner != null && runner.IsActive)
1001:         {
1002:             runner.Tick();
1003:             _shaderTickAccumulator += dt;
1004:             if (_shaderTickAccumulator >= 1f)
1005:             {
1006:                 _shaderTickAccumulator = 0f;
1007:                 if (!LoadingWindow.IsLoadingWindowActive && !string.IsNullOrEmpty(runner.StatusLine))
1008:                     InformationManager.DisplayMessage(new InformationMessage(runner.StatusLine));
1009:             }
1010:         }
1011:     }
1012: 
1013:     protected override void OnSubModuleUnloaded()
1014:     {
1015:         base.OnSubModuleUnloaded();
1016:         // Detach the AppDomain.UnhandledException subscription BEFORE IoC disposal so
1017:         // the hook doesn't hold a stale reference to a disposed CrashReportService
1018:         // across game-restart-in-same-process. Deep-review INC 3 (2026-05-25).
1019:         try { IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>()?.Unsubscribe(); }
1020:         catch { /* IoC may already be torn down — best-effort */ }
1021: 
1022:         // Reverse NativeSkinFixes hooks so DLL unload during reload-in-same-process
1023:         // doesn't leave dangling MinHook trampolines. Best-effort — swallows.
1024:         try { NativeSkinFixesInstaller.Uninstall(); }
1025:         catch { /* shutdown — never block */ }
1026: 
1027:         _harmony?.UnpatchAll("com.taom.mod");
1028:         IoC.Dispose();
1029: 
1030:         // Codex review #46 (2026-05-25) HIGH-01: clear the static service cache in
1031:         // the patch helper so the next module load resolves a fresh service graph from
1032:         // the new IoC container. Without this, Finalizers fire against a disposed
1033:         // FileLogger after reload and silently drop every log line.
1034:         TAOM.Features.CrashReport.Hooks.CrashReportPatchHelper.ResetForUnload();
1035:     }
1036: }

2026-07-10T17:33:08.170710Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.1 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "ExitWindowOpenedUtcTicks|ExitStallSampler|CloseExitWindow|LogE …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-07-10T17:33:08.170707Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2.1 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "ExcludedTargetNamespacePrefixes|IsExcludedTarget|GetAllPatched …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2060ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "ExitWindowOpenedUtcTicks|ExitStallSampler|CloseExitWindow|LogE …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2061ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "ExcludedTargetNamespacePrefixes|IsExcludedTarget|GetAllPatched …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
`rg` is not installed in this PowerShell environment, so I’m switching to `Select-String`/targeted file reads. The hook-level fix from review 72 is present: both closers now execute before any `IsEnabled` return.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Dependencies/Foundation/PatchShield.cs'; "'$start=170; $end=300; $i=0; Get-Content $p | ForEach-Object { $i++; if($i -ge $start -and $i -le $end){ '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Dependencies/Foundation/PatchShield.cs'; "'$start=300; $end=470; $i=0; Get-Content $p | ForEach-Object { $i++; if($i -ge $start -and $i -le $end){ '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main/SubModule.cs'; "'$start=740; $end=925; $i=0; Get-Content $p | ForEach-Object { $i++; if($i -ge $start -and $i -le $end){ '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main,TAOM.Tests,Dependencies -Recurse -Include *.cs | Select-String -Pattern 'ExitWindowOpenedUtcTicks|ExitStallSampler|CloseExitWindow|LogExitBegin|IsExitWindowActive|Patch43_BattleLoadDiagnostics|SetMainThread' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 538ms:
 300: 
 301:         // Codex A3 LOW fix 2026-05-27: overload-safe dedupe key. Was
 302:         // <DeclaringType>::<methodName> — overloaded methods shared a key, so the
 303:         // second overload's failure would skip cleanup. Now uses
 304:         // <Module.ModuleVersionId>:<MetadataToken> which is unique per method handle.
 305:         string targetKey;
 306:         try
 307:         {
 308:             targetKey = $"{originalMethod.Module.ModuleVersionId}:{originalMethod.MetadataToken}";
 309:         }
 310:         catch
 311:         {
 312:             // Fallback if Module/MetadataToken unavailable for this method handle.
 313:             try { targetKey = originalMethod.ToString(); }
 314:             catch { return; }
 315:         }
 316: 
 317:         lock (_lock)
 318:         {
 319:             if (_unpatched.Contains(targetKey)) return;  // already cleaned
 320:             _unpatched.Add(targetKey);
 321:         }
 322: 
 323:         try
 324:         {
 325:             var patches = Harmony.GetPatchInfo(originalMethod);
 326:             if (patches == null) return;
 327: 
 328:             var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 329:             foreach (var p in patches.Prefixes) if (p != null) owners.Add(p.owner ?? string.Empty);
 330:             foreach (var p in patches.Postfixes) if (p != null) owners.Add(p.owner ?? string.Empty);
 331:             foreach (var p in patches.Transpilers) if (p != null) owners.Add(p.owner ?? string.Empty);
 332:             foreach (var p in patches.Finalizers) if (p != null) owners.Add(p.owner ?? string.Empty);
 333: 
 334:             var harmony = new Harmony(HarmonyId);
 335:             foreach (var owner in owners)
 336:             {
 337:                 if (string.IsNullOrEmpty(owner) || owner == HarmonyId) continue;
 338: 
 339:                 // Refuse to unpatch protected infrastructure owners (Codex S1 HIGH fix
 340:                 // 2026-05-27). Filter now covers TAOM + vendored BUTR/MCM/Harmony.
 341:                 if (IsProtectedOwner(owner))
 342:                 {
 343:                     DiagLog.Log(Tag, $"refusing to unpatch protected owner '{owner}' on {targetKey}");
 344:                     continue;
 345:                 }
 346: 
 347:                 try
 348:                 {
 349:                     harmony.Unpatch(originalMethod, HarmonyPatchType.Prefix, owner);
 350:                     harmony.Unpatch(originalMethod, HarmonyPatchType.Postfix, owner);
 351:                     harmony.Unpatch(originalMethod, HarmonyPatchType.Transpiler, owner);
 352:                     DiagLog.Log(Tag, $"unpatched owner '{owner}' on {targetKey}");
 353: 
 354:                     lock (_ownerLock)
 355:                     {
 356:                         _ownerCounts.TryGetValue(owner, out var count);
 357:                         _ownerCounts[owner] = count + 1;
 358:                     }
 359:                 }
 360:                 catch (Exception unpatchEx)
 361:                 {
 362:                     DiagLog.LogCaught(Tag, $"Unpatch owner='{owner}' on {targetKey}", unpatchEx);
 363:                 }
 364:             }
 365:         }
 366:         catch (Exception ex2)
 367:         {
 368:             DiagLog.LogCaught(Tag, $"TryUnpatchOffendingPatches({targetKey})", ex2);
 369:         }
 370:     }
 371: 
 372:     /// <summary>
 373:     /// Writes a one-line summary of swallow stats. Wire to AppDomain.ProcessExit.
 374:     /// </summary>
 375:     public static void WriteSessionSummary()
 376:     {
 377:         try
 378:         {
 379:             string topOwner = "(none)";
 380:             lock (_ownerLock)
 381:             {
 382:                 if (_ownerCounts.Count > 0)
 383:                 {
 384:                     var top = _ownerCounts.OrderByDescending(k => k.Value).First();
 385:                     topOwner = $"{top.Key} ({top.Value})";
 386:                 }
 387:             }
 388:             DiagLog.Log(Tag,
 389:                 $"SESSION SUMMARY: shielded {ShieldedCount} method(s), unpatched {UnpatchedCount} target(s), " +
 390:                 $"swallowed {SwallowedTotal} exception(s) " +
 391:                 $"(MissingMethod {SwallowedMissingMethod}, MissingField {SwallowedMissingField}, " +
 392:                 $"TypeLoad {SwallowedTypeLoad}, other {SwallowedOther}). " +
 393:                 $"Top unpatched owner: {topOwner}.");
 394:         }
 395:         catch (Exception ex)
 396:         {
 397:             DiagLog.LogCaught(Tag, "WriteSessionSummary", ex);
 398:         }
 399:     }
 400: }

 succeeded in 543ms:
 170:             List<MethodBase> patched;
 171:             try
 172:             {
 173:                 patched = Harmony.GetAllPatchedMethods().ToList();
 174:             }
 175:             catch (Exception ex)
 176:             {
 177:                 DiagLog.LogCaught(Tag, "GetAllPatchedMethods", ex);
 178:                 return;
 179:             }
 180: 
 181:             int added = 0, skipped = 0, alreadyShielded = 0;
 182:             lock (_lock)
 183:             {
 184:                 foreach (var method in patched)
 185:                 {
 186:                     if (method == null) { skipped++; continue; }
 187:                     if (_shielded.Contains(method)) { alreadyShielded++; continue; }
 188: 
 189:                     // Don't shield our own methods.
 190:                     try
 191:                     {
 192:                         var declAsm = method.DeclaringType?.Assembly.GetName().Name ?? string.Empty;
 193:                         if (declAsm.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase))
 194:                         {
 195:                             _shielded.Add(method);
 196:                             skipped++;
 197:                             continue;
 198:                         }
 199:                     }
 200:                     catch { }
 201: 
 202:                     // Never shield hot UI-layer targets — a per-call __originalMethod
 203:                     // finalizer on the Gauntlet prefab system froze tournament exits for
 204:                     // ~107s (#331 round 2). See ExcludedTargetNamespacePrefixes.
 205:                     if (IsExcludedTarget(method))
 206:                     {
 207:                         _shielded.Add(method);
 208:                         skipped++;
 209:                         continue;
 210:                     }
 211: 
 212:                     try
 213:                     {
 214:                         bool isVoid = true;
 215:                         if (method is MethodInfo mi) isVoid = mi.ReturnType == typeof(void);
 216:                         var finalizer = isVoid ? voidFinalizer : resultFinalizer;
 217:                         harmony.Patch(method, prefix: null, postfix: null, transpiler: null,
 218:                             finalizer: new HarmonyMethod(finalizer));
 219:                         _shielded.Add(method);
 220:                         added++;
 221:                     }
 222:                     catch (Exception ex)
 223:                     {
 224:                         skipped++;
 225:                         DiagLog.LogCaught(Tag, $"shielding {method.DeclaringType?.FullName}.{method.Name}", ex);
 226:                     }
 227:                 }
 228:             }
 229: 
 230:             if (added > 0 || alreadyShielded == 0)
 231:             {
 232:                 DiagLog.Log(Tag, $"shield pass: +{added} new, {alreadyShielded} already-shielded, {skipped} skipped (total: {_shielded.Count})");
 233:             }
 234:         }
 235:         catch (Exception ex)
 236:         {
 237:             DiagLog.LogCaught(Tag, "Install", ex);
 238:         }
 239:     }
 240: 
 241:     /// <summary>
 242:     /// Finalizer for void-return methods. Catches the swallow-trinity and returns
 243:     /// silently to suppress the exception; non-matching exceptions are re-thrown by
 244:     /// returning the original exception (Harmony Finalizer convention).
 245:     /// </summary>
 246:     private static Exception? ShieldFinalizerVoid(MethodBase __originalMethod, Exception __exception)
 247:     {
 248:         return ShouldSwallow(__originalMethod, __exception, out var unwrapped) ? null : unwrapped;
 249:     }
 250: 
 251:     /// <summary>
 252:     /// Finalizer for return-value methods. Same swallow behavior; the patched method
 253:     /// returns its zero/default value when we swallow because we don't have access
 254:     /// to <c>__result</c> in a Finalizer (Harmony quirk). Acceptable trade-off:
 255:     /// the caller gets a "stub" return value, which is far better than a crash.
 256:     /// </summary>
 257:     private static Exception? ShieldFinalizerWithResult(MethodBase __originalMethod, Exception __exception)
 258:     {
 259:         return ShouldSwallow(__originalMethod, __exception, out var unwrapped) ? null : unwrapped;
 260:     }
 261: 
 262:     private static bool ShouldSwallow(MethodBase originalMethod, Exception exception, out Exception unwrapped)
 263:     {
 264:         unwrapped = exception;
 265:         if (exception == null) return false;
 266: 
 267:         // Unwrap TargetInvocationException to get at the real reason.
 268:         var ex = exception;
 269:         while (ex is TargetInvocationException && ex.InnerException != null)
 270:             ex = ex.InnerException;
 271:         unwrapped = ex;
 272: 
 273:         if (ex is MissingMethodException || ex is MissingFieldException || ex is TypeLoadException)
 274:         {
 275:             if (ex is MissingMethodException) Interlocked.Increment(ref _swallowedMissingMethod);
 276:             else if (ex is MissingFieldException) Interlocked.Increment(ref _swallowedMissingField);
 277:             else Interlocked.Increment(ref _swallowedTypeLoad);
 278: 
 279:             try
 280:             {
 281:                 var owner = originalMethod?.DeclaringType?.FullName ?? "?";
 282:                 var name = originalMethod?.Name ?? "?";
 283:                 DiagLog.Log(Tag, $"swallowed {ex.GetType().Name} from a patch on {owner}.{name}: {ex.Message}");
 284:             }
 285:             catch { }
 286: 
 287:             TryUnpatchOffendingPatches(originalMethod, ex);
 288:             return true;
 289:         }
 290: 
 291:         // Codex A2 LOW fix 2026-05-27: do NOT increment _swallowedOther here — this
 292:         // path RETHROWS the exception. The counter previously misled WriteSessionSummary
 293:         // into reporting rethrown exceptions as swallowed.
 294:         return false;
 295:     }
 296: 
 297:     private static void TryUnpatchOffendingPatches(MethodBase originalMethod, Exception ex)
 298:     {
 299:         if (originalMethod == null) return;
 300: 

 succeeded in 527ms:
 740:             IoC.Resolve<Features.EliteEmissary.IEliteEmissaryService>(),
 741:             IoC.Resolve<Features.EliteEmissary.IEliteEmissarySettingsProvider>(),
 742:             IoC.Resolve<Features.EliteEmissary.IEliteEmissaryConfigProvider>(),
 743:             IoC.Resolve<ISettlementOwnerAdapter>(),
 744:             IoC.Resolve<IModLogger>()));
 745: 
 746:         // CultureConversion — conquered cross-culture fiefs gradually adopt the new owner's culture
 747:         // (troops, militia, identity). Registered unconditionally so SyncData round-trips conversion
 748:         // records and completed overrides re-apply on load even when the MCM toggle is off.
 749:         campaignStarter.AddBehavior(new Features.CultureConversion.Hooks.CultureConversionBehavior(
 750:             IoC.Resolve<Features.CultureConversion.ICultureConversionService>(),
 751:             IoC.Resolve<Features.CultureConversion.ICultureConversionStore>(),
 752:             IoC.Resolve<IModLogger>()));
 753: 
 754:         // LotrIssues — suppress ALL 43 vanilla procedural issue behaviors (Sandbox registered them
 755:         // before this OnGameStart) and register the single LOTR custom-issue dispatcher in their
 756:         // place. New-campaign feature: a pre-suppression save keeps in-flight vanilla issues until
 757:         // they resolve, since their behaviors are only absent for newly-started campaigns here.
 758:         Features.LotrIssues.LotrIssueSuppression.SuppressAll(campaignStarter, IoC.Resolve<IModLogger>());
 759:         campaignStarter.AddBehavior(new Features.LotrIssues.LotrIssuesCampaignBehavior(
 760:             IoC.Resolve<Features.LotrIssues.ILotrIssueService>(),
 761:             IoC.Resolve<IModLogger>()));
 762:     }
 763: 
 764:     public override void OnGameInitializationFinished(Game game)
 765:     {
 766:         base.OnGameInitializationFinished(game);
 767: 
 768:         // Harmony patches are process-global (applied to methods, persist across games). Apply this
 769:         // whole per-game-init patch block ONCE per process — re-applying on a 2nd game init duplicates
 770:         // every prefix/postfix, restarts the BattleLoad watchdog, and CRASHES the non-idempotent
 771:         // DeliverOffSpring transpiler (chained twice, it can't find its already-NOPped anchor). The
 772:         // shader-precompile walk starts N custom games in one process and tripped exactly this on item 2;
 773:         // a player loading a 2nd campaign/custom-battle in one session hits the same crash.
 774:         // Mirrors _missionTimePatchesApplied in OnMissionBehaviorInitialize.
 775:         if (_gameInitPatchesApplied) return;
 776:         _gameInitPatchesApplied = true;
 777: 
 778:         _harmony.PatchCategory("Patch1_FirstTimeInit");
 779:         _harmony.PatchCategory("Patch2_RefreshTableau");
 780:         _harmony.PatchCategory("Patch3_SetRace");
 781:         _harmony.PatchCategory("Patch4_CharacterSpawner");
 782:         _harmony.PatchCategory("Patch5_FaceGen");
 783:         _harmony.PatchCategory("Late_Transpiler");
 784:         _harmony.PatchCategory("Late_ActionSetOverride");
 785:         _harmony.PatchCategory("Patch6_BannerEditor");
 786:         _harmony.PatchCategory("Patch7_FactionMap");
 787:         _harmony.PatchCategory("Patch9_RaceFilter");
 788:         _harmony.PatchCategory("Patch20_NarrativeHorseGuard");
 789:         _harmony.PatchCategory("Patch8_SiegeCampGuard");
 790:         _harmony.PatchCategory("Patch10_WeatherBoundsGuard");
 791:         _harmony.PatchCategory("Patch11_Diplomacy");
 792:         _harmony.PatchCategory("Patch12_WarOfTheRing");
 793: 
 794:         _harmony.PatchCategory("Patch14_Execution");
 795:         _harmony.PatchCategory("Patch15_BannerLayerLimit");
 796:         _harmony.PatchCategory("Patch16_AtmospherePersistence");
 797:         _harmony.PatchCategory("Patch17_TroopWeight");
 798:         _harmony.PatchCategory("Patch23_BannerColorPersistence");
 799:         _harmony.PatchCategory("Patch24_BannerDriftGuard");
 800:         _harmony.PatchCategory("Patch39_BanditPartySize");
 801:         _harmony.PatchCategory("Patch40_HideoutDescription");
 802:         _harmony.PatchCategory("Patch46_TournamentDwarfDismount");
 803:         // Patch47 RE-ENABLED 2026-06-12 after full exoneration: its 06-12 morning indictment
 804:         // ("post-sever tick AV") was actually the CanAttack charge crash at set_attack_entity
 805:         // (0x6BAB4E), which fired with AND without Patch47 and is fixed in data (LOTRLOME
 806:         // monster Flags). Patch47's own job verified working: severed riders die clean on-foot
 807:         // deaths (act_death_by_arrow class) instead of AVing in the native mounted-death path —
 808:         // which 1.4.6 still does on melee deaths (Die-path AV reading float-bits-as-index from
 809:         // a corrupted action record, debugger-proven 06-12). See docs/features/spider.md.
 810:         Features.Spider.Hooks.Agent_Die_SpiderDismount_Patch.Initialize();
 811:         _harmony.PatchCategory("Patch47_SpiderDeathDismount");
 812: 
 813:         // Patch48: the non-lethal sibling of Patch47. A CanDismount melee hit on a mounted Spider Rider AVs in
 814:         // native HandleBlowAux (reading 0x3) — the same broken non-vanilla mounted-dismount path Patch47 routes
 815:         // around on death. Strips CanDismount for spider riders so the native dismount never fires (the rider
 816:         // stays on the locked mount; damage still applies). Debugger-proven 2026-06-15. See docs/features/spider.md.
 817:         _harmony.PatchCategory("Patch48_SpiderHitDismountGuard");
 818: 
 819:         // Patch50: Finalizer swallowing a vanilla NRE in Agent.CheckToDropFlaggedItem (Agent.cs:3595),
 820:         // reached via the shared synthetic-bite path (CustomAttacksUtils.TakeDamage → RegisterBlow →
 821:         // OnAgentHit → affectedAgent.CheckToDropFlaggedItem) when a warg bites another warg (mount
 822:         // victim with a null wielded Item). Already caught by WargAttackService, but swallowing lets
 823:         // OnAgentHit finish and stops the log spam. Crash report 2026-06-17. See the patch doc-comment.
 824:         _harmony.PatchCategory("Patch50_DropFlaggedItemGuard");
 825: 
 826:         // Patch56_SceneNotificationVisualGuard: Finalizer swallowing a managed NRE in
 827:         // PopupSceneSpawnPoint.InitializeWithAgentVisuals, reached via GauntletSceneNotification.OpenScene
 828:         // when the become-king (or sibling) cinematic builds a character whose human AgentVisuals yields
 829:         // null — the engine derefs the human visual without a null guard (it guards only the mount). The
 830:         // finalizer aborts the cinematic cleanly (HideSceneNotification) so cinematics that CAN render
 831:         // still play. Fourth raw custom-race/visual render path (after Patch55). Crash reports
 832:         // 2026-06-24/25 (become ruler of empire_w/gondor). See the patch doc-comment.
 833:         _harmony.PatchCategory("Patch56_SceneNotificationVisualGuard");
 834: 
 835:         // Patch13_RaceAge — noise reduction (NOT a crash fix). NOPs the harmless
 836:         // mother.Race == father.Race SilentAssert in DeliverOffSpring that fires on every
 837:         // mixed-race birth (normal in TAOM). Stops the debugger break + debug-log spam.
 838:         _harmony.PatchCategory("Patch13_RaceAge");
 839: 
 840:         var resourceHook = IoC.Resolve<IOnPartyUpgradeResourceCheck>();
 841:         var specResLogger = IoC.Resolve<IModLogger>();
 842:         PartyCharacterVM_InitializeUpgrades_Patch.Initialize(resourceHook, specResLogger);
 843:         PartyScreenLogic_UpgradeTroop_Patch.Initialize(resourceHook, specResLogger);
 844:         PartyScreenLogic_AddCommand_Patch.Initialize(resourceHook, specResLogger);
 845:         RecruitmentVM_RecruitGate_Patch.Initialize(IoC.Resolve<IOnRecruitmentResourceGate>(), specResLogger);
 846:         _harmony.PatchCategory("Patch26_SpecialResources");
 847:         _harmony.PatchCategory("Patch51_RecruitmentResourceGate");
 848:         _harmony.PatchCategory("Patch27_CareerSystem");
 849:         _harmony.PatchCategory("Patch29_CCBodyProperties");
 850:         _harmony.PatchCategory("Patch44_CCNameAutofill");
 851:         _harmony.PatchCategory("Patch33_EquipPresets");
 852:         _harmony.PatchCategory("Patch34_QuickActions");
 853:         _harmony.PatchCategory("Patch35_CompanionTactics");
 854:         _harmony.PatchCategory("Patch36_FiefManagement");
 855:         SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(IoC.Resolve<INameplateFadeService>());
 856:         _harmony.PatchCategory("Patch38_SettlementNameplateFade");
 857: 
 858:         // Patch53_PartyIconScale — transpiler that rewrites the two hardcoded 0.3f campaign-map scale
 859:         // literals in MobilePartyVisual.AddCharacterToPartyIcon (leader figure + its mount) into a call
 860:         // to PartyIconScaleConfig.GetScale(), so both honour the MCM "Map Figure Scale" slider
 861:         // (default 0.15 = half vanilla). See docs/features/party-icon-scale.md.
 862:         Features.PartyIconScale.Hooks.Patch53_PartyIconScale.Initialize(IoC.Resolve<IModLogger>());
 863:         _harmony.PatchCategory("Patch53_PartyIconScale");
 864: 
 865:         // NavalTravel PARKED 2026-06-26 (#296/#120) — see the model-registration comment in OnGameStart.
 866:         // Patch54 (boat visual) + Patch57 (at-sea native-AV crash guard) are only meaningful while a party
 867:         // can be at sea, which only the (now-unregistered) TaomPartyNavigationModel enables — so neither is
 868:         // applied while the feature is parked. RE-ENABLE: uncomment both blocks with the model registration.
 869:         // Patch54_NavalTravelBoatVisual — render an at-sea party as a boat (base game renders no ship at sea).
 870:         // Features.NavalTravel.Hooks.Patch54_NavalTravelBoatVisual.Initialize(IoC.Resolve<Features.NavalTravel.INavalTravelService>(), IoC.Resolve<IModLogger>());
 871:         // _harmony.PatchCategory("Patch54_NavalTravelBoatVisual");
 872:         //
 873:         // Patch57_NavalAtSeaLandRescueGuard — prevent the native AV CTD on the hourly AI tick (the vanilla
 874:         // AIMoveToNearestLandBehavior's native cross-region pathfind AVs on TAOM_Map's missing naval navmesh,
 875:         // #120). Only fires for an at-sea party, which can't happen while the model is unregistered.
 876:         // var navalRescueLogger = IoC.Resolve<IModLogger>();
 877:         // Features.NavalTravel.Hooks.Patch57_NavalAtSeaLandRescueGuard.Initialize(IoC.Resolve<Features.NavalTravel.INavalTravelService>(), navalRescueLogger);
 878:         // try { _harmony.PatchCategory("Patch57_NavalAtSeaLandRescueGuard"); }
 879:         // catch (System.Exception ex) { navalRescueLogger.LogWarning($"[NavalTravel] Patch57 at-sea rescue guard failed to apply: {ex.Message}"); }
 880: 
 881:         // BattleLoadDiagnostics — phase-stamp the attack->battle-playable lifecycle so an
 882:         // intermittent battle-load hang leaves a log whose last line names the stuck phase
 883:         // (and, for the equipment phase, the agent + the item whose bo_ collision mesh is
 884:         // missing). The background stall watchdog auto-triggers a crash bundle on a freeze.
 885:         var battleLoadSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
 886:         var equipSnapshotAdapter = IoC.Resolve<IEquipmentSnapshotAdapter>();
 887:         var battleLoadStallMarker = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>();
 888:         Features.BattleLoadDiagnostics.Hooks.PlayerEncounter_Start_Patch.Initialize(battleLoadSvc);
 889:         Features.BattleLoadDiagnostics.Hooks.MissionState_OpenNew_Patch.Initialize(battleLoadSvc);
 890:         Features.BattleLoadDiagnostics.Hooks.BattleSceneSelection_Patch.Initialize(battleLoadSvc);
 891:         Features.BattleLoadDiagnostics.Hooks.Mission_Initialize_BattleLoad_Patch.Initialize(battleLoadSvc, battleLoadStallMarker);
 892:         Features.BattleLoadDiagnostics.Hooks.Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.Initialize(battleLoadSvc, equipSnapshotAdapter);
 893:         // Exit-phase probes (issue #331 — 30s-2min hang exiting tournaments): stamp the
 894:         // mission end -> map resume window so the dominant phase gap names the time sink.
 895:         Features.BattleLoadDiagnostics.Hooks.Mission_EndMission_ExitPhase_Patch.Initialize(battleLoadSvc);
 896:         Features.BattleLoadDiagnostics.Hooks.Mission_EndMissionInternal_ExitPhase_Patch.Initialize(battleLoadSvc);
 897:         Features.BattleLoadDiagnostics.Hooks.Mission_ClearUnreferencedResources_ExitPhase_Patch.Initialize(battleLoadSvc);
 898:         Features.BattleLoadDiagnostics.Hooks.MissionState_OnFinalize_ExitPhase_Patch.Initialize(battleLoadSvc);
 899:         Features.BattleLoadDiagnostics.Hooks.MapState_OnActivate_ExitPhase_Patch.Initialize(battleLoadSvc);
 900:         Features.BattleLoadDiagnostics.Hooks.MapState_OnTick_ExitPhase_Patch.Initialize(battleLoadSvc);
 901:         _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
 902:         IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();
 903: 
 904:         // Exit-stall stack sampler (#331 round 2): OnGameInitializationFinished runs on the
 905:         // game's main thread — the same thread the tournament-exit stall freezes — so this
 906:         // is a valid capture point for the sampler's main-thread reference.
 907:         var exitStallSampler = IoC.Resolve<Features.BattleLoadDiagnostics.ExitStallSampler>();
 908:         exitStallSampler.SetMainThread(System.Threading.Thread.CurrentThread);
 909:         exitStallSampler.Start();
 910: 
 911:         // Patch60 — release the tournament UI movie/layer at OnEndMission time. The engine's
 912:         // MissionGauntletTournamentView leaks both (nulls without release, unlike the practice
 913:         // view), deferring the Tournament-movie teardown into ScreenBase.HandleFinalize under
 914:         // the exit loading screen, where an in-flight prize tableau render stalls it ~108s (#331).
 915:         var tournamentExitLogger = IoC.Resolve<IModLogger>();
 916:         Features.Arena.Hooks.Patch60_TournamentExitMovieRelease.Initialize(tournamentExitLogger);
 917:         try { _harmony.PatchCategory("Patch60_TournamentExitMovieRelease"); }
 918:         catch (System.Exception ex) { tournamentExitLogger.LogWarning($"[Arena] Patch60 tournament-exit movie release failed to apply: {ex.Message}"); }
 919: 
 920:         // Manual patches for PRIVATE engine methods (AccessTools-resolved targets; can't use
 921:         // [HarmonyPatch] attribute binding + PatchCategory). Extracted verbatim to
 922:         // ManualPatchApplicator (ADR-002); apply order unchanged, each fail-safes with a warning.
 923:         ManualPatchApplicator.ApplyAll(_harmony);
 924:     }
 925: 

 succeeded in 1638ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\BattleSceneSelection_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\MapState_OnActivate_ExitPhase_Patch.cs:14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\MapState_OnActivate_ExitPhase_Patch.cs:25: if (svc == null || !svc.IsExitWindowActive) return;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\MapState_OnTick_ExitPhase_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\MapState_OnTick_ExitPhase_Patch.cs:23: if (svc == null || !svc.IsExitWindowActive) return;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\Mission_ClearUnreferencedResources_ExitPhase_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\Mission_EndMission_ExitPhase_Patch.cs:12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\Mission_EndMission_ExitPhase_Patch.cs:40: if (svc.IsExitWindowActive && hash == _lastStampedMissionHash) return;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\Mission_EndMission_ExitPhase_Patch.cs:47: svc.LogExitBegin(missionName, sceneName, agents, allAgents);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\Mission_EndMissionInternal_ExitPhase_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\Mission_Initialize_BattleLoad_Patch.cs:10: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OnFinalize_ExitPhase_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\MissionState_OpenNew_Patch.cs:15: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:16: container.Register<ExitStallSampler>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:21: private long _exitWindowOpenedUtcTicks; // 0 = closed; read via Interlocked (feeds ExitStallSampler)
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:41: CloseExitWindow();
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:78: CloseExitWindow();
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:116: public bool IsExitWindowActive => _exitWindowActive;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:118: public long ExitWindowOpenedUtcTicks => Interlocked.Read(ref _exitWindowOpenedUtcTicks);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:120: public void LogExitBegin(string missionName, string sceneName, int agentCount, int allAgentCount)
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:128: Interlocked.Exchange(ref _exitWindowOpenedUtcTicks, DateTime.UtcNow.Ticks);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:132: catch (Exception ex) { SafeWarn("LogExitBegin", ex); }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:183: CloseExitWindow();
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:188: private void CloseExitWindow()
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:191: Interlocked.Exchange(ref _exitWindowOpenedUtcTicks, 0L);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:12: // LogExitBegin, closed by FirstMapTick/ResetLifecycle/MissionInitialize), this samples the
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:22: public sealed class ExitStallSampler : IDisposable
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:42: public ExitStallSampler(
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:53: public void SetMainThread(Thread mainThread) => _mainThread = mainThread;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:72: long windowTicks = _service.ExitWindowOpenedUtcTicks;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsService.cs:47: bool IsExitWindowActive { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsService.cs:51: long ExitWindowOpenedUtcTicks { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsService.cs:53: void LogExitBegin(string missionName, string sceneName, int agentCount, int allAgentCount);
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:901: _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:907: var exitStallSampler = IoC.Resolve<Features.BattleLoadDiagnostics.ExitStallSampler>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:908: exitStallSampler.SetMainThread(System.Threading.Thread.CurrentThread);
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:909: exitStallSampler.Start();
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:131: _sut.LogExitBegin("TournamentFight", "arena_x", 20, 230);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:145: public void LogExitBegin_WhenEnabled_EmitsMissionSceneAgentsAndGcStats()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:147: _sut.LogExitBegin("TournamentFight", "arena_sturgia_a", 24, 234);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:155: public void LogExitBegin_WhenDisabled_WritesNothingAndWindowStaysClosed()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:158: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:160: Assert.IsFalse(_sut.IsExitWindowActive);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:164: public void LogExitBegin_OpensExitWindow()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:166: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:167: Assert.IsTrue(_sut.IsExitWindowActive);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:171: public void LogExitBegin_RestartsSequenceCounter()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:176: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:198: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:213: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:222: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:232: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:235: Assert.IsFalse(_sut.IsExitWindowActive);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:244: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:246: Assert.IsFalse(_sut.IsExitWindowActive);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:255: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:260: Assert.IsFalse(_sut.IsExitWindowActive);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:266: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:272: Assert.IsFalse(_sut.IsExitWindowActive);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:281: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:285: Assert.IsFalse(_sut.IsExitWindowActive);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:288: // ---- ExitWindowOpenedUtcTicks (feeds ExitStallSampler, #331 round 2) ----
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:293: public void LogExitBegin_SetsExitWindowOpenedTicks()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:295: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:296: Assert.AreNotEqual(0L, _sut.ExitWindowOpenedUtcTicks);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:302: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:304: Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:310: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:313: Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:319: _sut.LogExitBegin("m", "s", 1, 1);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:321: Assert.AreEqual(0L, _sut.ExitWindowOpenedUtcTicks);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:12: public class ExitStallSamplerTests
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:19: Assert.IsFalse(ExitStallSampler.ShouldSample(elapsedSeconds: 14.9, samplesTaken: 0));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:25: Assert.IsTrue(ExitStallSampler.ShouldSample(15.0, 0));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:26: Assert.IsFalse(ExitStallSampler.ShouldSample(15.0, 1), "second sample must wait for its own threshold");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:27: Assert.IsTrue(ExitStallSampler.ShouldSample(30.0, 1));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:28: Assert.IsFalse(ExitStallSampler.ShouldSample(30.0, 2), "third sample must wait for its own threshold");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:29: Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 2));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:35: Assert.IsFalse(ExitStallSampler.ShouldSample(999.0, 3));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:43: Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 0));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:44: Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 1));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:45: Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 2));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:46: Assert.IsFalse(ExitStallSampler.ShouldSample(60.0, 3));

codex
PatchShield now skips excluded targets by adding them to `_shielded` and continuing before the finalizer patch, so repeated install passes will not revisit them unless Harmony returns a different `MethodBase` instance for the same method. I’m checking Harmony’s registry semantics next because that affects whether the skip is stable.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'Main/Features/BattleLoadDiagnostics/Hooks/Mission_EndMission_ExitPhase_Patch.cs','Main/Features/BattleLoadDiagnostics/Hooks/Mission_EndMissionInternal_ExitPhase_Patch.cs','Main/Features/BattleLoadDiagnostics/Hooks/Mission_ClearUnreferencedResources_ExitPhase_Patch.cs','Main/Features/BattleLoadDiagnostics/Hooks/MissionState_OnFinalize_ExitPhase_Patch.cs','Main/Features/BattleLoadDiagnostics/Hooks/MapState_OnActivate_ExitPhase_Patch.cs','Main/Features/BattleLoadDiagnostics/Hooks/MapState_OnTick_ExitPhase_Patch.cs','Main/Features/BattleLoadDiagnostics/Hooks/BattleLoadPhaseBehavior.cs'); foreach("'$p in $files){ $i=0; Write-Output "--- $p"; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Dependencies,Main -Recurse -Include *.cs | Select-String -Pattern 'PatchShield.Install|OnSubModuleLoad|OnGameInitializationFinished|AliasStubSubModule|IncompatibleModDetector|OnBeforeInitialModuleScreenSetAsRoot' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path . -Recurse -Include *.csproj,packages.config | Select-String -Pattern 'Lib.Harmony|TargetFramework|net472' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-ChildItem -Path . -Recurse -Filter 0Harmony.dll -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName' in C:\Users\mikew\source\repos\TAOM
 succeeded in 575ms:
--- Main/Features/BattleLoadDiagnostics/Hooks/Mission_EndMission_ExitPhase_Patch.cs
   1: using System.Runtime.CompilerServices;
   2: using HarmonyLib;
   3: using TaleWorlds.MountAndBlade;
   4: 
   5: namespace TAOM.Features.BattleLoadDiagnostics.Hooks;
   6: 
   7: // Exit phase 1 (issue #331) — Mission.EndMission is the single marker the engine sets when a
   8: // mission decides to end (state -> EndingNextFrame); the actual teardown runs on the next
   9: // frame. Opens the exit window and stamps T0 with mission/scene/agent-count + GC stats so
  10: // the later phase deltas localize the tournament-exit hang.
  11: [HarmonyPatch(typeof(Mission), nameof(Mission.EndMission))]
  12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
  13: public static class Mission_EndMission_ExitPhase_Patch
  14: {
  15:     private static IBattleLoadDiagnosticsService? _service;
  16: 
  17:     // EndMission can be re-invoked for the same mission while it winds down; re-stamping
  18:     // would restart the exit stopwatch mid-exit and corrupt the deltas. Track the last
  19:     // stamped instance by identity hash (an int — never a strong ref that would keep the
  20:     // torn-down Mission graph alive).
  21:     private static int _lastStampedMissionHash;
  22: 
  23:     public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
  24: 
  25:     [HarmonyPostfix]
  26:     public static void Postfix(Mission __instance)
  27:     {
  28:         var svc = _service;
  29:         if (svc == null || !svc.IsEnabled) return;
  30:         try
  31:         {
  32:             // Campaign-scoped by design: the exit lifecycle ends at MapState activation,
  33:             // which custom battles don't have — opening the window there would leak it
  34:             // until the next campaign map activate and stamp a spurious MapResumed pair
  35:             // (deep-review data-flow finding, 2026-07-06). Same guard precedent as
  36:             // MissionState_OpenNew_Patch.
  37:             if (TaleWorlds.CampaignSystem.Campaign.Current == null) return;
  38: 
  39:             int hash = RuntimeHelpers.GetHashCode(__instance);
  40:             if (svc.IsExitWindowActive && hash == _lastStampedMissionHash) return;
  41:             _lastStampedMissionHash = hash;
  42: 
  43:             string missionName = MissionState.Current?.MissionName ?? "<unknown>";
  44:             string sceneName = __instance.SceneName ?? "<unknown>";
  45:             int agents = __instance.Agents?.Count ?? -1;
  46:             int allAgents = __instance.AllAgents?.Count ?? -1;
  47:             svc.LogExitBegin(missionName, sceneName, agents, allAgents);
  48:         }
  49:         catch { /* diagnostic only */ }
  50:     }
  51: }
--- Main/Features/BattleLoadDiagnostics/Hooks/Mission_EndMissionInternal_ExitPhase_Patch.cs
   1: using HarmonyLib;
   2: using TaleWorlds.MountAndBlade;
   3: 
   4: namespace TAOM.Features.BattleLoadDiagnostics.Hooks;
   5: 
   6: // Exit phase 2 (issue #331) — the private Mission.EndMissionInternal runs the managed
   7: // teardown: every behavior's OnEndMission/OnEndMissionInternal, agent OnRemove/OnDelete,
   8: // mission-object OnEndMission, then FreeResources + the native FinalizeMission call. The
   9: // Begin->Done delta is the whole managed-teardown + native-finalize window.
  10: [HarmonyPatch(typeof(Mission), "EndMissionInternal")]
  11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
  12: public static class Mission_EndMissionInternal_ExitPhase_Patch
  13: {
  14:     private static IBattleLoadDiagnosticsService? _service;
  15: 
  16:     public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
  17: 
  18:     [HarmonyPrefix]
  19:     public static void Prefix()
  20:     {
  21:         try { _service?.LogExitTeardownBegin(); }
  22:         catch { /* diagnostic only */ }
  23:     }
  24: 
  25:     [HarmonyPostfix]
  26:     public static void Postfix()
  27:     {
  28:         try { _service?.LogExitTeardownDone(); }
  29:         catch { /* diagnostic only */ }
  30:     }
  31: }
--- Main/Features/BattleLoadDiagnostics/Hooks/Mission_ClearUnreferencedResources_ExitPhase_Patch.cs
   1: using HarmonyLib;
   2: using TaleWorlds.MountAndBlade;
   3: 
   4: namespace TAOM.Features.BattleLoadDiagnostics.Hooks;
   5: 
   6: // Exit phase 5 (issue #331) — Mission.ClearUnreferencedResources runs Common.MemoryCleanupGC()
   7: // (a forced full GC) and, when forceClearGPUResources, the native MBAPI ClearResources call.
   8: // It fires at mission LOAD too; the service's exit-window gate keeps those calls silent, so
   9: // only the exit-time invocation (from Mission.OnMissionStateFinalize) is stamped. The
  10: // Begin->Done delta isolates "the hang is the mission-end GC / GPU resource clear".
  11: [HarmonyPatch(typeof(Mission), nameof(Mission.ClearUnreferencedResources))]
  12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
  13: public static class Mission_ClearUnreferencedResources_ExitPhase_Patch
  14: {
  15:     private static IBattleLoadDiagnosticsService? _service;
  16: 
  17:     public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
  18: 
  19:     [HarmonyPrefix]
  20:     public static void Prefix(bool forceClearGPUResources)
  21:     {
  22:         try { _service?.LogExitResourceClearBegin(forceClearGPUResources); }
  23:         catch { /* diagnostic only */ }
  24:     }
  25: 
  26:     [HarmonyPostfix]
  27:     public static void Postfix()
  28:     {
  29:         try { _service?.LogExitResourceClearDone(); }
  30:         catch { /* diagnostic only */ }
  31:     }
  32: }
--- Main/Features/BattleLoadDiagnostics/Hooks/MissionState_OnFinalize_ExitPhase_Patch.cs
   1: using HarmonyLib;
   2: using TaleWorlds.MountAndBlade;
   3: 
   4: namespace TAOM.Features.BattleLoadDiagnostics.Hooks;
   5: 
   6: // Exit phases 3/4 (issue #331) — MissionState.OnFinalize wraps Mission.OnMissionStateFinalize:
   7: // per-behavior OnMissionStateFinalized, the RemoveMissionBehavior loop (every OnRemoveBehavior),
   8: // and ClearUnreferencedResources (forced GC + native GPU clear when NeedsMemoryCleanup). This
   9: // fires when GameStateManager pops the MissionState — i.e. inside the exit loading screen.
  10: [HarmonyPatch(typeof(MissionState), "OnFinalize")]
  11: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
  12: public static class MissionState_OnFinalize_ExitPhase_Patch
  13: {
  14:     private static IBattleLoadDiagnosticsService? _service;
  15: 
  16:     public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
  17: 
  18:     [HarmonyPrefix]
  19:     public static void Prefix()
  20:     {
  21:         try { _service?.LogExitStateFinalizeBegin(); }
  22:         catch { /* diagnostic only */ }
  23:     }
  24: 
  25:     [HarmonyPostfix]
  26:     public static void Postfix()
  27:     {
  28:         try { _service?.LogExitStateFinalizeDone(); }
  29:         catch { /* diagnostic only */ }
  30:     }
  31: }
--- Main/Features/BattleLoadDiagnostics/Hooks/MapState_OnActivate_ExitPhase_Patch.cs
   1: using HarmonyLib;
   2: using TaleWorlds.CampaignSystem;
   3: using TaleWorlds.CampaignSystem.GameState;
   4: 
   5: namespace TAOM.Features.BattleLoadDiagnostics.Hooks;
   6: 
   7: // Exit phase 6 (issue #331) — MapState.OnActivate fires when the campaign map becomes the
   8: // active state again after the MissionState pops: the exit loading screen is effectively
   9: // over. Stamps GC stats (delta vs ExitBegin exposes the mission-end full GC) and whether a
  10: // save is running (SaveHandler.IsSaving — a save inside the exit window means the hang is
  11: // save-time, not teardown). Also fires at campaign start/load; the service's exit-window
  12: // gate keeps those silent.
  13: [HarmonyPatch(typeof(MapState), "OnActivate")]
  14: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
  15: public static class MapState_OnActivate_ExitPhase_Patch
  16: {
  17:     private static IBattleLoadDiagnosticsService? _service;
  18: 
  19:     public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
  20: 
  21:     [HarmonyPostfix]
  22:     public static void Postfix()
  23:     {
  24:         var svc = _service;
  25:         if (svc == null || !svc.IsExitWindowActive) return;
  26:         try
  27:         {
  28:             bool isSaving = Campaign.Current?.SaveHandler?.IsSaving ?? false;
  29:             svc.LogMapResumed(isSaving);
  30:         }
  31:         catch { /* diagnostic only */ }
  32:     }
  33: }
--- Main/Features/BattleLoadDiagnostics/Hooks/MapState_OnTick_ExitPhase_Patch.cs
   1: using HarmonyLib;
   2: using TaleWorlds.CampaignSystem;
   3: using TaleWorlds.CampaignSystem.GameState;
   4: 
   5: namespace TAOM.Features.BattleLoadDiagnostics.Hooks;
   6: 
   7: // Exit phase 7 (issue #331) — the first MapState.OnTick after MapResumed closes the exit
   8: // window: the MapResumed->FirstMapTick delta captures menu/VM re-init cost after activation.
   9: // HOT PATH: this postfix runs on every map frame forever, so the inactive-window early-out
  10: // is the first statement and does no work beyond two reads (per harmony-patches.md).
  11: [HarmonyPatch(typeof(MapState), "OnTick")]
  12: [HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
  13: public static class MapState_OnTick_ExitPhase_Patch
  14: {
  15:     private static IBattleLoadDiagnosticsService? _service;
  16: 
  17:     public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;
  18: 
  19:     [HarmonyPostfix]
  20:     public static void Postfix()
  21:     {
  22:         var svc = _service;
  23:         if (svc == null || !svc.IsExitWindowActive) return;
  24:         try
  25:         {
  26:             bool isSaving = Campaign.Current?.SaveHandler?.IsSaving ?? false;
  27:             svc.LogFirstMapTick(isSaving); // closes the exit window
  28:         }
  29:         catch { /* diagnostic only */ }
  30:     }
  31: }
--- Main/Features/BattleLoadDiagnostics/Hooks/BattleLoadPhaseBehavior.cs
   1: using TaleWorlds.MountAndBlade;
   2: 
   3: namespace TAOM.Features.BattleLoadDiagnostics.Hooks;
   4: 
   5: // Phase 6 — first OnMissionTick "battle playable" marker, then closes the loading window
   6: // so the stall watchdog stands down and phase-5 stops logging reinforcement spawns.
   7: // Inherits MissionLogic (NOT just MissionBehavior) per
   8: // feedback_missionbehaviortype_logic_requires_missionlogic_inheritance — a BehaviorType=Logic
   9: // behavior that isn't a MissionLogic null-casts and NREs every tick in CheckMissionEnded.
  10: public sealed class BattleLoadPhaseBehavior : MissionLogic
  11: {
  12:     private readonly IBattleLoadDiagnosticsService _service;
  13:     private readonly IBattleLoadStallMarker _stallMarker;
  14:     private bool _playableLogged;
  15: 
  16:     public BattleLoadPhaseBehavior(IBattleLoadDiagnosticsService service, IBattleLoadStallMarker stallMarker)
  17:     {
  18:         _service = service;
  19:         _stallMarker = stallMarker;
  20:     }
  21: 
  22:     public override void OnMissionTick(float dt)
  23:     {
  24:         if (_playableLogged) return;
  25:         _playableLogged = true;
  26:         try
  27:         {
  28:             var mission = Mission;
  29:             _service.LogBattlePlayable(mission?.SceneName ?? "<null>", mission?.Agents?.Count ?? 0);
  30:         }
  31:         catch { /* diagnostic only */ }
  32:         finally
  33:         {
  34:             // Reaching a tick means the load finished — stop watching + clear the inflight
  35:             // marker so no stall notice fires next session.
  36:             BattleLoadLoadingWindow.Close();
  37:             try { _stallMarker?.ClearInflight(); } catch { /* best-effort */ }
  38:         }
  39:     }
  40: 
  41:     public override void OnEndMissionInternal()
  42:     {
  43:         _playableLogged = false;
  44:         BattleLoadLoadingWindow.Close();
  45:         try { _stallMarker?.ClearInflight(); } catch { /* best-effort */ }
  46:     }
  47: }

2026-07-10T17:33:36.504143Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Dependencies\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\TAOM.Tests\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Dependencies\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\TAOM.Tests\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Dependencies\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\TAOM.Tests\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\lib\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\Dependencies\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\Dependencies\bin\Release\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\bin\Debug\net472\0Harmony.dll

 exited 1 in 813ms:
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Dependencies\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\TAOM.Tests\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Dependencies\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\TAOM.Tests\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Dependencies\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\TAOM.Tests\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\lib\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\Dependencies\bin\Debug\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\Dependencies\bin\Release\net472\0Harmony.dll
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\bin\Debug\net472\0Harmony.dll

 succeeded in 1097ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtender.cs:82: /// Should be called during `OnSubModuleLoad`
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtender.cs:98: /// Should be called during `OnSubModuleLoad`
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\IncompatibleModDetector.cs:23: /// of BetaDeps.Foundation.IncompatibleModDetector. The XML auto-modify path is
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\IncompatibleModDetector.cs:26: public static class IncompatibleModDetector
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\IncompatibleModDetector.cs:28: private const string Tag = "IncompatibleModDetector";
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:30: /// OnSubModuleLoad or OnBeforeInitialModuleScreenSetAsRoot.
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:151: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\SaveShield.cs:155: /// Call from a late-lifecycle hook (e.g., Dependencies/SubModule OnSubModuleLoad
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\VersionProbe.cs:10: /// reports and (future) by IncompatibleModDetector for version-conditional rules.
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:18: /// BetaDeps.Foundation.AliasStubSubModule. Future phases will append additional
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:19: /// shield installs (IncompatibleModDetector.RunEarlyPhase,
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:23: public class AliasStubSubModule : MBSubModuleBase
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:29: public AliasStubSubModule()
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:46: TrySwallow(IncompatibleModDetector.RunEarlyPhase, "ctor/IncompatEarly");
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:54: protected override void OnSubModuleLoad()
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:56: base.OnSubModuleLoad();
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:57: // Defence-in-depth: re-install in OnSubModuleLoad in case the ctor path
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:60: TrySwallow(SubModule.InstallAssemblyResolveHandler, "OnSubModuleLoad/AssemblyResolve");
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:62: () => DiagLog.Log(Tag, $"alias stub OnSubModuleLoad complete: {GetType().Assembly.GetName().Name}"),
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:63: "OnSubModuleLoad/Log");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:77: /// the four AliasStubSubModule ctors (which fire earlier when the stub modules
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:121: protected override void OnSubModuleLoad()
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:123: base.OnSubModuleLoad();
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:124: DiagLog.Log("Dependencies", "OnSubModuleLoad: entered");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:126: DiagLog.Log("Dependencies", $"OnSubModuleLoad: Harmony assembly = {typeof(Harmony).Assembly.GetName().Name} v{typeof(Harmony).Assembly.GetName().Version}");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:130: DiagLog.Log("Dependencies", "OnSubModuleLoad: applying Harmony guards");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:132: DiagLog.Log("Dependencies", "OnSubModuleLoad: Harmony guards applied OK");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:137: DiagLog.LogCaught("Dependencies", "OnSubModuleLoad/ApplyHarmonyGuards", ex);
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:141: DiagLog.Log("Dependencies", "OnSubModuleLoad: checking for duplicate Harmony");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:151: DiagLog.Log("Dependencies", "OnSubModuleLoad: forcing UIExtenderEx static cctor");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:153: DiagLog.Log("Dependencies", "OnSubModuleLoad: UIExtenderEx static cctor done");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:158: DiagLog.LogCaught("Dependencies", "OnSubModuleLoad/UIExtenderEx", ex);
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:164: // called from AliasStubSubModule.ctor, but observed 2026-05-27 — the launcher
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:165: // never constructs AliasStubSubModule because the alias stub module folders
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:167: // own bin/, not duplicated to each stub). OnSubModuleLoad fires for TAOM.Dependencies's
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:169: DiagLog.Log("Dependencies", "OnSubModuleLoad: installing defensive shields");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:172: // supposed to fire from stub ctors (very early), now fires at OnSubModuleLoad
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:173: // (~642ms later — acceptable; pre-OnSubModuleLoad crashes can't be diagnosed by
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:175: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → IncompatibleModDetector.RunEarlyPhase"); IncompatibleModDetector.RunEarlyPhase(); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:176: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "IncompatibleModDetector.RunEarlyPhase", ex); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:178: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → CollectAssemblyTypesShim.Install"); CollectAssemblyTypesShim.Install(); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:181: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → SubModuleConstructionGuard.Install"); SubModuleConstructionGuard.Install(); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:187: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → SaveShield.Install"); SaveShield.Install(); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:195: DiagLog.Log("Dependencies", "OnSubModuleLoad: → VersionProbe (triggering detection)");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:197: DiagLog.Log("Dependencies", $"OnSubModuleLoad: VersionProbe.IsDetected={detected} (Major={VersionProbe.Major}, Minor={VersionProbe.Minor}, Revision={VersionProbe.Revision})");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:209: DiagLog.Log("Dependencies", "OnSubModuleLoad: ProcessExit hook for session summary registered");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:213: DiagLog.Log("Dependencies", "OnSubModuleLoad: complete");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:214: EarlyLog.Info("[TAOM.Dependencies] OnSubModuleLoad complete");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:220: /// last-good). Override of MBSubModuleBase.OnGameInitializationFinished, the
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:223: public override void OnGameInitializationFinished(Game game)
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:225: base.OnGameInitializationFinished(game);
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:226: DiagLog.Log("Dependencies", "OnGameInitializationFinished: entered (main menu reached)");
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:228: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → MarkSessionLaunchSuccessful"); IncompatibleModDetector.MarkSessionLaunchSuccessful(); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:232: // lifecycle event (after our OnSubModuleLoad).
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:236: DiagLog.Log("Dependencies", "OnGameInitializationFinished: complete");
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\IBattleLoadStallMarker.cs:8: // that gap by mirroring Dependencies/Foundation/IncompatibleModDetector's pattern:
C:\Users\mikew\source\repos\TAOM\Main\Features\CharacterSelection\Patches\RefreshCharacterEntityAuxPatch.cs:31: // PatchCategory("Late_Transpiler") time crashed the mod during OnGameInitializationFinished,
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:19: // Mission.Current.CurrentTime — null during OnSubModuleLoad / OnGameInitializationFinished.
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\AppDomainExceptionHook.cs:39: // Subscribe() is called from SubModule.OnSubModuleLoad on the main game thread,
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:25: // MUST register FIRST in SubModule.OnSubModuleLoad to maximise coverage of
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:26: // other mods' OnSubModuleLoad throws. See docs/features/crash-report.md for the
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:113: [HarmonyPatch(typeof(MBSubModuleBase), "OnSubModuleLoad")]
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:115: public static class MBSubModuleBaseOnSubModuleLoadFinalizer
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\Hooks\Patch37_CrashReport.cs:119: => CrashReportPatchHelper.HandleAndSwallow(__exception, "TaleWorlds.MountAndBlade.MBSubModuleBase.OnSubModuleLoad");
C:\Users\mikew\source\repos\TAOM\Main\Features\CrashReport\CrashReportIoC.cs:63: // Hooks — registered as singletons so SubModule.OnSubModuleLoad can wire them
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\BasicCharacterTableau_RefreshCharacterTableau_Patch.cs:30: /// <c>SubModule.OnBeforeInitialModuleScreenSetAsRoot</c>, NOT the sibling CharacterTableau patches'
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\BasicCharacterTableau_RefreshCharacterTableau_Patch.cs:31: /// <c>Patch2_RefreshTableau</c>. Those are applied in <c>OnGameInitializationFinished</c> (campaign
C:\Users\mikew\source\repos\TAOM\Main\Features\HeroRace\Hooks\GauntletSceneNotification_OpenScene_Guard_Patch.cs:16: /// registered in <c>SubModule.OnGameInitializationFinished</c>, not the cold-menu site Patch55 uses). The
C:\Users\mikew\source\repos\TAOM\Main\Features\MainMenuCustomizer\ModuleMenuAdapter.cs:27: // option was registered and fully initialized before OnBeforeInitialModuleScreenSetAsRoot fires.
C:\Users\mikew\source\repos\TAOM\Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:22: /// Timing: registered from <c>SubModule.OnSubModuleLoad</c>, which completes for all modules before
C:\Users\mikew\source\repos\TAOM\Main\Features\Mcm\Hooks\Patch41_McmLayoutFix.cs:23: /// MCM's <c>ResourceInjector.Inject()</c> runs at <c>OnBeforeInitialModuleScreenSetAsRoot</c> — so the
C:\Users\mikew\source\repos\TAOM\Main\Features\NativeSkinFixes\NativeSkinFixesInstaller.cs:15: /// suppression). Called once from <c>TaomSubModule.OnBeforeInitialModuleScreenSetAsRoot</c>
C:\Users\mikew\source\repos\TAOM\Main\Features\SmartCavalryAI\Hooks\Patch31_FormationSetMovementOrder.cs:26: // Mission.Current.CurrentTime — null during OnSubModuleLoad / OnGameInitializationFinished.
C:\Users\mikew\source\repos\TAOM\Main\ManualPatchApplicator.cs:14: /// engine method has moved. Extracted verbatim from <c>SubModule.OnGameInitializationFinished</c>
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:102: protected override void OnSubModuleLoad()
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:104: base.OnSubModuleLoad();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:109: // after IoC.Configure() so its Finalizers cover the rest of OnSubModuleLoad
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:141: // MUST be applied here in OnSubModuleLoad: MCM's ResourceInjector.Inject() runs at
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:142: // OnBeforeInitialModuleScreenSetAsRoot (after every module's OnSubModuleLoad), so the Postfix
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:172: // in OnSubModuleLoad (process-static one-shot) — NOT the late OnGameInitializationFinished batch —
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:187: // HIGH). Applied here in OnSubModuleLoad like Patch58: loads are triggered from the main
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:240: // Remaining patches applied in OnGameInitializationFinished — View assembly must be initialized first
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:293: // which is null during OnSubModuleLoad and would crash JIT prep with NRE.
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:326: // dependency, safe in OnSubModuleLoad). The transpilers swap the AI IsCastle gate to a runtime
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:338: protected override void OnBeforeInitialModuleScreenSetAsRoot()
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:340: base.OnBeforeInitialModuleScreenSetAsRoot();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:343: // Patch55_BasicTableauRaceGuard — MUST be applied HERE, not in OnGameInitializationFinished.
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:346: // applied in OnGameInitializationFinished (campaign init) — too late to guard the save-list CTD
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:347: // (Codex C1, issue #299). By here, IoC.Configure() (OnSubModuleLoad) has already set the guard,
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:512: // OnGameInitializationFinished, and flip the `enabled` defaults back to true.
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:764: public override void OnGameInitializationFinished(Game game)
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:766: base.OnGameInitializationFinished(game);
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:904: // Exit-stall stack sampler (#331 round 2): OnGameInitializationFinished runs on the

2026-07-10T17:33:38.687996Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 3 seconds
Output:
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Bannerlord.UIExtenderEx.csproj:5: <TargetFramework>netstandard2.0</TargetFramework>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Bannerlord.UIExtenderEx.csproj:40: <!-- Keep Bannerlord.Lib.Harmony intil we upgrade to v2.3.3 or higher -->
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Bannerlord.UIExtenderEx.csproj:41: <PackageReference Include="Bannerlord.Lib.Harmony" Version="$(HarmonyVersion)" PrivateAssets="all" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Bannerlord.UIExtenderEx.Tests.csproj:5: <TargetFramework>net472</TargetFramework>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Bannerlord.UIExtenderEx.Tests.csproj:22: <PackageReference Include="Lib.Harmony" Version="$(HarmonyVersion)" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\rocks\Test\Mono.Cecil.Rocks.Tests.csproj:4: <TargetFrameworks>netcoreapp3.1;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\rocks\Mono.Cecil.Rocks.csproj:3: <TargetFrameworks>netstandard2.0;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\mdb\Test\Mono.Cecil.Mdb.Tests.csproj:4: <TargetFrameworks>netcoreapp3.1;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\mdb\Mono.Cecil.Mdb.csproj:3: <TargetFrameworks>netstandard2.0;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\pdb\Test\Mono.Cecil.Pdb.Tests.csproj:4: <TargetFrameworks>netcoreapp3.1;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\pdb\Mono.Cecil.Pdb.csproj:3: <TargetFrameworks>netstandard2.0;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Test\Mono.Cecil.Tests.csproj:4: <TargetFrameworks>netcoreapp3.1;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Test\Mono.Cecil.Tests.csproj:6: <ItemGroup Condition=" '$(TargetFramework)' == 'netcoreapp3.1' ">
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Mono.Cecil.csproj:3: <TargetFrameworks>netstandard2.0;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\Documentation.csproj:12: <ProjectReference Include="..\Lib.Harmony\Lib.Harmony.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\HarmonyTests.csproj:12: <ProjectReference Include="..\Lib.Harmony\Lib.Harmony.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\HarmonyTests.csproj:19: <PackageReference Condition="'$(TargetFramework)'!='net35'" Include="System.Text.RegularExpressions" Version="4.3.1" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\HarmonyTests.csproj:32: <ItemGroup Condition="'$(TargetFrameworkIdentifier)'=='.NETCoreApp'">
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:3: <PackageId>Lib.Harmony</PackageId>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:4: <TargetFrameworks>$(TargetFrameworks);netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:29: <PackageReference Condition="$(TargetFramework) == 'netcoreapp3.0'" Include="Microsoft.NetCore.App.Ref" Version="3.0.0" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:30: <PackageReference Condition="$(TargetFramework) == 'netcoreapp3.1'" Include="Microsoft.NetCore.App.Ref" Version="3.1.0" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:31: <PackageReference Condition="$(TargetFramework) == 'net5.0'" Include="Microsoft.NetCore.App.Ref" Version="5.0.0" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:32: <PackageReference Condition="$(TargetFramework) == 'net6.0'" Include="Microsoft.NetCore.App.Ref" Version="6.0.36" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:33: <PackageReference Condition="$(TargetFramework) == 'net7.0'" Include="Microsoft.NetCore.App.Ref" Version="7.0.20" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:34: <PackageReference Condition="$(TargetFramework) == 'net8.0'" Include="Microsoft.NetCore.App.Ref" Version="8.0.12" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:35: <PackageReference Condition="$(TargetFramework) == 'net9.0'" Include="Microsoft.NetCore.App.Ref" Version="9.0.1" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:36: <PackageReference Condition="$(TargetFramework) == 'net10.0'" Include="Microsoft.NetCore.App.Ref" Version="10.0.0" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:39: <!-- Replace Code with reference to Lib.Harmony.Ref for netstandard2.0 -->
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:40: <ItemGroup Condition="$(TargetFramework) == 'netstandard2.0'">
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:41: <ProjectReference Include="..\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj:3: <TargetFrameworks>netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj:11: <PackageId>Lib.Harmony.Ref</PackageId>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:3: <PackageId>Lib.Harmony.Thin</PackageId>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:4: <TargetFrameworks>$(TargetFrameworks);netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:24: <!-- Replace Code with reference to Lib.Harmony.Ref for netstandard2.0 -->
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:25: <ItemGroup Condition="$(TargetFramework) == 'netstandard2.0'">
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:26: <ProjectReference Include="..\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\TestLibrary\TestLibrary.csproj:9: <ProjectReference Include="..\Lib.Harmony\Lib.Harmony.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod\MonoMod.csproj:12: <TargetFrameworks>net35;net452;netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod\MonoMod.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.DebugIL\MonoMod.DebugIL.csproj:12: <TargetFrameworks>net452;netstandard2.0;netcoreapp2.1</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.DebugIL\MonoMod.DebugIL.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour\MonoMod.RuntimeDetour.csproj:12: <TargetFrameworks>net35;net452;netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour\MonoMod.RuntimeDetour.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour.HookGen\MonoMod.RuntimeDetour.HookGen.csproj:12: <TargetFrameworks>net35;net452;netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour.HookGen\MonoMod.RuntimeDetour.HookGen.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:12: <TargetFrameworks>net452;netcoreapp2.1;netcoreapp3.0;netcoreapp3.1</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:14: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 17.0.0 And $(NETCoreAppMaximumVersion) &gt;= 6.0">$(TargetFrameworks);net6.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:42: <Version Condition="'$(TargetFramework)' == 'netcoreapp1.0'">1.6.1</Version>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:43: <Version Condition="'$(TargetFramework)' == 'netcoreapp2.0'">2.0.0</Version>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:44: <Version Condition="'$(TargetFramework)' == 'netcoreapp2.1'">2.0.3</Version>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:56: <Version Condition="'$(TargetFramework)' == 'net452'">2.4.3</Version>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.Utils\MonoMod.Utils.csproj:12: <TargetFrameworks>net35;net452;netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.Utils\MonoMod.Utils.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -Include *.csproj,packages.config[0m | Se …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.pytest_cache' is denied.[0m

 exited 1 in 3018ms:
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-001\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-002\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-003\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\.claude\worktrees\impl-005\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Bannerlord.UIExtenderEx.csproj:5: <TargetFramework>netstandard2.0</TargetFramework>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Bannerlord.UIExtenderEx.csproj:40: <!-- Keep Bannerlord.Lib.Harmony intil we upgrade to v2.3.3 or higher -->
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Bannerlord.UIExtenderEx.csproj:41: <PackageReference Include="Bannerlord.Lib.Harmony" Version="$(HarmonyVersion)" PrivateAssets="all" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Bannerlord.UIExtenderEx.Tests.csproj:5: <TargetFramework>net472</TargetFramework>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Bannerlord.UIExtenderEx.Tests.csproj:22: <PackageReference Include="Lib.Harmony" Version="$(HarmonyVersion)" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\rocks\Test\Mono.Cecil.Rocks.Tests.csproj:4: <TargetFrameworks>netcoreapp3.1;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\rocks\Mono.Cecil.Rocks.csproj:3: <TargetFrameworks>netstandard2.0;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\mdb\Test\Mono.Cecil.Mdb.Tests.csproj:4: <TargetFrameworks>netcoreapp3.1;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\mdb\Mono.Cecil.Mdb.csproj:3: <TargetFrameworks>netstandard2.0;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\pdb\Test\Mono.Cecil.Pdb.Tests.csproj:4: <TargetFrameworks>netcoreapp3.1;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\symbols\pdb\Mono.Cecil.Pdb.csproj:3: <TargetFrameworks>netstandard2.0;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Test\Mono.Cecil.Tests.csproj:4: <TargetFrameworks>netcoreapp3.1;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Test\Mono.Cecil.Tests.csproj:6: <ItemGroup Condition=" '$(TargetFramework)' == 'netcoreapp3.1' ">
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\cecil-0.11.5\Mono.Cecil.csproj:3: <TargetFrameworks>netstandard2.0;net40</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\Documentation.csproj:12: <ProjectReference Include="..\Lib.Harmony\Lib.Harmony.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\HarmonyTests.csproj:12: <ProjectReference Include="..\Lib.Harmony\Lib.Harmony.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\HarmonyTests.csproj:19: <PackageReference Condition="'$(TargetFramework)'!='net35'" Include="System.Text.RegularExpressions" Version="4.3.1" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\HarmonyTests.csproj:32: <ItemGroup Condition="'$(TargetFrameworkIdentifier)'=='.NETCoreApp'">
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:3: <PackageId>Lib.Harmony</PackageId>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:4: <TargetFrameworks>$(TargetFrameworks);netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:29: <PackageReference Condition="$(TargetFramework) == 'netcoreapp3.0'" Include="Microsoft.NetCore.App.Ref" Version="3.0.0" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:30: <PackageReference Condition="$(TargetFramework) == 'netcoreapp3.1'" Include="Microsoft.NetCore.App.Ref" Version="3.1.0" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:31: <PackageReference Condition="$(TargetFramework) == 'net5.0'" Include="Microsoft.NetCore.App.Ref" Version="5.0.0" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:32: <PackageReference Condition="$(TargetFramework) == 'net6.0'" Include="Microsoft.NetCore.App.Ref" Version="6.0.36" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:33: <PackageReference Condition="$(TargetFramework) == 'net7.0'" Include="Microsoft.NetCore.App.Ref" Version="7.0.20" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:34: <PackageReference Condition="$(TargetFramework) == 'net8.0'" Include="Microsoft.NetCore.App.Ref" Version="8.0.12" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:35: <PackageReference Condition="$(TargetFramework) == 'net9.0'" Include="Microsoft.NetCore.App.Ref" Version="9.0.1" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:36: <PackageReference Condition="$(TargetFramework) == 'net10.0'" Include="Microsoft.NetCore.App.Ref" Version="10.0.0" ExcludeAssets="all" PrivateAssets="all" GeneratePathProperty="true" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:39: <!-- Replace Code with reference to Lib.Harmony.Ref for netstandard2.0 -->
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:40: <ItemGroup Condition="$(TargetFramework) == 'netstandard2.0'">
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony\Lib.Harmony.csproj:41: <ProjectReference Include="..\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj:3: <TargetFrameworks>netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj:11: <PackageId>Lib.Harmony.Ref</PackageId>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:3: <PackageId>Lib.Harmony.Thin</PackageId>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:4: <TargetFrameworks>$(TargetFrameworks);netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:24: <!-- Replace Code with reference to Lib.Harmony.Ref for netstandard2.0 -->
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:25: <ItemGroup Condition="$(TargetFramework) == 'netstandard2.0'">
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Lib.Harmony.Thin\Lib.Harmony.Thin.csproj:26: <ProjectReference Include="..\Lib.Harmony.Ref\Lib.Harmony.Ref.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\TestLibrary\TestLibrary.csproj:9: <ProjectReference Include="..\Lib.Harmony\Lib.Harmony.csproj" />
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod\MonoMod.csproj:12: <TargetFrameworks>net35;net452;netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod\MonoMod.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.DebugIL\MonoMod.DebugIL.csproj:12: <TargetFrameworks>net452;netstandard2.0;netcoreapp2.1</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.DebugIL\MonoMod.DebugIL.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour\MonoMod.RuntimeDetour.csproj:12: <TargetFrameworks>net35;net452;netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour\MonoMod.RuntimeDetour.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour.HookGen\MonoMod.RuntimeDetour.HookGen.csproj:12: <TargetFrameworks>net35;net452;netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.RuntimeDetour.HookGen\MonoMod.RuntimeDetour.HookGen.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:12: <TargetFrameworks>net452;netcoreapp2.1;netcoreapp3.0;netcoreapp3.1</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:14: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 17.0.0 And $(NETCoreAppMaximumVersion) &gt;= 6.0">$(TargetFrameworks);net6.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:42: <Version Condition="'$(TargetFramework)' == 'netcoreapp1.0'">1.6.1</Version>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:43: <Version Condition="'$(TargetFramework)' == 'netcoreapp2.0'">2.0.0</Version>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:44: <Version Condition="'$(TargetFramework)' == 'netcoreapp2.1'">2.0.3</Version>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.UnitTest\MonoMod.UnitTest.csproj:56: <Version Condition="'$(TargetFramework)' == 'net452'">2.4.3</Version>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.Utils\MonoMod.Utils.csproj:12: <TargetFrameworks>net35;net452;netstandard2.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\MonoMod-master\MonoMod.Utils\MonoMod.Utils.csproj:13: <TargetFrameworks Condition="$(MSBuildVersion) &gt;= 16.6.0 And $(NETCoreAppMaximumVersion) &gt;= 5.0">$(TargetFrameworks);net5.0</TargetFrameworks>
C:\Users\mikew\source\repos\TAOM\Dependencies\TAOM.Dependencies.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\Dependencies\TAOM.Dependencies.csproj:57: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:10: <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:85: via this ProjectReference, NOT via Lib.Harmony / Bannerlord.UIExtenderEx package or
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:102: <!-- DR3 (2026-05-22): Lib.Harmony + Bannerlord.UIExtenderEx PackageReferences are
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:105: types come from TAOM.Dependencies.dll (which has Lib.Harmony + Bannerlord.UIExtenderEx
C:\Users\mikew\source\repos\TAOM\Main\TAOM.csproj:110: <PackageReference Include="Lib.Harmony" Version="2.4.2" IncludeAssets="compile" />
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj:14: Migration/ binding-verification suite. Main exposes Lib.Harmony as compile-only, which
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj:19: <PackageReference Include="Lib.Harmony" Version="2.4.2" />
C:\Users\mikew\source\repos\TAOM\tools\BannerlordCraftingTool\BannerlordCraftingTool.csproj:4: <TargetFramework>net8.0-windows</TargetFramework>
[31;1mGet-ChildItem: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mGet-ChildItem -Path . -Recurse -Include *.csproj,packages.config[0m | Se …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\Users\mikew\source\repos\TAOM\.pytest_cache' is denied.[0m

codex
The install timing is two-pass: `PatchShield.Install` runs in `TAOM.Dependencies.OnSubModuleLoad` and again in `OnGameInitializationFinished`. That means the exclusion has to be correct on every pass; it is code-level, not only state-level, so any newly patched UI method should be skipped when first seen.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Dependencies/SubModule.cs'; "'$start=115; $end=240; $i=0; Get-Content $p | ForEach-Object { $i++; if($i -ge $start -and $i -le $end){ '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Dependencies/.vendor-source/Harmony-2.4.2.0/Lib.Harmony/Public/Harmony.cs'; if(Test-Path "'$p){ $i=0; Get-Content $p | ForEach-Object { $i++; if($_ -match '"'GetAllPatchedMethods|GetPatchInfo|Patch\\(' -or ("'$i -ge 150 -and $i -le 260)){ '"'{0,4}: {1}' -f "'$i,$_ } } } else { Get-ChildItem Dependencies/.vendor-source/Harmony-2.4.2.0 -Recurse -Include *.cs | Select-String -Pattern '"'GetAllPatchedMethods' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem Dependencies/.vendor-source/Harmony-2.4.2.0 -Recurse -Include *.cs | Select-String -Pattern 'GetAllPatchedMethods|GetPatchedMethods|PatchInfo|PatchClassProcessor' | Select-Object -First 120 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem Dependencies/.vendor-source/Harmony-2.4.2.0 -Recurse -Include *.cs | Select-String -Pattern 'new Dictionary|patchedMethods|GetOriginals|MethodPatcher|PatchFunctions' | Select-Object -First 160 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 566ms:
 115:                 }
 116:             }
 117:         }
 118:         return null;
 119:     }
 120: 
 121:     protected override void OnSubModuleLoad()
 122:     {
 123:         base.OnSubModuleLoad();
 124:         DiagLog.Log("Dependencies", "OnSubModuleLoad: entered");
 125:         EarlyLog.Info($"[TAOM.Dependencies] Harmony forked v{typeof(Harmony).Assembly.GetName().Version} loaded from {typeof(Harmony).Assembly.GetName().Name}");
 126:         DiagLog.Log("Dependencies", $"OnSubModuleLoad: Harmony assembly = {typeof(Harmony).Assembly.GetName().Name} v{typeof(Harmony).Assembly.GetName().Version}");
 127: 
 128:         try
 129:         {
 130:             DiagLog.Log("Dependencies", "OnSubModuleLoad: applying Harmony guards");
 131:             ApplyHarmonyGuards();
 132:             DiagLog.Log("Dependencies", "OnSubModuleLoad: Harmony guards applied OK");
 133:             EarlyLog.Info("[TAOM.Dependencies] UnpatchAll guard applied");
 134:         }
 135:         catch (Exception ex)
 136:         {
 137:             DiagLog.LogCaught("Dependencies", "OnSubModuleLoad/ApplyHarmonyGuards", ex);
 138:             EarlyLog.Error($"[TAOM.Dependencies] Failed to apply Harmony guards: {ex.Message}");
 139:         }
 140: 
 141:         DiagLog.Log("Dependencies", "OnSubModuleLoad: checking for duplicate Harmony");
 142:         CheckForDuplicateHarmony();
 143: 
 144:         try
 145:         {
 146:             // Codex review 2026-05-22 (P1): `_ = typeof(UIExtender)` only fetches the Type
 147:             // object — it does NOT execute the class's static constructor where
 148:             // UIConfigPatch.Patch / ViewModelPatch.Patch / etc. are applied.
 149:             // RunClassConstructor forces the static cctor to run (idempotent — JIT
 150:             // marks the type as initialized after the first call).
 151:             DiagLog.Log("Dependencies", "OnSubModuleLoad: forcing UIExtenderEx static cctor");
 152:             RuntimeHelpers.RunClassConstructor(typeof(Bannerlord.UIExtenderEx.UIExtender).TypeHandle);
 153:             DiagLog.Log("Dependencies", "OnSubModuleLoad: UIExtenderEx static cctor done");
 154:             EarlyLog.Info("[TAOM.Dependencies] UIExtenderEx static cctor executed (system patches applied)");
 155:         }
 156:         catch (Exception ex)
 157:         {
 158:             DiagLog.LogCaught("Dependencies", "OnSubModuleLoad/UIExtenderEx", ex);
 159:             EarlyLog.Error($"[TAOM.Dependencies] UIExtenderEx initialization failed: {ex.Message}");
 160:         }
 161: 
 162:         // DR3 Phase 4 C-series: install ALL the defensive shields here. Originally
 163:         // CollectAssemblyTypesShim + SubModuleConstructionGuard + RunEarlyPhase were
 164:         // called from AliasStubSubModule.ctor, but observed 2026-05-27 — the launcher
 165:         // never constructs AliasStubSubModule because the alias stub module folders
 166:         // have no `bin/<platform>/TAOM.Dependencies.dll` (the DLL lives in TAOM.Dependencies's
 167:         // own bin/, not duplicated to each stub). OnSubModuleLoad fires for TAOM.Dependencies's
 168:         // main SubModule deterministically, so all shields install from one known-working hook.
 169:         DiagLog.Log("Dependencies", "OnSubModuleLoad: installing defensive shields");
 170: 
 171:         // RunEarlyPhase writes session-launching.marker for crash-loop detection. Was
 172:         // supposed to fire from stub ctors (very early), now fires at OnSubModuleLoad
 173:         // (~642ms later — acceptable; pre-OnSubModuleLoad crashes can't be diagnosed by
 174:         // TAOM anyway since our code isn't loaded yet).
 175:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → IncompatibleModDetector.RunEarlyPhase"); IncompatibleModDetector.RunEarlyPhase(); }
 176:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "IncompatibleModDetector.RunEarlyPhase", ex); }
 177: 
 178:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → CollectAssemblyTypesShim.Install"); CollectAssemblyTypesShim.Install(); }
 179:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "CollectAssemblyTypesShim.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] CollectAssemblyTypesShim.Install failed: {ex.Message}"); }
 180: 
 181:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → SubModuleConstructionGuard.Install"); SubModuleConstructionGuard.Install(); }
 182:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "SubModuleConstructionGuard.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] SubModuleConstructionGuard.Install failed: {ex.Message}"); }
 183: 
 184:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
 185:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
 186: 
 187:         try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → SaveShield.Install"); SaveShield.Install(); }
 188:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "SaveShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] SaveShield.Install failed: {ex.Message}"); }
 189: 
 190:         // Trigger VersionProbe explicitly so the version is logged. Without this,
 191:         // VersionProbe's lazy-detect via Major/Minor getters never fires (no consumer
 192:         // touches it today). Observed 2026-05-27 — version probe silent in diag.log.
 193:         try
 194:         {
 195:             DiagLog.Log("Dependencies", "OnSubModuleLoad: → VersionProbe (triggering detection)");
 196:             var detected = VersionProbe.IsDetected;
 197:             DiagLog.Log("Dependencies", $"OnSubModuleLoad: VersionProbe.IsDetected={detected} (Major={VersionProbe.Major}, Minor={VersionProbe.Minor}, Revision={VersionProbe.Revision})");
 198:         }
 199:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "VersionProbe trigger", ex); }
 200: 
 201:         // Write a session summary to diag.log on process exit so users can see the
 202:         // shield's swallow-counts even if no crash dump is produced.
 203:         try
 204:         {
 205:             AppDomain.CurrentDomain.ProcessExit += (_, __) =>
 206:             {
 207:                 try { PatchShield.WriteSessionSummary(); } catch { }
 208:             };
 209:             DiagLog.Log("Dependencies", "OnSubModuleLoad: ProcessExit hook for session summary registered");
 210:         }
 211:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "ProcessExit hook", ex); EarlyLog.Error($"[TAOM.Dependencies] ProcessExit hook failed: {ex.Message}"); }
 212: 
 213:         DiagLog.Log("Dependencies", "OnSubModuleLoad: complete");
 214:         EarlyLog.Info("[TAOM.Dependencies] OnSubModuleLoad complete");
 215:     }
 216: 
 217:     /// <summary>
 218:     /// Called when the main menu has rendered — signals the crash-loop detector that
 219:     /// this session reached menu (deletes the launch marker, snapshots modlist as
 220:     /// last-good). Override of MBSubModuleBase.OnGameInitializationFinished, the
 221:     /// closest TaleWorlds lifecycle hook to "we made it past load."
 222:     /// </summary>
 223:     public override void OnGameInitializationFinished(Game game)
 224:     {
 225:         base.OnGameInitializationFinished(game);
 226:         DiagLog.Log("Dependencies", "OnGameInitializationFinished: entered (main menu reached)");
 227: 
 228:         try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → MarkSessionLaunchSuccessful"); IncompatibleModDetector.MarkSessionLaunchSuccessful(); }
 229:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "MarkSessionLaunchSuccessful", ex); EarlyLog.Error($"[TAOM.Dependencies] MarkSessionLaunchSuccessful failed: {ex.Message}"); }
 230: 
 231:         // Second PatchShield pass — captures patches registered by mods that hook this
 232:         // lifecycle event (after our OnSubModuleLoad).
 233:         try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
 234:         catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }
 235: 
 236:         DiagLog.Log("Dependencies", "OnGameInitializationFinished: complete");
 237:     }
 238: 
 239:     private static void ApplyHarmonyGuards()
 240:     {

 succeeded in 605ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\basics.cs:64: var originalMethods = Harmony.GetAllPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:208: var originals = GetAllPatchedMethods().ToList(); // keep as is to avoid "Collection was modified"
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:276: return GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:292: return GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:299: public static IEnumerable<MethodBase> GetAllPatchedMethods() => PatchProcessor.GetAllPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:158: public static IEnumerable<MethodBase> GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:280: GetAllPatchedMethods().Do(method =>

 succeeded in 595ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\basics.cs:64: var originalMethods = Harmony.GetAllPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\basics.cs:69: var myOriginalMethods = harmony.GetPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\basics.cs:73: void PatchInfo()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\basics.cs:80: var patches = Harmony.GetPatchInfo(original);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\basics.cs:98: PatchInfo();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:129: internal static PatchInfo GetPatchInfo(MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:134: return PatchInfoSerialization.Deserialize(bytes);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:138: internal static IEnumerable<MethodBase> GetPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:143: internal static void UpdatePatchInfo(MethodBase original, MethodInfo replacement, PatchInfo patchInfo)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:145: patchInfo.VersionCount++;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:146: var bytes = patchInfo.Serialize();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:51: var info = Harmony.GetPatchInfo(method);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:48: var patchInfo = HarmonySharedState.GetPatchInfo(original) ?? new PatchInfo();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:49: patchIndex = patchInfo.VersionCount + 1;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:14: internal static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:16: var debug = patchInfo.Debugging || Harmony.DEBUG;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:18: var sortedPrefixes = GetSortedPatchMethods(original, patchInfo.prefixes, debug);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:19: var sortedPostfixes = GetSortedPatchMethods(original, patchInfo.postfixes, debug);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:20: var sortedTranspilers = GetSortedPatchMethods(original, patchInfo.transpilers, debug);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:21: var sortedFinalizers = GetSortedPatchMethods(original, patchInfo.finalizers, debug);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:22: var sortedInnerPrefixes = GetInfixes(patchInfo.innerprefixes);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:23: var sortedInnerPostfixes = GetInfixes(patchInfo.innerpostfixes);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:62: var info = Harmony.GetPatchInfo(original);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchSorter.cs:158: return PatchInfoSerialization.PriorityComparer(p?.innerPatch, innerPatch.index, innerPatch.priority);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:92: /// <returns>A new <see cref="PatchClassProcessor"/> instance</returns>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:94: public PatchClassProcessor CreateClassProcessor(Type type) => new(this, type);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:206: bool IDCheck(Patch patchInfo) => harmonyID is null || patchInfo.owner == harmonyID;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:208: var originals = GetAllPatchedMethods().ToList(); // keep as is to avoid "Collection was modified"
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:212: var info = GetPatchInfo(original);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:215: info.Postfixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:216: info.Prefixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:217: info.InnerPostfixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:218: info.InnerPrefixes.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:220: info.Transpilers.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:222: info.Finalizers.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:276: return GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:277: .Select(GetPatchInfo)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:285: public static Patches GetPatchInfo(MethodBase method) => PatchProcessor.GetPatchInfo(method);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:290: public IEnumerable<MethodBase> GetPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:292: return GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:293: .Where(original => GetPatchInfo(original).Owners.Contains(Id));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:299: public static IEnumerable<MethodBase> GetAllPatchedMethods() => PatchProcessor.GetAllPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patch.cs:140: public int CompareTo(object obj) => PatchInfoSerialization.PriorityComparer(obj, index, priority);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:9: /// <summary>A PatchClassProcessor used to turn <see cref="HarmonyAttribute"/> on a class/type into patches</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:11: public class PatchClassProcessor
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:37: public PatchClassProcessor(Harmony instance, Type type)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:203: var patchInfo = HarmonySharedState.GetPatchInfo(job.original) ?? new PatchInfo();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:205: patchInfo.AddPrefixes(instance.Id, [.. job.prefixes]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:206: patchInfo.AddPostfixes(instance.Id, [.. job.postfixes]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:207: patchInfo.AddTranspilers(instance.Id, [.. job.transpilers]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:208: patchInfo.AddFinalizers(instance.Id, [.. job.finalizers]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:209: patchInfo.AddInnerPrefixes(instance.Id, [.. job.innerprefixes]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:210: patchInfo.AddInnerPostfixes(instance.Id, [.. job.innerpostfixes]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:212: replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:213: HarmonySharedState.UpdatePatchInfo(job.original, replacement, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:228: var patchInfo = HarmonySharedState.GetPatchInfo(job.original) ?? new PatchInfo();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:233: job.postfixes.Do(patch => patchInfo.RemovePatch(patch.method));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:234: job.prefixes.Do(patch => patchInfo.RemovePatch(patch.method));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:236: job.transpilers.Do(patch => patchInfo.RemovePatch(patch.method));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:238: job.finalizers.Do(patch => patchInfo.RemovePatch(patch.method));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:240: var replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:241: HarmonySharedState.UpdatePatchInfo(job.original, replacement, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:14: public class PatchInfo
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:158: public static IEnumerable<MethodBase> GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:162: return HarmonySharedState.GetPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:182: var patchInfo = HarmonySharedState.GetPatchInfo(original) ?? new PatchInfo();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:184: patchInfo.AddPrefixes(instance.Id, prefix);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:185: patchInfo.AddPostfixes(instance.Id, postfix);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:186: patchInfo.AddTranspilers(instance.Id, transpiler);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:187: patchInfo.AddFinalizers(instance.Id, finalizer);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:188: patchInfo.AddInnerPrefixes(instance.Id, innerprefix);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:189: patchInfo.AddInnerPostfixes(instance.Id, innerpostfix);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:191: var replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:192: HarmonySharedState.UpdatePatchInfo(original, replacement, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:209: var patchInfo = HarmonySharedState.GetPatchInfo(original);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:210: patchInfo ??= new PatchInfo();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:213: patchInfo.RemovePrefix(harmonyID);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:215: patchInfo.RemovePostfix(harmonyID);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:217: patchInfo.RemoveTranspiler(harmonyID);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:219: patchInfo.RemoveFinalizer(harmonyID);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:221: patchInfo.RemoveInnerPrefix(harmonyID);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:223: patchInfo.RemoveInnerPostfix(harmonyID);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:225: var replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:226: HarmonySharedState.UpdatePatchInfo(original, replacement, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:242: var patchInfo = HarmonySharedState.GetPatchInfo(original);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:243: patchInfo ??= new PatchInfo();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:245: patchInfo.RemovePatch(patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:247: var replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:248: HarmonySharedState.UpdatePatchInfo(original, replacement, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:257: public static Patches GetPatchInfo(MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:259: PatchInfo patchInfo;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:260: lock (locker) { patchInfo = HarmonySharedState.GetPatchInfo(method); }
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:261: if (patchInfo is null) return null;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:262: return new Patches(patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers, patchInfo.innerprefixes, patchInfo.innerpostfixes);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:280: GetAllPatchedMethods().Do(method =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:282: PatchInfo info;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:284: { info = HarmonySharedState.GetPatchInfo(method); }
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:13: internal static class PatchInfoSerialization
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:54: typeof(PatchInfo),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:68: /// <param name="patchInfo">The <see cref="PatchInfo"/></param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:71: internal static byte[] Serialize(this PatchInfo patchInfo)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:78: binaryFormatter.Serialize(streamMemory, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:83: return JsonSerializer.SerializeToUtf8Bytes(patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:89: /// <returns>A <see cref="PatchInfo"/></returns>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:91: internal static PatchInfo Deserialize(byte[] bytes)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:98: return (PatchInfo)binaryFormatter.Deserialize(streamMemory);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Serialization\PatchInfoSerialization.cs:103: return JsonSerializer.Deserialize<PatchInfo>(bytes, serializerOptions);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:2036: System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:2038: // For the sake of any static code analyzer, always throw exception, even if ExceptionDispatchInfo.Throw above was called.
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:15: static Patch[][] GetFixes(PatchInfo patchInfo) => [patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers, patchInfo.innerprefixes, patchInfo.innerpostfixes];
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:43: var patchInfo = new PatchInfo();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:44: patchInfo.AddPrefixes("prefixes", [hMethod]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:45: patchInfo.AddPostfixes("postfixes", [hMethod]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:46: patchInfo.AddTranspilers("transpilers", [hMethod]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:47: patchInfo.AddFinalizers("finalizers", [hMethod]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:48: patchInfo.AddInnerPrefixes("innerprefixes", [hMethod]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:49: patchInfo.AddInnerPostfixes("innerpostfixes", [hMethod]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:50: patchInfo.VersionCount = 123;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:52: PatchInfoSerialization.useBinaryFormatter = false;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Extras\PatchSerialization.cs:53: var result = PatchInfoSerialization.Serialize(patchInfo);

 succeeded in 607ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\basics.cs:64: var originalMethods = Harmony.GetAllPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\basics.cs:69: var myOriginalMethods = harmony.GetPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\CodeTranspiler.cs:26: var nonExisting = new Dictionary<string, object>();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Emitter.cs:192: Emit(MethodPatcherTools.LoadIndOpCodeFor(pType));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Emitter.cs:214: Emit(OpCodes.Ldloc, localState[MethodPatcherTools.ARGS_ARRAY_VAR]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Emitter.cs:224: Emit(MethodPatcherTools.StoreIndOpCodeFor(pType));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Emitter.cs:234: Emit(OpCodes.Ldloc, localState[MethodPatcherTools.ARGS_ARRAY_VAR]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:65: stateField.SetValue(null, new Dictionary<MethodBase, byte[]>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:70: originalsField.SetValue(null, new Dictionary<MethodInfo, MethodBase>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:75: originalsMonoField.SetValue(null, new Dictionary<long, MethodBase[]>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:138: internal static IEnumerable<MethodBase> GetPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:48: var originalVariables = MethodPatcherTools.DeclareOriginalLocalVariables(generator, method);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:54: var sortedTranspilers = PatchFunctions.GetSortedPatchMethods(method, [.. info.Transpilers], false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:230: if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:290: if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:358: if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:419: var replacements = new Dictionary<CodeInstruction, CodeInstruction[]>();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:50: patch = MethodPatcherTools.CreateDynamicMethod(original, $"_Patch{patchIndex}", debug);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:10: internal class MethodPatcherTools
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:104: return MethodPatcherTools.OriginalParameters(fix).Any(pair =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:8: internal static class PatchFunctions
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:198: public static MethodInfo ReversePatch(MethodBase original, HarmonyMethod standin, MethodInfo transpiler = null) => PatchFunctions.ReversePatch(standin, original, transpiler);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:208: var originals = GetAllPatchedMethods().ToList(); // keep as is to avoid "Collection was modified"
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:276: return GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:290: public IEnumerable<MethodBase> GetPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:292: return GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:299: public static IEnumerable<MethodBase> GetAllPatchedMethods() => PatchProcessor.GetAllPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:212: replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:240: var replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:158: public static IEnumerable<MethodBase> GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:162: return HarmonySharedState.GetPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:191: var replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:225: var replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:247: var replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:270: public static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches) => PatchFunctions.GetSortedPatchMethods(original, patches, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:279: var assemblies = new Dictionary<string, Assembly>();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:280: GetAllPatchedMethods().Do(method =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:293: var result = new Dictionary<string, Version>();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\ReversePatcher.cs:39: return PatchFunctions.ReversePatch(standin, original, transpiler);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\CodeMatcher.cs:130: lastMatches = new Dictionary<string, CodeInstruction>(lastMatches),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\Extensions.cs:838: // Returns a new dictionary with entries merged from given dictionaries.
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\Extensions.cs:843: var dict = new Dictionary<K, V>();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\Extensions.cs:854: // Returns a new dictionary copied from given dictionary with keys run through a transform function.
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\Extensions.cs:857: var dict = new Dictionary<K, V>();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:36: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:63: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:86: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:108: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:131: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:154: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:177: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:199: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:221: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:248: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:275: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Patching\PatchSorting.cs:297: var methods = PatchFunctions.GetSortedPatchMethods(null, patchInstances, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestAttributes.cs:37: var prefixes = PatchFunctions.GetSortedPatchMethods(method, [.. patches.Prefixes], false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestAttributes.cs:38: var postfixes = PatchFunctions.GetSortedPatchMethods(method, [.. patches.Postfixes], false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestAttributes.cs:39: var finalizers = PatchFunctions.GetSortedPatchMethods(method, [.. patches.Finalizers], false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:232: return new Dictionary<string, IATestCase<T, F>>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:243: return new Dictionary<string, IATestCase<T, F>>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:253: return new Dictionary<string, IATestCase<T, F>>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:264: return new Dictionary<string, IATestCase<T, F>>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:273: return new Dictionary<string, IATestCase<T, F>>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:309: var newExpectedCaseToConstraint = new Dictionary<string, ReusableConstraint>();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:322: return expectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:339: var newExpectedCaseToConstraint = FieldMissingOnTypeT(expectedCaseToConstraint).Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:350: return newExpectedCaseToConstraint.Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:361: return IncompatibleInstanceType(expectedCaseToConstraint).Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:370: var newExpectedCaseToConstraint = new Dictionary<string, ReusableConstraint>(expectedCaseToConstraint);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:385: ReusableConstraints(new Dictionary<string, IResolveConstraint>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:401: ReusableConstraints(new Dictionary<string, IResolveConstraint>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:417: ReusableConstraints(new Dictionary<string, IResolveConstraint>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:437: ReusableConstraints(new Dictionary<string, IResolveConstraint>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Tools\TestFieldRefAccess.cs:465: FieldMissingOnTypeT(expectedCaseToConstraint_ClassInstance).Merge(ReusableConstraints(new Dictionary<string, IResolveConstraint>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\HarmonyTests\Traverse\Assets\TraverseTypes.cs:28: var d = new Dictionary<T, List<string>>();

codex
The exit window now has two latches: the bool gates main-thread phase logs and the tick value arms the background sampler. The writes are not atomic as a pair, so I’m enumerating the open/close interleavings rather than treating the unit tests as sufficient.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Public/Harmony.cs','Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Public/PatchProcessor.cs','Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/HarmonySharedState.cs','Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/PatchFunctions.cs','Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/MethodCreator.cs'); foreach("'$p in $files){ Write-Output "--- $p"; $i=0; Get-Content $p | ForEach-Object { $i++; if($p -like '"'*Harmony.cs' -and "'$i -ge 285 -and $i -le 305){'"'{0,4}: {1}' -f "'$i,$_}; if($p -like '"'*PatchProcessor.cs' -and (("'$i -ge 150 -and $i -le 195) -or ($i -ge 257 -and $i -le 264))){'"'{0,4}: {1}' -f "'$i,$_}; if($p -like '"'*HarmonySharedState.cs' -and ("'$i -ge 120 -and $i -le 150)){'"'{0,4}: {1}' -f "'$i,$_}; if($p -like '"'*PatchFunctions.cs' -and ("'$i -ge 1 -and $i -le 80)){'"'{0,4}: {1}' -f "'$i,$_}; if($p -like '"'*MethodCreator.cs' -and ("'$i -ge 250 -and $i -le 380)){'"'{0,4}: {1}' -f "'$i,$_} } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony -Recurse -Include *.cs | Select-String -Pattern '__originalMethod|GetMethodFromHandle|OriginalMethod|Ldtoken|MethodBase' | Select-Object -First 120 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem Dependencies/.vendor-source/Harmony-2.4.2.0 -Recurse -Include *.cs | Select-String -Pattern 'Finalizer|finalizer|__exception|catch' | Select-Object -First 160 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord -Recurse -Include *.cs -ErrorAction SilentlyContinue | Select-String -Pattern 'class WidgetTemplate|void OnRelease|bool IsCustomType|class WidgetFactory|class SettlementNameplateWidget' | Select-Object -First 160 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 602ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\AccessCache.cs:26: readonly Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>> declaredMethods = [];
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\AccessCache.cs:30: readonly Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>> inheritedMethods = [];
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\AccessCache.cs:90: internal MethodBase GetMethodInfo(Type type, string name, Type[] arguments, MemberType memberType = MemberType.Any, bool declaredOnly = false)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\CodeTranspiler.cs:206: internal static List<object> GetTranspilerCallParameters(ILGenerator generator, MethodInfo transpiler, MethodBase method, IEnumerable instructions)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\CodeTranspiler.cs:213: else if (type.IsAssignableFrom(typeof(MethodBase)))
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\CodeTranspiler.cs:221: internal List<CodeInstruction> GetResult(ILGenerator generator, MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Emitter.cs:46: if (argument is MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Emitter.cs:163: internal void PrepareArgumentArray(MethodBase original)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Emitter.cs:200: internal void RestoreArgumentArray(MethodBase original, LocalBuilderState localState)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:39: static readonly Dictionary<MethodBase, byte[]> state;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:40: static readonly Dictionary<MethodInfo, MethodBase> originals;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:41: static readonly Dictionary<long, MethodBase[]> originalsMono;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:65: stateField.SetValue(null, new Dictionary<MethodBase, byte[]>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:70: originalsField.SetValue(null, new Dictionary<MethodInfo, MethodBase>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:75: originalsMonoField.SetValue(null, new Dictionary<long, MethodBase[]>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:78: state = (Dictionary<MethodBase, byte[]>)stateField.GetValue(null);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:83: originals = (Dictionary<MethodInfo, MethodBase>)originalsField.GetValue(null);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:88: originalsMono = (Dictionary<long, MethodBase[]>)originalsMonoField.GetValue(null);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:105: module.ImportReference(typeof(Dictionary<MethodBase, byte[]>))
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:111: module.ImportReference(typeof(Dictionary<MethodInfo, MethodBase>))
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:117: module.ImportReference(typeof(Dictionary<long, MethodBase[]>))
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:129: internal static PatchInfo GetPatchInfo(MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:138: internal static IEnumerable<MethodBase> GetPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:143: internal static void UpdatePatchInfo(MethodBase original, MethodInfo replacement, PatchInfo patchInfo)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:158: internal static MethodBase GetRealMethod(MethodInfo method, bool useReplacement)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\HarmonySharedState.cs:176: internal static MethodBase GetStackFrameMethod(StackFrame frame, bool useReplacement)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Infix.cs:16: internal MethodBase InnerMethod => patch.innerMethod.Method;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Infix.cs:19: internal bool Matches(MethodBase method, int index, int total) // index is 1-based
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\InjectedParameter.cs:11: OriginalMethod,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\InjectedParameter.cs:27: internal const string ORIGINAL_METHOD_PARAM = "__originalMethod";
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\InjectedParameter.cs:56: { ORIGINAL_METHOD_PARAM, InjectionType.OriginalMethod },
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:17: internal MethodCopier(MethodBase fromMethod, ILGenerator toILGenerator, LocalBuilder[] existingVariables = null)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:28: if (config.MethodBase is null)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:29: throw new ArgumentNullException("config.methodbase");
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:30: reader = new MethodBodyReader(config.MethodBase, config.il);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:41: internal static List<CodeInstruction> GetInstructions(ILGenerator generator, MethodBase method, int maxTranspilers)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:66: readonly MethodBase method;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:81: internal static List<ILInstruction> GetInstructions(ILGenerator generator, MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:91: internal MethodBodyReader(MethodBase method, ILGenerator generator)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:694: internal ThisParameter(MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:31: config.originalVariables = this.DeclareOriginalLocalVariables(config.MethodBase);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:14: internal readonly MethodBase original;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:15: internal readonly MethodBase source; // for reverse patch
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:25: MethodBase original,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:26: MethodBase source,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:87: internal MethodBase MethodBase => source ?? original;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:136: if (parameter.injectionType == InjectionType.OriginalMethod)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:196: if (injectionType == InjectionType.OriginalMethod)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:389: var delegateOriginal = harmonyMethod.GetOriginalMethod();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:484: internal static LocalBuilder[] DeclareOriginalLocalVariables(this MethodCreator creator, MethodBase member)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:710: static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:711: static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:712: static bool EmitOriginalBaseMethod(MethodBase original, List<CodeInstruction> codes)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:715: codes.Add(Ldtoken[method]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:717: codes.Add(Ldtoken[constructor]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:723: codes.Add(Ldtoken[type]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorTools.cs:724: codes.Add(Call[type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:14: internal const string ORIGINAL_METHOD_PARAM = "__originalMethod";
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:24: internal static DynamicMethodDefinition CreateDynamicMethod(MethodBase original, string suffix, bool debug)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:91: internal static LocalBuilder[] DeclareOriginalLocalVariables(ILGenerator il, MethodBase member)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:128: static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:129: static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:130: internal static bool EmitOriginalBaseMethod(MethodBase original, Emitter emitter)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:133: emitter.Emit(OpCodes.Ldtoken, method);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:135: emitter.Emit(OpCodes.Ldtoken, constructor);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:141: emitter.Emit(OpCodes.Ldtoken, type);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:142: emitter.Emit(OpCodes.Call, type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:10: internal static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches, bool debug)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:14: internal static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:50: internal static MethodInfo ReversePatch(HarmonyMethod standin, MethodBase original, MethodInfo postTranspiler)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchModels.cs:12: internal MethodBase original;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchModels.cs:47: internal Dictionary<MethodBase, Job> state = [];
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchModels.cs:49: internal Job GetJob(MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchTools.cs:14: private static readonly Dictionary<MethodBase, ICoreDetour> detours = [];
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchTools.cs:19: internal static void DetourMethod(MethodBase method, MethodBase replacement)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchTools.cs:114: internal static MethodBase GetOriginalMethod(this HarmonyMethod attr)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:88: public PatchProcessor CreateProcessor(MethodBase original) => new(this, original);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:101: public ReversePatcher CreateReversePatcher(MethodBase original, HarmonyMethod standin) => new(this, original, standin);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:181: public MethodInfo Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null/*, HarmonyMethod infix = null*/)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:198: public static MethodInfo ReversePatch(MethodBase original, HarmonyMethod standin, MethodInfo transpiler = null) => PatchFunctions.ReversePatch(standin, original, transpiler);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:231: public void Unpatch(MethodBase original, HarmonyPatchType type, string harmonyID = "*")
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:241: public void Unpatch(MethodBase original, MethodInfo patch)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:285: public static Patches GetPatchInfo(MethodBase method) => PatchProcessor.GetPatchInfo(method);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:290: public IEnumerable<MethodBase> GetPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:299: public static IEnumerable<MethodBase> GetAllPatchedMethods() => PatchProcessor.GetAllPatchedMethods();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:305: public static MethodBase GetOriginalMethod(MethodInfo replacement)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:315: public static MethodBase GetMethodFromStackframe(StackFrame frame)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:324: public static MethodBase GetOriginalMethodFromStackframe(StackFrame frame)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\HarmonyMethod.cs:327: public static List<HarmonyMethod> GetFromMethod(MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\HarmonyMethod.cs:338: public static HarmonyMethod GetMergedFromMethod(MethodBase method) => HarmonyMethod.Merge(GetFromMethod(method));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patch.cs:117: public MethodInfo GetMethod(MethodBase original)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patch.cs:124: if (parameters[0].ParameterType != typeof(MethodBase)) return method;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:86: MethodBase lastOriginal = null;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:112: MethodBase lastOriginal = null;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:119: void ReversePatch(ref MethodBase lastOriginal)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:126: var annotatedOriginal = patchMethod.info.GetOriginalMethod();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:136: List<MethodInfo> BulkPatch(List<MethodBase> originals, ref MethodBase lastOriginal, bool unpatch)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:168: List<MethodInfo> PatchWithAttributes(ref MethodBase lastOriginal, bool unpatch)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:173: lastOriginal = patchMethod.info.GetOriginalMethod();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:244: List<MethodBase> GetBulkMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:253: var list = new List<MethodBase>();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:254: list.AddRange(AccessTools.GetDeclaredConstructors(type).Cast<MethodBase>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:255: list.AddRange(AccessTools.GetDeclaredMethods(type).Cast<MethodBase>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:257: list.AddRange(props.Select(prop => prop.GetGetMethod(true)).Where(method => method is not null).Cast<MethodBase>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:258: list.AddRange(props.Select(prop => prop.GetSetMethod(true)).Where(method => method is not null).Cast<MethodBase>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:262: var result = new List<MethodBase>();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:264: var targetMethods = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null, null);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:283: var targetMethod = RunMethod<HarmonyTargetMethod, MethodBase>(null, null, method => method is null ? "null" : null);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:290: void ReportException(Exception exception, MethodBase original)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:15: readonly MethodBase original;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:29: public PatchProcessor(Harmony instance, MethodBase original)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:158: public static IEnumerable<MethodBase> GetAllPatchedMethods()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:257: public static Patches GetPatchInfo(MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:270: public static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches) => PatchFunctions.GetSortedPatchMethods(original, patches, false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:316: public static ILGenerator CreateILGenerator(MethodBase original)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:331: public static List<CodeInstruction> GetOriginalInstructions(MethodBase original, ILGenerator generator = null) => MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, 0);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:338: public static List<CodeInstruction> GetOriginalInstructions(MethodBase original, out ILGenerator generator)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:350: public static List<CodeInstruction> GetCurrentInstructions(MethodBase original, int maxTranspilers = int.MaxValue, ILGenerator generator = null) => MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, maxTranspilers);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:358: public static List<CodeInstruction> GetCurrentInstructions(MethodBase original, out ILGenerator generator, int maxTranspilers = int.MaxValue)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:368: public static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBody(MethodBase method)

 succeeded in 622ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\annotations_combining.cs:43: static void Finalizer() { }
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:18: // - if there are any finalizers (otherwise, skip try-catch)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:21: //   one finalizer returns a non-void result
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:27: // finalizers get all the arguments a prefix could get too
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:28: // plus one new one: "Exception __exception"
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:32: // here finalizers are called without try-catch so they are
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:36: SimpleFinalizer(ref result);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:37: ex = EditFinalizer(ex, ref result);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:43: catch (Exception e)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:47: // finalizers will get another chance here, so they are
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:53: try { SimpleFinalizer(ref result); } catch { }
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:54: try { ex = EditFinalizer(ex, ref result); } catch { }
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:59: // alternative 1: all finalizers are returning void
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:64: // alternative 2: at least one non-void finalizer
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:74: public static void SimpleFinalizer(ref R result) { }
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\execution_with.cs:75: public static Exception EditFinalizer(Exception ex, ref R result) => ex;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\patching-finalizer.cs:1: namespace Patching_Finalizer
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Documentation\examples\patching-finalizer.cs:21: static Exception Finalizer(Exception __exception) => new MyException("Oops", __exception);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\CodeTranspiler.cs:56: return false; // no need, no try-catch info
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Emitter.cs:258: case ExceptionBlockType.BeginCatchBlock:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\Emitter.cs:259: il.BeginCatchBlock(block.catchType);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\FaultBlockRewriter.cs:80: Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, typeof(object))),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\InjectedParameter.cs:32: internal const string EXCEPTION_VAR = "__exception";
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:118: catch { typeArguments = null; }
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:125: catch { methodArguments = null; }
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:272: // The CatchType property is not meaningful for Filter or Finally clauses.
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCopier.cs:288: instr5.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, exception.CatchType));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:102: if (config.finalizers.Count > 0)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:143: if (config.finalizers.Count > 0)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:153: _ = AddFinalizers(false);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:163: // end try, begin catch
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:164: config.AddCode(this.MarkBlock(ExceptionBlockType.BeginCatchBlock));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:168: var endFinalizerLabel = config.DefineLabel();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:169: config.AddCode(Brtrue[endFinalizerLabel]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:171: var rethrowPossible = AddFinalizers(true);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:173: config.AddCode(Nop.WithLabels(endFinalizerLabel));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:187: // end catch
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:194: if (methodEndsInDeadCode == false || config.skipOriginalLabel is not null || config.finalizers.Count > 0 || config.postfixes.Count > 0)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:345: internal bool AddFinalizers(bool catchExceptions)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:350: config.finalizers.Do(fix =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:352: if (catchExceptions)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:401: if (catchExceptions)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreator.cs:403: config.AddCode(this.MarkBlock(ExceptionBlockType.BeginCatchBlock));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:19: internal readonly List<MethodInfo> finalizers;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:30: List<MethodInfo> finalizers,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:40: this.finalizers = finalizers;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodCreatorConfig.cs:89: internal IEnumerable<MethodInfo> Fixes => prefixes.Union(postfixes).Union(finalizers);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\MethodPatcherTools.cs:19: internal const string EXCEPTION_VAR = "__exception";
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchArgumentExtensions.cs:28: catch (NotSupportedException)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchArgumentExtensions.cs:41: catch (NotSupportedException)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchArgumentExtensions.cs:54: catch (NotSupportedException)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:21: var sortedFinalizers = GetSortedPatchMethods(original, patchInfo.finalizers, debug);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:31: sortedFinalizers,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:43: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchFunctions.cs:87: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchModels.cs:17: internal List<HarmonyMethod> finalizers = [];
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchModels.cs:34: case HarmonyPatchType.Finalizer:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchModels.cs:35: finalizers.Add(patch.info);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchModels.cs:66: job.finalizers.Count +
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchModels.cs:84: HarmonyPatchType.Finalizer,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchTools.cs:157: case MethodType.Finalizer:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchTools.cs:158: return AccessTools.DeclaredFinalizer(attr.declaringType);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Internal\PatchTools.cs:201: catch (AmbiguousMatchException ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Attributes.cs:27: Finalizer = 7,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Attributes.cs:114: /// <summary>A finalizer</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Attributes.cs:115: Finalizer,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Attributes.cs:658: /// <summary>Specifies the Finalizer function in a patch class</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Attributes.cs:661: public class HarmonyFinalizer : Attribute
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\ExceptionBlock.cs:13: /// <summary>The beginning of a catch block</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\ExceptionBlock.cs:15: BeginCatchBlock,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\ExceptionBlock.cs:42: /// <summary>Catch type</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\ExceptionBlock.cs:44: public Type catchType;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\ExceptionBlock.cs:48: /// <param name="catchType">The catch type</param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\ExceptionBlock.cs:50: public ExceptionBlock(ExceptionBlockType blockType, Type catchType = null)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\ExceptionBlock.cs:53: this.catchType = catchType ?? typeof(object);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:40: catch
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:178: /// <param name="finalizer">An optional finalizer method wrapped in a <see cref="HarmonyMethod"/> object</param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:181: public MethodInfo Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null/*, HarmonyMethod infix = null*/)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:187: _ = processor.AddFinalizer(finalizer);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Harmony.cs:222: info.Finalizers.DoIf(IDCheck, patchInfo => Unpatch(original, patchInfo.PatchMethod));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:97: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:208: patchInfo.AddFinalizers(instance.Id, [.. job.finalizers]);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:215: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:238: job.finalizers.Do(patch => patchInfo.RemovePatch(patch.method));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:346: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchClassProcessor.cs:369: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patches.cs:23: /// <summary>A collection of finalizer <see cref="Patch"/></summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patches.cs:25: public readonly ReadOnlyCollection<Patch> Finalizers;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patches.cs:46: result.UnionWith(Finalizers.Select(p => p.owner));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patches.cs:57: /// <param name="finalizers">An array of finalizeres as <see cref="Patch"/></param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patches.cs:61: public Patches(Patch[] prefixes, Patch[] postfixes, Patch[] transpilers, Patch[] finalizers, Patch[] innerprefixes, Patch[] innerpostfixes)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patches.cs:66: finalizers ??= [];
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\Patches.cs:73: Finalizers = finalizers.ToList().AsReadOnly();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:37: /// <summary>Finalizers as an array of <see cref="Patch"/></summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:42: public Patch[] finalizers = [];
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:66: || finalizers.Any(p => p.debug)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:127: /// <summary>Adds finalizers</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:131: internal void AddFinalizers(string owner, params HarmonyMethod[] methods) => finalizers = Add(owner, methods, finalizers);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:133: /// <summary>Adds a finalizer</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:136: public void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug) => AddFinalizers(owner, new HarmonyMethod(patch, priority, before, after, debug));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:138: /// <summary>Removes finalizers</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:139: /// <param name="owner">The owner of the finalizers, or <c>*</c> for all</param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:141: public void RemoveFinalizer(string owner) => finalizers = Remove(owner, finalizers);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchInfo.cs:173: finalizers = [.. finalizers.Where(p => p.PatchMethod != patch)];
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:20: HarmonyMethod finalizer;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:95: /// <summary>Adds a finalizer</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:96: /// <param name="finalizer">The finalizer as a <see cref="HarmonyMethod"/></param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:99: public PatchProcessor AddFinalizer(HarmonyMethod finalizer)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:101: this.finalizer = finalizer;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:105: /// <summary>Adds a finalizer</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:106: /// <param name="fixMethod">The finalizer method</param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:109: public PatchProcessor AddFinalizer(MethodInfo fixMethod)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:111: finalizer = new HarmonyMethod(fixMethod);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:187: patchInfo.AddFinalizers(instance.Id, finalizer);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:218: if (type == HarmonyPatchType.All || type == HarmonyPatchType.Finalizer)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:219: patchInfo.RemoveFinalizer(harmonyID);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:262: return new Patches(patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers, patchInfo.innerprefixes, patchInfo.innerpostfixes);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:288: info.finalizers.Do(fix => assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:117: /// This calls and returns <see cref="Assembly.GetTypes"/>, while catching any thrown <see cref="ReflectionTypeLoadException"/>.
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:128: catch (ReflectionTypeLoadException ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:346: catch (InvalidOperationException ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:455: catch (InvalidOperationException ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:702: catch (AmbiguousMatchException ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:804: /// <summary>Gets the reflection information for a finalizer</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:805: /// <param name="type">The class/type that defines the finalizer</param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:806: /// <returns>A method or null when type is null or when the finalizer cannot be found</returns>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:808: public static MethodInfo Finalizer(Type type) => Method(type, "Finalize");
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:810: /// <summary>Gets the reflection information for a directly declared finalizer</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:811: /// <param name="type">The class/type that defines the finalizer</param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:812: /// <returns>A method or null when type is null or when the finalizer cannot be found</returns>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:814: public static MethodInfo DeclaredFinalizer(Type type) => DeclaredMethod(type, "Finalize");
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1285: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1326: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1379: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1446: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1501: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1537: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1569: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1600: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1633: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1680: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1719: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:1742: catch (Exception ex)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessToolsExtensions.cs:186: /// <summary>Gets the reflection information for a finalizer</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessToolsExtensions.cs:187: /// <param name="type">The class/type that defines the finalizer</param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessToolsExtensions.cs:188: /// <returns>A method or null when type is null or when the finalizer cannot be found</returns>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessToolsExtensions.cs:190: public static MethodInfo Finalizer(this Type type) => AccessTools.Finalizer(type);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessToolsExtensions.cs:192: /// <summary>Gets the reflection information for a directly declared finalizer</summary>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessToolsExtensions.cs:193: /// <param name="type">The class/type that defines the finalizer</param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessToolsExtensions.cs:194: /// <returns>A method or null when type is null or when the finalizer cannot be found</returns>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessToolsExtensions.cs:196: public static MethodInfo DeclaredFinalizer(this Type type) => AccessTools.DeclaredFinalizer(type);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\FileLog.cs:246: /// <remarks>Logs the start of an exception handling block (e.g., <c>.try</c>, <c>.catch</c>, <c>.finally</c>, <c>.fault</c>),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\FileLog.cs:250: /// information about the block type (e.g., try, catch, finally) and any associated metadata.</param>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\FileLog.cs:262: case ExceptionBlockType.BeginCatchBlock:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\FileLog.cs:263: // fake log a LEAVE code since BeginCatchBlock() does add it
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\FileLog.cs:269: LogBuffered($".catch {block.catchType}");
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\FileLog.cs:275: // fake log a LEAVE code since BeginCatchBlock() does add it
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\FileLog.cs:287: // fake log a LEAVE code since BeginCatchBlock() does add it
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\FileLog.cs:299: // fake log a LEAVE code since BeginCatchBlock() does add it
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\FileLog.cs:313: /// <remarks>This method handles the logging of specific types of exception blocks, such as the end of a try-catch or

 succeeded in 627ms:
--- Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Public/Harmony.cs
 285: 		public static Patches GetPatchInfo(MethodBase method) => PatchProcessor.GetPatchInfo(method);
 286: 
 287: 		/// <summary>Gets the methods this instance has patched</summary>
 288: 		/// <returns>An enumeration of original methods/constructors</returns>
 289: 		///
 290: 		public IEnumerable<MethodBase> GetPatchedMethods()
 291: 		{
 292: 			return GetAllPatchedMethods()
 293: 				.Where(original => GetPatchInfo(original).Owners.Contains(Id));
 294: 		}
 295: 
 296: 		/// <summary>Gets all patched original methods in the appdomain</summary>
 297: 		/// <returns>An enumeration of patched original methods/constructors</returns>
 298: 		///
 299: 		public static IEnumerable<MethodBase> GetAllPatchedMethods() => PatchProcessor.GetAllPatchedMethods();
 300: 
 301: 		/// <summary>Gets the original method from a given replacement method</summary>
 302: 		/// <param name="replacement">A replacement method (patched original method)</param>
 303: 		/// <returns>The original method/constructor or <c>null</c> if not found</returns>
 304: 		///
 305: 		public static MethodBase GetOriginalMethod(MethodInfo replacement)
--- Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Public/PatchProcessor.cs
 150: 		{
 151: 			innerpostfix = new HarmonyMethod(fixMethod);
 152: 			return this;
 153: 		}
 154: 
 155: 		/// <summary>Gets all patched original methods in the appdomain</summary>
 156: 		/// <returns>An enumeration of patched method/constructor</returns>
 157: 		///
 158: 		public static IEnumerable<MethodBase> GetAllPatchedMethods()
 159: 		{
 160: 			lock (locker)
 161: 			{
 162: 				return HarmonySharedState.GetPatchedMethods();
 163: 			}
 164: 		}
 165: 
 166: 		/// <summary>Applies all registered patches</summary>
 167: 		/// <returns>The generated replacement method</returns>
 168: 		///
 169: 		public MethodInfo Patch()
 170: 		{
 171: 			if (original is null)
 172: 				throw new NullReferenceException($"Null method for {instance.Id}");
 173: 
 174: 			if (original.IsDeclaredMember() is false)
 175: 			{
 176: 				var declaredMember = original.GetDeclaredMember();
 177: 				throw new ArgumentException($"You can only patch implemented methods/constructors. Patch the declared method {declaredMember.FullDescription()} instead.");
 178: 			}
 179: 
 180: 			lock (locker)
 181: 			{
 182: 				var patchInfo = HarmonySharedState.GetPatchInfo(original) ?? new PatchInfo();
 183: 
 184: 				patchInfo.AddPrefixes(instance.Id, prefix);
 185: 				patchInfo.AddPostfixes(instance.Id, postfix);
 186: 				patchInfo.AddTranspilers(instance.Id, transpiler);
 187: 				patchInfo.AddFinalizers(instance.Id, finalizer);
 188: 				patchInfo.AddInnerPrefixes(instance.Id, innerprefix);
 189: 				patchInfo.AddInnerPostfixes(instance.Id, innerpostfix);
 190: 
 191: 				var replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
 192: 				HarmonySharedState.UpdatePatchInfo(original, replacement, patchInfo);
 193: 				return replacement;
 194: 			}
 195: 		}
 257: 		public static Patches GetPatchInfo(MethodBase method)
 258: 		{
 259: 			PatchInfo patchInfo;
 260: 			lock (locker) { patchInfo = HarmonySharedState.GetPatchInfo(method); }
 261: 			if (patchInfo is null) return null;
 262: 			return new Patches(patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers, patchInfo.innerprefixes, patchInfo.innerpostfixes);
 263: 		}
 264: 
--- Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/HarmonySharedState.cs
 120: 			typedef.Fields.Add(new FieldDefinition(
 121: 				"version",
 122: 				Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
 123: 				module.ImportReference(typeof(int))
 124: 			));
 125: 
 126: 			return ReflectionHelper.Load(module).GetType(name);
 127: 		}
 128: 
 129: 		internal static PatchInfo GetPatchInfo(MethodBase method)
 130: 		{
 131: 			byte[] bytes;
 132: 			lock (state) bytes = state.GetValueSafe(method);
 133: 			if (bytes is null) return null;
 134: 			return PatchInfoSerialization.Deserialize(bytes);
 135: 		}
 136: 
 137: 		[SuppressMessage("Style", "IDE0305")]
 138: 		internal static IEnumerable<MethodBase> GetPatchedMethods()
 139: 		{
 140: 			lock (state) return state.Keys.ToArray();
 141: 		}
 142: 
 143: 		internal static void UpdatePatchInfo(MethodBase original, MethodInfo replacement, PatchInfo patchInfo)
 144: 		{
 145: 			patchInfo.VersionCount++;
 146: 			var bytes = patchInfo.Serialize();
 147: 			lock (state) state[original] = bytes;
 148: 			lock (originals) originals[replacement.Identifiable()] = original;
 149: 			if (AccessTools.IsMonoRuntime)
 150: 			{
--- Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/PatchFunctions.cs
   1: using System;
   2: using System.Collections.Generic;
   3: using System.Linq;
   4: using System.Reflection;
   5: 
   6: namespace HarmonyLib
   7: {
   8: 	internal static class PatchFunctions
   9: 	{
  10: 		internal static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches, bool debug)
  11: 			=> [.. new PatchSorter(patches, debug).Sort().Select(p => p.GetMethod(original))];
  12: 		private static List<Infix> GetInfixes(Patch[] patches) => [.. patches.Select(p => new Infix(p))];
  13: 
  14: 		internal static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo)
  15: 		{
  16: 			var debug = patchInfo.Debugging || Harmony.DEBUG;
  17: 
  18: 			var sortedPrefixes = GetSortedPatchMethods(original, patchInfo.prefixes, debug);
  19: 			var sortedPostfixes = GetSortedPatchMethods(original, patchInfo.postfixes, debug);
  20: 			var sortedTranspilers = GetSortedPatchMethods(original, patchInfo.transpilers, debug);
  21: 			var sortedFinalizers = GetSortedPatchMethods(original, patchInfo.finalizers, debug);
  22: 			var sortedInnerPrefixes = GetInfixes(patchInfo.innerprefixes);
  23: 			var sortedInnerPostfixes = GetInfixes(patchInfo.innerpostfixes);
  24: 
  25: 			var patcher = new MethodCreator(new MethodCreatorConfig(
  26: 				original,
  27: 				null,
  28: 				sortedPrefixes,
  29: 				sortedPostfixes,
  30: 				sortedTranspilers,
  31: 				sortedFinalizers,
  32: 				sortedInnerPrefixes,
  33: 				sortedInnerPostfixes,
  34: 				debug
  35: 			));
  36: 			var (replacement, finalInstructions) = patcher.CreateReplacement();
  37: 			if (replacement is null) throw new MissingMethodException($"Cannot create replacement for {original.FullDescription()}");
  38: 
  39: 			try
  40: 			{
  41: 				PatchTools.DetourMethod(original, replacement);
  42: 			}
  43: 			catch (Exception ex)
  44: 			{
  45: 				throw HarmonyException.Create(ex, finalInstructions);
  46: 			}
  47: 			return replacement;
  48: 		}
  49: 
  50: 		internal static MethodInfo ReversePatch(HarmonyMethod standin, MethodBase original, MethodInfo postTranspiler)
  51: 		{
  52: 			if (standin is null)
  53: 				throw new ArgumentNullException(nameof(standin));
  54: 			if (standin.method is null)
  55: 				throw new ArgumentNullException(nameof(standin), $"{nameof(standin)}.{nameof(standin.method)} is NULL");
  56: 
  57: 			var debug = (standin.debug ?? false) || Harmony.DEBUG;
  58: 
  59: 			var transpilers = new List<MethodInfo>();
  60: 			if (standin.reversePatchType == HarmonyReversePatchType.Snapshot)
  61: 			{
  62: 				var info = Harmony.GetPatchInfo(original);
  63: 				transpilers.AddRange(GetSortedPatchMethods(original, [.. info.Transpilers], debug));
  64: 			}
  65: 			if (postTranspiler is not null) transpilers.Add(postTranspiler);
  66: 
  67: 			var emptyFix = new List<MethodInfo>();
  68: 			var emptyInner = new List<Infix>();
  69: 			var patcher = new MethodCreator(new MethodCreatorConfig(
  70: 				standin.method,
  71: 				original,
  72: 				emptyFix,
  73: 				emptyFix,
  74: 				transpilers,
  75: 				emptyFix,
  76: 				emptyInner,
  77: 				emptyInner,
  78: 				debug
  79: 			));
  80: 			var (replacement, finalInstructions) = patcher.CreateReplacement();
--- Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/MethodCreator.cs
 250: 
 251: 					config.AddCode(Nop.WithLabels(label));
 252: 				}
 253: 				else if (tmpObjectVar != null)
 254: 				{
 255: 					config.AddCode(Ldloc[tmpObjectVar]);
 256: 					config.AddCode(Unbox_Any[AccessTools.GetReturnedType(config.original)]);
 257: 					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
 258: 				}
 259: 				tmpBoxVars.Do(tmpBoxVar =>
 260: 				{
 261: 					config.AddCode(new CodeInstruction(config.OriginalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
 262: 					config.AddCode(Ldloc[tmpBoxVar.Key]);
 263: 					config.AddCode(Unbox_Any[tmpBoxVar.Value]);
 264: 					config.AddCode(Stobj[tmpBoxVar.Value]);
 265: 				});
 266: 
 267: 				var returnType = fix.ReturnType;
 268: 				if (returnType != typeof(void))
 269: 				{
 270: 					if (returnType != typeof(bool))
 271: 						throw new Exception($"Prefix patch {fix} has not \"bool\" or \"void\" return type: {fix.ReturnType}");
 272: 					config.AddCode(Stloc[config.runOriginalVariable]);
 273: 				}
 274: 
 275: 				if (skipLabel.HasValue)
 276: 					config.AddCode(Nop.WithLabels(skipLabel.Value));
 277: 			}
 278: 		}
 279: 
 280: 		internal bool AddPostfixes(bool passthroughPatches)
 281: 		{
 282: 			var result = false;
 283: 			var original = config.original;
 284: 			var originalIsStatic = original.IsStatic;
 285: 			foreach (var fix in config.postfixes.Where(fix => passthroughPatches == (fix.ReturnType != typeof(void))))
 286: 			{
 287: 				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
 288: 				config.AddCodes(this.EmitCallParameter(fix, true, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
 289: 				config.AddCode(Call[fix]);
 290: 				if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
 291: 					config.AddCodes(this.RestoreArgumentArray());
 292: 				if (tmpInstanceBoxingVar != null)
 293: 				{
 294: 					config.AddCode(Ldarg_0);
 295: 					config.AddCode(Ldloc[tmpInstanceBoxingVar]);
 296: 					config.AddCode(Unbox_Any[original.DeclaringType]);
 297: 					config.AddCode(Stobj[original.DeclaringType]);
 298: 				}
 299: 				if (refResultUsed)
 300: 				{
 301: 					var label = config.DefineLabel();
 302: 					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
 303: 					config.AddCode(Brfalse_S[label]);
 304: 
 305: 					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
 306: 					config.AddCode(Callvirt[AccessTools.Method(config.GetLocal(InjectionType.ResultRef).LocalType, "Invoke")]);
 307: 					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
 308: 					config.AddCode(Ldnull);
 309: 					config.AddCode(Stloc[config.GetLocal(InjectionType.ResultRef)]);
 310: 
 311: 					config.AddCode(Nop.WithLabels(label));
 312: 				}
 313: 				else if (tmpObjectVar != null)
 314: 				{
 315: 					config.AddCode(Ldloc[tmpObjectVar]);
 316: 					config.AddCode(Unbox_Any[AccessTools.GetReturnedType(original)]);
 317: 					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
 318: 				}
 319: 				tmpBoxVars.Do(tmpBoxVar =>
 320: 				{
 321: 					config.AddCode(new CodeInstruction(originalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
 322: 					config.AddCode(Ldloc[tmpBoxVar.Key]);
 323: 					config.AddCode(Unbox_Any[tmpBoxVar.Value]);
 324: 					config.AddCode(Stobj[tmpBoxVar.Value]);
 325: 				});
 326: 
 327: 				if (fix.ReturnType != typeof(void))
 328: 				{
 329: 					var firstFixParam = fix.GetParameters().FirstOrDefault();
 330: 					var hasPassThroughResultParam = firstFixParam is not null && fix.ReturnType == firstFixParam.ParameterType;
 331: 					if (hasPassThroughResultParam)
 332: 						result = true;
 333: 					else
 334: 					{
 335: 						if (firstFixParam is not null)
 336: 							throw new Exception($"Return type of pass through postfix {fix} does not match type of its first parameter");
 337: 
 338: 						throw new Exception($"Postfix patch {fix} must have a \"void\" return type");
 339: 					}
 340: 				}
 341: 			}
 342: 			return result;
 343: 		}
 344: 
 345: 		internal bool AddFinalizers(bool catchExceptions)
 346: 		{
 347: 			var rethrowPossible = true;
 348: 			var original = config.original;
 349: 			var originalIsStatic = original.IsStatic;
 350: 			config.finalizers.Do(fix =>
 351: 			{
 352: 				if (catchExceptions)
 353: 					config.AddCode(this.MarkBlock(ExceptionBlockType.BeginExceptionBlock));
 354: 
 355: 				var tmpBoxVars = new List<KeyValuePair<LocalBuilder, Type>>();
 356: 				config.AddCodes(this.EmitCallParameter(fix, false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, tmpBoxVars));
 357: 				config.AddCode(Call[fix]);
 358: 				if (MethodPatcherTools.OriginalParameters(fix).Any(pair => pair.realName == MethodPatcherTools.ARGS_ARRAY_VAR))
 359: 					config.AddCodes(this.RestoreArgumentArray());
 360: 				if (tmpInstanceBoxingVar != null)
 361: 				{
 362: 					config.AddCode(Ldarg_0);
 363: 					config.AddCode(Ldloc[tmpInstanceBoxingVar]);
 364: 					config.AddCode(Unbox_Any[original.DeclaringType]);
 365: 					config.AddCode(Stobj[original.DeclaringType]);
 366: 				}
 367: 				if (refResultUsed)
 368: 				{
 369: 					var label = config.DefineLabel();
 370: 					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
 371: 					config.AddCode(Brfalse_S[label]);
 372: 
 373: 					config.AddCode(Ldloc[config.GetLocal(InjectionType.ResultRef)]);
 374: 					config.AddCode(Callvirt[AccessTools.Method(config.GetLocal(InjectionType.ResultRef).LocalType, "Invoke")]);
 375: 					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
 376: 					config.AddCode(Ldnull);
 377: 					config.AddCode(Stloc[config.GetLocal(InjectionType.ResultRef)]);
 378: 
 379: 					config.AddCode(Nop.WithLabels(label));
 380: 				}

 succeeded in 48122ms:
E:\Decompiled_Bannerlord\_categories_v1.4.5\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate\SettlementNameplateWidget.cs:11: public class SettlementNameplateWidget(UIContext context) : Widget(context), IComparable<SettlementNameplateWidget>
E:\Decompiled_Bannerlord\_categories_v1.4.5\UI\TaleWorlds.GauntletUI.CodeGenerator\TaleWorlds.GauntletUI.CodeGenerator\WidgetTemplateGenerateContext.cs:9: public class WidgetTemplateGenerateContext
E:\Decompiled_Bannerlord\_categories_v1.4.5\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetFactory.cs:11: public class WidgetFactory
E:\Decompiled_Bannerlord\_categories_v1.4.5\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetFactory.cs:123: public bool IsCustomType(string typeName)
E:\Decompiled_Bannerlord\_categories_v1.4.5\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetPrefab.cs:397: public void OnRelease()
E:\Decompiled_Bannerlord\_categories_v1.4.5\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetTemplate.cs:11: public class WidgetTemplate
E:\Decompiled_Bannerlord\_categories_v1.4.5\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetTemplate.cs:247: public void OnRelease()
E:\Decompiled_Bannerlord\_categories_v1.4.6\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate\SettlementNameplateWidget.cs:11: public class SettlementNameplateWidget(UIContext context) : Widget(context), IComparable<SettlementNameplateWidget>
E:\Decompiled_Bannerlord\_categories_v1.4.6\UI\TaleWorlds.GauntletUI.CodeGenerator\TaleWorlds.GauntletUI.CodeGenerator\WidgetTemplateGenerateContext.cs:9: public class WidgetTemplateGenerateContext
E:\Decompiled_Bannerlord\_categories_v1.4.6\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetFactory.cs:11: public class WidgetFactory
E:\Decompiled_Bannerlord\_categories_v1.4.6\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetFactory.cs:123: public bool IsCustomType(string typeName)
E:\Decompiled_Bannerlord\_categories_v1.4.6\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetPrefab.cs:397: public void OnRelease()
E:\Decompiled_Bannerlord\_categories_v1.4.6\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetTemplate.cs:11: public class WidgetTemplate
E:\Decompiled_Bannerlord\_categories_v1.4.6\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetTemplate.cs:247: public void OnRelease()
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.GauntletUI.CodeGenerator.cs:842: public class WidgetTemplateGenerateContext
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.GauntletUI.PrefabSystem.cs:966: public class WidgetFactory
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.GauntletUI.PrefabSystem.cs:1078: public bool IsCustomType(string typeName)
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.GauntletUI.PrefabSystem.cs:1680: public void OnRelease()
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.GauntletUI.PrefabSystem.cs:1697: public class WidgetTemplate
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.GauntletUI.PrefabSystem.cs:1937: public void OnRelease()
E:\Decompiled_Bannerlord\_editor_build\TaleWorlds.MountAndBlade.GauntletUI.Widgets.cs:17816: public class SettlementNameplateWidget(UIContext context) : Widget(context), IComparable<SettlementNameplateWidget>
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.CodeGenerator.cs:842: public class WidgetTemplateGenerateContext
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.PrefabSystem.cs:965: public class WidgetFactory
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.PrefabSystem.cs:1077: public bool IsCustomType(string typeName)
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.PrefabSystem.cs:1679: public void OnRelease()
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.PrefabSystem.cs:1696: public class WidgetTemplate
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.GauntletUI.PrefabSystem.cs:1932: public void OnRelease()
E:\Decompiled_Bannerlord\_shipping_build\TaleWorlds.MountAndBlade.GauntletUI.Widgets.cs:17822: public class SettlementNameplateWidget(UIContext context) : Widget(context), IComparable<SettlementNameplateWidget>
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.GauntletUI.CodeGenerator.cs:842: public class WidgetTemplateGenerateContext
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.GauntletUI.PrefabSystem.cs:965: public class WidgetFactory
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.GauntletUI.PrefabSystem.cs:1077: public bool IsCustomType(string typeName)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.GauntletUI.PrefabSystem.cs:1679: public void OnRelease()
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.GauntletUI.PrefabSystem.cs:1696: public class WidgetTemplate
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.GauntletUI.PrefabSystem.cs:1932: public void OnRelease()
E:\Decompiled_Bannerlord\_shipping_build_v1.4.5\TaleWorlds.MountAndBlade.GauntletUI.Widgets.cs:17802: public class SettlementNameplateWidget(UIContext context) : Widget(context), IComparable<SettlementNameplateWidget>
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.GauntletUI.CodeGenerator.cs:842: public class WidgetTemplateGenerateContext
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.GauntletUI.PrefabSystem.cs:965: public class WidgetFactory
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.GauntletUI.PrefabSystem.cs:1077: public bool IsCustomType(string typeName)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.GauntletUI.PrefabSystem.cs:1679: public void OnRelease()
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.GauntletUI.PrefabSystem.cs:1696: public class WidgetTemplate
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.GauntletUI.PrefabSystem.cs:1932: public void OnRelease()
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.GauntletUI.Widgets.cs:17822: public class SettlementNameplateWidget(UIContext context) : Widget(context), IComparable<SettlementNameplateWidget>
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.Widgets.Nameplate\SettlementNameplateWidget.cs:11: public class SettlementNameplateWidget(UIContext context) : Widget(context), IComparable<SettlementNameplateWidget>
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.CodeGenerator\TaleWorlds.GauntletUI.CodeGenerator\WidgetTemplateGenerateContext.cs:9: public class WidgetTemplateGenerateContext
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetFactory.cs:11: public class WidgetFactory
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetFactory.cs:123: public bool IsCustomType(string typeName)
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetPrefab.cs:397: public void OnRelease()
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetTemplate.cs:11: public class WidgetTemplate
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.PrefabSystem\TaleWorlds.GauntletUI.PrefabSystem\WidgetTemplate.cs:247: public void OnRelease()

codex
Harmony evidence supports the exclusion mechanics: `GetAllPatchedMethods()` returns keys from Harmony’s shared `Dictionary<MethodBase, byte[]>`, and patching updates that same key with serialized patch info. That makes the `_shielded` idempotence meaningful for normal method targets.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.GauntletUI.PrefabSystem.cs','E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.MountAndBlade.GauntletUI.Widgets.cs'); foreach("'$p in $files){ Write-Output "--- $p"; $i=0; Get-Content $p | ForEach-Object { $i++; if($p -like '"'*PrefabSystem.cs' -and (("'$i -ge 1070 -and $i -le 1090) -or ($i -ge 1885 -and $i -le 1955) -or ($i -ge 1668 -and $i -le 1695))){'"'{0,5}: {1}' -f "'$i,$_}; if($p -like '"'*Widgets.cs' -and ("'$i -ge 17815 -and $i -le 17940)){ '"'{0,5}: {1}' -f "'$i,$_ } } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\_shipping_build_v1.4.6 -Filter *.cs | Select-String -Pattern 'class MBSubModuleBase|OnGameInitializationFinished\\(|OnApplicationTick\\(|RunOnApplicationThread|ApplicationTick' | Select-Object -First 160 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-ChildItem Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal -Include MethodCreatorTools.cs,MethodPatcherTools.cs -File | ForEach-Object { $p=$_.FullName; Write-Output "--- $p"; $i=0; Get-Content $p | ForEach-Object { $i++; if(($i -ge 120 -and $i -le 150) -or ($i -ge 700 -and $i -le 730)){ '"'{0,4}: {1}' -f "'$i,$_ } } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-ChildItem Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal -Include MethodCreator.cs -File | ForEach-Object { $p=$_.FullName; Write-Output "--- $p"; $i=0; Get-Content $p | ForEach-Object { $i++; if(($i -ge 90 -and $i -le 175) -or ($i -ge 380 -and $i -le 425)){ '"'{0,4}: {1}' -f "'$i,$_ } } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 515ms:
 succeeded in 499ms:
 succeeded in 973ms:
--- E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.GauntletUI.PrefabSystem.cs
 1070: 	}
 1071: 
 1072: 	public Type GetBuiltinType(string name)
 1073: 	{
 1074: 		return _builtinTypes[name];
 1075: 	}
 1076: 
 1077: 	public bool IsCustomType(string typeName)
 1078: 	{
 1079: 		return _customTypePaths.ContainsKey(typeName);
 1080: 	}
 1081: 
 1082: 	public string GetCustomTypePath(string name)
 1083: 	{
 1084: 		if (_customTypePaths.TryGetValue(name, out var value))
 1085: 		{
 1086: 			return value;
 1087: 		}
 1088: 		TaleWorlds.Library.Debug.FailedAssert("false", "C:\\BuildAgent\\work\\mb3\\TaleWorlds.Shared\\Source\\GauntletUI\\TaleWorlds.GauntletUI.PrefabSystem\\WidgetFactory.cs", "GetCustomTypePath", 141);
 1089: 		return "";
 1090: 	}
 1668: 
 1669: 	public WidgetInstantiationResult Instantiate(WidgetCreationData widgetCreationData)
 1670: 	{
 1671: 		return RootTemplate.Instantiate(widgetCreationData, new Dictionary<string, WidgetAttributeTemplate>());
 1672: 	}
 1673: 
 1674: 	public WidgetInstantiationResult Instantiate(WidgetCreationData widgetCreationData, Dictionary<string, WidgetAttributeTemplate> parameters)
 1675: 	{
 1676: 		return RootTemplate.Instantiate(widgetCreationData, parameters);
 1677: 	}
 1678: 
 1679: 	public void OnRelease()
 1680: 	{
 1681: 		RootTemplate.OnRelease();
 1682: 	}
 1683: 
 1684: 	public ConstantDefinition GetConstantValue(string name)
 1685: 	{
 1686: 		Constants.TryGetValue(name, out var value);
 1687: 		return value;
 1688: 	}
 1689: 
 1690: 	public string GetParameterDefaultValue(string name)
 1691: 	{
 1692: 		Parameters.TryGetValue(name, out var value);
 1693: 		return value;
 1694: 	}
 1695: }
 1885: 		SetAttributes(widgetCreationData, widgetInstantiationResult, parameters);
 1886: 		foreach (PrefabExtension prefabExtension in prefabExtensionContext.PrefabExtensions)
 1887: 		{
 1888: 			prefabExtension.AfterAttributesSet(widgetCreationData, widgetInstantiationResult, parameters);
 1889: 		}
 1890: 		return widgetInstantiationResult;
 1891: 	}
 1892: 
 1893: 	private WidgetInstantiationResult CreateWidgets(WidgetCreationData widgetCreationData)
 1894: 	{
 1895: 		_usedFactory = widgetCreationData.WidgetFactory;
 1896: 		PrefabExtensionContext prefabExtensionContext = _usedFactory.PrefabExtensionContext;
 1897: 		UIContext context = widgetCreationData.Context;
 1898: 		Widget widget = null;
 1899: 		Widget parent = widgetCreationData.Parent;
 1900: 		WidgetInstantiationResult widgetInstantiationResult = null;
 1901: 		WidgetInstantiationResult widgetInstantiationResult2 = null;
 1902: 		if (_usedFactory.IsCustomType(_type))
 1903: 		{
 1904: 			WidgetInstantiationResult widgetInstantiationResult3 = _usedFactory.GetCustomType(_type).RootTemplate.CreateWidgets(widgetCreationData);
 1905: 			_customTypeChildren.AddRange(widgetInstantiationResult3.Children.Select((WidgetInstantiationResult c) => c.Template));
 1906: 			widget = widgetInstantiationResult3.Widget;
 1907: 			widgetInstantiationResult = new WidgetInstantiationResult(widget, this, widgetInstantiationResult3);
 1908: 			widgetInstantiationResult2 = widgetInstantiationResult.GetLogicalOrDefaultChildrenLocation();
 1909: 		}
 1910: 		else
 1911: 		{
 1912: 			widget = _usedFactory.CreateBuiltinWidget(context, _type);
 1913: 			widgetInstantiationResult = new WidgetInstantiationResult(widget, this);
 1914: 			parent?.AddChild(widget);
 1915: 			foreach (PrefabExtension prefabExtension in prefabExtensionContext.PrefabExtensions)
 1916: 			{
 1917: 				prefabExtension.OnWidgetCreated(widgetCreationData, widgetInstantiationResult, ChildCount);
 1918: 			}
 1919: 			widgetInstantiationResult2 = widgetInstantiationResult;
 1920: 		}
 1921: 		widget.Tag = Tag;
 1922: 		widget.Id = Id;
 1923: 		foreach (WidgetTemplate child in _children)
 1924: 		{
 1925: 			WidgetCreationData widgetCreationData2 = new WidgetCreationData(widgetCreationData, widgetInstantiationResult2);
 1926: 			WidgetInstantiationResult item = child.CreateWidgets(widgetCreationData2);
 1927: 			widgetInstantiationResult2.Children.Add(item);
 1928: 		}
 1929: 		return widgetInstantiationResult;
 1930: 	}
 1931: 
 1932: 	public void OnRelease()
 1933: 	{
 1934: 		if (_usedFactory.IsCustomType(_type))
 1935: 		{
 1936: 			_usedFactory.OnUnload(_type);
 1937: 		}
 1938: 		foreach (WidgetTemplate child in _children)
 1939: 		{
 1940: 			child.OnRelease();
 1941: 		}
 1942: 		foreach (WidgetTemplate customTypeChild in _customTypeChildren)
 1943: 		{
 1944: 			customTypeChild.OnRelease();
 1945: 		}
 1946: 	}
 1947: 
 1948: 	private void SetAttributes(WidgetCreationData widgetCreationData, WidgetInstantiationResult widgetInstantiationResult, Dictionary<string, WidgetAttributeTemplate> parameters)
 1949: 	{
 1950: 		BrushFactory brushFactory = widgetCreationData.BrushFactory;
 1951: 		SpriteData spriteData = widgetCreationData.SpriteData;
 1952: 		PrefabExtensionContext prefabExtensionContext = widgetCreationData.PrefabExtensionContext;
 1953: 		Widget widget = widgetInstantiationResult.Widget;
 1954: 		WidgetPrefab prefab = widgetInstantiationResult.Template.Prefab;
 1955: 		foreach (PrefabExtension prefabExtension in prefabExtensionContext.PrefabExtensions)
--- E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.MountAndBlade.GauntletUI.Widgets.cs
17815: 		protected override void OnDisconnectedFromRoot()
17816: 		{
17817: 			base.OnDisconnectedFromRoot();
17818: 			_allChildrenNameplates.Clear();
17819: 			_allChildrenNameplates = null;
17820: 		}
17821: 	}
17822: 	public class SettlementNameplateWidget(UIContext context) : Widget(context), IComparable<SettlementNameplateWidget>
17823: 	{
17824: 		public enum TutorialAnimState
17825: 		{
17826: 			Idle,
17827: 			Start,
17828: 			FirstFrame,
17829: 			Playing
17830: 		}
17831: 
17832: 		private float _positionTimer;
17833: 
17834: 		private bool _updatePositionNextFrame;
17835: 
17836: 		private TutorialAnimState _tutorialAnimState;
17837: 
17838: 		private float _lerpThreshold = 5E-05f;
17839: 
17840: 		private float _lerpModifier = 10f;
17841: 
17842: 		private Vector2 _cachedItemSize;
17843: 
17844: 		private bool _lateUpdateActionAdded;
17845: 
17846: 		private Vec2 _position;
17847: 
17848: 		private bool _isVisibleOnMap;
17849: 
17850: 		private bool _isTracked;
17851: 
17852: 		private bool _isInsideWindow;
17853: 
17854: 		private bool _isTargetedByTutorial;
17855: 
17856: 		private int _relationType = -1;
17857: 
17858: 		private int _wSign;
17859: 
17860: 		private float _wPos;
17861: 
17862: 		private float _distanceToCamera;
17863: 
17864: 		private bool _isInRange;
17865: 
17866: 		private bool _canParley;
17867: 
17868: 		private bool _hasPort;
17869: 
17870: 		private SettlementNameplateItemWidget _nameplateItem;
17871: 
17872: 		private ListPanel _notificationListPanel;
17873: 
17874: 		private ListPanel _eventsListPanel;
17875: 
17876: 		private float _screenEdgeAlphaTarget => 1f;
17877: 
17878: 		private float _normalNeutralAlphaTarget => 0.35f;
17879: 
17880: 		private float _normalAllyAlphaTarget => 0.5f;
17881: 
17882: 		private float _normalEnemyAlphaTarget => 0.35f;
17883: 
17884: 		private float _trackedAlphaTarget => 0.8f;
17885: 
17886: 		private float _trackedColorFactorTarget => 1.3f;
17887: 
17888: 		private float _normalColorFactorTarget => 1f;
17889: 
17890: 		public Vec2 Position
17891: 		{
17892: 			get
17893: 			{
17894: 				return _position;
17895: 			}
17896: 			set
17897: 			{
17898: 				if (_position != value)
17899: 				{
17900: 					_position = value;
17901: 					OnPropertyChanged(value, "Position");
17902: 				}
17903: 			}
17904: 		}
17905: 
17906: 		public bool IsVisibleOnMap
17907: 		{
17908: 			get
17909: 			{
17910: 				return _isVisibleOnMap;
17911: 			}
17912: 			set
17913: 			{
17914: 				if (_isVisibleOnMap != value)
17915: 				{
17916: 					if (_isVisibleOnMap && !value)
17917: 					{
17918: 						_positionTimer = 0f;
17919: 					}
17920: 					_isVisibleOnMap = value;
17921: 					OnPropertyChanged(value, "IsVisibleOnMap");
17922: 				}
17923: 			}
17924: 		}
17925: 
17926: 		public bool IsTracked
17927: 		{
17928: 			get
17929: 			{
17930: 				return _isTracked;
17931: 			}
17932: 			set
17933: 			{
17934: 				if (_isTracked != value)
17935: 				{
17936: 					_isTracked = value;
17937: 					OnPropertyChanged(value, "IsTracked");
17938: 				}
17939: 			}
17940: 		}

 succeeded in 1047ms:
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.CampaignSystem.cs:10604: base.GameManager.OnGameInitializationFinished(base.CurrentGame);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.Core.cs:14756: public abstract void OnGameInitializationFinished(Game game);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:89: internal delegate void Managed_ApplicationTick_delegate(float dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:91: internal delegate void Managed_ApplicationTickLight_delegate(float dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:176: Delegates[4] = new Managed_ApplicationTick_delegate(Managed_ApplicationTick);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:177: Delegates[5] = new Managed_ApplicationTickLight_delegate(Managed_ApplicationTickLight);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:243: [MonoPInvokeCallback(typeof(Managed_ApplicationTick_delegate))]
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:244: internal static void Managed_ApplicationTick(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:246: Managed.ApplicationTick(dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:249: [MonoPInvokeCallback(typeof(Managed_ApplicationTickLight_delegate))]
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:250: internal static void Managed_ApplicationTickLight(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.AutoGenerated.cs:252: Managed.ApplicationTickLight(dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.cs:1023: void OnApplicationTick(float dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.cs:1485: internal static void ApplicationTick(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.cs:1494: _components[i].OnApplicationTick(dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.cs:1499: internal static void ApplicationTickLight(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.DotNet.cs:1506: _components[i].OnApplicationTick(dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.Engine.cs:6271: internal static void OnApplicationTick(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.Engine.cs:6378: void IManagedComponent.OnApplicationTick(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.Engine.cs:6380: EngineController.OnApplicationTick(dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.Library.cs:11681: public void OnApplicationTick(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.Library.cs:11694: Debug.FailedAssert(text, "C:\\BuildAgent\\work\\mb3\\TaleWorlds.Shared\\Source\\Base\\TaleWorlds.Library\\TestContext.cs", "OnApplicationTick", 177);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:59815: public override void OnGameInitializationFinished(Game game)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:59819: item.OnGameInitializationFinished(game);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:61265: public abstract class MBSubModuleBase
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:61299: protected internal virtual void OnApplicationTick(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:61343: public virtual void OnGameInitializationFinished(Game game)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:68476: base.GameManager.OnGameInitializationFinished(base.CurrentGame);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:74464: void IManagedComponent.OnApplicationTick(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:74466: Module.CurrentModule.OnApplicationTick(dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:100237: internal void OnApplicationTick(float dt)
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:100263: _testContext.OnApplicationTick(dt);
E:\Decompiled_Bannerlord\_shipping_build_v1.4.6\TaleWorlds.MountAndBlade.cs:100297: item.OnApplicationTick(dt);

codex
The engine-side hot targets match the namespace exclusion: `WidgetFactory.IsCustomType` and `WidgetTemplate.OnRelease` are in `TaleWorlds.GauntletUI.PrefabSystem`, and the nameplate target is in `TaleWorlds.MountAndBlade.GauntletUI.Widgets`. The exclusion covers all three named prefixes.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/MethodCreatorTools.cs','Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/MethodPatcherTools.cs'); foreach("'$p in $files){ Write-Output "--- $p"; $i=0; Get-Content $p | ForEach-Object { $i++; if(($i -ge 120 -and $i -le 150) -or ($i -ge 700 -and $i -le 730)){ '"'{0,4}: {1}' -f "'$i,$_ } } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/MethodCreator.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if(($i -ge 90 -and $i -le 175) -or ($i -ge 380 -and $i -le 425)){ '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/MethodCreatorTools.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if(($i -ge 185 -and $i -le 205) -or ($i -ge 704 -and $i -le 727)){ '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path E:\\Decompiled_Bannerlord\\_shipping_build_v1.4.6 -Filter *.cs | Select-String -Pattern 'public override void OnGameInitializationFinished|protected internal virtual void OnApplicationTick|protected virtual void OnApplicationTick|void IManagedComponent.OnApplicationTick|internal void OnApplicationTick\\(' -Context 0,16 | Select-Object -First 12 | ForEach-Object { "'$_.Context.PreContext + $_.Line + $_.Context.PostContext | ForEach-Object { $_ } ; '"'---' }" in C:\Users\mikew\source\repos\TAOM
 succeeded in 529ms:
--- Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/MethodCreatorTools.cs
 120: 			}
 121: 			return codes;
 122: 		}
 123: 
 124: 		internal static bool AffectsOriginal(this MethodCreator creator, MethodInfo fix)
 125: 		{
 126: 			if (fix.ReturnType == typeof(bool))
 127: 				return true;
 128: 
 129: 			if (creator.config.injections.TryGetValue(fix, out var injectedParameters) == false)
 130: 				return false;
 131: 
 132: 			return injectedParameters.Any(parameter =>
 133: 			{
 134: 				if (parameter.injectionType == InjectionType.Instance)
 135: 					return false;
 136: 				if (parameter.injectionType == InjectionType.OriginalMethod)
 137: 					return false;
 138: 				if (parameter.injectionType == InjectionType.State)
 139: 					return false;
 140: 
 141: 				var p = parameter.parameterInfo;
 142: 				if (p.IsOut || p.IsRetval)
 143: 					return true;
 144: 				var type = p.ParameterType;
 145: 				if (type.IsByRef)
 146: 					return true;
 147: 				if (AccessTools.IsValue(type) is false && AccessTools.IsStruct(type) is false)
 148: 					return true;
 149: 
 150: 				return false;
 700: 				TypeCode.Int32 or TypeCode.UInt32 => Ldind_I4,
 701: 				TypeCode.Int64 or TypeCode.UInt64 => Ldind_I8,
 702: 				TypeCode.Single => Ldind_R4,
 703: 				TypeCode.Double => Ldind_R8,
 704: 				TypeCode.DateTime or TypeCode.Decimal => throw new NotSupportedException(),
 705: 				TypeCode.Empty or TypeCode.Object or TypeCode.DBNull or TypeCode.String => Ldind_Ref,
 706: 				_ => Ldind_Ref,
 707: 			};
 708: 		}
 709: 
 710: 		static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)]);
 711: 		static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)]);
 712: 		static bool EmitOriginalBaseMethod(MethodBase original, List<CodeInstruction> codes)
 713: 		{
 714: 			if (original is MethodInfo method)
 715: 				codes.Add(Ldtoken[method]);
 716: 			else if (original is ConstructorInfo constructor)
 717: 				codes.Add(Ldtoken[constructor]);
 718: 			else
 719: 				return false;
 720: 
 721: 			var type = original.ReflectedType;
 722: 			if (type.IsGenericType)
 723: 				codes.Add(Ldtoken[type]);
 724: 			codes.Add(Call[type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1]);
 725: 			return true;
 726: 		}
 727: 
 728: 		static readonly HashSet<Type> PrimitivesWithObjectTypeCode = [typeof(nint), typeof(nuint), typeof(IntPtr), typeof(UIntPtr)];
 729: 		static CodeInstruction StoreIndOpCodeFor(Type type)
 730: 		{
--- Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Internal/MethodPatcherTools.cs
 120: 					return true;
 121: 				if (AccessTools.IsValue(type) is false && AccessTools.IsStruct(type) is false)
 122: 					return true;
 123: 
 124: 				return false;
 125: 			});
 126: 		}
 127: 
 128: 		static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)]);
 129: 		static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)]);
 130: 		internal static bool EmitOriginalBaseMethod(MethodBase original, Emitter emitter)
 131: 		{
 132: 			if (original is MethodInfo method)
 133: 				emitter.Emit(OpCodes.Ldtoken, method);
 134: 			else if (original is ConstructorInfo constructor)
 135: 				emitter.Emit(OpCodes.Ldtoken, constructor);
 136: 			else
 137: 				return false;
 138: 
 139: 			var type = original.ReflectedType;
 140: 			if (type.IsGenericType)
 141: 				emitter.Emit(OpCodes.Ldtoken, type);
 142: 			emitter.Emit(OpCodes.Call, type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1);
 143: 			return true;
 144: 		}
 145: 
 146: 		static readonly HashSet<Type> PrimitivesWithObjectTypeCode = [typeof(nint), typeof(nuint), typeof(IntPtr), typeof(UIntPtr)];
 147: 		internal static OpCode LoadIndOpCodeFor(Type type)
 148: 		{
 149: 			if (PrimitivesWithObjectTypeCode.Contains(type))
 150: 				return OpCodes.Ldind_I;

 succeeded in 498ms:
  90: 						else
  91: 						{
  92: 							continue;
  93: 						}
  94: 					}
  95: 					var privateStateVariable = config.DeclareLocal(type);
  96: 					config.AddLocal(varName, privateStateVariable);
  97: 					config.AddCodes(this.GenerateVariableInit(privateStateVariable));
  98: 				}
  99: 			});
 100: 
 101: 			config.finalizedVariable = null;
 102: 			if (config.finalizers.Count > 0)
 103: 			{
 104: 				config.finalizedVariable = config.DeclareLocal(typeof(bool));
 105: 				config.AddCodes(this.GenerateVariableInit(config.finalizedVariable));
 106: 				config.exceptionVariable = config.DeclareLocal(typeof(Exception));
 107: 				config.AddLocal(InjectionType.Exception, config.exceptionVariable);
 108: 				config.AddCodes(this.GenerateVariableInit(config.exceptionVariable));
 109: 				// begin try
 110: 				config.AddCode(this.MarkBlock(ExceptionBlockType.BeginExceptionBlock));
 111: 			}
 112: 
 113: 			AddPrefixes();
 114: 			if (config.skipOriginalLabel.HasValue)
 115: 				config.AddCodes([Ldloc[config.runOriginalVariable], Brfalse[config.skipOriginalLabel.Value]]);
 116: 
 117: 			var copier = new MethodCopier(config);
 118: 			foreach (var transpiler in config.transpilers)
 119: 				copier.AddTranspiler(transpiler);
 120: 			copier.AddTranspiler(PatchTools.m_GetExecutingAssemblyReplacementTranspiler);
 121: 
 122: 			var endLabels = new List<Label>();
 123: 			var replacement = copier.Finalize(true, out var hasReturnCode, out var methodEndsInDeadCode, endLabels);
 124: 
 125: 			replacement = [.. AddInfixes(replacement)];
 126: 
 127: 			config.AddCode(Nop["start original"]);
 128: 			config.AddCodes(this.CleanupCodes(replacement, endLabels));
 129: 			config.AddCode(Nop["end original"]);
 130: 			if (endLabels.Count > 0)
 131: 				config.AddCode(Nop.WithLabels(endLabels));
 132: 			if (config.resultVariable is not null && hasReturnCode)
 133: 				config.AddCode(Stloc[config.resultVariable]);
 134: 			if (config.skipOriginalLabel.HasValue)
 135: 				config.AddCode(Nop.WithLabels(config.skipOriginalLabel.Value));
 136: 
 137: 			_ = AddPostfixes(false);
 138: 			if (config.resultVariable is not null && (hasReturnCode || (methodEndsInDeadCode && config.skipOriginalLabel.HasValue)))
 139: 				config.AddCode(Ldloc[config.resultVariable]);
 140: 
 141: 			var needsToStorePassthroughResult = AddPostfixes(true);
 142: 
 143: 			if (config.finalizers.Count > 0)
 144: 			{
 145: 				var exceptionVariable = config.GetLocal(InjectionType.Exception);
 146: 
 147: 				if (needsToStorePassthroughResult)
 148: 				{
 149: 					config.AddCode(Stloc[config.resultVariable]);
 150: 					config.AddCode(Ldloc[config.resultVariable]);
 151: 				}
 152: 
 153: 				_ = AddFinalizers(false);
 154: 				config.AddCode(Ldc_I4_1);
 155: 				config.AddCode(Stloc[config.finalizedVariable]);
 156: 				var noExceptionLabel1 = config.DefineLabel();
 157: 				config.AddCode(Ldloc[exceptionVariable]);
 158: 				config.AddCode(Brfalse[noExceptionLabel1]);
 159: 				config.AddCode(Ldloc[exceptionVariable]);
 160: 				config.AddCode(Throw);
 161: 				config.AddCode(Nop.WithLabels(noExceptionLabel1));
 162: 
 163: 				// end try, begin catch
 164: 				config.AddCode(this.MarkBlock(ExceptionBlockType.BeginCatchBlock));
 165: 				config.AddCode(Stloc[exceptionVariable]);
 166: 
 167: 				config.AddCode(Ldloc[config.finalizedVariable]);
 168: 				var endFinalizerLabel = config.DefineLabel();
 169: 				config.AddCode(Brtrue[endFinalizerLabel]);
 170: 
 171: 				var rethrowPossible = AddFinalizers(true);
 172: 
 173: 				config.AddCode(Nop.WithLabels(endFinalizerLabel));
 174: 
 175: 				var noExceptionLabel2 = config.DefineLabel();
 380: 				}
 381: 				else if (tmpObjectVar != null)
 382: 				{
 383: 					config.AddCode(Ldloc[tmpObjectVar]);
 384: 					config.AddCode(Unbox_Any[AccessTools.GetReturnedType(original)]);
 385: 					config.AddCode(Stloc[config.GetLocal(InjectionType.Result)]);
 386: 				}
 387: 				tmpBoxVars.Do(tmpBoxVar =>
 388: 				{
 389: 					config.AddCode(new CodeInstruction(originalIsStatic ? OpCodes.Ldarg_0 : OpCodes.Ldarg_1));
 390: 					config.AddCode(Ldloc[tmpBoxVar.Key]);
 391: 					config.AddCode(Unbox_Any[tmpBoxVar.Value]);
 392: 					config.AddCode(Stobj[tmpBoxVar.Value]);
 393: 				});
 394: 
 395: 				if (fix.ReturnType != typeof(void))
 396: 				{
 397: 					config.AddCode(Stloc[config.GetLocal(InjectionType.Exception)]);
 398: 					rethrowPossible = false;
 399: 				}
 400: 
 401: 				if (catchExceptions)
 402: 				{
 403: 					config.AddCode(this.MarkBlock(ExceptionBlockType.BeginCatchBlock));
 404: 					config.AddCode(Pop);
 405: 					config.AddCode(this.MarkBlock(ExceptionBlockType.EndExceptionBlock));
 406: 				}
 407: 			});
 408: 
 409: 			return rethrowPossible;
 410: 		}
 411: 
 412: 		IEnumerable<CodeInstruction> AddInfixes(IEnumerable<CodeInstruction> instructions)
 413: 		{
 414: 			var callGroups = instructions
 415: 			.Where(ins => ins.opcode == OpCodes.Call || ins.opcode == OpCodes.Callvirt)
 416: 			.Where(ins => ins.operand is MethodInfo)
 417: 			.GroupBy(ins => (MethodInfo)ins.operand);
 418: 
 419: 			var replacements = new Dictionary<CodeInstruction, CodeInstruction[]>();
 420: 			foreach (var (innerMethod, calls) in callGroups.Select(g => (g.Key, Calls: g.ToList())))
 421: 			{
 422: 				var total = calls.Count;
 423: 				for (var i = 0; i < total; i++)
 424: 				{
 425: 					var callInstruction = calls[i];

 succeeded in 512ms:
 185: 			{
 186: 				injections.RemoveAt(0);
 187: 				parameters.RemoveAt(0);
 188: 			}
 189: 
 190: 			foreach (var injection in injections)
 191: 			{
 192: 				var injectionType = injection.injectionType;
 193: 				var paramRealName = injection.realName;
 194: 				var paramType = injection.parameterInfo.ParameterType;
 195: 
 196: 				if (injectionType == InjectionType.OriginalMethod)
 197: 				{
 198: 					if (EmitOriginalBaseMethod(original, codes))
 199: 						continue;
 200: 
 201: 					codes.Add(Ldnull);
 202: 					continue;
 203: 				}
 204: 
 205: 				if (injectionType == InjectionType.Exception)
 704: 				TypeCode.DateTime or TypeCode.Decimal => throw new NotSupportedException(),
 705: 				TypeCode.Empty or TypeCode.Object or TypeCode.DBNull or TypeCode.String => Ldind_Ref,
 706: 				_ => Ldind_Ref,
 707: 			};
 708: 		}
 709: 
 710: 		static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle)]);
 711: 		static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", [typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)]);
 712: 		static bool EmitOriginalBaseMethod(MethodBase original, List<CodeInstruction> codes)
 713: 		{
 714: 			if (original is MethodInfo method)
 715: 				codes.Add(Ldtoken[method]);
 716: 			else if (original is ConstructorInfo constructor)
 717: 				codes.Add(Ldtoken[constructor]);
 718: 			else
 719: 				return false;
 720: 
 721: 			var type = original.ReflectedType;
 722: 			if (type.IsGenericType)
 723: 				codes.Add(Ldtoken[type]);
 724: 			codes.Add(Call[type.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1]);
 725: 			return true;
 726: 		}
 727: 

 succeeded in 771ms:
		void IManagedComponent.OnApplicationTick(float dt)
		{
			EngineController.OnApplicationTick(dt);
		}

		private void OnInitialize()
		{
			List<Type> typesSafe = AssemblyLoader.LoadFrom(ManagedCallbacksDll).GetTypesSafe();
			Type type = null;
			foreach (Type item in typesSafe)
			{
				if (Enumerable.Contains<Type>(item.GetInterfaces(), typeof(ICallbackManager)))
				{
					type = item;
					break;
				}
			}
---
		public override void OnGameInitializationFinished(Game game)
		{
			foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
			{
				item.OnGameInitializationFinished(game);
			}
			foreach (SkeletonScale objectType in Game.Current.ObjectManager.GetObjectTypeList<SkeletonScale>())
			{
				sbyte[] array = new sbyte[objectType.BoneNames.Count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = Skeleton.GetBoneIndexFromName(objectType.SkeletonModel, objectType.BoneNames[i]);
				}
				objectType.SetBoneIndices(array);
			}
		}

---
		protected internal virtual void OnApplicationTick(float dt)
		{
		}

		protected internal virtual void AfterAsyncTickTick(float dt)
		{
		}

		protected internal virtual void InitializeGameStarter(Game game, IGameStarter starterObject)
		{
		}

		public virtual void OnGameLoaded(Game game, object initializerObject)
		{
		}

		public virtual void OnAfterGameLoaded(Game game)
---
		void IManagedComponent.OnApplicationTick(float dt)
		{
			Module.CurrentModule.OnApplicationTick(dt);
		}

		[MBCallback(null, false)]
		internal static void CheckSharedStructureSizes()
		{
			_callbackManager.CheckSharedStructureSizes();
		}

		[MBCallback(null, false)]
		internal static void EngineApiMethodInterfaceInitializer(int id, IntPtr pointer)
		{
			_engineApiPointers.Add(id, pointer);
		}

---
		internal void OnApplicationTick(float dt)
		{
			bool isOnlyCoreContentEnabled = IsOnlyCoreContentEnabled;
			IsOnlyCoreContentEnabled = Utilities.IsOnlyCoreContentEnabled();
			if (isOnlyCoreContentEnabled != IsOnlyCoreContentEnabled && isOnlyCoreContentEnabled)
			{
				InitialState initialState;
				if ((initialState = GameStateManager.Current.ActiveState as InitialState) != null)
				{
					Utilities.DisableCoreGame();
					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=CaSafuAH}Content Download Complete").ToString(), new TextObject("{=1nKa4pQX}Rest of the game content has been downloaded.").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: false, new TextObject("{=yS7PvrTD}OK").ToString(), null, delegate
					{
						initialState.RefreshContentState();
					}, null));
				}
				else
				{
---

codex
For the CLR-specific suspects I’m not relying on memory. I’m going to compile and run a tiny net472 probe under `C:\tmp` to verify `Thread.Suspend` nesting and `System.Threading.Timer` callback overlap on the runtime this mod targets.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$src = @'"'
using System;
using System.Threading;

public static class Program
{
    private static volatile bool _run = true;
    private static int _ticks;
    private static int _active;
    private static int _maxActive;
    private static int _timerCalls;

    public static void Main()
    {
        Console.WriteLine(\"Runtime=\" + Environment.Version);
        TestSuspendNesting();
        TestTimerOverlap();
    }

    private static void TestSuspendNesting()
    {
        _run = true;
        _ticks = 0;
        var t = new Thread(() => { while (_run) { Interlocked.Increment(ref _ticks); Thread.Sleep(1); } });
        t.IsBackground = true;
        t.Start();
        Thread.Sleep(100);
        int before = _ticks;
#pragma warning disable 0618
        t.Suspend();
        Thread.Sleep(100);
        int afterFirstSuspend = _ticks;
        t.Suspend();
        Thread.Sleep(100);
        int afterSecondSuspend = _ticks;
        t.Resume();
        Thread.Sleep(150);
        int afterOneResume = _ticks;
        t.Resume();
#pragma warning restore 0618
        Thread.Sleep(150);
        int afterTwoResumes = _ticks;
        _run = false;
        t.Join(1000);
        Console.WriteLine(\"SuspendNesting before={0} first={1} second={2} oneResume={3} twoResumes={4}\", before, afterFirstSuspend, afterSecondSuspend, afterOneResume, afterTwoResumes);
    }

    private static void TestTimerOverlap()
    {
        _active = 0;
        _maxActive = 0;
        _timerCalls = 0;
        Timer timer = null;
        timer = new Timer(_ =>
        {
            int now = Interlocked.Increment(ref _active);
            int old;
            while (now > (old = _maxActive)) Interlocked.CompareExchange(ref _maxActive, now, old);
            Interlocked.Increment(ref _timerCalls);
            Thread.Sleep(500);
            Interlocked.Decrement(ref _active);
        }, null, 0, 100);
        Thread.Sleep(1800);
        timer.Dispose();
        Thread.Sleep(700);
        Console.WriteLine(\"TimerOverlap calls={0} maxActive={1}\", _timerCalls, _maxActive);
    }
}
'@
"'$dir='"'C:\\tmp\\taom-thread-probe'
New-Item -ItemType Directory -Force -Path "'$dir | Out-Null
$cs=Join-Path $dir '"'Probe.cs'
"'$exe=Join-Path $dir '"'Probe.exe'
Set-Content -Path "'$cs -Value $src -Encoding ASCII
$csc='"'C:\\Windows\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe'
if ("'!(Test-Path $csc)) { $csc='"'C:\\Windows\\Microsoft.NET\\Framework\\v4.0.30319\\csc.exe' }
& "'$csc /nologo /target:exe /out:$exe $cs
& $exe' in C:\Users\mikew\source\repos\TAOM
2026-07-10T17:36:00.907471Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.6 seconds
Output:
error CS2001: Source file 'C:\tmp\taom-thread-probe\Probe.cs' could not be found
warning CS2008: No source files specified
[31;1mNew-Item: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  72 | [0m [36;1mNew-Item -ItemType Directory -Force -Path $dir[0m | Out-Null[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\tmp\taom-thread-probe' is denied.[0m
[31;1mSet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  75 | [0m [36;1mSet-Content -Path $cs -Value $src -Encoding ASCII[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCould not find a part of the path 'C:\tmp\taom-thread-probe\Probe.cs'.[0m
[31;1m&: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  79 | [0m & [36;1m$exe[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m   ~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'C:\tmp\taom-thread-probe\Probe.exe' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 599ms:
error CS2001: Source file 'C:\tmp\taom-thread-probe\Probe.cs' could not be found
warning CS2008: No source files specified
[31;1mNew-Item: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  72 | [0m [36;1mNew-Item -ItemType Directory -Force -Path $dir[0m | Out-Null[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mAccess to the path 'C:\tmp\taom-thread-probe' is denied.[0m
[31;1mSet-Content: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  75 | [0m [36;1mSet-Content -Path $cs -Value $src -Encoding ASCII[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mCould not find a part of the path 'C:\tmp\taom-thread-probe\Probe.cs'.[0m
[31;1m&: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m  79 | [0m & [36;1m$exe[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m   ~~~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'C:\tmp\taom-thread-probe\Probe.exe' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

codex
The first probe write failed because `C:\tmp` is not writable in this sandbox despite the advertised root. I’m going to use an in-memory `Add-Type` probe under Windows PowerShell/.NET Framework instead, which avoids adding scratch files to the repo.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$PSVersionTable | Out-String' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'if (Get-Command powershell.exe -ErrorAction SilentlyContinue) { powershell.exe -NoProfile -Command "$PSVersionTable.PSVersion.ToString(); [System.Environment]::Version.ToString()" } else { '"'no powershell.exe' }" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path C:\\tmp -Force -ErrorAction SilentlyContinue | Select-Object -First 20 | Format-Table -AutoSize | Out-String" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Test-Path C:\\tmp; (Get-Item C:\\tmp -ErrorAction SilentlyContinue | Format-List * | Out-String)" in C:\Users\mikew\source\repos\TAOM
 succeeded in 594ms:

Name                           Value
----                           -----
PSVersion                      7.6.2
PSEdition                      Core
GitCommitId                    7.6.2
OS                             Microsoft Windows 10.0.26200
Platform                       Win32NT
PSCompatibleVersions           {1.0, 2.0, 3.0, 4.0…}
PSRemotingProtocolVersion      2.4
SerializationVersion           1.1.0.1
WSManStackVersion              3.0



 succeeded in 656ms:

    Directory: C:\tmp

Mode        LastWriteTime Length Name
----        ------------- ------ ----
d----   4/9/2026  4:24 PM        bl-harmony-decompiled
d----  2/13/2026  1:30 PM        claude
d----   4/9/2026  4:23 PM        harmony-decompiled
d----   4/9/2026 12:46 PM        uiextenderex_src
d----   4/9/2026  2:27 PM        uiextenderex-decompiled
-a---  5/30/2026  9:57 PM   3351 AbilityCatalogService.cs
-a---  1/24/2026  9:27 PM   4433 common_lords.txt
-a---  5/30/2026  9:57 PM  20875 CraftingPieceCatalogService.cs
-a---  5/30/2026  9:56 PM  26970 CrossReferenceService.cs
-a---  5/30/2026  9:57 PM  17710 FactionCatalogService.cs
-a---   4/5/2026  2:04 PM   8230 final_report.txt
-a---  1/24/2026  9:27 PM   5727 find_common_lords.ps1
-a---  1/24/2026  9:27 PM    866 find_missing_lords.ps1
-a---   4/7/2026  5:09 PM  17101 gen_remaining_careers.py
-a---  1/24/2026  9:28 PM   4226 generate_lord_templates.ps1
-a---  1/24/2026  8:52 PM  44313 is_female_template.xslt
-a---  5/30/2026  9:57 PM   7769 ItemCatalogService.cs
-a---  5/30/2026  9:57 PM   3703 ItemTraitCatalogService.cs
-a---  5/30/2026  9:56 PM   2643 IValidationService.cs
-a---  1/24/2026  9:26 PM   8366 lotraom_lords.txt



 succeeded in 654ms:
True

PSPath              : Microsoft.PowerShell.Core\FileSystem::C:\tmp
PSParentPath        : Microsoft.PowerShell.Core\FileSystem::C:\
PSChildName         : tmp
PSDrive             : C
PSProvider          : Microsoft.PowerShell.Core\FileSystem
PSIsContainer       : True
Mode                : d----
ModeWithoutHardLink : d----
BaseName            : tmp
ResolvedTarget      : C:\tmp
Target              : 
LinkType            : 
Name                : tmp
Parent              : C:\
Root                : C:\
Exists              : True
FullName            : C:\tmp
Extension           : 
CreationTime        : 1/24/2026 8:52:40 PM
CreationTimeUtc     : 1/25/2026 2:52:40 AM
LastAccessTime      : 7/10/2026 12:36:17 PM
LastAccessTimeUtc   : 7/10/2026 5:36:17 PM
LastWriteTime       : 6/5/2026 10:27:44 PM
LastWriteTimeUtc    : 6/6/2026 3:27:44 AM
LinkTarget          : 
UnixFileMode        : -1
Attributes          : Directory



2026-07-10T17:36:17.774558Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.8 seconds
Output:
At line:1 char:68
+ ... .Management.Automation.PSVersionHashTable.PSVersion.ToString(); [Syst ...
+                                                                  ~
An expression was expected after '('.
    + CategoryInfo          : ParserError: (:) [], ParentContainsErrorRecordException
    + FullyQualifiedErrorId : ExpectedExpression
 

 exited 1 in 766ms:
At line:1 char:68
+ ... .Management.Automation.PSVersionHashTable.PSVersion.ToString(); [Syst ...
+                                                                  ~
An expression was expected after '('.
    + CategoryInfo          : ParserError: (:) [], ParentContainsErrorRecordException
    + FullyQualifiedErrorId : ExpectedExpression
 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "powershell.exe -NoProfile -Command '"'$PSVersionTable.PSVersion.ToString(); [System.Environment]::Version.ToString()'"'" in C:\Users\mikew\source\repos\TAOM
 succeeded in 535ms:
5.1.26100.8655
4.0.30319.42000

codex
Windows PowerShell 5.1 is available and is running CLR 4.0.30319, so the probe can exercise the same Thread APIs Bannerlord’s net472 code uses.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "powershell.exe -NoProfile -Command @'
"'$src = @"
using System;
using System.Threading;

public static class ThreadTimerProbe
{
    private static volatile bool _run = true;
    private static int _ticks;
    private static int _active;
    private static int _maxActive;
    private static int _timerCalls;

    public static void Run()
    {
        Console.WriteLine("Runtime=" + Environment.Version);
        TestSuspendNesting();
        TestTimerOverlap();
    }

    private static void TestSuspendNesting()
    {
        _run = true;
        _ticks = 0;
        var t = new Thread(() => { while (_run) { Interlocked.Increment(ref _ticks); Thread.Sleep(1); } });
        t.IsBackground = true;
        t.Start();
        Thread.Sleep(100);
        int before = _ticks;
#pragma warning disable 0618
        t.Suspend();
        Thread.Sleep(100);
        int afterFirstSuspend = _ticks;
        t.Suspend();
        Thread.Sleep(100);
        int afterSecondSuspend = _ticks;
        t.Resume();
        Thread.Sleep(150);
        int afterOneResume = _ticks;
        t.Resume();
#pragma warning restore 0618
        Thread.Sleep(150);
        int afterTwoResumes = _ticks;
        _run = false;
        t.Join(1000);
        Console.WriteLine("SuspendNesting before={0} first={1} second={2} oneResume={3} twoResumes={4}", before, afterFirstSuspend, afterSecondSuspend, afterOneResume, afterTwoResumes);
    }

    private static void TestTimerOverlap()
    {
        _active = 0;
        _maxActive = 0;
        _timerCalls = 0;
        Timer timer = null;
        timer = new Timer(_ =>
        {
            int now = Interlocked.Increment(ref _active);
            int old;
            while (now > (old = _maxActive)) Interlocked.CompareExchange(ref _maxActive, now, old);
            Interlocked.Increment(ref _timerCalls);
            Thread.Sleep(500);
            Interlocked.Decrement(ref _active);
        }, null, 0, 100);
        Thread.Sleep(1800);
        timer.Dispose();
        Thread.Sleep(700);
        Console.WriteLine("TimerOverlap calls={0} maxActive={1}", _timerCalls, _maxActive);
    }
}
"@
Add-Type -TypeDefinition $src -Language CSharp
[ThreadTimerProbe]::Run()
'"'@" in C:\Users\mikew\source\repos\TAOM
2026-07-10T17:36:39.057964Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 1.3 seconds
Output:
Runtime=4.0.30319.42000
Exception calling "Run" with "0" argument(s): "Thread is not user-suspended; it cannot be resumed."
At line:71 char:1
+ [ThreadTimerProbe]::Run()
+ ~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : NotSpecified: (:) [], MethodInvocationException
    + FullyQualifiedErrorId : ThreadStateException
 

 exited 1 in 1326ms:
Runtime=4.0.30319.42000
Exception calling "Run" with "0" argument(s): "Thread is not user-suspended; it cannot be resumed."
At line:71 char:1
+ [ThreadTimerProbe]::Run()
+ ~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : NotSpecified: (:) [], MethodInvocationException
    + FullyQualifiedErrorId : ThreadStateException
 

codex
The first suspend probe already surfaced the key behavior: after two `Suspend()` calls, the second `Resume()` threw `ThreadStateException` because the first resume had already released the thread. I’m rerunning with exception capture so the review can cite exact values instead of just the thrown message.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "powershell.exe -NoProfile -Command @'
"'$src = @"
using System;
using System.Threading;

public static class ThreadTimerProbe2
{
    private static volatile bool _run = true;
    private static int _ticks;
    private static int _active;
    private static int _maxActive;
    private static int _timerCalls;

    public static void Run()
    {
        Console.WriteLine("Runtime=" + Environment.Version);
        TestSuspendNesting();
        TestTimerOverlap();
    }

    private static void TestSuspendNesting()
    {
        _run = true;
        _ticks = 0;
        var t = new Thread(() => { while (_run) { Interlocked.Increment(ref _ticks); Thread.Sleep(1); } });
        t.IsBackground = true;
        t.Start();
        Thread.Sleep(100);
        int before = _ticks;
        string secondSuspend = "ok", firstResume = "ok", secondResume = "ok";
#pragma warning disable 0618
        try { t.Suspend(); } catch (Exception ex) { Console.WriteLine("firstSuspend=" + ex.GetType().Name + ":" + ex.Message); }
        Thread.Sleep(100);
        int afterFirstSuspend = _ticks;
        try { t.Suspend(); } catch (Exception ex) { secondSuspend = ex.GetType().Name + ":" + ex.Message; }
        Thread.Sleep(100);
        int afterSecondSuspend = _ticks;
        try { t.Resume(); } catch (Exception ex) { firstResume = ex.GetType().Name + ":" + ex.Message; }
        Thread.Sleep(150);
        int afterOneResume = _ticks;
        try { t.Resume(); } catch (Exception ex) { secondResume = ex.GetType().Name + ":" + ex.Message; }
#pragma warning restore 0618
        Thread.Sleep(150);
        int afterTwoResumes = _ticks;
        _run = false;
        t.Join(1000);
        Console.WriteLine("SuspendNesting before={0} first={1} second={2} oneResume={3} twoResumes={4} secondSuspend={5} firstResume={6} secondResume={7}", before, afterFirstSuspend, afterSecondSuspend, afterOneResume, afterTwoResumes, secondSuspend, firstResume, secondResume);
    }

    private static void TestTimerOverlap()
    {
        _active = 0;
        _maxActive = 0;
        _timerCalls = 0;
        Timer timer = null;
        timer = new Timer(_ =>
        {
            int now = Interlocked.Increment(ref _active);
            int old;
            while (now > (old = _maxActive)) Interlocked.CompareExchange(ref _maxActive, now, old);
            Interlocked.Increment(ref _timerCalls);
            Thread.Sleep(500);
            Interlocked.Decrement(ref _active);
        }, null, 0, 100);
        Thread.Sleep(1800);
        timer.Dispose();
        Thread.Sleep(700);
        Console.WriteLine("TimerOverlap calls={0} maxActive={1}", _timerCalls, _maxActive);
    }
}
"@
Add-Type -TypeDefinition $src -Language CSharp
[ThreadTimerProbe2]::Run()
'"'@" in C:\Users\mikew\source\repos\TAOM
 succeeded in 3914ms:
Runtime=4.0.30319.42000
SuspendNesting before=8 first=9 second=9 oneResume=20 twoResumes=30 secondSuspend=ok firstResume=ok secondResume=ThreadStateException:Thread is not user-suspended; it cannot be resumed.
TimerOverlap calls=17 maxActive=5

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProvider.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main/Features/BattleLoadDiagnostics -Recurse -Include *.cs | Select-String -Pattern 'EnableStallWatchdog|IsWatchdog|Sampler|EnableBattleLoadDiagnostics|SettingsProvider' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main -Recurse -Include *.cs | Select-String -Pattern 'IsEnabled =>|EnableBattleLoadDiagnostics|EnableStallWatchdog' | Select-Object -First 120 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 531ms:
   1: using MCM.Abstractions.Attributes;
   2: using MCM.Abstractions.Attributes.v2;
   3: using MCM.Abstractions.Base.Global;
   4: 
   5: namespace TAOM.Features.BattleLoadDiagnostics;
   6: 
   7: // Dedicated MCM page (mirrors CrashReportSettings). Defaults are the "diagnose now"
   8: // posture — everything ON — because we ship this specifically to capture the
   9: // intermittent battle-load hang from user machines. Players who hit perf issues can
  10: // disable it.
  11: public sealed class BattleLoadDiagnosticsSettings : AttributeGlobalSettings<BattleLoadDiagnosticsSettings>
  12: {
  13:     public override string Id => "TAOM.BattleLoadDiagnostics";
  14:     public override string DisplayName => "TAOM — Battle Load Diagnostics";
  15:     public override string FolderName => "TAOM";
  16:     public override string FormatType => "json2";
  17: 
  18:     [SettingPropertyGroup("Master")]
  19:     [SettingPropertyBool("Enable Battle Load Diagnostics", Order = 0,
  20:         HintText = "Logs the full attack->battle-playable lifecycle (encounter, scene selection, Mission.Initialize, every initial-spawn agent's equipment + collision-mesh names) to the TAOM debug log. Leave ON while diagnosing the intermittent battle-load hang — the LAST line in the log names the stuck phase / agent.")]
  21:     public bool EnableBattleLoadDiagnostics { get; set; } = true;
  22: 
  23:     [SettingPropertyGroup("Stall Watchdog")]
  24:     [SettingPropertyBool("Enable Stall Watchdog", Order = 0,
  25:         HintText = "A background-thread timer that detects a battle stuck on the loading screen and writes a 'STILL LOADING' marker naming the last phase reached. Runs off the main thread so it still fires when the game is frozen. Default ON.")]
  26:     public bool EnableStallWatchdog { get; set; } = true;
  27: 
  28:     [SettingPropertyGroup("Stall Watchdog")]
  29:     [SettingPropertyBool("Auto-Write Crash Bundle On Stall", Order = 1,
  30:         HintText = "When the watchdog fires, also write a crash-report ZIP (under Logs/) so you can send it in one action. Requires Crash Report capture enabled. Default ON.")]
  31:     public bool EnableStallWatchdogBundle { get; set; } = true;
  32: 
  33:     [SettingPropertyGroup("Stall Watchdog")]
  34:     [SettingPropertyInteger("Stall Threshold (seconds)", 10, 600, Order = 2,
  35:         HintText = "How long a battle load may run before the watchdog flags it as stalled. Default 300s (5 min) — large custom siege scenes (e.g. Minas Tirith) legitimately take minutes to load on first entry.")]
  36:     public int StallWatchdogSeconds { get; set; } = 300;
  37: }

 succeeded in 557ms:
   1: using TAOM.Core.Validation;
   2: 
   3: namespace TAOM.Features.BattleLoadDiagnostics;
   4: 
   5: // Reads the MCM page, fail-open to defaults if MCM isn't ready. The watchdog threshold
   6: // is range/NaN-guarded per the config-validation rule (belt-and-braces — the MCM integer
   7: // attribute already clamps 10..300, but a provider must never pass an unvalidated value
   8: // into a comparison).
   9: public sealed class BattleLoadDiagnosticsSettingsProvider : IBattleLoadDiagnosticsSettingsProvider
  10: {
  11:     private const double DefaultWatchdogSeconds = 300d;
  12:     private const double MinWatchdogSeconds = 10d;
  13:     private const double MaxWatchdogSeconds = 600d;
  14: 
  15:     public bool IsEnabled =>
  16:         BattleLoadDiagnosticsSettings.Instance?.EnableBattleLoadDiagnostics ?? true;
  17: 
  18:     public bool StallWatchdogEnabled =>
  19:         BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdog ?? true;
  20: 
  21:     public bool StallWatchdogBundleEnabled =>
  22:         BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdogBundle ?? true;
  23: 
  24:     public double StallWatchdogSeconds
  25:     {
  26:         get
  27:         {
  28:             double raw = BattleLoadDiagnosticsSettings.Instance?.StallWatchdogSeconds ?? (int)DefaultWatchdogSeconds;
  29:             return FiniteFloatValidator.IsFiniteInRange(raw, MinWatchdogSeconds, MaxWatchdogSeconds)
  30:                 ? raw
  31:                 : DefaultWatchdogSeconds;
  32:         }
  33:     }
  34: }

 succeeded in 613ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:10: container.Register<IBattleLoadDiagnosticsSettingsProvider, BattleLoadDiagnosticsSettingsProvider>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:16: container.Register<ExitStallSampler>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:14: private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:21: private long _exitWindowOpenedUtcTicks; // 0 = closed; read via Interlocked (feeds ExitStallSampler)
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:25: IBattleLoadDiagnosticsSettingsProvider settings,
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettings.cs:21: public bool EnableBattleLoadDiagnostics { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettings.cs:26: public bool EnableStallWatchdog { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettings.cs:31: public bool EnableStallWatchdogBundle { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:9: public sealed class BattleLoadDiagnosticsSettingsProvider : IBattleLoadDiagnosticsSettingsProvider
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:16: BattleLoadDiagnosticsSettings.Instance?.EnableBattleLoadDiagnostics ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:19: BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdog ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:22: BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdogBundle ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:25: private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:42: IBattleLoadDiagnosticsSettingsProvider settings,
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:9: // Exit-stall stack sampler (#331 round 2). The ~107s tournament-exit stall freezes the MAIN
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:18: // thread mid-GC and then allocating can deadlock the sampler before Resume — acceptable for
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:20: // whole capture is try/catch'd with Resume in finally, and the sampler only ever runs while
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:22: public sealed class ExitStallSampler : IDisposable
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:34: private readonly IBattleLoadDiagnosticsSettingsProvider _settings;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:42: public ExitStallSampler(
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:44: IBattleLoadDiagnosticsSettingsProvider settings,
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsService.cs:50: /// stack sampler (#331 round 2) the same way BattleLoadLoadingWindow feeds the watchdog.</summary>
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsSettingsProvider.cs:6: public interface IBattleLoadDiagnosticsSettingsProvider

 succeeded in 939ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\AlignmentDesertion\AlignmentDesertionService.cs:24: public bool IsEnabled => _settings.IsEnabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\AlignmentDesertion\AlignmentDesertionSettingsProvider.cs:19: public bool IsEnabled => TaomSettings.Instance?.EnableAlignmentDesertion ?? _defaults.Enabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\AlignmentRecruitment\RecruitmentAlignmentSettingsProvider.cs:19: public bool IsEnabled => TaomSettings.Instance?.EnableAlignmentRecruitment ?? _defaults.Enabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\BanditScalingService.cs:12: public bool IsEnabled => _settings.IsEnabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\BanditManagement\BanditScalingSettingsProvider.cs:15: public bool IsEnabled => TaomSettings.Instance?.EnableBanditScaling ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:33: public bool IsEnabled => _settings.IsEnabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettings.cs:21: public bool EnableBattleLoadDiagnostics { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettings.cs:26: public bool EnableStallWatchdog { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettings.cs:31: public bool EnableStallWatchdogBundle { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:15: public bool IsEnabled =>
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:16: BattleLoadDiagnosticsSettings.Instance?.EnableBattleLoadDiagnostics ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:19: BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdog ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:22: BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdogBundle ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\CastleRecruitment\CastleRecruitmentService.cs:25: public bool IsEnabled => _settings.IsEnabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\CastleRecruitment\CastleRecruitmentSettingsProvider.cs:20: public bool IsEnabled => TaomSettings.Instance?.EnableCastleRecruitment ?? _defaults.Enabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\CultureConversion\CultureConversionSettingsProvider.cs:24: public bool IsEnabled => TaomSettings.Instance?.EnableCultureConversion ?? _defaults.Enabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\EliteEmissaryService.cs:41: public bool IsEnabled => _settings.IsEnabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\EliteEmissary\EliteEmissarySettingsProvider.cs:17: public bool IsEnabled => TaomSettings.Instance?.EnableEliteEmissary ?? _defaultEnabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\EquipPresetsSettingsProvider.cs:9: public bool IsEnabled => TaomSettings.Instance?.EnableEquipmentPresets ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\MixedFormations\MixedFormationsSettingsProvider.cs:7: public bool IsEnabled => TaomSettings.Instance?.EnableMixedFormations ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\NavalTravel\NavalTravelService.cs:24: public bool IsEnabled => _settings.IsEnabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\NavalTravel\NavalTravelSettingsProvider.cs:21: public bool IsEnabled => TaomSettings.Instance?.EnableNavalTravel ?? _defaults.Enabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\SiegeDismount\SiegeDismountSettingsProvider.cs:7: public bool IsEnabled => TaomSettings.Instance?.EnableSiegeDismount ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\SmartCavalryAI\SmartCavalryAISettingsProvider.cs:8: public bool IsEnabled => TaomSettings.Instance?.EnableSmartCavalryAI ?? false;

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 465ms:
   1: namespace TAOM.Features.BattleLoadDiagnostics;
   2: 
   3: // Wraps the MCM static behind an interface so services never read the singleton directly
   4: // (testable; ADR layer rule). All getters fail-open to the "diagnose now" defaults if
   5: // MCM isn't ready yet.
   6: public interface IBattleLoadDiagnosticsSettingsProvider
   7: {
   8:     bool IsEnabled { get; }
   9:     bool StallWatchdogEnabled { get; }
  10:     bool StallWatchdogBundleEnabled { get; }
  11:     double StallWatchdogSeconds { get; }
  12: }

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony -Recurse -Include *.cs | Select-String -Pattern 'IsDeclaredMember|GetDeclaredMember' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Tools/AccessToolsExtensions.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if($i -ge 500 -and $i -le 565){ '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Tools/Extensions.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if($_ -match '"'IsDeclaredMember|GetDeclaredMember' -or ("'$i -ge 455 -and $i -le 510)){ '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Dependencies/.vendor-source/Bannerlord.UIExtenderEx-2.13.2 -Recurse -Include *.cs | Select-String -Pattern 'WidgetFactory|WidgetTemplate|IsCustomType|OnRelease|HarmonyPatch|Patch\\(' | Select-Object -First 160 | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 657ms:
 succeeded in 670ms:
 455: 		}
 456: 
 457: 		/// <summary>Tests for any form of Ldloc*</summary>
 458: 		/// <param name="code">The <see cref="CodeInstruction"/></param>
 459: 		/// <param name="variable">The optional local variable</param>
 460: 		/// <returns>True if it matches one of the variations</returns>
 461: 		///
 462: 		public static bool IsLdloc(this CodeInstruction code, LocalBuilder variable = null)
 463: 		{
 464: 			if (opcodesLoadingLocalNormal.Contains(code.opcode) is false)
 465: 				if (opcodesLoadingLocalByAddress.Contains(code.opcode) is false)
 466: 					return false;
 467: 			return variable is null || Equals(variable, code.operand);
 468: 		}
 469: 
 470: 		/// <summary>Tests for any form of Stloc*</summary>
 471: 		/// <param name="code">The <see cref="CodeInstruction"/></param>
 472: 		/// <param name="variable">The optional local variable</param>
 473: 		/// <returns>True if it matches one of the variations</returns>
 474: 		///
 475: 		public static bool IsStloc(this CodeInstruction code, LocalBuilder variable = null)
 476: 		{
 477: 			if (opcodesStoringLocal.Contains(code.opcode) is false)
 478: 				return false;
 479: 			return variable is null || Equals(variable, code.operand);
 480: 		}
 481: 
 482: 		/// <summary>Tests if the code instruction branches</summary>
 483: 		/// <param name="code">The <see cref="CodeInstruction"/></param>
 484: 		/// <param name="label">The label if the instruction is a branch operation or <see langword="null"/> if not</param>
 485: 		/// <returns>True if the instruction branches</returns>
 486: 		///
 487: 		public static bool Branches(this CodeInstruction code, out Label? label)
 488: 		{
 489: 			if (opcodesBranching.Contains(code.opcode))
 490: 			{
 491: 				label = (Label)code.operand;
 492: 				return true;
 493: 			}
 494: 			label = null;
 495: 			return false;
 496: 		}
 497: 
 498: 		/// <summary>Tests if the code instruction calls the method/constructor</summary>
 499: 		/// <param name="code">The <see cref="CodeInstruction"/></param>
 500: 		/// <param name="method">The method</param>
 501: 		/// <returns>True if the instruction calls the method or constructor</returns>
 502: 		///
 503: 		public static bool Calls(this CodeInstruction code, MethodInfo method)
 504: 		{
 505: 			if (method is null)
 506: 				throw new ArgumentNullException(nameof(method));
 507: 			if (code.opcode != OpCodes.Call && code.opcode != OpCodes.Callvirt)
 508: 				return false;
 509: 			return Equals(code.operand, method);
 510: 		}

 succeeded in 684ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:174: if (original.IsDeclaredMember() is false)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Public\PatchProcessor.cs:176: var declaredMember = original.GetDeclaredMember();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:949: public static bool IsDeclaredMember<T>(this T member) where T : MemberInfo => member.DeclaringType == member.ReflectedType;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:955: public static T GetDeclaredMember<T>(this T member) where T : MemberInfo
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Harmony-2.4.2.0\Harmony\Tools\AccessTools.cs:957: if (member.DeclaringType is null || member.IsDeclaredMember())

 succeeded in 694ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:25: internal sealed record PrefabPatch(Type Type, Action<XmlDocument> Patcher);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:29: AccessTools2.GetDeclaredDelegate<GetPrefabNamesAndPathsFromCurrentPathDelegate>(typeof(WidgetFactory), "GetPrefabNamesAndPathsFromCurrentPath");
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:48: public IEnumerable<string> GetMoviesToPatch()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:101: public void RegisterPatch(string movie, Type prefabType, Action<XmlDocument> patcher)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:119: public void RegisterPatch(string movie, Type prefabType, Action<XmlNode> patcher)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:121: //RegisterPatch(movie, (XmlDocument node) => patcher(node));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:139: public void RegisterPatch(string movie, string? xpath, Type prefabType, Action<XmlNode> patcher) => RegisterPatch(movie, prefabType, node =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:183: /// Get path for movie from WidgetFactory
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:188: if (PrefabNamesMethod?.Invoke(UIResourceManager.WidgetFactory) is { } paths)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:194: MessageUtils.DisplayUserError("UIExtenderEx could not find WidgetFactory.GetPrefabNamesAndPathsFromCurrentPath!");
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.cs:217: patch(document);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.Prefabs.cs:21: public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertPatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.Prefabs.cs:59: public void RegisterPatch(string movie, string? xpath, PrefabExtensionSetAttributePatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.Prefabs.cs:86: public void RegisterPatch(string movie, string? xpath, PrefabExtensionReplacePatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.Prefabs.cs:123: public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertAsSiblingPatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.Prefabs2.cs:36: public void RegisterPatch(string movie, string? xpath, PrefabExtensionInsertPatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\PrefabComponent.Prefabs2.cs:363: public void RegisterPatch(string movie, string? xpath, PrefabExtensionSetAttributePatch patch) => RegisterPatch(movie, xpath, patch.GetType(), node =>
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\ViewModelComponent.cs:100: void Patch(Type viewModelType_)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\ViewModelComponent.cs:104: ViewModelWithMixinPatch.Patch(_harmony, viewModelType_, refreshMethodName);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\ViewModelComponent.cs:118: Patch(type);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\ViewModelComponent.cs:123: Patch(viewModelType);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Components\ViewModelComponent.cs:137: _harmony.Unpatch(originalMethodInfo, patchedMethodInfo);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\GauntletMoviePatch.cs:39: public static void Patch(Harmony harmony)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\GauntletMoviePatch.cs:43: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\GauntletMoviePatch.cs:49: private static void LoadPrefix(WidgetFactory widgetFactory, string movieName, IViewModel? datasource, ref bool doNotUseGeneratedPrefabs)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\GauntletMoviePatch.cs:51: static IEnumerable<string> GetAllInvolvedAutoGenNames(WidgetFactory widgetFactory, string movieName, IViewModel? datasource)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\GauntletMoviePatch.cs:65: if (_generatedPrefabs?.Invoke(widgetFactory.GeneratedPrefabContext) is { } generatedPrefabs)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\GauntletMoviePatch.cs:80: if (widgetFactory.GeneratedPrefabContext.InstantiatePrefab(context, movieName, variantName, data) is { } autogenResult)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\GauntletMoviePatch.cs:96: var moviesPatched = new HashSet<string>(UIExtender.GetAllRuntimes().SelectMany(x => x.PrefabComponent.GetMoviesToPatch()));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\GauntletMoviePatch.cs:97: var moviesInvolved = new HashSet<string>(GetAllInvolvedAutoGenNames(widgetFactory, movieName, datasource));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\UIConfigPatch.cs:8: public static void Patch(Harmony harmony)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\UIConfigPatch.cs:10: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\ViewModelPatch.cs:17: public static void Patch(Harmony harmony)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\ViewModelPatch.cs:19: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\ViewModelPatch.cs:23: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\ViewModelPatch.cs:44: private static bool ExecuteCommandPatch(object __instance, string commandName, object[] parameters)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\ViewModelWithMixinPatch.cs:26: public static void Patch(Harmony harmony, Type viewModelType, string? refreshMethodName = null)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\ViewModelWithMixinPatch.cs:32: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\ViewModelWithMixinPatch.cs:37: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\ViewModelWithMixinPatch.cs:57: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\WidgetPrefabPatch.cs:20: public static void Patch(Harmony harmony)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\WidgetPrefabPatch.cs:22: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Patches\WidgetPrefabPatch.cs:28: new HarmonyMethod(typeof(WidgetPrefabPatch), nameof(LoadFromDocument))).Patch();
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Prefabs\PrefabExtensionInsertPatch.cs:30: protected ModulePrefabExtensionInsertPatch(string name, string moduleName)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\Prefabs\PrefabExtensionInsertPatch.cs:68: protected EmbedPrefabExtensionInsertPatch(Assembly assembly, string path)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\BrushFactoryManager.cs:46: internal static void Patch(Harmony harmony)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\BrushFactoryManager.cs:48: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\BrushFactoryManager.cs:52: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\BrushFactoryManager.cs:58: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\BrushFactoryManager.cs:61: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\BrushFactoryManager.cs:64: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\BrushFactoryManager.cs:67: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\BrushFactoryManager.cs:70: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:22: public static class WidgetFactoryManager
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:28: private static readonly AccessTools.FieldRef<WidgetFactory, IDictionary>? _liveCustomTypes =
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:29: AccessTools2.FieldRefAccess<WidgetFactory, IDictionary>("_liveCustomTypes");
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:41: UIResourceManager.WidgetFactory.PrefabExtensionContext,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:42: UIResourceManager.WidgetFactory.WidgetAttributeContext,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:49: UIResourceManager.WidgetFactory.PrefabExtensionContext,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:50: UIResourceManager.WidgetFactory.WidgetAttributeContext,
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:65: public static void Patch(Harmony harmony)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:67: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:68: AccessTools2.DeclaredMethod(typeof(WidgetFactory), "GetCustomType"),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:69: prefix: new HarmonyMethod(typeof(WidgetFactoryManager), nameof(GetCustomTypePrefix)));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:71: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:72: AccessTools2.DeclaredMethod(typeof(WidgetFactory), "CreateBuiltinWidget"),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:73: prefix: new HarmonyMethod(typeof(WidgetFactoryManager), nameof(CreateBuiltinWidgetPrefix)));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:75: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:76: AccessTools2.DeclaredMethod(typeof(WidgetFactory), "GetWidgetTypes"),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:77: postfix: new HarmonyMethod(typeof(WidgetFactoryManager), nameof(GetWidgetTypesPostfix)));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:79: harmony.Patch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:80: AccessTools2.DeclaredMethod(typeof(WidgetFactory), "IsCustomType"),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:81: prefix: new HarmonyMethod(typeof(WidgetFactoryManager), nameof(IsCustomTypePrefix)));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:84: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:85: AccessTools2.DeclaredMethod(typeof(WidgetFactory), "OnUnload"),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:86: prefix: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(OnUnloadPrefix)));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:91: // Preventing inlining IsCustomType
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:92: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:93: AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:CreateWidgets"),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:94: transpiler: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:95: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:96: AccessTools2.DeclaredMethod("TaleWorlds.GauntletUI.PrefabSystem.WidgetTemplate:OnRelease"),
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:97: transpiler: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:99: harmony.TryPatch(
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:101: transpiler: AccessTools2.DeclaredMethod(typeof(WidgetFactoryManager), nameof(BlankTranspiler)));
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:132: private static bool IsCustomTypePrefix(string typeName, ref bool __result)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\ResourceManager\WidgetFactoryManager.cs:144: private static bool GetCustomTypePrefix(WidgetFactory __instance, string typeName, ref WidgetPrefab __result)
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtender.cs:33: GauntletMoviePatch.Patch(Harmony);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtender.cs:35: UIConfigPatch.Patch(Harmony);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtender.cs:37: ViewModelPatch.Patch(Harmony);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtender.cs:38: WidgetPrefabPatch.Patch(Harmony);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtender.cs:39: BrushFactoryManager.Patch(Harmony);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtender.cs:40: WidgetFactoryManager.Patch(Harmony);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtenderRuntime.cs:72: PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtenderRuntime.cs:76: PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtenderRuntime.cs:80: PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtenderRuntime.cs:83: PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtenderRuntime.cs:86: PrefabComponent.RegisterPatch(xmlExtension.Movie, patch.GetType(), patch.Apply);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtenderRuntime.cs:89: PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch.GetType(), patch.Apply);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtenderRuntime.cs:93: PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\src\Bannerlord.UIExtenderEx\UIExtenderRuntime.cs:96: PrefabComponent.RegisterPatch(xmlExtension.Movie, xmlExtension.XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\TestPrefabs\TestPrefabExtensionInsertAsSiblingAppendPatch.cs:15: public TestPrefabExtensionInsertAsSiblingAppendPatch()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\TestPrefabs\TestPrefabExtensionInsertAsSiblingPrependPatch.cs:15: public TestPrefabExtensionInsertAsSiblingPrependPatch()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\TestPrefabs\TestPrefabExtensionInsertPatch.cs:17: public TestPrefabExtensionInsertPatch()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\TestPrefabs\TestPrefabExtensionReplacePatch.cs:14: public TestPrefabExtensionReplacePatch()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:41: var widgetTemplateInsert = UIResourceManager.WidgetFactory.GetCustomType("Insert").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:42: var childrenInsert1 = GetChildren(widgetTemplateInsert);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:48: var widgetTemplateReplace = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildren").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:49: var childrenReplace1 = GetChildren(widgetTemplateReplace);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:55: var widgetTemplateInsertAsSiblingAppend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingAppend").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:56: var childrenInsertAsSiblingAppend1 = GetChildren(widgetTemplateInsertAsSiblingAppend);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:62: var widgetTemplateInsertAsSiblingPrepend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingPrepend").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:63: var childrenInsertAsSiblingPrepend1 = GetChildren(widgetTemplateInsertAsSiblingPrepend);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:69: var widgetTemplateSetAttribute = UIResourceManager.WidgetFactory.GetCustomType("SetAttribute").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:70: var childrenSetAttribute1 = GetChildren(widgetTemplateSetAttribute);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:82: var widgetTemplateInsert = UIResourceManager.WidgetFactory.GetCustomType("Insert").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:83: var childrenInsert1 = GetChildren(widgetTemplateInsert);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:89: var widgetTemplateReplace = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildren").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:90: var childrenReplace1 = GetChildren(widgetTemplateReplace);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:96: var widgetTemplateInsertAsSiblingAppend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingAppend").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:97: var childrenInsertAsSiblingAppend1 = GetChildren(widgetTemplateInsertAsSiblingAppend);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:103: var widgetTemplateInsertAsSiblingPrepend = UIResourceManager.WidgetFactory.GetCustomType("InsertAsSiblingPrepend").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:104: var childrenInsertAsSiblingPrepend1 = GetChildren(widgetTemplateInsertAsSiblingPrepend);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:110: var widgetTemplateSetAttribute = UIResourceManager.WidgetFactory.GetCustomType("SetAttribute").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs\IntegrationTests\PrefabsTests.cs:111: var childrenSetAttribute1 = GetChildren(widgetTemplateSetAttribute);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\TestPrefabs\TestPrefabExtensionInsertXmlDocumentPatch.cs:17: public TestPrefabExtensionInsertXmlDocumentPatch()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\TestPrefabs\TestPrefabExtensionInsertXmlNodePatch.cs:17: public TestPrefabExtensionInsertXmlNodePatch()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\TestPrefabs\TestPrefabExtensionInsertXmlNodesPatch.cs:19: public TestPrefabExtensionInsertXmlNodesPatch()
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:44: var widgetTemplateInsert = UIResourceManager.WidgetFactory.GetCustomType("Insert2").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:45: var optionsScreenWidget = GetChildren(widgetTemplateInsert);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:55: var widgetTemplateSetAttribute = UIResourceManager.WidgetFactory.GetCustomType("SetAttribute2").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:56: var optionsScreenWidget = GetChildren(widgetTemplateSetAttribute);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:67: var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("Append2").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:68: var optionsScreenWidget = GetChildren(widgetTemplateAppend);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:77: var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("Prepend2").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:78: var optionsScreenWidget = GetChildren(widgetTemplateAppend);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:88: var optionsScreenWidget = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildren2").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:102: var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("AppendRemoveRootNode").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:103: var optionsScreenWidget = GetChildren(widgetTemplateAppend);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:113: var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("PrependRemoveRootNode").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:114: var optionsScreenWidget = GetChildren(widgetTemplateAppend);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:125: var widgetTemplateAppend = UIResourceManager.WidgetFactory.GetCustomType("ReplaceKeepChildrenRemoveRootNode").RootTemplate;
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\IntegrationTests\Prefabs2Tests.cs:126: var optionsScreenWidget = GetChildren(widgetTemplateAppend);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:55: var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, patchedDocument);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:61: prefabComponent.RegisterPatch(MovieName, XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:83: var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, patchedDocument.DocumentElement, 2);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:89: prefabComponent.RegisterPatch(MovieName, XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:117: prefabComponent.RegisterPatch(MovieName, XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:137: var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, "<ValidRoot><SomeChild/></ValidRoot>", 10);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:143: prefabComponent.RegisterPatch(MovieName, XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:163: var patch = PatchCreator.ConstructInsertPatch(InsertType.Replace, "<DiscardedRoot><SomeChild1/><SomeChild2/></DiscardedRoot>", 10, true);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:169: prefabComponent.RegisterPatch(MovieName, XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:196: prefabComponent.RegisterPatch(MovieName, XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:222: prefabComponent.RegisterPatch(MovieName, XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:245: var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, patchedDocument!.DocumentElement!.ChildNodes.Cast<XmlNode>());
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:251: prefabComponent.RegisterPatch(MovieName, XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:279: var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, new List<XmlNode> { patchedDocument1, patchedDocument2, patchedDocument3 });
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:285: prefabComponent.RegisterPatch(MovieName, XPath, patch);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:309: var patch = PatchCreator.ConstructInsertPatch(InsertType.Child, patchedDocument);
C:\Users\mikew\source\repos\TAOM\Dependencies\.vendor-source\Bannerlord.UIExtenderEx-2.13.2\tests\Bannerlord.UIExtenderEx.Tests\Prefabs2\UnitTests\PrefabComponentPrefabs2Tests.cs:315: prefabComponent.RegisterPatch(MovieName, XPath, patch);

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'Dependencies/.vendor-source/Harmony-2.4.2.0/Harmony/Tools/AccessTools.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if($i -ge 943 -and $i -le 970){ '"'{0,4}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 494ms:
 943: 		}
 944: 
 945: 		/// <summary>Test if a class member is actually an concrete implementation</summary>
 946: 		/// <param name="member">A member</param>
 947: 		/// <returns>True if the member is a declared</returns>
 948: 		///
 949: 		public static bool IsDeclaredMember<T>(this T member) where T : MemberInfo => member.DeclaringType == member.ReflectedType;
 950: 
 951: 		/// <summary>Gets the real implementation of a class member</summary>
 952: 		/// <param name="member">A member</param>
 953: 		/// <returns>The member itself if its declared. Otherwise the member that is actually implemented in some base type</returns>
 954: 		///
 955: 		public static T GetDeclaredMember<T>(this T member) where T : MemberInfo
 956: 		{
 957: 			if (member.DeclaringType is null || member.IsDeclaredMember())
 958: 				return member;
 959: 
 960: 			var metaToken = member.MetadataToken;
 961: 			var members = member.DeclaringType?.GetMembers(all) ?? [];
 962: 			foreach (var other in members)
 963: 				if (other.MetadataToken == metaToken)
 964: 					return (T)other;
 965: 
 966: 			return member;
 967: 		}
 968: 
 969: 		/// <summary>Gets the reflection information for a directly declared constructor</summary>
 970: 		/// <param name="type">The class/type where the constructor is declared</param>

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path TAOM.Tests,Dependencies -Recurse -Include *.cs | Select-String -Pattern 'PatchShield|ExcludedTargetNamespacePrefixes|IsExcludedTarget|ShieldedCount' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 1069ms:
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\DiagLog.cs:11: /// PatchShield / SaveShield Finalizers depend on DiagLog being safe to call from
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:20: /// BetaDeps parity (DR3 Phase 4 — 2026-05-25, port of BetaDeps.Foundation.PatchShield).
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:24: /// Opt-out: place a file named <c>patchshield-disabled.flag</c> in the
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:26: /// a crash is masked by PatchShield vs an actual problem in TAOM.
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:32: public static class PatchShield
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:34: private const string Tag = "PatchShield";
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:35: private const string HarmonyId = "TAOM.Dependencies.Foundation.PatchShield";
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:36: private const string DisableFlagName = "patchshield-disabled.flag";
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:87: private static readonly string[] ExcludedTargetNamespacePrefixes =
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:98: private static bool IsExcludedTarget(MethodBase method)
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:103: foreach (var prefix in ExcludedTargetNamespacePrefixes)
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:122: public static int ShieldedCount { get { lock (_lock) return _shielded.Count; } }
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:151: DiagLog.Log(Tag, "patchshield-disabled.flag present — PatchShield install skipped");
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:158: var voidFinalizer = typeof(PatchShield).GetMethod(
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:161: var resultFinalizer = typeof(PatchShield).GetMethod(
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:204: // ~107s (#331 round 2). See ExcludedTargetNamespacePrefixes.
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:205: if (IsExcludedTarget(method))
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\PatchShield.cs:389: $"SESSION SUMMARY: shielded {ShieldedCount} method(s), unpatched {UnpatchedCount} target(s), " +
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\ReflectionUtils.cs:8: /// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\RuntimeLog.cs:9: /// PatchShield, SaveShield, FailedModsCatalog, and IncompatibleModDetector for their
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\SaveShield.cs:30: /// Different from <see cref="PatchShield"/>: PatchShield shields EVERY patched method
C:\Users\mikew\source\repos\TAOM\Dependencies\Foundation\SaveShield.cs:132: public static int ShieldedCount { get { lock (_lock) return _shielded.Count; } }
C:\Users\mikew\source\repos\TAOM\Dependencies\AliasStubSubModule.cs:50: // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:184: try { DiagLog.Log("Dependencies", "OnSubModuleLoad: → PatchShield.Install (pass 1)"); PatchShield.Install(); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:185: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install failed: {ex.Message}"); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:207: try { PatchShield.WriteSessionSummary(); } catch { }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:231: // Second PatchShield pass — captures patches registered by mods that hook this
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:233: try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
C:\Users\mikew\source\repos\TAOM\Dependencies\SubModule.cs:234: catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }

codex
The latch split looks benign so far: the only meaningful mismatch is a tiny close window where the background sampler can see old ticks after the bool is already false. That can at worst produce one stale sample; it cannot keep phase logging alive because the phase hooks read the bool.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.MountAndBlade.cs'; "'$ranges=@(@(59790,59825),@(61260,61350),@(74455,74472),@(100230,100310)); foreach($r in $ranges){ Write-Output "--- $p $($r[0])-$($r[1])"; $i=0; Get-Content $p | ForEach-Object { $i++; if($i -ge $r[0] -and $i -le $r[1]){ '"'{0,6}: {1}' -f "'$i,$_ } } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.CampaignSystem.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if($i -ge 10580 -and $i -le 10615){ '"'{0,6}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.DotNet.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; if($i -ge 1478 -and $i -le 1508){ '"'{0,6}: {1}' -f "'$i,$_ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main/Features/Arena,TAOM.Tests -Recurse -Include *.cs | Select-String -Pattern 'Patch60|ReleaseMovie|RemoveLayer|TournamentExitMovieRelease|HasLayer' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 622ms:
  1478: 	{
  1479: 		ManagedObject.FinalizeManagedObjects();
  1480: 		Common.MemoryCleanupGC();
  1481: 		ManagedObjectOwner.LogFinalize();
  1482: 	}
  1483: 
  1484: 	[LibraryCallback(null, false)]
  1485: 	internal static void ApplicationTick(float dt)
  1486: 	{
  1487: 		ManagedObject.HandleManagedObjects();
  1488: 		DotNetObject.HandleDotNetObjects();
  1489: 		NativeObject.HandleNativeObjects();
  1490: 		ManagedObjectOwner.GarbageCollect();
  1491: 		NativeTelemetryManager.Update();
  1492: 		for (int i = 0; i < _components.Count; i++)
  1493: 		{
  1494: 			_components[i].OnApplicationTick(dt);
  1495: 		}
  1496: 	}
  1497: 
  1498: 	[LibraryCallback(null, false)]
  1499: 	internal static void ApplicationTickLight(float dt)
  1500: 	{
  1501: 		DotNetObject.HandleDotNetObjects();
  1502: 		NativeObject.HandleNativeObjects();
  1503: 		NativeTelemetryManager.Update();
  1504: 		for (int i = 0; i < _components.Count; i++)
  1505: 		{
  1506: 			_components[i].OnApplicationTick(dt);
  1507: 		}
  1508: 	}

 succeeded in 1006ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:14: // OnMissionScreenFinalize nulls _gauntletMovie/_gauntletLayer WITHOUT ReleaseMovie/RemoveLayer
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:30: [HarmonyPatchCategory("Patch60_TournamentExitMovieRelease")]
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:31: public static class Patch60_TournamentExitMovieRelease
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:33: // internal (not private) so Patch60TournamentExitMovieReleaseTests can drift-guard the
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:59: _logger?.LogWarning($"[Arena] Patch60 capture failed — tournament UI will leak (vanilla behavior): {ex.GetType().Name}: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:73: // focus; ReleaseMovie is idempotence-guarded (Contains + IsReleased) and RemoveLayer
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:76: // these stamps split the cost between ReleaseMovie and RemoveLayer for the RCA.
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:78: layer.ReleaseMovie(movie);
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:82: if (screen != null && screen.HasLayer(layer))
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:83: screen.RemoveLayer(layer);
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:85: _logger?.LogInfo($"[Arena] Patch60 tournament UI released: ReleaseMovie={releaseMs}ms RemoveLayer={sw.ElapsedMilliseconds - releaseMs}ms");
C:\Users\mikew\source\repos\TAOM\Main\Features\Arena\Hooks\Patch60_TournamentExitMovieRelease.cs:89: _logger?.LogWarning($"[Arena] Patch60 release failed — tournament UI leaked this exit (vanilla behavior): {ex.GetType().Name}: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Arena\Patch60TournamentExitMovieReleaseTests.cs:8: /// Drift-guard for the two private-field bindings <see cref="Patch60_TournamentExitMovieRelease"/>
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Arena\Patch60TournamentExitMovieReleaseTests.cs:11: /// ReleaseMovie/RemoveLayer, deferring the Tournament-movie teardown into ScreenBase.HandleFinalize
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Arena\Patch60TournamentExitMovieReleaseTests.cs:21: public class Patch60TournamentExitMovieReleaseTests
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Arena\Patch60TournamentExitMovieReleaseTests.cs:36: Patch60_TournamentExitMovieRelease.LayerField,
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\Arena\Patch60TournamentExitMovieReleaseTests.cs:49: Patch60_TournamentExitMovieRelease.MovieField,

 succeeded in 1691ms:
 10580: 				_campaignBehaviorManager.InitializeCampaignBehaviors(campaignGameStarter.CampaignBehaviors);
 10581: 				_campaignBehaviorManager.LoadBehaviorData();
 10582: 				_campaignBehaviorManager.RegisterEvents();
 10583: 			}
 10584: 			foreach (INonReadyObjectHandler campaignBehavior in GetCampaignBehaviors<INonReadyObjectHandler>())
 10585: 			{
 10586: 				campaignBehavior.OnBeforeNonReadyObjectsDeleted();
 10587: 			}
 10588: 			if (_gameLoadingType != GameLoadingType.Tutorial)
 10589: 			{
 10590: 				campaignGameStarter.UnregisterNonReadyObjects();
 10591: 			}
 10592: 			if (_gameLoadingType == GameLoadingType.SavedCampaign)
 10593: 			{
 10594: 				InitializeCampaignObjectsOnAfterLoad();
 10595: 			}
 10596: 			else if (_gameLoadingType == GameLoadingType.NewCampaign || _gameLoadingType == GameLoadingType.Tutorial)
 10597: 			{
 10598: 				CampaignObjectManager.InitializeOnNewGame();
 10599: 			}
 10600: 			InitializeCachedLists();
 10601: 			InitializeDefaultEquipments();
 10602: 			NameGenerator.Initialize();
 10603: 			base.CurrentGame.OnGameStart();
 10604: 			base.GameManager.OnGameInitializationFinished(base.CurrentGame);
 10605: 		}
 10606: 
 10607: 		private void CalculateCachedStatsOnLoad()
 10608: 		{
 10609: 			ItemRoster.CalculateCachedStatsOnLoad();
 10610: 		}
 10611: 
 10612: 		private void InitializeBasicObjectXmls()
 10613: 		{
 10614: 			base.ObjectManager.LoadXML("SPCultures");
 10615: 			base.ObjectManager.LoadXML("Concepts");

 succeeded in 2560ms:
--- E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.MountAndBlade.cs 59790-59825
 59790: 
 59791: 		public override void RegisterSubModuleTypes()
 59792: 		{
 59793: 			foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
 59794: 			{
 59795: 				item.RegisterSubModuleTypes();
 59796: 			}
 59797: 		}
 59798: 
 59799: 		public override void AfterRegisterSubModuleObjects(bool isSavedCampaign)
 59800: 		{
 59801: 			foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
 59802: 			{
 59803: 				item.AfterRegisterSubModuleObjects(isSavedCampaign);
 59804: 			}
 59805: 		}
 59806: 
 59807: 		public override void InitializeGameStarter(Game game, IGameStarter starterObject)
 59808: 		{
 59809: 			foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
 59810: 			{
 59811: 				item.InitializeGameStarter(game, starterObject);
 59812: 			}
 59813: 		}
 59814: 
 59815: 		public override void OnGameInitializationFinished(Game game)
 59816: 		{
 59817: 			foreach (MBSubModuleBase item in Module.CurrentModule.CollectSubModules())
 59818: 			{
 59819: 				item.OnGameInitializationFinished(game);
 59820: 			}
 59821: 			foreach (SkeletonScale objectType in Game.Current.ObjectManager.GetObjectTypeList<SkeletonScale>())
 59822: 			{
 59823: 				sbyte[] array = new sbyte[objectType.BoneNames.Count];
 59824: 				for (int i = 0; i < array.Length; i++)
 59825: 				{
--- E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.MountAndBlade.cs 61260-61350
 61260: 		public override int GetHashCode()
 61261: 		{
 61262: 			return index;
 61263: 		}
 61264: 	}
 61265: 	public abstract class MBSubModuleBase
 61266: 	{
 61267: 		protected internal virtual void OnSubModuleLoad()
 61268: 		{
 61269: 		}
 61270: 
 61271: 		protected internal virtual void OnSubModuleUnloaded()
 61272: 		{
 61273: 		}
 61274: 
 61275: 		protected internal virtual void OnBeforeInitialModuleScreenSetAsRoot()
 61276: 		{
 61277: 		}
 61278: 
 61279: 		protected internal virtual void RegisterSubModuleTypes()
 61280: 		{
 61281: 		}
 61282: 
 61283: 		protected internal virtual void OnNewModuleLoad()
 61284: 		{
 61285: 		}
 61286: 
 61287: 		public virtual void OnConfigChanged()
 61288: 		{
 61289: 		}
 61290: 
 61291: 		protected internal virtual void OnBeforeGameStart(MBGameManager mbGameManager, List<string> disabledModules)
 61292: 		{
 61293: 		}
 61294: 
 61295: 		protected internal virtual void OnGameStart(Game game, IGameStarter gameStarterObject)
 61296: 		{
 61297: 		}
 61298: 
 61299: 		protected internal virtual void OnApplicationTick(float dt)
 61300: 		{
 61301: 		}
 61302: 
 61303: 		protected internal virtual void AfterAsyncTickTick(float dt)
 61304: 		{
 61305: 		}
 61306: 
 61307: 		protected internal virtual void InitializeGameStarter(Game game, IGameStarter starterObject)
 61308: 		{
 61309: 		}
 61310: 
 61311: 		public virtual void OnGameLoaded(Game game, object initializerObject)
 61312: 		{
 61313: 		}
 61314: 
 61315: 		public virtual void OnAfterGameLoaded(Game game)
 61316: 		{
 61317: 		}
 61318: 
 61319: 		public virtual void OnNewGameCreated(Game game, object initializerObject)
 61320: 		{
 61321: 		}
 61322: 
 61323: 		public virtual void BeginGameStart(Game game)
 61324: 		{
 61325: 		}
 61326: 
 61327: 		public virtual void OnCampaignStart(Game game, object starterObject)
 61328: 		{
 61329: 		}
 61330: 
 61331: 		public virtual void RegisterSubModuleObjects(bool isSavedCampaign)
 61332: 		{
 61333: 		}
 61334: 
 61335: 		public virtual void AfterRegisterSubModuleObjects(bool isSavedCampaign)
 61336: 		{
 61337: 		}
 61338: 
 61339: 		public virtual void OnMultiplayerGameStart(Game game, object starterObject)
 61340: 		{
 61341: 		}
 61342: 
 61343: 		public virtual void OnGameInitializationFinished(Game game)
 61344: 		{
 61345: 		}
 61346: 
 61347: 		public virtual void OnAfterGameInitializationFinished(Game game, object starterObject)
 61348: 		{
 61349: 		}
 61350: 
--- E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.MountAndBlade.cs 74455-74472
 74455: 		}
 74456: 
 74457: 		[MBCallback(null, false)]
 74458: 		internal static void Finalize()
 74459: 		{
 74460: 			Module.FinalizeCurrentModule();
 74461: 			Common.MemoryCleanupGC();
 74462: 		}
 74463: 
 74464: 		void IManagedComponent.OnApplicationTick(float dt)
 74465: 		{
 74466: 			Module.CurrentModule.OnApplicationTick(dt);
 74467: 		}
 74468: 
 74469: 		[MBCallback(null, false)]
 74470: 		internal static void CheckSharedStructureSizes()
 74471: 		{
 74472: 			_callbackManager.CheckSharedStructureSizes();
--- E:/Decompiled_Bannerlord/_shipping_build_v1.4.6/TaleWorlds.MountAndBlade.cs 100230-100310
100230: 					int serverTickRate = Convert.ToInt32(array[i + 1]);
100231: 					StartupInfo.ServerTickRate = serverTickRate;
100232: 					i++;
100233: 				}
100234: 			}
100235: 		}
100236: 
100237: 		internal void OnApplicationTick(float dt)
100238: 		{
100239: 			bool isOnlyCoreContentEnabled = IsOnlyCoreContentEnabled;
100240: 			IsOnlyCoreContentEnabled = Utilities.IsOnlyCoreContentEnabled();
100241: 			if (isOnlyCoreContentEnabled != IsOnlyCoreContentEnabled && isOnlyCoreContentEnabled)
100242: 			{
100243: 				InitialState initialState;
100244: 				if ((initialState = GameStateManager.Current.ActiveState as InitialState) != null)
100245: 				{
100246: 					Utilities.DisableCoreGame();
100247: 					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=CaSafuAH}Content Download Complete").ToString(), new TextObject("{=1nKa4pQX}Rest of the game content has been downloaded.").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: false, new TextObject("{=yS7PvrTD}OK").ToString(), null, delegate
100248: 					{
100249: 						initialState.RefreshContentState();
100250: 					}, null));
100251: 				}
100252: 				else
100253: 				{
100254: 					InformationManager.ShowInquiry(new InquiryData(new TextObject("{=CaSafuAH}Content Download Complete").ToString(), new TextObject("{=BFhMw4bl}Rest of the game content has been downloaded. Do you want to return to the main menu?").ToString(), isAffirmativeOptionShown: true, isNegativeOptionShown: true, new TextObject("{=aeouhelq}Yes").ToString(), new TextObject("{=8OkPHu4f}No").ToString(), OnConfirmReturnToMainMenu, null));
100255: 					_enableCoreContentOnReturnToRoot = true;
100256: 				}
100257: 			}
100258: 			if (_synchronizationContext == null)
100259: 			{
100260: 				_synchronizationContext = new SingleThreadedSynchronizationContext();
100261: 				SynchronizationContext.SetSynchronizationContext(_synchronizationContext);
100262: 			}
100263: 			_testContext.OnApplicationTick(dt);
100264: 			if (!GameNetwork.MultiplayerDisabled)
100265: 			{
100266: 				OnNetworkTick(dt);
100267: 			}
100268: 			if (GameStateManager.Current == null)
100269: 			{
100270: 				GameStateManager.Current = GlobalGameStateManager;
100271: 			}
100272: 			if (GameStateManager.Current == GlobalGameStateManager)
100273: 			{
100274: 				if (LoadingFinished && GlobalGameStateManager.ActiveState == null)
100275: 				{
100276: 					if (ReturnToEditorState)
100277: 					{
100278: 						ReturnToEditorState = false;
100279: 						SetEditorScreenAsRootScreen();
100280: 					}
100281: 					else
100282: 					{
100283: 						SetInitialModuleScreenAsRootScreen();
100284: 					}
100285: 				}
100286: 				GlobalGameStateManager.OnTick(dt);
100287: 			}
100288: 			Utilities.RunJobs();
100289: 			PlatformServices.Instance?.Tick(dt);
100290: 			_synchronizationContext.Tick();
100291: 			if (GameManagerBase.Current != null)
100292: 			{
100293: 				GameManagerBase.Current.OnTick(dt);
100294: 			}
100295: 			foreach (MBSubModuleBase item in CollectSubModules())
100296: 			{
100297: 				item.OnApplicationTick(dt);
100298: 			}
100299: 			JobManager.OnTick(dt);
100300: 			AvatarServices.UpdateAvatarServices(dt);
100301: 		}
100302: 
100303: 		private void OnConfirmReturnToMainMenu()
100304: 		{
100305: 			MBGameManager.EndGame();
100306: 		}
100307: 
100308: 		private void OnNetworkTick(float dt)
100309: 		{
100310: 			foreach (MBSubModuleBase item in CollectSubModules())

codex
Current v1.4.6 evidence supports the main-thread assumption indirectly: the native application tick drives `Module.CurrentModule.OnApplicationTick`, and campaign initialization calls `GameManager.OnGameInitializationFinished`, which then invokes each `MBSubModuleBase.OnGameInitializationFinished`. I have not found an existing invariant tying the captured thread to later ticks.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "FullyQualifiedName~BattleLoadDiagnostics" --no-restore --verbosity minimal' in C:\Users\mikew\source\repos\TAOM
2026-07-10T17:39:13.000966Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 431ms:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

codex
The targeted `dotnet test` run is blocked before MSBuild starts by the sandboxed .NET first-time-use sentinel under `C:\Users\CodexSandboxOffline\.dotnet`; I’ll note that in the review rather than treating tests as executed.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs Dependencies/Foundation/PatchShield.cs Main/SubModule.cs | Select-Object -First 260' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$p='"'TAOM.Tests/Features/Arena/Patch60TournamentExitMovieReleaseTests.cs'; "'$i=0; Get-Content $p | ForEach-Object { $i++; '"'{0,4}: {1}' -f "'$i,$_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path docs/reviews -Filter '*tournament*round2*' -ErrorAction SilentlyContinue | Select-Object FullName,Length,LastWriteTime | Format-Table -AutoSize | Out-String" in C:\Users\mikew\source\repos\TAOM
 succeeded in 451ms:
 M CHANGELOG.md
 M CLAUDE.md
 M Dependencies/Foundation/PatchShield.cs
 M Main/Features/Arena/Hooks/Patch60_TournamentExitMovieRelease.cs
 M Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs
 M Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs
 M Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsService.cs
 M Main/SubModule.cs
 M Main/_Module/GUI/PreFabs/CareerSystem/AbilityHUD.xml
 M Main/_Module/ModuleData/special_resources/special_resources_config.xml
 M TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsServiceTests.cs
 M docs/features/arena.md
 M docs/features/battle-load-diagnostics.md
 M docs/features/career-system.md
 M docs/features/gui-sprite-system.md
 M docs/features/special-resources.md
 M docs/migration/dr3-maintenance.md
 M docs/reviews/LESSONS-LEARNED.md
 M docs/reviews/rca-tournament-exit-hang-2026-07-06.md
?? Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs
?? TAOM.Tests/Features/BattleLoadDiagnostics/ExitStallSamplerTests.cs
?? docs/reviews/codex-adversarial-tournament-exit-round2-2026-07-10.md
?? docs/reviews/codex-adversarial-tournament-exit-round2-2026-07-10.prompt.md
warning: could not open directory '.pytest_cache/': Permission denied

 succeeded in 566ms:
   1: using Microsoft.VisualStudio.TestTools.UnitTesting;
   2: using TAOM.Features.Arena.Hooks;
   3: using TAOM.Tests.Migration;
   4: 
   5: namespace TAOM.Tests.Features.Arena;
   6: 
   7: /// <summary>
   8: /// Drift-guard for the two private-field bindings <see cref="Patch60_TournamentExitMovieRelease"/>
   9: /// resolves at type-load against the installed engine: <c>MissionGauntletTournamentView._gauntletLayer</c>
  10: /// and <c>._gauntletMovie</c> (issue #331 — the engine's tournament view nulls both without
  11: /// ReleaseMovie/RemoveLayer, deferring the Tournament-movie teardown into ScreenBase.HandleFinalize
  12: /// where an in-flight prize tableau render hangs the exit ~108s).
  13: /// <para>
  14: /// <c>HarmonyPatchBindingTests</c> already covers the patch's <c>[HarmonyPatch]</c> target
  15: /// (<c>OnMissionScreenFinalize</c>). The two <c>AccessTools.FieldRefAccess</c> lookups live inside the
  16: /// patch and are NOT covered there — a 1.4.x field rename would silently disable the release fix
  17: /// (fail-safe back to today's leak + hang) with no other red test. These are those red tests.
  18: /// </para>
  19: /// </summary>
  20: [TestClass]
  21: public class Patch60TournamentExitMovieReleaseTests
  22: {
  23:     private static bool _gameLoaded;
  24: 
  25:     [ClassInitialize]
  26:     public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();
  27: 
  28:     [TestMethod]
  29:     [TestCategory("BindingVerification")]
  30:     public void GauntletLayerField_BindingResolves_AgainstInstalledEngine()
  31:     {
  32:         if (!_gameLoaded)
  33:             Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
  34: 
  35:         Assert.IsNotNull(
  36:             Patch60_TournamentExitMovieRelease.LayerField,
  37:             "MissionGauntletTournamentView._gauntletLayer did not resolve against the installed engine — " +
  38:             "engine drift would silently disable the tournament-exit movie release (fail-safe to the vanilla leak + hang).");
  39:     }
  40: 
  41:     [TestMethod]
  42:     [TestCategory("BindingVerification")]
  43:     public void GauntletMovieField_BindingResolves_AgainstInstalledEngine()
  44:     {
  45:         if (!_gameLoaded)
  46:             Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
  47: 
  48:         Assert.IsNotNull(
  49:             Patch60_TournamentExitMovieRelease.MovieField,
  50:             "MissionGauntletTournamentView._gauntletMovie did not resolve against the installed engine — " +
  51:             "engine drift would silently disable the tournament-exit movie release (fail-safe to the vanilla leak + hang).");
  52:     }
  53: }

 succeeded in 588ms:
diff --git a/Dependencies/Foundation/PatchShield.cs b/Dependencies/Foundation/PatchShield.cs
index f038fa79..038ade36 100644
--- a/Dependencies/Foundation/PatchShield.cs
+++ b/Dependencies/Foundation/PatchShield.cs
@@ -74,6 +74,42 @@ public static class PatchShield
         return false;
     }
 
+    // Issue #331 round 2 (2026-07-09, measured): NEVER shield the Gauntlet/2D UI layer.
+    // A shield finalizer binds __originalMethod, so Harmony's generated wrapper pays a
+    // MethodBase.GetMethodFromHandle + try/catch on EVERY CALL (~50µs). The Gauntlet
+    // prefab system contains per-widget-recursion methods that UIExtenderEx patches
+    // (WidgetFactory.IsCustomType prefix, WidgetTemplate.OnRelease blank-transpiler);
+    // a tournament's accumulated template tree calls them ~2 MILLION times at release,
+    // so the shield tax amplified a milliseconds-scale teardown into a measured 104-109s
+    // frozen exit (+8,276 gen0 GCs, invariant across sessions — stack-sampled proof in
+    // docs/reviews/rca-tournament-exit-hang-2026-07-06.md round 2). Shield value there
+    // is nil anyway: the only patcher of that layer is BUTR's own UIExtenderEx.
+    private static readonly string[] ExcludedTargetNamespacePrefixes =
+    {
+        "TaleWorlds.GauntletUI",
+        "TaleWorlds.TwoDimension",
+        // Round-2 compat review (2026-07-10): TAOM's own Patch38 target
+        // (SettlementNameplateWidget.DetermineTargetAlphaValue, ~3000 calls/sec on the
+        // campaign map) lives here and was silently paying the shield tax every frame.
+        // Same rationale as above: hot widget/view layer, shield value nil.
+        "TaleWorlds.MountAndBlade.GauntletUI",
+    };
+
+    private static bool IsExcludedTarget(MethodBase method)
+    {
+        try
+        {
+            var ns = method.DeclaringType?.Namespace ?? string.Empty;
+            foreach (var prefix in ExcludedTargetNamespacePrefixes)
+            {
+                if (ns.StartsWith(prefix, StringComparison.Ordinal))
+                    return true;
+            }
+        }
+        catch { /* fail open — an unreadable type just gets shielded as before */ }
+        return false;
+    }
+
     private static readonly Dictionary<string, int> _ownerCounts =
         new(StringComparer.OrdinalIgnoreCase);
     private static readonly object _ownerLock = new();
@@ -163,6 +199,16 @@ public static class PatchShield
                     }
                     catch { }
 
+                    // Never shield hot UI-layer targets — a per-call __originalMethod
+                    // finalizer on the Gauntlet prefab system froze tournament exits for
+                    // ~107s (#331 round 2). See ExcludedTargetNamespacePrefixes.
+                    if (IsExcludedTarget(method))
+                    {
+                        _shielded.Add(method);
+                        skipped++;
+                        continue;
+                    }
+
                     try
                     {
                         bool isVoid = true;
diff --git a/Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs b/Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs
index 41e1d34e..b5ed9e3c 100644
--- a/Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs
+++ b/Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs
@@ -18,6 +18,7 @@ public sealed class BattleLoadDiagnosticsService : IBattleLoadDiagnosticsService
     private int _seq;
     private volatile string _currentStatusLine = "phase=<none>";
     private volatile bool _exitWindowActive;
+    private long _exitWindowOpenedUtcTicks; // 0 = closed; read via Interlocked (feeds ExitStallSampler)
 
     public BattleLoadDiagnosticsService(
         IModLogger logger,
@@ -37,7 +38,7 @@ public sealed class BattleLoadDiagnosticsService : IBattleLoadDiagnosticsService
         // Window state transitions are UNCONDITIONAL — a stale exit window must close even
         // while the master toggle is off, or a mid-window toggle-off latches it and the next
         // map activation emits spurious Exit* lines (deep-review data-flow finding, 2026-07-06).
-        _exitWindowActive = false;
+        CloseExitWindow();
         if (!IsEnabled) return;
         try
         {
@@ -74,7 +75,7 @@ public sealed class BattleLoadDiagnosticsService : IBattleLoadDiagnosticsService
     {
         // A mission starting means any still-open exit window is stale (chained mission
         // without map activation) — close it unconditionally before entry-phase logging.
-        _exitWindowActive = false;
+        CloseExitWindow();
         if (!IsEnabled) return;
         Emit(BattleLoadPhase.MissionInitialize, $"scene='{sceneName}'");
     }
@@ -114,6 +115,8 @@ public sealed class BattleLoadDiagnosticsService : IBattleLoadDiagnosticsService
 
     public bool IsExitWindowActive => _exitWindowActive;
 
+    public long ExitWindowOpenedUtcTicks => Interlocked.Read(ref _exitWindowOpenedUtcTicks);
+
     public void LogExitBegin(string missionName, string sceneName, int agentCount, int allAgentCount)
     {
         if (!IsEnabled) return;
@@ -122,6 +125,7 @@ public sealed class BattleLoadDiagnosticsService : IBattleLoadDiagnosticsService
             Interlocked.Exchange(ref _seq, 0);
             _stopwatch.Restart();
             _exitWindowActive = true;
+            Interlocked.Exchange(ref _exitWindowOpenedUtcTicks, DateTime.UtcNow.Ticks);
             Emit(BattleLoadPhase.ExitBegin,
                 $"mission='{missionName}' scene='{sceneName}' agents={agentCount}/{allAgentCount} {GcStats()}");
         }
@@ -176,11 +180,17 @@ public sealed class BattleLoadDiagnosticsService : IBattleLoadDiagnosticsService
             Emit(BattleLoadPhase.FirstMapTick, $"isSaving={isSaving}");
         // Close unconditionally — the hook only calls this while the window is open, and a
         // mid-window toggle-off must not latch the window (only the LOGGING is gated).
-        _exitWindowActive = false;
+        CloseExitWindow();
     }
 
     private bool IsExitPhaseLoggable() => IsEnabled && _exitWindowActive;
 
+    private void CloseExitWindow()
+    {
+        _exitWindowActive = false;
+        Interlocked.Exchange(ref _exitWindowOpenedUtcTicks, 0L);
+    }
+
     // gen0/gen1/gen2 collection counts + managed heap size. Deltas between ExitBegin and
     // MapResumed expose a mission-end full GC (Common.MemoryCleanupGC) as the time sink.
     private static string GcStats()
diff --git a/Main/SubModule.cs b/Main/SubModule.cs
index a632a9fb..ee54dfd8 100644
--- a/Main/SubModule.cs
+++ b/Main/SubModule.cs
@@ -901,6 +901,13 @@ public class SubModule : MBSubModuleBase
         _harmony.PatchCategory("Patch43_BattleLoadDiagnostics");
         IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();
 
+        // Exit-stall stack sampler (#331 round 2): OnGameInitializationFinished runs on the
+        // game's main thread — the same thread the tournament-exit stall freezes — so this
+        // is a valid capture point for the sampler's main-thread reference.
+        var exitStallSampler = IoC.Resolve<Features.BattleLoadDiagnostics.ExitStallSampler>();
+        exitStallSampler.SetMainThread(System.Threading.Thread.CurrentThread);
+        exitStallSampler.Start();
+
         // Patch60 — release the tournament UI movie/layer at OnEndMission time. The engine's
         // MissionGauntletTournamentView leaks both (nulls without release, unlike the practice
         // view), deferring the Tournament-movie teardown into ScreenBase.HandleFinalize under
warning: in the working copy of 'Dependencies/Foundation/PatchShield.cs', LF will be replaced by CRLF the next time Git touches it
warning: in the working copy of 'Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs', LF will be replaced by CRLF the next time Git touches it

 succeeded in 568ms:

FullName                                                                                                    Length LastW
                                                                                                                   riteT
                                                                                                                   ime
--------                                                                                                    ------ -----
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md        461326 7/10…
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.prompt.md   8053 7/10…



codex
I found two likely actionable issues and a couple of lower-risk hardening/doc items. I’m doing one last pass for stale docs/comments because this feature is diagnostic-heavy and its comments are part of the operating contract.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'docs/features/battle-load-diagnostics.md','docs/reviews/rca-tournament-exit-hang-2026-07-06.md','docs/migration/dr3-maintenance.md','CHANGELOG.md'); foreach("'$p in $files){ $i=0; Get-Content $p | ForEach-Object { $i++; if($_ -match '"'reflection-invoked|hidden from the reference|\\+8s|\\+20s|\\+45s|\\+15s|\\+30s|\\+60s|Thread.Suspend|ExitStallSampler'){ '{0}:{1}: {2}' -f "'$p,$i,$_ } } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main/Features/BattleLoadDiagnostics,docs/features,docs/reviews,docs/migration -Recurse -Include *.cs,*.md | Select-String -Pattern '\\+8s|\\+20s|\\+45s|reflection-invoked|hidden from the reference assemblies' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main/Features/BattleLoadDiagnostics,TAOM.Tests/Features/BattleLoadDiagnostics -Recurse -Include *.cs | Select-String -Pattern 'ShouldSample|Poll\\(|_samplesTaken|CaptureMainThreadStack|Thread.Suspend|Resume|LogWarning' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main/Features/BattleLoadDiagnostics,Main/SubModule.cs -Recurse -Include *.cs | Select-String -Pattern 'EnableBattleLoadDiagnostics|IsEnabled =>|ExitStallSampler|SetMainThread|Start\\(|StallWatchdogEnabled|EnableStallWatchdog' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 575ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Domain\BattleLoadPhase.cs:29: MapResumed,
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\MapState_OnActivate_ExitPhase_Patch.cs:29: svc.LogMapResumed(isSaving);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\MapState_OnTick_ExitPhase_Patch.cs:7: // Exit phase 7 (issue #331) — the first MapState.OnTick after MapResumed closes the exit
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\MapState_OnTick_ExitPhase_Patch.cs:8: // window: the MapResumed->FirstMapTick delta captures menu/VM re-init cost after activation.
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\Mission_EndMission_ExitPhase_Patch.cs:34: // until the next campaign map activate and stamp a spurious MapResumed pair
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:171: public void LogMapResumed(bool isSaving)
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:174: Emit(BattleLoadPhase.MapResumed, $"isSaving={isSaving} {GcStats()}");
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:195: // MapResumed expose a mission-end full GC (Common.MemoryCleanupGC) as the time sink.
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:226: try { _logger.LogWarning($"{Tag} {where} failed: {ex.GetType().Name}: {ex.Message}"); }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:55: _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:62: private void Poll()
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:103: _logger.LogWarning($"{Tag} WATCHDOG bundle failed: {ex.GetType().Name}: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:109: try { _logger.LogWarning($"{Tag} WATCHDOG poll failed: {ex.GetType().Name}: {ex.Message}"); }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:16: // Thread.Suspend/StackTrace(Thread) are obsolete-but-functional on net472; this is the
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:18: // thread mid-GC and then allocating can deadlock the sampler before Resume — acceptable for
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:20: // whole capture is try/catch'd with Resume in finally, and the sampler only ever runs while
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:40: private int _samplesTaken;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:58: _timer = new Timer(_ => Poll(), null, PollInterval, PollInterval);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:62: public static bool ShouldSample(double elapsedSeconds, int samplesTaken)
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:66: private void Poll()
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:75: _samplesTaken = 0;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:83: _samplesTaken = 0;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:87: if (!ShouldSample(elapsed, _samplesTaken)) return;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:89: int sampleIndex = ++_samplesTaken;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:90: CaptureMainThreadStack(sampleIndex, elapsed);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:94: try { _logger.LogWarning($"{Tag} poll failed: {ex.GetType().Name}: {ex.Message}"); }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:99: private void CaptureMainThreadStack(int sampleIndex, double elapsedSeconds)
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:104: _logger.LogWarning($"{Tag} sample#{sampleIndex} skipped — main thread not set/alive");
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:109: // Thread.Suspend/Resume + StackTrace(Thread,bool) are obsolete-as-WARNING on net472
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:114: thread.Suspend();
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:121: _logger.LogWarning($"{Tag} sample#{sampleIndex} capture failed: {ex.GetType().Name}: {ex.Message}");
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:125: try { thread.Resume(); }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:126: catch { /* resume must never throw out */ }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:132: // Format AFTER resume — keep the suspended window as small as possible.
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsService.cs:60: void LogMapResumed(bool isSaving);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:119: _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:138: _sut.LogMapResumed(false);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:190: _sut.LogMapResumed(false);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:220: public void LogMapResumed_InWindow_IncludesGcStatsAndSavingFlag()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:223: _sut.LogMapResumed(true);
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:225: s.Contains("MapResumed") && s.Contains("isSaving=True") &&
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsServiceTests.cs:237: _sut.LogMapResumed(false); // window closed — must be silent
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:8: /// Thread.Suspend capture is game-only per ADR-008; the schedule decision is what unit
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:15: public void ShouldSample_BeforeFirstThreshold_ReturnsFalse()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:19: Assert.IsFalse(ExitStallSampler.ShouldSample(elapsedSeconds: 14.9, samplesTaken: 0));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:23: public void ShouldSample_AtEachThreshold_FiresInOrder()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:25: Assert.IsTrue(ExitStallSampler.ShouldSample(15.0, 0));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:26: Assert.IsFalse(ExitStallSampler.ShouldSample(15.0, 1), "second sample must wait for its own threshold");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:27: Assert.IsTrue(ExitStallSampler.ShouldSample(30.0, 1));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:28: Assert.IsFalse(ExitStallSampler.ShouldSample(30.0, 2), "third sample must wait for its own threshold");
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:29: Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 2));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:33: public void ShouldSample_AllSamplesTaken_ReturnsFalse()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:35: Assert.IsFalse(ExitStallSampler.ShouldSample(999.0, 3));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:39: public void ShouldSample_LateArm_CatchesUpOneAtATime()
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:43: Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 0));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:44: Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 1));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:45: Assert.IsTrue(ExitStallSampler.ShouldSample(60.0, 2));
C:\Users\mikew\source\repos\TAOM\TAOM.Tests\Features\BattleLoadDiagnostics\ExitStallSamplerTests.cs:46: Assert.IsFalse(ExitStallSampler.ShouldSample(60.0, 3));

 succeeded in 569ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:8: // the first marker. Rich attacker/defender data isn't populated at Start() yet, so the
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\Hooks\PlayerEncounter_Start_Patch.cs:35: svc.LogEncounterStart(size);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsIoC.cs:16: container.Register<ExitStallSampler>(Reuse.Singleton);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:21: private long _exitWindowOpenedUtcTicks; // 0 = closed; read via Interlocked (feeds ExitStallSampler)
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:33: public bool IsEnabled => _settings.IsEnabled;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:46: _stopwatch.Restart();
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:51: public void LogEncounterStart(int mainPartySize)
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:56: try { if (!_stopwatch.IsRunning) _stopwatch.Restart(); } catch { /* clock best-effort */ }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsService.cs:126: _stopwatch.Restart();
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettings.cs:21: public bool EnableBattleLoadDiagnostics { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettings.cs:26: public bool EnableStallWatchdog { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettings.cs:31: public bool EnableStallWatchdogBundle { get; set; } = true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:15: public bool IsEnabled =>
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:16: BattleLoadDiagnosticsSettings.Instance?.EnableBattleLoadDiagnostics ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:18: public bool StallWatchdogEnabled =>
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:19: BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdog ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadDiagnosticsSettingsProvider.cs:22: BattleLoadDiagnosticsSettings.Instance?.EnableStallWatchdogBundle ?? true;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:52: public void Start()
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\BattleLoadStallWatchdog.cs:68: if (!_settings.IsEnabled || !_settings.StallWatchdogEnabled) return;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:22: public sealed class ExitStallSampler : IDisposable
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:42: public ExitStallSampler(
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:53: public void SetMainThread(Thread mainThread) => _mainThread = mainThread;
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:55: public void Start()
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsService.cs:20: void LogEncounterStart(int mainPartySize);
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\IBattleLoadDiagnosticsSettingsProvider.cs:9: bool StallWatchdogEnabled { get; }
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\StallReportNotifier.cs:73: Process.Start(new ProcessStartInfo
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\StallReportNotifier.cs:82: Process.Start(new ProcessStartInfo
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:434: protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:436: base.OnGameStart(game, gameStarterObject);
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:902: IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:907: var exitStallSampler = IoC.Resolve<Features.BattleLoadDiagnostics.ExitStallSampler>();
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:908: exitStallSampler.SetMainThread(System.Threading.Thread.CurrentThread);
C:\Users\mikew\source\repos\TAOM\Main\SubModule.cs:909: exitStallSampler.Start();

 succeeded in 693ms:
docs/features/battle-load-diagnostics.md:79: ### The exit-stall stack sampler (`ExitStallSampler`, #331 round 2)
docs/features/battle-load-diagnostics.md:81: Phase gaps say *where* time went; only a stack says *what the frozen thread was doing*. `ExitStallSampler` is the watchdog's exit-side sibling: a background `Timer` polls the service's `ExitWindowOpenedUtcTicks` latch (nonzero exactly while the exit window is open; maintained by the same unconditional closers), and when a stall crosses **+15s/+30s/+60s** it suspends the MAIN thread (captured as `Thread.CurrentThread` in `SubModule.OnGameInitializationFinished`), walks it with the reflection-invoked `StackTrace(Thread, bool)` constructor (present in the net472 runtime, hidden from the reference assemblies), resumes, and logs the frames as `[ExitStall] sample#N` lines. Three samples of a deterministic stall show whether the top frames are stationary (one loop) or phased. Thresholds sit above the healthy tournament exit (~9.5s residual, measured 2026-07-10) so normal exits never log a false sample. Known accepted risk (documented in the class): suspending a thread mid-GC and allocating before resume can deadlock the sampler — acceptable for dev-machine diagnostics on reproducible stalls. **This sampler named the #331 round-2 sink in a single repro** (`PatchShield.ShieldFinalizerVoid` atop the `WidgetTemplate.OnRelease` recursion) after two multi-agent static rounds had bounded it wrong — see the RCA round-2 section and LESSONS-LEARNED "sample the live stack".
docs/reviews/rca-tournament-exit-hang-2026-07-06.md:35: **How it was named:** static analysis was exhausted (22+ agents over two rounds had bounded every generic widget-release path and reality contradicted the bounds), so an in-process `ExitStallSampler` captured the frozen MAIN thread's managed stack at +8/+20/+45s into the stall. One repro sufficed:
docs/reviews/rca-tournament-exit-hang-2026-07-06.md:49: **Fix:** `PatchShield.ExcludedTargetNamespacePrefixes` — never shield `TaleWorlds.GauntletUI`/`TaleWorlds.TwoDimension` targets. Measured result: exit 105-109s → **9.5s** (`ReleaseMovie=8,822ms`, gen0 +3). Residual = UIExtenderEx's legitimate prefix wrapper at ~10^6 calls; accepted per simplicity criterion. Patch60 stays (real leak, cost-neutral relocation, and its `ReleaseMovie=Nms` stamp is the permanent regression canary). Sampler kept as standing diagnostics, thresholds raised to +15/+30/+60s above the known-good residual.
docs/reviews/rca-tournament-exit-hang-2026-07-06.md:68: | 7 | LOW | `ExitStallSampler`'s reflection-invoked `StackTrace(Thread,bool)` was justified by a FALSE comment ("hidden from the reference assemblies") — the ctor is present, obsolete-as-warning. The original compile failure was a wrong NAMED ARGUMENT (`fNeedFileInfo:` vs the real `needFileInfo`), misdiagnosed as a missing ctor. | Evidence discipline (misdiagnosed compile error) | CS1503 "cannot convert Thread to int" reads like overload-absence; named-argument mismatch silently excludes the intended overload from resolution. The workaround worked, so the wrong explanation was never re-tested. | Fixed: direct `new StackTrace(thread, needFileInfo: false)` under the existing pragma; reflection machinery deleted. Rule of thumb: before writing a "the API is missing" comment, re-try the call with positional arguments — a named-arg typo produces the same error class. |
CHANGELOG.md:35: - **Measured, not modeled:** new `ExitStallSampler` (`Main/Features/BattleLoadDiagnostics/`) — background thread that photographs the MAIN thread's managed stack at +15/+30/+60s into any exit stall (armed by the exit window's new `ExitWindowOpenedUtcTicks`; `Thread.Suspend` + the obsolete-as-warning `StackTrace(Thread,bool)` ctor, net472). First repro named the sink in one shot: `PatchShield.ShieldFinalizerVoid` atop a 16-deep `WidgetTemplate.OnRelease_Patch2` recursion; the second sample caught `MethodBase.GetMethodFromHandle` inside `WidgetFactory.IsCustomType_Patch2`.
CHANGELOG.md:38: - Patch60 (round 1) stays: the leak it fixes is real and its relocation is cost-neutral; its new per-exit `ReleaseMovie=Nms` stamp is the permanent regression canary. Sampler thresholds raised to +15/+30/+60s (above the known-good residual) and kept as standing diagnostics.
CHANGELOG.md:7682: 34 new tests across 4 new classes ([`CultureItemPoolServiceClassifierTests`](TAOM.Tests/Features/CultureMarketplace/CultureItemPoolServiceClassifierTests.cs) — 6, [`GetRoutedItemsForCultureTests`](TAOM.Tests/Features/CultureMarketplace/GetRoutedItemsForCultureTests.cs) — 6, [`CultureMarketplaceBehaviorGuaranteedStockTests`](TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceBehaviorGuaranteedStockTests.cs) — 8 via reflection-invoked private methods with mocked adapter, [`CultureMarketplaceBehaviorFilterTests`](TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceBehaviorFilterTests.cs) — 9 same shape) plus 5 new tests extending the existing ConfigProvider tests for `min_stock` validation. Full suite 2321/2323 green (was 2287). CultureMarketplace+adapter scope: 87/87 (was 53).

 succeeded in 1050ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\BattleLoadDiagnostics\ExitStallSampler.cs:13: // main thread's managed stack at +8s/+20s/+45s and logs the frames. Three samples of a
C:\Users\mikew\source\repos\TAOM\docs\features\battle-load-diagnostics.md:81: Phase gaps say *where* time went; only a stack says *what the frozen thread was doing*. `ExitStallSampler` is the watchdog's exit-side sibling: a background `Timer` polls the service's `ExitWindowOpenedUtcTicks` latch (nonzero exactly while the exit window is open; maintained by the same unconditional closers), and when a stall crosses **+15s/+30s/+60s** it suspends the MAIN thread (captured as `Thread.CurrentThread` in `SubModule.OnGameInitializationFinished`), walks it with the reflection-invoked `StackTrace(Thread, bool)` constructor (present in the net472 runtime, hidden from the reference assemblies), resumes, and logs the frames as `[ExitStall] sample#N` lines. Three samples of a deterministic stall show whether the top frames are stationary (one loop) or phased. Thresholds sit above the healthy tournament exit (~9.5s residual, measured 2026-07-10) so normal exits never log a false sample. Known accepted risk (documented in the class): suspending a thread mid-GC and allocating before resume can deadlock the sampler — acceptable for dev-machine diagnostics on reproducible stalls. **This sampler named the #331 round-2 sink in a single repro** (`PatchShield.ShieldFinalizerVoid` atop the `WidgetTemplate.OnRelease` recursion) after two multi-agent static rounds had bounded it wrong — see the RCA round-2 section and LESSONS-LEARNED "sample the live stack".
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:120: **How it was named:** static analysis was exhausted (22+ agents over two rounds had bounded every generic widget-release path and reality contradicted the bounds), so an in-process `ExitStallSampler` captured the frozen MAIN thread's managed stack at +8/+20/+45s into the stall. One repro sufficed:
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:153: | 7 | LOW | `ExitStallSampler`'s reflection-invoked `StackTrace(Thread,bool)` was justified by a FALSE comment ("hidden from the reference assemblies") — the ctor is present, obsolete-as-warning. The original compile failure was a wrong NAMED ARGUMENT (`fNeedFileInfo:` vs the real `needFileInfo`), misdiagnosed as a missing ctor. | Evidence discipline (misdiagnosed compile error) | CS1503 "cannot convert Thread to int" reads like overload-absence; named-argument mismatch silently excludes the intended overload from resolution. The workaround worked, so the wrong explanation was never re-tested. | Fixed: direct `new StackTrace(thread, needFileInfo: false)` under the existing pragma; reflection machinery deleted. Rule of thumb: before writing a "the API is missing" comment, re-try the call with positional arguments — a named-arg typo produces the same error class. |
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:479: Phase gaps say *where* time went; only a stack says *what the frozen thread was doing*. `ExitStallSampler` is the watchdog's exit-side sibling: a background `Timer` polls the service's `ExitWindowOpenedUtcTicks` latch (nonzero exactly while the exit window is open; maintained by the same unconditional closers), and when a stall crosses **+15s/+30s/+60s** it suspends the MAIN thread (captured as `Thread.CurrentThread` in `SubModule.OnGameInitializationFinished`), walks it with the reflection-invoked `StackTrace(Thread, bool)` constructor (present in the net472 runtime, hidden from the reference assemblies), resumes, and logs the frames as `[ExitStall] sample#N` lines. Three samples of a deterministic stall show whether the top frames are stationary (one loop) or phased. Thresholds sit above the healthy tournament exit (~9.5s residual, measured 2026-07-10) so normal exits never log a false sample. Known accepted risk (documented in the class): suspending a thread mid-GC and allocating before resume can deadlock the sampler — acceptable for dev-machine diagnostics on reproducible stalls. **This sampler named the #331 round-2 sink in a single repro** (`PatchShield.ShieldFinalizerVoid` atop the `WidgetTemplate.OnRelease` recursion) after two multi-agent static rounds had bounded it wrong — see the RCA round-2 section and LESSONS-LEARNED "sample the live stack".
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:767: 13: // main thread's managed stack at +8s/+20s/+45s and logs the frames. Three samples of a
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:7598: "C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$files=@('"'docs/features/battle-load-diagnostics.md','docs/reviews/rca-tournament-exit-hang-2026-07-06.md','docs/migration/dr3-maintenance.md','CHANGELOG.md'); foreach("'$p in $files){ $i=0; Get-Content $p | ForEach-Object { $i++; if($_ -match '"'reflection-invoked|hidden from the reference|\\+8s|\\+20s|\\+45s|\\+15s|\\+30s|\\+60s|Thread.Suspend|ExitStallSampler'){ '{0}:{1}: {2}' -f "'$p,$i,$_ } } }' in C:\Users\mikew\source\repos\TAOM
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:7600: "C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path Main/Features/BattleLoadDiagnostics,docs/features,docs/reviews,docs/migration -Recurse -Include *.cs,*.md | Select-String -Pattern '\\+8s|\\+20s|\\+45s|reflection-invoked|hidden from the reference assemblies' | ForEach-Object { '{0}:{1}: {2}' -f "'$_.Path,$_.LineNumber,$_.Line.Trim() }' in C:\Users\mikew\source\repos\TAOM
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:7702: docs/features/battle-load-diagnostics.md:81: Phase gaps say *where* time went; only a stack says *what the frozen thread was doing*. `ExitStallSampler` is the watchdog's exit-side sibling: a background `Timer` polls the service's `ExitWindowOpenedUtcTicks` latch (nonzero exactly while the exit window is open; maintained by the same unconditional closers), and when a stall crosses **+15s/+30s/+60s** it suspends the MAIN thread (captured as `Thread.CurrentThread` in `SubModule.OnGameInitializationFinished`), walks it with the reflection-invoked `StackTrace(Thread, bool)` constructor (present in the net472 runtime, hidden from the reference assemblies), resumes, and logs the frames as `[ExitStall] sample#N` lines. Three samples of a deterministic stall show whether the top frames are stationary (one loop) or phased. Thresholds sit above the healthy tournament exit (~9.5s residual, measured 2026-07-10) so normal exits never log a false sample. Known accepted risk (documented in the class): suspending a thread mid-GC and allocating before resume can deadlock the sampler — acceptable for dev-machine diagnostics on reproducible stalls. **This sampler named the #331 round-2 sink in a single repro** (`PatchShield.ShieldFinalizerVoid` atop the `WidgetTemplate.OnRelease` recursion) after two multi-agent static rounds had bounded it wrong — see the RCA round-2 section and LESSONS-LEARNED "sample the live stack".
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:7703: docs/reviews/rca-tournament-exit-hang-2026-07-06.md:35: **How it was named:** static analysis was exhausted (22+ agents over two rounds had bounded every generic widget-release path and reality contradicted the bounds), so an in-process `ExitStallSampler` captured the frozen MAIN thread's managed stack at +8/+20/+45s into the stall. One repro sufficed:
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:7705: docs/reviews/rca-tournament-exit-hang-2026-07-06.md:68: | 7 | LOW | `ExitStallSampler`'s reflection-invoked `StackTrace(Thread,bool)` was justified by a FALSE comment ("hidden from the reference assemblies") — the ctor is present, obsolete-as-warning. The original compile failure was a wrong NAMED ARGUMENT (`fNeedFileInfo:` vs the real `needFileInfo`), misdiagnosed as a missing ctor. | Evidence discipline (misdiagnosed compile error) | CS1503 "cannot convert Thread to int" reads like overload-absence; named-argument mismatch silently excludes the intended overload from resolution. The workaround worked, so the wrong explanation was never re-tested. | Fixed: direct `new StackTrace(thread, needFileInfo: false)` under the existing pragma; reflection machinery deleted. Rule of thumb: before writing a "the API is missing" comment, re-try the call with positional arguments — a named-arg typo produces the same error class. |
C:\Users\mikew\source\repos\TAOM\docs\reviews\codex-adversarial-tournament-exit-round2-2026-07-10.md:7708: CHANGELOG.md:7682: 34 new tests across 4 new classes ([`CultureItemPoolServiceClassifierTests`](TAOM.Tests/Features/CultureMarketplace/CultureItemPoolServiceClassifierTests.cs) — 6, [`GetRoutedItemsForCultureTests`](TAOM.Tests/Features/CultureMarketplace/GetRoutedItemsForCultureTests.cs) — 6, [`CultureMarketplaceBehaviorGuaranteedStockTests`](TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceBehaviorGuaranteedStockTests.cs) — 8 via reflection-invoked private methods with mocked adapter, [`CultureMarketplaceBehaviorFilterTests`](TAOM.Tests/Features/CultureMarketplace/CultureMarketplaceBehaviorFilterTests.cs) — 9 same shape) plus 5 new tests extending the existing ConfigProvider tests for `min_stock` validation. Full suite 2321/2323 green (was 2287). CultureMarketplace+adapter scope: 87/87 (was 53).
C:\Users\mikew\source\repos\TAOM\docs\reviews\LESSONS-LEARNED.md:756: Two multi-agent rounds (22+ agents) "refuted" the true tournament-exit sink with arithmetic built on ASSUMED counts (widgets ~10^3, "scopes small") — while the decisive evidence sat unexploited in the logs: the gen0 delta was byte-identical (+8,276) across hangs with 4 vs 461 vs 745 agents in different towns, proving a deterministic fixed workload. One in-process stack sample (`ExitStallSampler`: background thread, `Thread.Suspend` + reflection-invoked `StackTrace(Thread,bool)` on net472) named the sink in a single repro after three days of modeling.
C:\Users\mikew\source\repos\TAOM\docs\reviews\rca-tournament-exit-hang-2026-07-06.md:35: **How it was named:** static analysis was exhausted (22+ agents over two rounds had bounded every generic widget-release path and reality contradicted the bounds), so an in-process `ExitStallSampler` captured the frozen MAIN thread's managed stack at +8/+20/+45s into the stall. One repro sufficed:
C:\Users\mikew\source\repos\TAOM\docs\reviews\rca-tournament-exit-hang-2026-07-06.md:68: | 7 | LOW | `ExitStallSampler`'s reflection-invoked `StackTrace(Thread,bool)` was justified by a FALSE comment ("hidden from the reference assemblies") — the ctor is present, obsolete-as-warning. The original compile failure was a wrong NAMED ARGUMENT (`fNeedFileInfo:` vs the real `needFileInfo`), misdiagnosed as a missing ctor. | Evidence discipline (misdiagnosed compile error) | CS1503 "cannot convert Thread to int" reads like overload-absence; named-argument mismatch silently excludes the intended overload from resolution. The workaround worked, so the wrong explanation was never re-tested. | Fixed: direct `new StackTrace(thread, needFileInfo: false)` under the existing pragma; reflection machinery deleted. Rule of thumb: before writing a "the API is missing" comment, re-try the call with positional arguments — a named-arg typo produces the same error class. |

codex
**VANILLA/RUNTIME EVIDENCE**

- Harmony 2.4.2: `Harmony.GetAllPatchedMethods()` delegates to `PatchProcessor.GetAllPatchedMethods()`, which returns `HarmonySharedState.GetPatchedMethods()`; that returns `state.Keys.ToArray()`. `PatchProcessor.Patch()` updates the same `state[original]` key via `HarmonySharedState.UpdatePatchInfo`. So PatchShield’s `_shielded.Add(method)` skip is stable for normal original `MethodBase` targets.
- Harmony `__originalMethod`: `MethodCreatorTools` emits `ldtoken` + `MethodBase.GetMethodFromHandle` for `InjectionType.OriginalMethod`; `MethodCreator` adds try/catch when finalizers exist. This matches the measured per-call wrapper tax.
- net472 empirical probe on CLR `4.0.30319.42000`: two `Suspend()` calls on an already suspended thread did not stack. One `Resume()` resumed the worker; the second `Resume()` threw `ThreadStateException: Thread is not user-suspended`.
- net472 empirical probe: `System.Threading.Timer` callbacks overlap. A 100ms-period callback sleeping 500ms produced `calls=17 maxActive=5`.
- Vanilla v1.4.6 UI hot path: `WidgetFactory.IsCustomType` is a dictionary lookup; `WidgetTemplate.CreateWidgets` appends custom child templates; `WidgetTemplate.OnRelease` recurses through `_children` and `_customTypeChildren`. The three excluded namespaces cover the measured hot methods and the Patch38 nameplate target.

**THREADING ANALYSIS**

- Open path: `LogExitBegin` writes `_exitWindowActive=true` then nonzero ticks. If the timer polls between those writes it sees ticks `0` and skips one poll. Exit-phase hooks read the bool on the main thread after `LogExitBegin` returns, so they see the open window. Benign.
- Close path: `CloseExitWindow` writes `_exitWindowActive=false` then ticks `0`. Exit hooks stop immediately on the bool. A timer poll exactly between those writes can see stale nonzero ticks and take one stale sample, but it cannot keep exit logging alive or relatch the window. Benign.
- Toggle-off path: the round-1 P2 fix is present at the outer hooks. `PlayerEncounter_Start_Patch` calls `ResetLifecycle()` before `IsEnabled`; `Mission_Initialize_BattleLoad_Patch` calls `LogMissionInitialize()` regardless of `IsEnabled`. The service closes ticks in both paths. Clean.
- Timer overlap path: not benign. Timer callbacks can overlap, `_samplesTaken` is unsynchronized, and overlapping captures can double-suspend the same main thread. Runtime evidence shows the second suspend does not require two resumes, so a later callback can resume the main thread while an earlier `StackTrace(Thread)` capture still assumes it is suspended.

**EXCLUSION SAFETY**

The exclusion removes PatchShield protection from all targets under:

- `TaleWorlds.GauntletUI`
- `TaleWorlds.TwoDimension`
- `TaleWorlds.MountAndBlade.GauntletUI`

That includes UIExtenderEx prefab/widget patches, TAOM Patch38 nameplate fade, and any third-party mod patching that UI layer. The tradeoff is acceptable for TAOM as a total conversion: this layer is per-widget/per-frame hot, the measured shield cost was catastrophic, and the most important patcher there is vendored UIExtenderEx. A third-party outdated UI mod could now throw instead of being swallowed, but shielding that namespace recreates the incident class. No PatchShield finding.

**FINDINGS OR OBSERVATIONS**

P1: No findings.

[P2] Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs:66 — Timer Reentrancy — `System.Threading.Timer` callbacks can overlap, but `Poll()` has no reentrancy guard and mutates `_samplesTaken` with `++_samplesTaken`; overlapping captures can race sample numbering and resume the main thread out from under another capture — Add an `Interlocked.Exchange`/`finally` `_pollActive` guard around `Poll()`.

[P2] Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs:70 — Unsafe Sampler Gating — the class documents `Thread.Suspend` deadlock risk as “dev-machine diagnostic,” but `BattleLoadDiagnosticsSettings.EnableBattleLoadDiagnostics` defaults true and the provider fail-opens true; users cannot keep battle-load diagnostics enabled while disabling only the exit sampler — Add a separate MCM/provider gate for `ExitStallSampler`, preferably opt-in or at least independently disableable.

[P3] Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs:119 — Suspended-Window Allocation — if `new StackTrace(thread, needFileInfo:false)` throws, the catch logs while the main thread is still suspended, expanding the exact suspend-plus-allocation risk the comment tries to bound — Store exception type/message locally, resume in `finally`, then log after resume.

[P3] Main/SubModule.cs:904 — Main-Thread Assumption — current v1.4.6 evidence supports `OnGameInitializationFinished` as an application-thread callback, but there is no runtime invariant if a future engine version moves game init to a loader thread — Capture/check the tick thread from `OnApplicationTick`, or disable the sampler with a warning on thread mismatch.

[P3] Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs:13 — Comment Drift — header still says `+8s/+20s/+45s`; code/tests/docs use `+15s/+30s/+60s` — Update the comment.

[P3] docs/features/battle-load-diagnostics.md:81 — Doc Drift — doc still says the sampler uses a “reflection-invoked” `StackTrace(Thread,bool)` hidden from reference assemblies, but the fixed code now calls the ctor directly — Update the doc to match the round-2 fix.

**QUALITY GATES**

Targeted `dotnet test` could not run: the sandboxed .NET CLI fails before MSBuild with `UnauthorizedAccessException` writing `C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel`.

P1: 0 | P2: 2 | P3: 4  
VERDICT: ISSUES FOUND
SUCCESS: The process with PID 40556 (child process of PID 16576) has been terminated.
SUCCESS: The process with PID 16576 (child process of PID 27992) has been terminated.
SUCCESS: The process with PID 27992 (child process of PID 46396) has been terminated.
SUCCESS: The process with PID 46396 (child process of PID 32396) has been terminated.
SUCCESS: The process with PID 50784 (child process of PID 68160) has been terminated.
SUCCESS: The process with PID 68160 (child process of PID 56156) has been terminated.
SUCCESS: The process with PID 56156 (child process of PID 29140) has been terminated.
SUCCESS: The process with PID 29140 (child process of PID 58964) has been terminated.
SUCCESS: The process with PID 58964 (child process of PID 32396) has been terminated.
tokens used
216,257
**VANILLA/RUNTIME EVIDENCE**

- Harmony 2.4.2: `Harmony.GetAllPatchedMethods()` delegates to `PatchProcessor.GetAllPatchedMethods()`, which returns `HarmonySharedState.GetPatchedMethods()`; that returns `state.Keys.ToArray()`. `PatchProcessor.Patch()` updates the same `state[original]` key via `HarmonySharedState.UpdatePatchInfo`. So PatchShield’s `_shielded.Add(method)` skip is stable for normal original `MethodBase` targets.
- Harmony `__originalMethod`: `MethodCreatorTools` emits `ldtoken` + `MethodBase.GetMethodFromHandle` for `InjectionType.OriginalMethod`; `MethodCreator` adds try/catch when finalizers exist. This matches the measured per-call wrapper tax.
- net472 empirical probe on CLR `4.0.30319.42000`: two `Suspend()` calls on an already suspended thread did not stack. One `Resume()` resumed the worker; the second `Resume()` threw `ThreadStateException: Thread is not user-suspended`.
- net472 empirical probe: `System.Threading.Timer` callbacks overlap. A 100ms-period callback sleeping 500ms produced `calls=17 maxActive=5`.
- Vanilla v1.4.6 UI hot path: `WidgetFactory.IsCustomType` is a dictionary lookup; `WidgetTemplate.CreateWidgets` appends custom child templates; `WidgetTemplate.OnRelease` recurses through `_children` and `_customTypeChildren`. The three excluded namespaces cover the measured hot methods and the Patch38 nameplate target.

**THREADING ANALYSIS**

- Open path: `LogExitBegin` writes `_exitWindowActive=true` then nonzero ticks. If the timer polls between those writes it sees ticks `0` and skips one poll. Exit-phase hooks read the bool on the main thread after `LogExitBegin` returns, so they see the open window. Benign.
- Close path: `CloseExitWindow` writes `_exitWindowActive=false` then ticks `0`. Exit hooks stop immediately on the bool. A timer poll exactly between those writes can see stale nonzero ticks and take one stale sample, but it cannot keep exit logging alive or relatch the window. Benign.
- Toggle-off path: the round-1 P2 fix is present at the outer hooks. `PlayerEncounter_Start_Patch` calls `ResetLifecycle()` before `IsEnabled`; `Mission_Initialize_BattleLoad_Patch` calls `LogMissionInitialize()` regardless of `IsEnabled`. The service closes ticks in both paths. Clean.
- Timer overlap path: not benign. Timer callbacks can overlap, `_samplesTaken` is unsynchronized, and overlapping captures can double-suspend the same main thread. Runtime evidence shows the second suspend does not require two resumes, so a later callback can resume the main thread while an earlier `StackTrace(Thread)` capture still assumes it is suspended.

**EXCLUSION SAFETY**

The exclusion removes PatchShield protection from all targets under:

- `TaleWorlds.GauntletUI`
- `TaleWorlds.TwoDimension`
- `TaleWorlds.MountAndBlade.GauntletUI`

That includes UIExtenderEx prefab/widget patches, TAOM Patch38 nameplate fade, and any third-party mod patching that UI layer. The tradeoff is acceptable for TAOM as a total conversion: this layer is per-widget/per-frame hot, the measured shield cost was catastrophic, and the most important patcher there is vendored UIExtenderEx. A third-party outdated UI mod could now throw instead of being swallowed, but shielding that namespace recreates the incident class. No PatchShield finding.

**FINDINGS OR OBSERVATIONS**

P1: No findings.

[P2] Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs:66 — Timer Reentrancy — `System.Threading.Timer` callbacks can overlap, but `Poll()` has no reentrancy guard and mutates `_samplesTaken` with `++_samplesTaken`; overlapping captures can race sample numbering and resume the main thread out from under another capture — Add an `Interlocked.Exchange`/`finally` `_pollActive` guard around `Poll()`.

[P2] Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs:70 — Unsafe Sampler Gating — the class documents `Thread.Suspend` deadlock risk as “dev-machine diagnostic,” but `BattleLoadDiagnosticsSettings.EnableBattleLoadDiagnostics` defaults true and the provider fail-opens true; users cannot keep battle-load diagnostics enabled while disabling only the exit sampler — Add a separate MCM/provider gate for `ExitStallSampler`, preferably opt-in or at least independently disableable.

[P3] Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs:119 — Suspended-Window Allocation — if `new StackTrace(thread, needFileInfo:false)` throws, the catch logs while the main thread is still suspended, expanding the exact suspend-plus-allocation risk the comment tries to bound — Store exception type/message locally, resume in `finally`, then log after resume.

[P3] Main/SubModule.cs:904 — Main-Thread Assumption — current v1.4.6 evidence supports `OnGameInitializationFinished` as an application-thread callback, but there is no runtime invariant if a future engine version moves game init to a loader thread — Capture/check the tick thread from `OnApplicationTick`, or disable the sampler with a warning on thread mismatch.

[P3] Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs:13 — Comment Drift — header still says `+8s/+20s/+45s`; code/tests/docs use `+15s/+30s/+60s` — Update the comment.

[P3] docs/features/battle-load-diagnostics.md:81 — Doc Drift — doc still says the sampler uses a “reflection-invoked” `StackTrace(Thread,bool)` hidden from reference assemblies, but the fixed code now calls the ctor directly — Update the doc to match the round-2 fix.

**QUALITY GATES**

Targeted `dotnet test` could not run: the sandboxed .NET CLI fails before MSBuild with `UnauthorizedAccessException` writing `C:\Users\CodexSandboxOffline\.dotnet\10.0.301.toolpath.sentinel`.

P1: 0 | P2: 2 | P3: 4  
VERDICT: ISSUES FOUND
