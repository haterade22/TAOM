# Codex adversarial review -- re-patch crash fix (issue #288)

You are an adversarial reviewer. Read the actual files in this repo and decompile vanilla targets where
relevant. Verify each Known Suspect as CONFIRMED or DISPUTED with evidence (cite file:line and, for
TaleWorlds APIs, decompile the INSTALLED v1.4.6 DLLs at
`E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/` via ilspycmd -- do
NOT trust `E:/Decompiled_Bannerlord/`). Use `--` not the em-dash. Output to this file's `.md` sibling.

## The change (2 files only -- ignore all other uncommitted work in the tree)

Crash (issue #288): the ShaderPrecompilation walk crashed entering item 2/9 with a HarmonyException.
Root cause: `SubModule.OnGameInitializationFinished` applies ~26 `_harmony.PatchCategory(...)` calls +
manual `_harmony.Patch(...)` + `BattleLoadStallWatchdog.Start()` on EVERY game init with no guard.
Harmony patches are process-global, so the 2nd game init in one process re-applies everything; the
non-idempotent `DeliverOffSpring_RaceAssert_Patch` transpiler, chained twice, runs the 2nd pass on IL
the 1st already NOPped, cannot find its `Debug.SilentAssert` anchor, and throws.

File 1 -- `Main/SubModule.cs`: added `private static bool _gameInitPatchesApplied;` and, at the TOP of
`OnGameInitializationFinished` (right AFTER `base.OnGameInitializationFinished(game)`), a guard:
`if (_gameInitPatchesApplied) return; _gameInitPatchesApplied = true;` -- so the whole patch-wiring body
runs once per process. Mirrors the existing `_missionTimePatchesApplied` guard in
`OnMissionBehaviorInitialize`.

File 2 -- `Main/Features/RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs`: the two
`throw new ArgumentException(...)` sites (anchor-not-found) were replaced with
`LogTranspilerDegradation(...); return newInstructions.AsEnumerable();`, and a private static
`LogTranspilerDegradation` helper added that does `try { IoC.Resolve<IModLogger>()?.LogWarning(...) } catch {}`.
Mirrors the existing graceful-degradation pattern in
`Main/Features/CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs`.

## Known Suspects -- CONFIRM or DISPUTE each with evidence

1. **Guard correctness / blast radius.** Read the FULL body of `OnGameInitializationFinished`. Does the
   once-per-process guard break ANYTHING that genuinely needs per-game re-execution? Classify every
   statement (PatchCategory / manual Patch / static Initialize / IoC.Resolve / watchdog Start). Confirm
   the `game` parameter is never used inside the guarded body, and that the genuine per-game
   `campaignStarter.AddBehavior` / `AddModel` registrations live in a DIFFERENT method (`OnGameStart`),
   not here. Flag any statement that captures per-game state or registers a per-Game callback inside the
   guarded body (that would be a real bug under the guard).

2. **Watchdog lifetime.** Read `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs`. Is
   `Start()` correct to call once per process (process-lifetime singleton), or does it need a fresh
   Start per game/mission? Does it have its own idempotency guard? Does the once-guard change its
   behavior for the worse?

3. **Transpiler soft-fail returns UNMODIFIED IL.** In `DeliverOffSpring_RaceAssert_Patch.cs`, confirm
   both early-return points (`callIndex < 0` and `startIndex < 0`) return `newInstructions` while it is
   still unmodified (the NOP-mutation loop runs AFTER both checks). Confirm the soft-fail is harmless
   (the patch is pure noise-reduction). Confirm the `LogTranspilerDegradation` try/catch can't surface
   an exception to the Harmony caller.

4. **PatchShield interaction.** `TAOM.Dependencies.Foundation.PatchShield` installs a Finalizer on every
   Harmony patch and auto-unpatches an offending owner on MissingMethod/MissingField/TypeLoad. With the
   once-per-process guard, if PatchShield unpatches a TAOM category on a transient failure, the guard
   prevents re-application on a later game init -- is that a real regression risk, or acceptable
   (the pre-guard per-game re-application was itself the crash, so "re-apply every game for resilience"
   was never a working design)? Assess and give a verdict. If it IS a real risk, propose the minimal
   mitigation.

5. **base call ordering.** Confirm `base.OnGameInitializationFinished(game)` runs every game init
   (outside the guard) and that this is correct against the MBSubModuleBase contract in installed v1.4.6.

## Also look for what we might have missed

- Any other non-idempotent transpiler in a per-game-init category that would STILL crash if its category
  is ever re-applied via a path the guard doesn't cover (e.g. a direct `PatchCategory` call elsewhere).
  Grep `Main/**/Hooks/**` + `Main/**/Patches/**` for `[HarmonyTranspiler]` + `throw`.
- Thread-safety of the `_gameInitPatchesApplied` flag (is `OnGameInitializationFinished` always on the
  main thread?).
- Whether `_gameInitPatchesApplied` being `static` (vs instance) matters -- is `SubModule` a singleton
  the engine constructs once?

## Output

A findings section (0 CRITICAL / N HIGH / N MED / N LOW), a per-suspect CONFIRM/DISPUTE table with
evidence, and a short verdict (SHIP / NEEDS-FIX). The TAOM verdict is that this is a clean root-cause fix
mirroring two existing proven patterns; try hard to find a hole in that.
