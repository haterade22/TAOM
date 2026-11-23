# Codex pre-review: themed movie fallback in a BannerlordCoop session (#1545)

You are a read-only adversarial reviewer. Do not edit files. Repo: E:/repos/taom-coop-fallback, branch
fix/coop-themed-movie-fallback. Review the uncommitted diff: `git -C E:/repos/taom-coop-fallback diff` plus the
new file TAOM.Tests/Features/FactionUI/HarmonyFinalizerScopeTests.cs.

## The change

`Main/Features/FactionUI/Hooks/GauntletLayerLoadMoviePatch.cs` swaps vanilla Gauntlet movies for themed ones
(prefix), and its finalizer falls back to the vanilla movie when the themed build throws. BannerlordCoop
(E:/repos/BannerlordCoop/source/GameInterface/Services/UI/Patches/RobustnessPatches.cs) puts a finalizer on the
engine's private `GauntletMovie.LoadMovie` that logs and swallows every exception, so in a Coop session the
layer's `LoadMovie` returns normally with a movie whose `RootWidget` is null. The new postfix code detects
`__result.Movie is { RootWidget: null }` for a swapped load, calls `GauntletLayer.ReleaseMovie` on it and throws,
so the existing finalizer records the failure and loads vanilla. The test pins that Harmony's finalizer catches a
postfix's exception.

## Engine source (installed v1.5.7)

C:/Users/mikew/.taom-src/v1.5.7-202611101605/TaleWorlds.GauntletUI.Data.GauntletMovie.cs and
TaleWorlds.Engine.GauntletUI.GauntletLayer.cs. For other types: `pwsh tools/taom-src.ps1 path <Type>`.

## Questions

1. Can `RootWidget` be null after a load that did NOT fail (any GauntletMovie or generated-prefab path, a missing
   prefab, a resource refresh)? Would the new check then wrongly drop a good themed screen?
2. Where in `GauntletMovie.LoadMovie` can the build throw? If it throws after `RootView` is set (in
   `RefreshBindingWithChildren` or `Context.OnMovieLoaded`), the check misses it: how bad is that state, and is
   there a cheap signal for it?
3. After `ReleaseMovie` on the half-built movie, is everything the constructor attached gone (root node, prefab
   and brush change subscriptions, `Context.OnMovieLoaded/Released` counts)? Does `Release()` throw on a movie
   whose `RootView` is null? Does the finalizer's `RemoveWhatTheFailedBuildAttached` then remove anything it
   should not?
4. The finalizer replaces `__result` with the vanilla movie, or rethrows for TAOM's own faction screen. Trace both
   from the postfix's throw: does the caller always get a usable identifier, and does `_loadingVanillaFallback`
   stop recursion?
5. Without Coop (solo, dedicated server, BannerlordTogether) is behaviour unchanged?
6. Anything else in the diff that is wrong, including the docs and the test.

## Output

Findings ranked P1 (ships a defect), P2, P3, each with file:line, the evidence you read, and a concrete failure
scenario. Say PASS if you find none. Mark any engine claim you could not read as UNVERIFIED.
