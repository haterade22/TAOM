# Codex Adversarial Review: MixedFormations

## Feature Description

MixedFormations re-orders melee + ranged units within a single formation while it holds position. Active during `MovementOrder.MovementStateEnum.Hold` only. The feature has 1 Harmony Prefix (Patch30 on `Formation.GetOrderPositionOfUnit`) and no GameModel overrides. Layout choices: Vanilla (default), InfantryFront-RangedBack, RangedFront-InfantryBack, RangedWings-InfantryCenter, Checkerboard.

This is the second feature ported from the external-developer drop at `Downloads/Features_fixed/`. The first port (SiegeDismount, completed) had a Codex pass that found 2 HIGH + 1 MEDIUM bug. Match that bar.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- Main/Features/MixedFormations/FormationLayoutService.cs -- service orchestrator with per-formation cache + auto-apply + cycle
- Main/Features/MixedFormations/LayoutPositioner.cs -- pure-function slot-assignment math (4 layouts)
- Main/Features/MixedFormations/IFormationLayoutService.cs -- service interface
- Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs -- wraps TaomSettings.Instance for testability
- Main/Features/MixedFormations/Models/{FormationLayoutType, FormationUnit, SlotAssignment}.cs
- Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs -- per-frame tick + hotkey poll + team-adapter construction
- Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs -- Harmony Prefix on Formation.GetOrderPositionOfUnit
- Main/Features/MixedFormations/MixedFormationsIoC.cs
- Main/Adapters/IFormationAdapter.cs -- NEW; load-bearing for SmartCavalryAI (feature 3) + CompanionTactics (feature 7)
- Main/Adapters/FormationAdapter.cs -- wraps Formation; service never sees Formation directly (ADR-007)
- Main/Features/TaomSettings.cs -- the appended `Battle Tactics/Mixed Formations` group at the bottom (4 settings)
- Main/IoC.cs -- look for MixedFormationsIoC.RegisterMixedFormationsFeature
- Main/SubModule.cs -- look for `_harmony.PatchCategory("Patch30_MixedFormations")` in OnSubModuleLoad and `mission.AddMissionBehavior(new MixedFormationsMissionBehavior())` in OnMissionBehaviorInitialize
- TAOM.Tests/Features/MixedFormations/{LayoutPositionerTests, FormationLayoutServiceTests}.cs -- 36 unit tests
- docs/features/mixed-formations.md -- feature doc with the two known-limitations from /deep-review Agent 5

ORIGINAL DECOMPILED SOURCE (for behavior parity check):
- Downloads/Features_fixed/_decompiled/MixedFormations/MixedFormations.decompiled.cs

## KNOWN SUSPECTS

Several of these were already addressed during /deep-review (5-agent pass on this feature). Confirm those fixes are correct OR find new ways the same class of bug could occur. New attack lines (NEW SUSPECT) are fresh hypotheses Codex should independently verify.

1. ALREADY-FIXED -- IoC.RESOLVE IN HOT PATH. The Patch30 Prefix used to resolve `IFormationLayoutService` per-call (up to 40,000x per frame in 200-unit formations). Fix added a `private static IFormationLayoutService? _service` field with `_service ??= IoC.Resolve<...>()` lazy init. CONFIRM the fix at `Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs` lines 13-19. Specifically verify:
   - The static field is in the patch class itself (not on a separate static helper that might not initialize correctly).
   - There is no thread-safety issue: Harmony patches can fire from multiple threads in some Bannerlord contexts (mission render vs simulation). The `??=` operator is NOT thread-safe (read-modify-write race window). Is this an issue here, or does Bannerlord guarantee single-threaded patch invocation?
   - The cached service is a Singleton (Reuse.Singleton in IoC.cs). It does NOT need to be cleared on mission end since the singleton itself is process-lifetime.

