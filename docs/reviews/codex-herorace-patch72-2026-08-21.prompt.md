# Adversarial review: HeroRace Patch72 tableau framing + console tuner + MCM eye height

You are an independent adversarial reviewer on TAOM, a Mount & Blade II: Bannerlord **1.4.8** total
conversion at `e:\repos\TAOM`. Assume this changeset has a shipping bug and find it. A Claude
seven-dimension review already ran and its findings are listed at the bottom as ALREADY FIXED. Do not
re-report those. Your value is what it missed.

## Ground rules

- **Verify engine claims against the INSTALLED DLLs**, not the decompile dump at
  `E:\Decompiled_Bannerlord\` (it can lag). Use `pwsh tools/taom-src.ps1 path <FullTypeName>` which
  returns a path to decompiled source of the installed 1.4.8 assemblies.
- Build: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` (never `./build.ps1`).
- Tests: `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId=`
  Current state: **6878 passing, 0 failing, 2 skipped**. Main builds clean.
- Read-only. Report, do not edit.
- **A finding must cite code you actually read.** Quote it. Unverified suspicion is worth reporting
  only if you label it as such.

## IMPORTANT: another session is editing this tree concurrently

Files under `Main/Features/ArmyTargeting/`, `Main/Adapters/IMapReachAdapter.cs`,
`Main/Adapters/MapReachAdapter.cs`, `Main/_Module/ModuleData/configs/army_targeting.json` and
`TAOM.Tests/Features/ArmyTargeting/` belong to a DIFFERENT piece of work. **Ignore them entirely.**
That session also added 3 MCM settings to the shared `Main/Features/TaomSettings.cs`
(`ArmyDefenderPriority`, `ArmyReachRadiusInTownGaps`, `EnableWarTheaters`) and removed
`LongRangePriorityBoostScale`. Only `EnableDwarfEyeHeight` and `DwarfEyeHeightAdjuster` in that file
are in scope for you.

## What this changeset does

The `HeroRace` feature had a latent bug: `CharacterTableauService` (221 lines, ~30 private-field
reflection bindings, a full reimplementation of `CharacterTableau.RefreshCharacterTableau` plus
`UpdateMount`) was registered in `HeroRaceIoC` and **invoked by nothing**. The per-race 3D framing
offsets in `ModuleData/configs/CharacterAvatarPatch.json` were therefore parsed and never applied;
only the 2D portrait path (`Patch4_CharacterSpawner` → `CharacterSpawnerService`) was live.

1. **Deleted** `CharacterTableauService`, `ICharacterTableauService`,
   `RacePositionConfigurationService`, `IRacePositionConfigurationService`, `RaceTableauPositioning`
   and its test file. All dead.
2. **Added `Patch72_TableauRacePosition`**
   (`Main/Features/HeroRace/Hooks/CharacterTableau_RefreshCharacterTableau_PositionPatch.cs`), a
   Harmony **postfix** on `CharacterTableau.RefreshCharacterTableau` binding **eight** private fields
   by `____name` injection. It sets the character and mount entity **origins absolutely** from the
   tableau's own spawn frames and writes **only** `origin`, never `rotation`.
3. **Added `RacePositionStore`** as single owner of both config files.
4. **Added five `taom.*` console commands** plus `RacePositionTuningParser`.
5. **Dwarf eye height is now an MCM slider** (was a compile-time `-0.2f`), mutating the SHARED base
   `Monster`, capturing the pre-mutation pair on first sight and writing it back when toggled off.
   `EyeHeightAdjustmentHook` takes `IReflectionService` by constructor injection.
6. **Added `RacePositionConfigValidator`** (NaN / range / duplicate row validation at load).

## Files in scope

```
Main/Features/HeroRace/Hooks/CharacterTableau_RefreshCharacterTableau_PositionPatch.cs
Main/Features/HeroRace/Hooks/CharacterTableau_RefreshCharacterTableau_Patch.cs
Main/Features/HeroRace/TableauPositionService.cs
Main/Features/HeroRace/ITableauPositionService.cs
Main/Features/HeroRace/RacePositionStore.cs
Main/Features/HeroRace/IRacePositionStore.cs
Main/Features/HeroRace/Configuration/RacePositionConfig.cs
Main/Features/HeroRace/Configuration/RacePositionConfigValidator.cs
Main/Features/HeroRace/EyeHeightAdjustment.cs
Main/Features/HeroRace/EyeHeightAdjustmentHook.cs
Main/Features/HeroRace/HeroRaceSettingsProvider.cs
Main/Features/HeroRace/IHeroRaceSettingsProvider.cs
Main/Features/HeroRace/LiveTableauRef.cs
Main/Features/HeroRace/CharacterSpawnerService.cs
Main/Features/HeroRace/HeroRaceIoC.cs
Main/Features/HeroRace/Cheats/RacePositionTuningCheats.cs
Main/Features/HeroRace/Cheats/RacePositionTuningParser.cs
Main/SubModule.cs                       (only the Patch72 category line)
Main/Features/TaomSettings.cs           (only the two Dwarf Eye Height properties)
Main/_Module/ModuleData/configs/CharacterAvatarPatch.json
TAOM.Tests/Features/HeroRace/**
```

