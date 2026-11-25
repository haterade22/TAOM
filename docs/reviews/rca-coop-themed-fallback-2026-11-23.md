# RCA: Themed movie fallback in a BannerlordCoop session (#1545, 2026-11-23)

## Top-line

In a BannerlordCoop session, Coop's `RobustnessPatches` finalizer on the engine's private `GauntletMovie.LoadMovie`
swallows a themed prefab's build exception, so `GauntletLayerLoadMoviePatch`'s finalizer never ran and a broken themed
screen never fell back to vanilla. The first fix detected the failure from the returned movie's state (`RootWidget ==
null`). A tier 1 review (Codex at xhigh, lenses 2 and 5) found that this missed a binding failure, left a movie whose
`Release` throws in the layer, and would have thrown out of screens with no vanilla movie. The redesign marks the build
itself; the re-review (Codex at high, lenses 2 and 5) passed it.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | P1 (Codex) | A prefab that fails to parse leaves `_moviePrefab` null, so `Release` throws at `GauntletMovie.cs:113` before `GauntletLayer.ReleaseMovie` removes the identifier; the layer's teardown then releases it again and throws (lens 2 F1, lens 5 flow 3; Codex reproduced it with the installed assemblies). | Partial engine state | The draft assumed `ReleaseMovie` always completes on a movie the layer returned. | `Discard` removes the identifier from `_movieIdentifiers` when `Release` throws; reflection-site gate row and catalogue row. |
| 2 | P2 (Codex) | A throw while binding (after `RootView` is set) left a non-null root, so the draft took the half-bound movie as a success (lens 5 flow 4). | Failure inferred from state | The draft read the effect of one failure point and named it the failure. | `GauntletMovieBuildWatchPatch` marks each movie while `LoadMovie` builds it; Harmony skips its postfix whenever the build throws, finalizer or not (`HarmonyPatchSemanticsTests`). |
| 3 | MEDIUM | A swap with no vanilla movie (`CareerScreen`, `MainAgentHUD`) would have thrown out of the screen's `OnInitialize` under Coop, where it used to open half built (lens 5 flow 5). | Fallback assumed | The finalizer's rethrow path was written for the faction screen, whose launcher catches. | The postfix acts only when `HasVanillaFallback`. |
| 4 | LOW | The themed Camp icon was not gated on the themed map bar, so it could stand beside vanilla's bar (lens 5 flow 6). | Coupled themes | Older than #1545; the army panel got its gate in #1532. | `CanTheme("TAOMMapBar")` on the Camp case; `BeginLoad_FieldCampOverlayAfterTheThemedMapBarFailedToBuild_StaysVanilla`. |
| 5 | LOW | Docs overstated the first fix; a comment named the wrong caller of `LoadMovie`; one shared report flag would have been spent by an expected `Release` failure (lenses 2 and 5, both passes). | Doc accuracy | | Docs reworded; `_reportedDiscard`. |

- FOLLOW-UP: under Coop, a swap with no vanilla movie still opens half built and is retried on every open, because `LoadFailed` never runs for it (lens 5 re-review). no issue: this is the behaviour before #1545, and changing it needs a decision on what those screens should show instead.

## Root-cause pattern

The draft inferred "the build failed" from the state one failure point leaves behind. Each other failure point leaves
different state, so the inference was wrong for most of them. Observing the failure where it happens (a postfix that
does not run) covers every failure point at once.

## Feedback memories to codify

None beyond the lesson in `docs/reviews/lessons/adapters-taleworlds-api.md`.
