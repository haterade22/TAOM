# Codex re-review: themed movie fallback in a BannerlordCoop session (#1545), after fixes

You are a read-only adversarial reviewer. Do not edit files. Repo: E:/repos/taom-coop-fallback, branch
fix/coop-themed-movie-fallback. Review the whole uncommitted change: `git -C E:/repos/taom-coop-fallback diff`
plus the untracked files (`git -C E:/repos/taom-coop-fallback status --short`).

Your first pass (docs/reviews/raw/codex-prereview-coop-themed-fallback-2026-11-23.md) found P1 (a half-built
movie whose Release throws stays in the layer and throws again at teardown) and P2 (a binding failure after
RootView exists was taken as success). Two Claude lenses also found: swaps with no vanilla movie
(CareerScreen, MainAgentHUD) would now throw out of the screen's OnInitialize; the themed Camp icon was not
gated on the themed map bar.

## The redesign

- New `Main/Features/FactionUI/Hooks/GauntletMovieBuildWatchPatch.cs`: prefix and postfix on the private
  `GauntletMovie.LoadMovie()`. The prefix marks the movie in a ConditionalWeakTable; the postfix clears it.
  Harmony skips the postfix when the original throws, even when Coop's finalizer swallows the exception
  (pinned by `TAOM.Tests/Features/FactionUI/HarmonyPatchSemanticsTests.cs`).
- `GauntletLayerLoadMoviePatch.Postfix`: only for a swap with `HasVanillaFallback`, when the mark is still
  set: `Discard` (ReleaseMovie; if that throws, remove the identifier from the private `_movieIdentifiers`
  MBList by reflection) and throw, so the existing finalizer records LoadFailed and loads vanilla.
- `FrontEndMovieService`: `TaomFieldCampOverlay` now needs `CanTheme("TAOMMapBar")`.

Engine source (installed v1.5.7): C:/Users/mikew/.taom-src/v1.5.7-202611101605/ (GauntletMovie, GauntletLayer,
GauntletView, MBList). Coop: E:/repos/BannerlordCoop/source/GameInterface/Services/UI/Patches/RobustnessPatches.cs.

## Questions

1. Are P1 and P2 closed? Trace a parse failure, an instantiate failure and a binding failure under Coop.
2. Can the mark be left set for a movie that did build (a nested movie load inside binding, a resource refresh
   or hot reload re-entering LoadMovie, generated prefabs, threads)? Would that drop a good themed screen?
3. After `Discard`, is the layer consistent: identifier gone, root node detached (by `Release` or by the
   finalizer's `RemoveWhatTheFailedBuildAttached`), no second Release at teardown? Is a skipped
   `RefreshContextName` on the reflection path harmful?
4. Is behaviour unchanged in solo, on the dedicated server and under BannerlordTogether?
5. Anything else wrong in the diff, docs or tests.

## Output

Findings ranked P1, P2, P3, each with file:line, evidence read, and a failure scenario. Say PASS if none. Mark
claims you could not read as UNVERIFIED.