## Known suspects: go here first

1. **The four-way origin selection in Patch72.** Character origin is
   `swapped ? _characterMountPositionFrame.origin : _initialSpawnFrame.origin`; mount origin is
   `swapped ? _mountCharacterPositionFrame.origin : _mountSpawnPoint.origin`. Verify each against
   decompiled vanilla `CharacterTableau.RefreshCharacterTableau` and `UpdateMount` on 1.4.8. A single
   swapped pair is a real visual bug that only appears on a mounted hero after pressing swap-places.

2. **Idempotence.** The patch re-applies on every refresh. It claims safety because it writes an
   ABSOLUTE origin derived from vanilla's own spawn frames. Attack that: is there ANY path where the
   spawn frames themselves are mutated, where `_agentVisuals` is not re-`Refresh`ed before the
   postfix runs, or where `_mountVisuals` survives a refresh without being recreated? Check
   `AgentVisuals.Refresh` → `_data.AgentVisuals.SetFrame(_data.FrameData)` and `UpdateMount`'s
   `else if (_mountVisuals != null)` branch.

3. **The buffer swap.** Vanilla swaps `_agentVisuals` and `_oldAgentVisuals` inside the method and
   calls `SetVisible(false)` on the newly-swapped-in one. Our postfix offsets `_agentVisuals`
   (post-swap). Is that the buffer that becomes VISIBLE, or the hidden one? Trace
   `_agentVisualLoadingCounter` and the `OnTick` visibility handoff. If we offset the wrong buffer,
   the offset applies one refresh late or flickers.

4. **Interaction with the existing prefix.** `Patch2_RefreshTableau` is a PREFIX on the same method
   and calls `____oldAgentVisuals.Refresh(...)` itself before vanilla runs. Does that invalidate any
   assumption the postfix makes about which buffer holds what?

5. **Native null.** `AgentVisuals.GetEntity()` returns a `GameEntity`, which derives from
   `NativeObject` and overloads `==`. The patch writes `if (gameEntity == (GameEntity)null) return;`.
   Confirm that actually detects a destroyed native entity on 1.4.8, and that `visuals?.GetEntity()`
   on a live-but-torn-down `AgentVisuals` cannot throw before that check.

6. **Shared-Monster mutation.** `EyeHeightAdjustmentHook` mutates the object returned by
   `FaceGen.GetBaseMonsterFromRace(dwarf)`. Confirm it is genuinely a shared singleton. Then ask what
   else reads `Monster.StandingEyeHeight` / `CrouchEyeHeight` at runtime (agent aim origin? camera?
   AI vision? ranged targeting?) and whether moving it for every dwarf agent has combat consequences
   beyond the player camera. The MCM hint claims it also moves the aim origin for dwarf troops;
   verify that claim is true, and whether it is desirable.

7. **Re-entrancy.** `EyeHeightAdjustmentHook.OnGetBaseMonsterFromRace` is a postfix on
   `FaceGen.GetBaseMonsterFromRace`, and inside it calls
   `_faceGenAdapter.GetBaseMonsterFromRace(0)`, re-entering the patched method. The `race <= 0` guard
   is supposed to terminate that. Confirm no path recurses, including when FaceGen is not ready.

8. **`RacePositionConfig.WriteConfig` uses `File.Replace`.** Confirm behaviour when the target is
   read-only, on a different volume from the temp file, or when the `.prev` already exists. This
   writes into the player's game install.

9. **Startup ordering.** `HeroRaceIoC` eagerly `container.Resolve<ITableauPositionService>()` at
   registration time, which constructs `RacePositionStore`, which does synchronous file I/O during
   `IoC.Configure`. Confirm `IPathService` and `IModLogger` are registered before
   `RegisterHeroRaceFeature` runs (`Main/IoC.cs`), and that `IPathService.ConfigPath` resolves
   correctly that early in module load.

10. **Console command shape.** A `[CommandLineArgumentFunction]` with the wrong shape throws inside
    the engine's unguarded discovery loop, past a native boundary: a startup hazard, not a broken
    console. Verify all five new commands, and that no static initialiser on
    `RacePositionTuningCheats` or `RacePositionTuningParser` can throw during type load.

## Already fixed; do NOT re-report

Rows follow the entity not the place (the swap bug); console race-name validation; `mount_` rejected
on the image surface; the fourth dead config loader in `Patch1_FirstTimeInit`; finiteness gate moved
into the store so both surfaces inherit it; atomic save with `.prev`; `IReflectionService` injected
into the eye-height hook; MCM hint corrected to say the write-back is deferred; `Resolve` out-param
contract unified to 0 on every failure; wiring test; shipped-config parse test; backing-field binding
test; MCM naming/format/const consistency; `WeakReference` reuse; `TryGetValue` double lookup.

## Output

For each finding: **severity (P1/P2/P3)**, file:line, what is wrong, the exact code or decompiled
output proving it, a concrete failure scenario (inputs → wrong behaviour), and the minimal fix.
State explicitly if you find nothing in a suspect area; a cleared suspect is useful.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
