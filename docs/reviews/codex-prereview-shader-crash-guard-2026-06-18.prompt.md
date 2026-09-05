# Codex pre-review -- ShaderPrecompileCrashGuard (per-scene crash auto-skip)

You are an adversarial reviewer. Read the actual files in this repo. Verify each Known Suspect as
CONFIRMED or DISPUTED with file:line evidence. Use `--` not the em-dash. This is a focused review of a
small, self-contained feature (file-backed crash guard + runner wiring) -- no heavy vanilla-decompile
needed; the only TaleWorlds surface is pre-existing (`MBGameManager`, `LoadingWindow`, `Utilities`),
unchanged by this diff.

## What the change does (4 files; ignore all other uncommitted work in the tree)

A user's mods-removed shader-precompile walk HARD-CRASHED at item 9 = `taom_rohan_battle_fords_of_isen_forceatmo`
with a pure-native ACCESS_VIOLATION during the scene's `MissionInitialize` (concurrent `pbr_terrain`
input-layout-9 compile; GPU/driver-specific -- the scene loads fine on the dev machine). A native
scene-load crash is not catchable in managed code, so it hard-stops the walk; an affected user can never
get past that item.

The fix adds a per-scene crash guard so the walk self-heals: it records a scene that crashed the process
during load and skips it on subsequent walks.

- NEW `Main/Features/ShaderPrecompilation/IShaderPrecompileCrashGuard.cs` + `ShaderPrecompileCrashGuard.cs`
  -- file-backed, mirrors `Main/Features/BattleLoadDiagnostics/BattleLoadStallMarker.cs`. Two files in
  `Logs/`: an inflight marker (`shader-precompile-inflight.marker`, scene id the walk is loading now) +
  a persistent skip list (`shader-precompile-crashed-scenes.txt`). API: `MarkLoading(sceneId)`,
  `ClearLoading()`, `ConsumeAndGetSkipSet()`. Pure statics `ParseInflightScene` / `ParseCrashedScenes` /
  `FormatInflight` are unit-tested.
- MODIFIED `Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs`: ctor adds the guard; `Begin()`
  calls `ConsumeAndGetSkipSet()` then filters the scene list (`scenes.Where(s => !skip.Contains(s))`,
  case-insensitive HashSet) before `BuildPlan`; `StartCurrentItem()` calls `MarkLoading(item.SceneId)`
  for `ScenePass` items only (before `MBGameManager.StartNewGame`); `TickEnding`'s resolution block calls
  `ClearLoading()`; `Finish()` calls `ClearLoading()` (belt-and-suspenders); added `using System.Linq;`.
- MODIFIED `Main/Features/ShaderPrecompilation/ShaderPrecompilationIoC.cs`: registers the guard (Singleton).
- NEW test file (10 tests).

## Known Suspects -- CONFIRM or DISPUTE each with evidence

1. **False-skip on a non-crash end.** The marker must be left ONLY by a true process crash. Trace every
   item-end path in the runner state machine and confirm each clears the marker: normal compiled-settled
   (`TickRunning` AdvanceItem -> `BeginEnd` -> Ending -> `TickEnding` resolution -> `ClearLoading`); a
   per-item decider timeout (`TickRunning` AbortItem -> same); a never-rendered start timeout
   (`TickStarting` -> `BeginEnd` -> ...); `OnItemFailed` -> `BeginEnd` -> ...; a managed `StartNewGame`
   throw (`StartCurrentItem` catch -> `BeginEnd` -> ...). If ANY non-crash end path can leave the marker
   set, a GOOD scene gets dropped on the next walk -- flag it. Confirm `ClearLoading` lives at `TickEnding`'s
   resolution (so it fires for both the clean-menu and the timeout branch) and not only on the success path.

2. **Marker set BEFORE the crash window.** `MarkLoading` is in `StartCurrentItem` just before `StartNewGame`,
   and the native crash was during `MissionInitialize` (after `StartNewGame`, before the rendering callback).
   Confirm the marker is written before the scene load begins so a crash during load is captured. Confirm
   `MarkLoading` is gated to `PrecompileItemKind.ScenePass` (the character battle is not skippable).

3. **Character battle / plan integrity.** If `battle_terrain_029` (the character-battle scene id) ever
   landed in the skip list, would it break the plan? Confirm `Begin()` filters only the SCENE LIST passed
   to `ShaderPrecompilePlanner.BuildPlan`, and the planner ALWAYS prepends the character battle regardless
   -- so an empty/all-skipped scene list still yields a valid 1-item plan, and the character battle is
   never marked (so its id can't enter the list via this path).

4. **File I/O safety.** Confirm every guard method (`MarkLoading`, `ClearLoading`, `ConsumeAndGetSkipSet`,
   and the privates) swallows all exceptions (try/catch) so no disk error (read-only Logs, lock, missing
   dir) can throw into the runner and break the walk. Mirror-check against `BattleLoadStallMarker`.

5. **Thread-safety.** The guard is touched only from the main thread (runner `Begin`/`StartCurrentItem`/
   `TickEnding`/`Finish`, all driven by `SubModule.OnApplicationTick`). Unlike `BattleLoadStallWatchdog`
   (a background `Timer`), the guard has NO background reader, so no `volatile`/lock is needed. Confirm
   nothing reads the guard's files off-thread.

6. **De-dup + persistence.** Confirm `AppendCrashedScene` de-dupes (re-reads the list, skips if present)
   so a scene that crashes on multiple walks appears once. Confirm the skip set persists across walks
   (read from the file each `Begin`), and the user can reset by deleting the file (the path is logged).

7. **Reset/idempotency.** Confirm `ConsumeAndGetSkipSet` consumes (deletes) the inflight marker after
   reading it, so the same crash isn't re-counted on a later walk; and that calling it with no inflight +
   no crashed file returns an empty set (clean first run).

## Also look for what we might have missed

- Any path where the marker survives a CLEAN walk (e.g. the last item's `TickEnding` clears, then `Finish`
  -- redundant but confirm no double-free / no-op issue).
- Case-sensitivity: scene ids are matched case-insensitively in the skip set but the precompile_scenes.txt
  ids are lowercase -- confirm no mismatch that would fail to skip.
- The `StringComparer.OrdinalIgnoreCase` HashSet in `Begin` vs the guard's own `OrdinalIgnoreCase` set --
  consistent?

## Output

A findings section (0 CRITICAL / N HIGH / N MED / N LOW), a per-suspect CONFIRM/DISPUTE table with
evidence, and a verdict (SHIP / NEEDS-FIX). The TAOM verdict is that this is a clean, well-tested guard
mirroring a proven sibling; try hard to find a false-skip or a throw-into-the-runner path.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
