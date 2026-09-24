# Verify COMP-01, lens: by design or already decided

Checker: fresh adversarial pass, lens "is it by design or already decided". Baseline `b2e387db`
(`git show b2e387db:Main/SubModule.cs`, read from a scratchpad copy; the working-tree file is another
session's). No build or test run.

## COMP-01: Guard every Harmony category apply (64 bare `PatchCategory` calls)

- **Outcome**: CONFIRMED
- **Impact today**: MED (a crash-class mechanism that needs a drifted binding or a foreign IL change
  to fire; no field occurrence beyond the documented 2026-07-31 preview-batch case)

**Decision search (what I looked for, and what came back):**

- `docs/adrs/*.md`: no ADR mentions `SubModule` patch application, try/catch policy or startup failure
  handling. Nothing covers it.
- `.claude/rules/harmony-patches.md:61`: says to register in a `SubModule.cs` apply batch and check the
  apply timing only. It says nothing about guarding, either way.
- `docs/ai-includes/orientation.md` trap index: no row on category apply failures.
- BRIEF "Decided tradeoffs": "TAOM hooks fail open" is about Claude Code shell hooks (`|| true`,
  `exit 0`), not Harmony. None of the other rows touch this.
- `docs/ai-includes/code-quality.md:233-258` ("Fail Fast", "Don't Swallow Errors"): a general style
  guide, and it cuts both ways. The fix sketch logs at Error and raises one red on-screen message, so
  it passes "don't log and continue silently" (`:258`).
- **The repo's own lessons say to guard.** `docs/reviews/lessons/harmony-il.md:172-188` ("A sequence of
  unguarded `PatchCategory` calls fails as a group, and the log cannot tell you it did") says: "isolate
  each in its own try/catch and log the outcome per category — a failure must name itself." It scopes <!-- lint-allow-dash -->
  that rule to "a batch of patch categories [that] backs one user-visible feature", which is why it was
  applied only to the preview batch (`SubModule.cs:1467-1501`). That is a scoping gap. It is not a
  decision to leave the others bare. `docs/reviews/rca-prone-character-tableau-2026-07-31.md:79-82`
  records the same defect as a contributing cause.
  `docs/reviews/REVIEW-LOG.md:1905-1914` fixed the same shape for `Patch68` ("a throw would have aborted
  `Patch30` and every category after it") and noted that "no dimension owned 'does this survive
  startup?'". `docs/reviews/lessons/build-tooling-workflow.md:1142-1157` requires failures to be loud,
  and the fix sketch's failure list does that.
- **PatchShield does not cover it.** `docs/reference/engine/submodule-lifecycle-and-harmony.md:59-62`
  says PatchShield makes "an engine-bump signature drift degrade gracefully instead of crashing". But
  `Dependencies/Foundation/PatchShield.cs:130-242` (at `b2e387db`) walks
  `Harmony.GetAllPatchedMethods()` and attaches a finalizer to methods that are already patched. It
  guards exceptions raised at runtime inside patched methods. It does nothing about a throw from
  `PatchCategory` itself, which is where a renamed target fails. The codebase says so in its own
  comments: `SubModule.cs:1513-1516` ("a PatchCategory throw here would brick startup") and
  `docs/features/player-switcher.md:26`.
- **Patch37 does not cover it either. Someone arguing "by design" could point to it, and the premise is
  wrong.** `SubModule.cs:187-189` and `docs/features/crash-report.md:284` say that attaching Patch37
  first lets its finalizers cover "downstream PatchCategory calls" in TAOM's own `OnSubModuleLoad`. The
  only relevant target is `[HarmonyPatch(typeof(MBSubModuleBase), "OnSubModuleLoad")]`
  (`Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs:113-120`). That finalizer is on the base
  method's body. `TAOM.SubModule.OnSubModuleLoad` is a separate override method, and it is already on
  the stack when Patch37 is applied. The finalizer therefore cannot see a throw from TAOM's own override
  body, and none of Patch37's targets is `OnGameInitializationFinished`. So that coverage claim is not a
  mitigation. It is a separate doc inaccuracy (see the corrected evidence below).
- `plans/_audit/2026-06-12-harvest.md`: the June audit did not raise this finding and did not reject it,
  so there is no earlier "rejected, do not re-raise" record. BRIEF's "Rejected this session" list does
  not include it either.

**The finding's evidence, re-read at `b2e387db`:**

- Bare count: the method bounds are `OnSubModuleLoad` 111-628, `OnGameInitializationFinished`
  1449-1910 and `OnMissionBehaviorInitialize` 1911-2077. I counted lines that match
  `^        _harmony\.PatchCategory\(` inside each range: 13 in `OnSubModuleLoad` (223, 259, 291, 292,
  302, 487, 528, 532, 538, 543, 549, 565, 624) and 50 in `OnGameInitializationFinished` (1510-1745).
  Line 1922 in `OnMissionBehaviorInitialize` sits inside an `if (!_missionTimePatchesApplied)` block
  with no try. Total 64, as claimed. Guarded: 11 in `OnSubModuleLoad` (199, 320, 362, 372, 397, 410,
  433, 452, 475, 559, 575), 8 in `OnGameInitializationFinished` (1493, 1521, 1537, 1813, 1826, 1850,
  1861, 1863), and 1 at 646 in `OnBeforeInitialModuleScreenSetAsRoot`. That is 20, as claimed.
- The flag is set before the batch: `SubModule.cs:1464-1465`. The confirmation is right.
- Engine rethrow: `Module.InitializeSubModuleBases` (`Module.cs:201`) wraps each `OnSubModuleLoad` in
  try/catch (`:206-221`), then calls `MBDebug.Print` and `SetCrashReportCustomString`, then
  `throw new Exception()` at `:220`. The cited range 204-220 is fine.
- `MBGameManager.OnGameInitializationFinished` loops over submodules with no catch
  (`MBGameManager.cs:110-115`). Its caller is `Campaign.cs:1471`. Both confirmed.
- `ManualPatchApplicator.ApplyAll(_harmony)` is at `SubModule.cs:1869`. Confirmed.
- UNVERIFIED, and the finding already says so: what native does with the exception
  `Module.Initialize` rethrows (it is an `[MBCallback]`, `Module.cs:260-261`), and what happens above
  `Campaign.OnInitialize`. A Patch37 finalizer on `Module.OnApplicationTick` or `ScreenManager.Tick`
  might swallow the game-init throw if the campaign load runs under one of them. Even then the
  remaining categories stay unapplied for the process, so the "half-patched session" consequence holds
  either way.

**Corrected evidence:** none needed for COMP-01 itself. One adjacent inaccuracy, useful to whoever
writes the fix: `Main/SubModule.cs:187-189` and `docs/features/crash-report.md:284` overstate Patch37's
coverage of TAOM's own `OnSubModuleLoad` for the reason above. The COMP-01 fix makes that claim moot
for `PatchCategory`, and the doc line should be corrected at the same time.

## What I did not cover

- I did not trace native handling of the `Module.Initialize` rethrow, or the managed frames above
  `Campaign.OnInitialize` (they stay UNVERIFIED, as the finding says).
- I did not read Harmony 2.4.2's `PatchClassProcessor` source to confirm the exception type on a
  missing target. I relied on the codebase's own statements (`SubModule.cs:312-315, 1513-1516`,
  `player-switcher.md:26`) that a missing target throws at `PatchCategory` time.
- I did not re-check the test-impact claim (`FieldCampWiringTests.cs:191` leading-dot spelling), which
  is outside this lens.
- I did not review the other lane findings.
