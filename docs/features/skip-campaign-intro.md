# Skip Campaign Intro

## Overview

Skips the vanilla SandBox campaign intro video when starting a new game, dropping straight into
character creation. Previously, clicking "Enter The Age Of Men" (TAOM's renamed Sandbox new-game
button) finished loading and then played the ~3-minute `campaign_intro.ivf` cutscene, which the player
had to press Escape to skip on every new game.

## Why This Exists

- **Vanilla behavior:** `SandBoxGameManager.OnLoadFinished()` builds a `VideoPlaybackState` for
  `Modules/SandBox/Videos/CampaignIntro/campaign_intro.ivf` and pushes it onto the state stack on every
  new game (unless the game is in development mode). When the video finishes — or the player presses
  Escape — its finished-delegate launches character creation.
- **TAOM requirement:** The vanilla intro is a native-Bannerlord/Calradia cinematic with no relevance
  to the Middle-earth setting, and players start many new games during testing and play. We want a new
  game to go straight to character creation.
- **Without this feature:** Every new game forces the player to watch (or Escape past) the Calradia
  intro before reaching character creation.

## Architecture

### Design Challenge

The intro is 100% vanilla — TAOM does not trigger it. The decision and playback live inside
`SandBoxGameManager.OnLoadFinished()`, a sealed engine method in `SandBox.dll`. There is no config hook
or event to suppress the video.

### Solution Approach

A single thin Harmony **Prefix** on `SandBoxGameManager.OnLoadFinished` that mirrors the engine's own
no-video path. The engine already has a built-in intro skip: when `Game.Current.IsDevelopmentMode` is
true, `OnLoadFinished` calls the private `LaunchSandboxCharacterCreation()` directly instead of building
a `VideoPlaybackState`. The Prefix forces that same path for a normal new game:

- `LoadingSavedGame == true` → return true (run vanilla — save-loads take a different branch and never
  play the intro).
- `LoadingSavedGame == false` → call the private `LaunchSandboxCharacterCreation()` (== the engine's
  dev-mode branch) + set `MBGameManager.IsLoaded = true` (== the method's trailing line), then return
  false so the original never builds the video state.
- Any missing reflection binding or thrown exception → return true (fail safe to the vanilla video;
  the skip is never allowed to break new-game start).

The patch is **hardcoded always-skip** — no MCM toggle.

```
SandBoxGameManager.OnLoadFinished   (vanilla engine, new-game branch)
        │  Harmony Prefix
        ▼
Patch58_SkipCampaignIntro.Prefix
   ├─ save-load?  → return true (vanilla)
   ├─ binding missing / throw → return true (vanilla intro)
   └─ new game → LaunchSandboxCharacterCreation() + IsLoaded=true → return false (no video)
```

### Apply timing

The category is applied in `SubModule.OnSubModuleLoad` (a process-static one-shot), **not** the late
`OnGameInitializationFinished` batch. `OnLoadFinished` fires during the new-game load sequence — after
campaign init but before character creation — so the patch must already be attached before any new game
can start. `SandBox.dll` is a depended module loaded before TAOM, so `SandBoxGameManager` is patchable
at `OnSubModuleLoad` (the same place TAOM applies its other cross-module categories).

## Configuration

None. Hardcoded always-skip; no JSON/XML config and no MCM toggle.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SkipCampaignIntro/Hooks/Patch58_SkipCampaignIntro.cs` | The `[HarmonyPatchCategory("Patch58_SkipCampaignIntro")]` Prefix + cached reflection bindings + `Initialize(IModLogger)`. |
| `Main/SubModule.cs` | Registration: `Patch58_SkipCampaignIntro.Initialize(...)` + `_harmony.PatchCategory("Patch58_SkipCampaignIntro")` in `OnSubModuleLoad`. |

## Dependencies

- `SandBox.SandBoxGameManager` — the patched type (vanilla SandBox module). The private
  `LaunchSandboxCharacterCreation()` binding is resolved via `AccessTools.Method`; both reflection
  bindings are `AccessTools`-cached in static fields.
- `TaleWorlds.MountAndBlade.MBGameManager.IsLoaded` (protected setter) — set via `AccessTools.PropertySetter`.
- `IModLogger` (Core/Logging) — fallback warning logging only.

## Tests

- `TAOM.Tests/Features/SkipCampaignIntro/Patch58SkipCampaignIntroTests.cs` — 2 drift-guard tests that
  resolve the two internal reflection bindings (`LaunchSandboxCharacterCreation`, `IsLoaded` setter)
  against the installed engine. The patch's `[HarmonyPatch]` target (`OnLoadFinished`) is additionally
  covered by `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs`. Harmony patch *invocation* is not unit
  tested (requires a live game) per project convention.

## How to revert to the vanilla intro

Remove (or comment out) the `Features.SkipCampaignIntro.Hooks.Patch58_SkipCampaignIntro` block in
`SubModule.OnSubModuleLoad` and rebuild. There is no runtime toggle.

## Changelog

- 2026-06-30 — Initial implementation. Prefix on `SandBoxGameManager.OnLoadFinished` skips the vanilla
  `campaign_intro.ivf` on new game (mirrors the engine's IsDevelopmentMode no-video bypass); save-loads
  untouched; fail-safe to vanilla on binding failure.

## GitHub Issue

- **Issue:** [#303](https://github.com/haterade22/TAOM/issues/303) — Skip the campaign intro video on new game
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
