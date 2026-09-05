# RCA — HarmonyException re-patch crash on the 2nd game-init in one process (2026-06-18)

The ShaderPrecompilation walk crashed entering **item 2/9** with a `HarmonyLib.HarmonyException`
(`ArgumentException: Cannot find Debug.SilentAssert call in DeliverOffSpring IL`) thrown out of
`SubModule.OnGameInitializationFinished` re-applying `Patch13_RaceAge`. **Not** the #287 d3dcompiler
crash. Root cause: `OnGameInitializationFinished` re-applies the whole ~26-category patch block on
**every** game init with no guard; the non-idempotent `DeliverOffSpring` transpiler, chained twice,
runs the second pass on IL the first already NOPped, can't find its anchor, and throws. **General
latent bug** — any player loading a 2nd campaign/custom-battle in one session hits it; the shader walk
(N games per process) just made it deterministic on item 2. Issue #288. Fix verified by `/deep-review`
(5 agents, all clean — guard blast-radius traced statement-by-statement, IL anchor re-verified against
installed v1.4.6) AND by Codex gpt-5.5 xhigh (**SHIP, 0/0/0/0** — independently swept every transpiler
in `Hooks/`+`Patches/` to confirm no other throwing-anchor sibling remains, and confirmed `PatchCategory`
is only ever called in `SubModule.cs`).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH (prod crash) | `OnGameInitializationFinished` re-applies all ~26 patch categories + manual patches + watchdog start on EVERY game init with no guard. Harmony patches are process-global, so re-application duplicates every prefix/postfix, restarts the watchdog, and re-chains transpilers. | Lifecycle / idempotency — work that is process-global run per-game | No test or play path had EVER started 2 games in one process before the shader walk. Single-campaign sessions only ever called `OnGameInitializationFinished` once, so the per-game re-application was invisible. The assumption "this runs once" was never stated or guarded. | FIXED: `_gameInitPatchesApplied` once-per-process guard (mirrors the existing `_missionTimePatchesApplied`). Lesson: Harmony patch *application* is a process-global, once-per-process operation — it belongs gated, never run per-game. |
| 2 | HIGH (proximate throw) | `DeliverOffSpring_RaceAssert_Patch.Transpiler` THROWS `ArgumentException` when it can't find its `Debug.SilentAssert` IL anchor — so when chained twice (2nd application sees already-NOPped IL), it crashes patch application instead of no-op'ing. | Non-idempotent IL-mutating transpiler that hard-throws | The transpiler always found its anchor on the FIRST (and, pre-shader-walk, only) application, so the throw branch never fired in practice. The throw was written as a "should never happen" guard, not as a re-application hazard. | FIXED: soft-fail (return unmodified IL + warn) instead of throw, mirroring `RefreshCharacterEntityAuxPatch`. Lesson: an IL-mutating transpiler must be idempotent OR its application gated once; a transpiler that throws on missing-anchor is a latent crash the moment it is re-applied. |
| 3 | LOW | `using System;` left dead in the transpiler file after the `throw new ArgumentException` sites were removed. | Dead using from the fix | Introduced by the fix itself; C# doesn't warn on unused usings so the build stayed 0/0. | FIXED in-session (deep-review Agent 1 flagged it). |

## Root-cause pattern

**The same lesson was already learned once and not generalized.** `RefreshCharacterEntityAuxPatch`
(the `Late_Transpiler` category) was converted from throw-on-missing-anchor to graceful soft-fail in
**Phase 9b #160** — its own comment documents that "any of the three lookups throwing ArgumentException
at PatchCategory time crashed the mod during OnGameInitializationFinished." That fix was applied to one
transpiler but **not swept across the other throwing transpilers in per-game-init categories.**
`DeliverOffSpring_RaceAssert_Patch` was the straggler with the identical shape — and it crashed the
moment a code path (the shader walk) re-applied its category.

Two distinct latent bugs compounded: (1) the *architectural* one — patch application run per-game
instead of once-per-process — and (2) the *local* one — a transpiler that throws instead of degrading.
Either fix alone stops this crash; both together fix the whole class (the guard stops re-application of
*everything*; the soft-fail makes the transpiler robust to any future re-application route).

## Why each deep-review agent verdict landed where it did (review OF the fix)

This RCA's deep-review was of the *fix*, not the original bug (the bug surfaced in production). The
review's job was to validate the load-bearing assumption that the once-guard is safe:

- **Agent 5 (data-flow / blast-radius)** — the decisive agent. Classified every statement in
  `OnGameInitializationFinished` (L548-697) as process-global patch-wiring (categories a-d), proved the
  `game` parameter is never used in the guarded body, confirmed the watchdog is a process-lifetime
  singleton, and confirmed the genuine per-game `AddBehavior`/`AddModel` registrations live in the
  separate `OnGameStart` method (untouched by the guard). This is exactly the trace that had to be done
  before trusting the broad guard.
- **Agent 2 (API)** re-verified against **installed v1.4.6** that `DeliverOffSpring(Hero,Hero,bool)` and
  the `Debug.SilentAssert` + `get_Race` IL anchors are still present — so the happy path is unaffected
  and the soft-fail only changes the (rare) missing-anchor branch.
- **Agents 1/3/4** confirmed standards/perf/closeout; Agent 1 caught the LOW dead-using.

## Feedback memories to codify

Genuine systemic pattern worth a memory (two HIGH-class instances now: this + Phase 9b #160):

- **`feedback_transpiler_idempotency_or_gated_once`** (NEW): An IL-mutating Harmony transpiler is a
  re-application hazard. It must EITHER be idempotent (re-applying yields the same IL) OR its category
  must be applied exactly once per process. A transpiler that `throw`s when it can't find its anchor is
  a latent crash the first time anything re-applies its category. When you convert one throwing
  transpiler to soft-fail, **sweep every sibling transpiler of the same shape** — grep
  `Main/**/Hooks/**` + `Main/**/Patches/**` for `[HarmonyTranspiler]` + `throw` and confirm each either
  degrades gracefully or is gated. (Phase 9b #160 fixed `RefreshCharacterEntityAuxPatch` but missed
  `DeliverOffSpring_RaceAssert_Patch`, which then crashed 2026-06-18.)
- **`feedback_patch_application_is_once_per_process`** (NEW): Harmony patch *application*
  (`PatchCategory` / `_harmony.Patch`) is process-global and belongs run exactly once. Applying it in a
  per-game callback (`OnGameInitializationFinished`) without a guard re-applies on every 2nd+ game in a
  session — duplicating prefix/postfix execution, re-chaining transpilers, and restarting background
  threads. Gate per-game-init patch blocks with a `static bool _xxxApplied` (the `_missionTimePatchesApplied`
  pattern). Surfaces only when something starts >1 game per process (custom-battle relaunch, the shader
  walk) — so it ships invisibly in single-campaign testing.

## Status

Both fixes applied; build 0 warnings / 0 errors; `TAOM.Tests` green except the pre-existing 9
`GetVolunteerTroopId_DolGuldur*` failures (unrelated parallel work). The fix is in-game-validated by:
(a) the shader walk advancing past item 2/9 to 9/9, and (b) loading a 2nd campaign/custom-battle in one
session without a HarmonyException — both pending the user's re-test.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