2. ALREADY-FIXED -- DEFAULT THROW IN SWITCH. `LayoutPositioner.BuildInitialAssignment` had no `default:` arm; a future 6th `FormationLayoutType` value would silently produce an empty assignment. Fix added `default: throw new ArgumentOutOfRangeException(...)`. CONFIRM at `LayoutPositioner.cs` lines 67-73. Specifically verify:
   - The throw happens BEFORE the `return assignment;` line (so an unsupported value doesn't return a half-built assignment).
   - The thrown exception propagates out cleanly. The caller is `EnsureAssignment` in `FormationLayoutService.cs:155` which has no try-catch; the exception flows to `ComputeUnitPlanePosition` line 60 which has no try-catch; it flows to the patch's outer try-catch at `Patch30_FormationGetOrderPositionOfUnit.cs:18-44`, which DOES have a `catch { return true; }`. So the failure mode is "silently fall through to vanilla on unsupported layout." That's a compromise -- in DEVELOPMENT a developer-pass throw would have surfaced the missing case; in PRODUCTION the catch swallows it. Is this the right balance, or should the catch in the patch only swallow specific exception types and let `ArgumentOutOfRangeException` bubble?

3. NEW SUSPECT -- HOT-PATH ALLOCATION via `FormationAdapter.Units`. The getter does `_formation.UnitsWithoutLooseDetachedOnes.OfType<Agent>().OrderBy(a => a.Index).Select(...).ToList()` -- per access, this allocates a `List<FormationUnit>` plus enumerator state. /deep-review Agent 3 verified this is currently safe because `ComputeUnitPlanePosition` does NOT call `formation.Units`; only `BuildInitialAssignment` (cache miss path) and `ApplyDefaultsToFormations` (per-second pass) call it. CONFIRM by tracing every callsite of `formation.Units` in the entire MixedFormations codebase. DISPUTE if you find a hot-path callsite the deep-review missed. (Specifically check `IsMixedFormation` at `FormationLayoutService.cs:122-138` -- this iterates `formation.Units` -- where is it called from, and how often?)

4. NEW SUSPECT -- VANILLA `Formation.GetOrderPositionOfUnit` SIDE EFFECTS. The Prefix returns `false` to skip vanilla when our service produces a position. Decompile `Formation.GetOrderPositionOfUnit` from the installed v1.3.15 DLL and trace ALL state mutations the vanilla method performs in the Hold-state branch. /deep-review Agent 5 verified that for `MovementStateEnum.Hold` (which is the only state where the patch returns false), the vanilla path is essentially read-only -- the `_lastPosition` mutation only happens for `Follow`-order, not `Hold`. CONFIRM this independently by reading the decompiled vanilla method. Pay special attention to:
   - `Arrangement.GetWorldPositionOfUnitOrDefault(unit)` -- read-only?
   - `_movementOrder.CreateNewOrderWorldPositionMT(...)` -- the MovementOrder struct is captured by value/ref?
   - Any cache invalidation calls (`_cachedOrderedAndAvailableUnitPositionIndices` etc.) that the vanilla path performs and our skip drops.

5. NEW SUSPECT -- INPUT.IsKeyDown SEMANTICS. `MixedFormationsMissionBehavior.HandleCycleHotkey` polls `Input.IsKeyDown(InputKey.L)` every frame. Does this PASSIVE READ the key state, or does it CONSUME the input event (preventing it from reaching other listeners)? If `L` is also bound to a vanilla action (e.g., "look at unit", "loot all", etc. -- whatever the engine binds L to), do we get double-firing OR does our read prevent the vanilla binding? Decompile `Input.IsKeyDown` and verify. If it consumes, we have a hidden conflict; if it doesn't, the user could press `L` and get both effects.

6. NEW SUSPECT -- WIDTH/INTERVAL EDGE CASES. `LayoutPositioner.BuildInitialAssignment:14-16`:
   ```
   var unitInterval = Math.Max(1f, formation.Interval + 1f);
   var filesPerRow = formation.Width > 1f
       ? Math.Max(1, (int)Math.Round(formation.Width / unitInterval))
       : Math.Max(1, (int)Math.Ceiling(Math.Sqrt(Math.Max(1, formation.CountOfUnits))));
   ```
   Test these inputs:
   - Width = 0 -- falls to sqrt branch, OK.
   - Width = exactly 1 -- the `> 1f` guard means it falls to sqrt, OK.
   - Width = NaN -- `> 1f` returns false, falls to sqrt, OK if CountOfUnits is sane.
   - Width = float.PositiveInfinity -- sqrt(CountOfUnits) is finite, division Width/unitInterval would be infinity. The Round(infinity) returns int.MinValue (or throws OverflowException? -- verify). Then Max(1, int.MinValue) = 1. OK?
   - Interval = -1 -- `Math.Max(1f, -1+1)` = `Math.Max(1f, 0)` = 1. OK.
   - Interval = -100 -- `Math.Max(1f, -99)` = 1. OK.
   - Interval = NaN -- `Math.Max(1f, NaN)` = ? In IEEE 754, NaN comparisons return false; `Math.Max` documentation: "If a or b, or both a and b, are equal to NaN, NaN is returned." So unitInterval = NaN. Then Width/NaN = NaN. Round(NaN) = ?. Verify the failure mode chain.
   FLAG any input that produces a slot.row or slot.file that is NaN, infinity, or extreme-value (causing Vec2 math to produce out-of-bounds positions on the map).

7. NEW SUSPECT -- DIRECTION VECTOR NORMALIZATION. `FormationLayoutService.ComputeUnitPlanePosition:65`:
   ```
   var direction = formation.Direction;
   var rotated = direction.TransformToParentUnitF(localOffset);
   ```
   `TransformToParentUnitF` assumes `direction` is a UNIT vector (length 1). If `formation.Direction` is the zero vector (e.g., formation just spawned, facing not yet computed), the rotation produces the zero vector -- localOffset gets multiplied by zero, all units stack at OrderPosition. Decompile vanilla `Formation.Direction` getter to confirm:
   - Is it always normalized?
   - Can it be the zero vector during early-tick or not-yet-positioned formations?
   - Should we add a `if (direction.LengthSquared < epsilon) return null;` guard?

8. NEW SUSPECT -- SINGLETON LIFECYCLE ON SAVE/LOAD. `FormationLayoutService` is `Reuse.Singleton`. Its dicts are in-memory only. If the player saves mid-mission, exits to main menu, reloads the save, what happens?
   - `MixedFormationsMissionBehavior.OnEndMission` fires when the original mission unloads (or does it? -- depends on how Bannerlord handles mid-mission save+reload).
   - The `Formation` references in the cache dicts are now STALE (point to disposed Formation instances from the prior mission).
   - Next mission constructs FRESH `Formation` objects with different references. The stale cache is harmless (never reads since FormationKey lookups don't match). But it leaks memory.
   - VERIFY: trace `OnEndMission` to confirm the dicts ARE cleared, OR document this as a known leak (small -- one entry per formation in the prior mission, ~4 entries -- not catastrophic).

9. NEW SUSPECT -- TaomSettings PROPERTIES INSERTED BY MIXEDFORMATIONS SESSION FOR PARALLEL WIP. `TaomSettings.cs` was extended with `Battle Tactics/Smart Cavalry` group (6 properties) and `Fief Management` group (3 properties) to unblock parallel SmartCavalryAI and FiefManagement sessions. CONFIRM:
   - The added settings exactly match the prompts at `docs/feature-port-prompts/feature-3-smartcavalryai.md` and `docs/feature-port-prompts/feature-4-fiefmanagement.md`.
   - The `GroupOrder` values (22 for Smart Cavalry, 25 for Fief Management) don't collide with other groups.
   - The defaults are reasonable.
   This is NOT MixedFormations functionality -- it's cross-session coordination. Flag any deviation from the prompt specs but rate at LOW severity since these settings aren't consumed by MixedFormations.

## FILES TO REVIEW

### New Service / Adapter / Hook Files

- Main/Features/MixedFormations/Models/FormationLayoutType.cs
- Main/Features/MixedFormations/Models/FormationUnit.cs
- Main/Features/MixedFormations/Models/SlotAssignment.cs
- Main/Features/MixedFormations/IFormationLayoutService.cs
- Main/Features/MixedFormations/FormationLayoutService.cs
- Main/Features/MixedFormations/ILayoutPositioner.cs
- Main/Features/MixedFormations/LayoutPositioner.cs
- Main/Features/MixedFormations/IMixedFormationsSettingsProvider.cs
- Main/Features/MixedFormations/MixedFormationsSettingsProvider.cs
- Main/Features/MixedFormations/MixedFormationsIoC.cs
- Main/Features/MixedFormations/Hooks/MixedFormationsMissionBehavior.cs
- Main/Features/MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs
- Main/Adapters/IFormationAdapter.cs
- Main/Adapters/FormationAdapter.cs
- Main/Adapters/Models/MovementOrderType.cs (added by SmartCavalryAI session for cross-feature use of FormationAdapter)

### Modified Files (review only the MixedFormations-related additions)

- Main/Features/TaomSettings.cs -- the appended `Battle Tactics/Mixed Formations` group, the `Battle Tactics/Smart Cavalry` group, the `Fief Management` group
- Main/IoC.cs -- the line `MixedFormationsIoC.RegisterMixedFormationsFeature(container)` and the `using TAOM.Features.MixedFormations;` import
- Main/SubModule.cs -- the line `_harmony.PatchCategory("Patch30_MixedFormations")` in `OnSubModuleLoad`, the line `mission.AddMissionBehavior(new MixedFormationsMissionBehavior())` in `OnMissionBehaviorInitialize`, and the `using TAOM.Features.MixedFormations.Hooks;` import

### Test Files

- TAOM.Tests/Features/MixedFormations/LayoutPositionerTests.cs (11 tests)
- TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs (25 tests)

### Documentation

- docs/features/mixed-formations.md

### Vanilla Decompilation Targets

This feature has 1 Harmony Prefix and uses several Formation/Vec2/Scene APIs. Verify against the INSTALLED v1.3.15 DLLs at `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`:

- TaleWorlds.MountAndBlade.Formation -- GetOrderPositionOfUnit body (especially the Hold-state branch), Direction property, OrderPosition, OrderPositionIsValid, Width, Interval, GetMovementState, UnitsWithoutLooseDetachedOnes, CurrentPosition, QuerySystem
- TaleWorlds.MountAndBlade.MovementOrder -- MovementStateEnum (verify `Hold` is ordinal 1)
- TaleWorlds.MountAndBlade.OrderType -- enum values (verify `ChargeWithTarget`, NOT `ChargeToTarget`)
- TaleWorlds.MountAndBlade.MovementOrder.MovementOrderEnum -- enum values (verify `ChargeToTarget = 3` exists)
- TaleWorlds.Library.Vec2 -- TransformToParentUnitF (paste body), arithmetic operators
- TaleWorlds.Library.Vec3 -- 4-arg constructor
- TaleWorlds.Engine.Scene -- GetGroundHeightAtPosition signature and side effects
- TaleWorlds.Engine.WorldPosition -- 2-arg constructor (Scene, Vec3)
- TaleWorlds.Engine.BodyFlags -- CommonCollisionExcludeFlags = 0x2071B189u
- TaleWorlds.InputSystem.Input -- IsKeyDown(InputKey) semantics (passive read or input consumer?)

Use: ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/<dll>" -t "Full.Type.Name"

## REQUIRED SECTIONS

### VANILLA CODE

Decompile from the installed v1.3.15 DLLs and paste as code blocks:

- `Formation.GetOrderPositionOfUnit(Agent unit)` full method body -- focus on the Hold-state branch and what state mutations it performs
- `Formation.Direction` getter (especially the case where formation is freshly spawned)
- `Vec2.TransformToParentUnitF(Vec2 a)` full method body
- `Input.IsKeyDown(InputKey)` body -- is it a passive read or does it consume?
- `Scene.GetGroundHeightAtPosition(Vec3, BodyFlags)` -- does it have side effects we should know about?
- `MovementOrder.MovementStateEnum` enum declaration with explicit ordinals

### STATE MACHINE TRACE

For `FormationLayoutService`, walk through every state transition with concrete numbers:

- 4 melee + 6 ranged formation, Width=8, Interval=1, layout=InfantryFrontRangedBack:
  - filesPerRow = 8 / (1+1) = 4
  - melee fills rows 0..0 (4 units / 4 per row), NextMeleeIndex=4
  - ranged starts at row (4+3)/4 = 1, fills rows 1..2 (6 units / 4 per row), NextRangedIndex=6
  - VERIFY: position of agent index 0 should be at slot (0, -2). Position of agent index 5 (first ranged) should be at slot (1, -2). Verify by reading the LayoutPositionerTests.

- Same formation, agent dies, then a new agent (index 99, ranged) joins mid-mission:
  - AssignNextSlot with InfantryFront, isRanged=true: num3=NextRangedIndex++ =6, num4=(NextMeleeIndex+filesPerRow-1)/filesPerRow = (4+3)/4 = 1. row=1+6/4=2, file=6%4 - 2 = 0.
  - Verify the agent gets slot (2, 0). Confirm this matches the test `AssignNextSlot_NewMeleeAfterInitial_AdvancesNextMeleeCounter` and similar.

### PERFORMANCE TRACE

Walk through one frame of `Patch30_FormationGetOrderPositionOfUnit.Prefix` for a 200-unit formation:
- 200 calls to the Prefix (one per unit).
- Each call: `_service ??= IoC.Resolve<>()` -- after first call, this is a single field read. Confirm via decompilation.
- Each call: `new FormationAdapter(__instance)` -- allocation. Lifetime of this adapter? Does anything cache the Formation reference inside the adapter beyond the Prefix's stack frame?
- Each call: `service.ComputeUnitPlanePosition(formation, agentIndex, agentIsRanged)` -- inside this, dict lookups, `EnsureAssignment` (cache hit path), then `assignment.ByAgentIndex.TryGetValue` -- O(1).
- For 200 units: 200 adapter allocations + 200 dict lookups. The adapter allocation is the only meaningful GC pressure.
- VERIFY: would caching the FormationAdapter on the patch class (keyed by Formation reference) reduce allocations? Or is the adapter so cheap it's not worth caching?

### TaomSettings CROSS-REFERENCE

- Verify `EnableMixedFormations` (bool, default true), `MixedFormationsDefaultLayout` (int 0-3, default 0), `MixedFormationsCycleHotkey` (string, default "L"), `MixedFormationsDebug` (bool, default false) are present in TaomSettings.cs with correct attributes (SettingPropertyBool, SettingPropertyInteger, SettingPropertyText, SettingPropertyGroup with GroupOrder=21).
- Verify the dropped settings (`InfantryRowDepth`, `RangedRowDepth`) are NOT in TaomSettings -- the original developer's module had them as dead config; per `feedback_user_facing_promise_must_match_code.md` they were removed on port. Confirm the audit was thorough.
- Verify the SmartCavalryAI and FiefManagement settings added by THIS session for cross-coordination match the prompts at `docs/feature-port-prompts/feature-3-smartcavalryai.md` and `docs/feature-port-prompts/feature-4-fiefmanagement.md`.

### FINDINGS OR OBSERVATIONS

Group by severity: CRITICAL / HIGH / MEDIUM / LOW / INFO.

For each finding, provide: file:line, what's wrong, what to change, why.

## QUALITY GATES

- Did you decompile vanilla types from installed v1.3.15 DLLs (NOT E:\Decompiled_Bannerlord -- that is v1.4)?
- Did you paste code blocks from both TAOM source and vanilla decompiled source?
- Did you trace every state transition in the FormationLayoutService state machine with concrete numbers?
- Did you verify each Known Suspect with explicit CONFIRMED / DISPUTED + evidence?
- Did you run the WIDTH/INTERVAL edge cases (NaN, Infinity, negative) through the math?
- Did you decompile `Input.IsKeyDown` to determine if it consumes input events?
- Section N skips any suspect or says "could not verify" -- engage with each.

## PRIOR REVIEW LESSONS

SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Data flow review caught castle_orthanc_gate / castle_gundabad_wall false-positive (SiegeDismount review #34). Modifier-loss API audit caught wrong AddToCounts overload (SiegeDismount review #34). User-facing-promise audit caught dead InfantryRowDepth + RangedRowDepth MCM settings (MixedFormations port).

FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections. Codex missed sentinel-vs-terminal collision in shader-precompilation polling. Codex did not engage with the Known Suspects format on SiegeDismount review #34 and reported its own findings instead -- ENGAGE WITH KNOWN SUSPECTS THIS TIME.

## OUTPUT TO

docs/reviews/codex-adversarial-mixedformations-2026-05-06.md
