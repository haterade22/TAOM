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
