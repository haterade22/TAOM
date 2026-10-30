# Codex review: 1.5.x port of the PR #1064 campaign fixes (2026-10-30)

You are the reviewer (read `.ai/roles/reviewer.md` and `.ai/review-reference.md`). This is a
REVIEW-ONLY assignment: do not edit, create, stage or delete any file, and run no build that
deploys (`./build.ps1` is forbidden). Other sessions have uncommitted work in this tree; it is out of
scope. Report findings only.

## Scope (uncommitted working tree on `bannerlord-1.5.x`, engine Bannerlord v1.5.6)

Read the hunks with `git diff -- <file>`; the test file is new and untracked, read it whole.

- `Main/Features/SupplyLines/SupplyCaravanService.cs`: `ComputeNavPathPoints` only.
- `Client/Features/SupplyLines/SupplyRouteVisualService.cs`: `SyncArrows` only.
- `Main/Features/AlignmentDesertion/Hooks/AlignmentDesertionBehavior.cs`: two `MapEvent` guards.
- `TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs` (new).
- `TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionBehaviorTests.cs`: the last test method.
- `docs/features/supply-lines.md` (one Traps bullet), `docs/features/alignment-desertion.md`
  ("Battle rosters" section, one Tests bullet).

## Intent

An outside contributor (PR #1064, written for Bannerlord 1.4.8) reported two native crashes and a
desertion problem. This change ports the fixes to 1.5.x:

1. `ComputeNavPathPoints` used to call `IMapScene.GetPathBetweenAIFaces` with null exclusions and
   region costs 100/100. It now passes
   `PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.NavigationType.Default)`
   and both `MapDistanceModel` region-switch costs, as vanilla `MobileParty.ComputePath` does, and it
   skips the query when either endpoint `CampaignVec2.Face.IsValid()` is false (native code indexes
   the face table without a bounds check). The result feeds the supply caravan's MOVEMENT path
   (`SupplyCaravanService.cs` near line 331, `TickPositions`) and the route arrows
   (`SupplyRouteVisualService.SampleRoute`).
2. `SyncArrows` used to call `Campaign.Current.MapSceneWrapper.GetTerrainHeightAndNormal` with no
   guard. It now reads `Scene.HasTerrainHeightmap` on its `scene` parameter
   (`MapScreen.Instance.MapScene`, the same native scene as the wrapper's) and otherwise uses
   `IMapScene.GetHeightAtPoint(new CampaignVec2(position, isOnLand: true), ref height)`.
3. `AlignmentDesertionBehavior` returns from both daily handlers when the party's `MapEvent` is set,
   matching vanilla `DesertionCampaignBehavior.DailyTickParty`.

## Already verified (do not redo; challenge it only with new evidence)

- All signatures bind on the installed v1.5.6 DLLs (`pwsh tools/taom-src.ps1 path <Full.Type.Name>`;
  decompiles cached in `C:\Users\mikew\.taom-src\v1.5.6\`).
- Native (v1.5.6 `TaleWorlds.Native.dll`): `has_terrain_heightmap` is `*(scene+0x2bf0) != 0`;
  `get_terrain_height_and_normal` dereferences that pointer unchecked; both path-query entry points
  skip the exclusion pointer when the count is below 1 and read the face table unchecked.
- The vanilla `TaomPartyNavigationModel` replacement is parked and not registered; both models are
  vanilla (`DefaultMapDistanceModel` costs are 0/0). Naval DLC paths are not findings.
- Tests: new tests failed before the fix and pass after; deliberate mutants (null exclusions, an
  unguarded terrain query) fail them.

## Known suspects (look hardest here)

- Does excluding mountain, lake and river faces strand or straight-line a supply caravan where the
  old soft-cost search found a route (an origin and a player on one mesh island joined only through
  excluded faces, a source settlement whose gate sits next to excluded faces)? What does the player
  see then?
- Can `CampaignVec2.Face` be valid while `IsValid()` (terrain check) is false for a real caravan or
  player position, and does the start-face seeding in native search handle it?
- Desertion: any party state where `MapEvent` stays set for days (a stuck battle, a siege, a raid,
  an army waiting) so that this guard suppresses desertion vanilla would also suppress, or the reverse?
- Do the IL fingerprint tests pin the behaviour without failing on a legitimate rewrite?

## Output

For each finding: severity (CRITICAL, HIGH, MEDIUM, LOW), `file:line`, the defect, its impact, and
the proving code or engine source. Mark anything you could not prove UNVERIFIED. End with a one-line
verdict.
