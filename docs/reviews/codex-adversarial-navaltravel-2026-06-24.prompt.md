Adversarial code review of the TAOM "NavalTravel" feature. It unlocks Bannerlord's base-engine naval-travel system (campaign-map water pathing, embark/disembark, native party-as-ship rendering) for everyone WITHOUT the paid Naval DLC, by overriding the base GameModel `DefaultPartyNavigationModel`. The installed game is v1.4.6. Be skeptical: CONFIRM or DISPUTE each item with evidence from the actual source.

This feature has NO kingdom/culture/troop/settlement IDs -- it is pure terrain + navigation logic. The TAOM ID cheatsheet is therefore not relevant here; do not flag culture/kingdom ID issues (there are none).

CONTEXT -- HOW THE ENGINE WORKS (verified against installed v1.4.6):
- The whole naval system ships in the base engine. It is gated OFF by `DefaultPartyNavigationModel.HasNavalNavigationCapability` returning false.
- The official NavalDLC swaps in its own `NavalPartyNavigationModel` (returns true when the party owns a ship) plus Calradia content. TAOM does NOT depend on NavalDLC -- it reimplements that small model with the ship gate replaced by a config/MCM gate.
- The player click-to-sail path is base-engine: a map move handler calls `Helpers.NavigationHelper.CanPlayerNavigateToPosition(point, out navType)` -> `Campaign.Current.Models.PartyNavigationModel.CanPlayerNavigateToPosition(...)` -> `MobileParty.SetMoveGoToPoint(point, navType)`. So overriding the model is sufficient; there is no Harmony patch.

READ FIRST:
- docs/features/naval-travel.md
- Main/_Module/ModuleData/naval_travel/naval_travel_config.json
- Main/Features/NavalTravel/Models/TaomPartyNavigationModel.cs  (the core file -- scrutinize hardest)

VANILLA REFERENCE -- base `DefaultPartyNavigationModel` (TaleWorlds.CampaignSystem.GameComponents, installed v1.4.6), decompiled this session:

public class DefaultPartyNavigationModel : PartyNavigationModel
{
  private int[] _invalidTerrainTypes;
  public override float GetEmbarkDisembarkThresholdDistance() => 0f;
  private static bool IsTerrainTypeValidForDefault(TerrainType t) =>
    t==Plain||t==Desert||t==Snow||t==Forest||t==Steppe||t==Swamp||t==Dune||t==Bridge||t==Fording||t==Beach;
  public DefaultPartyNavigationModel() { /* builds _invalidTerrainTypes = all TerrainType where !IsTerrainTypeValidForDefault */ }
  public override int[] GetInvalidTerrainTypesForNavigationType(NavigationType n) =>
    (n==Default||n==All) ? _invalidTerrainTypes : new int[0];
  public override bool IsTerrainTypeValidForNavigationType(TerrainType t, NavigationType n) =>
    (n==Default||n==All) ? IsTerrainTypeValidForDefault(t) : false;
  public override bool HasNavalNavigationCapability(MobileParty p) => false;
  public override bool CanPlayerNavigateToPosition(CampaignVec2 v, out NavigationType n) {
    n = Default;
    if (!v.Face.IsValid() || !MobileParty.MainParty.Position.IsOnLand || !v.IsOnLand) return false;
    return !GetInvalidTerrainTypesForNavigationType(n).Contains(v.Face.FaceGroupIndex);
  }
}

VANILLA REFERENCE -- the official NavalDLC `NavalPartyNavigationModel` (the model TAOM is porting), decompiled this session. TAOM should match this EXCEPT HasNavalNavigationCapability:

public class NavalPartyNavigationModel : PartyNavigationModel {
  public override float GetEmbarkDisembarkThresholdDistance() => 0.5f;
  private static bool IsTerrainTypeValidForNaval(TerrainType t) =>
    (int)t==8||(int)t==10||(int)t==11||(int)t==18||(int)t==19||(int)t==23||(int)t==24||(int)t==25;
  // builds invalid-types cache for Default(1)/Naval(2)/All(3) from IsTerrainTypeValidForNavigationType
  public override bool IsTerrainTypeValidForNavigationType(TerrainType t, NavigationType n) {
    if ((int)n==2) return IsTerrainTypeValidForNaval(t);
    if ((int)n==3) return IsTerrainTypeValidForNaval(t) ? true : _baseModel.IsTerrainTypeValidForNavigationType(t,n);
    return _baseModel.IsTerrainTypeValidForNavigationType(t,n);
  }
  public override int[] GetInvalidTerrainTypesForNavigationType(NavigationType n) => cache[n] (or new int[0]);
  public override bool HasNavalNavigationCapability(MobileParty p) {
    // true if p.Ships.Count>0, else if attached-to has capability, else if any attached party has ships
  }
  public override bool CanPlayerNavigateToPosition(CampaignVec2 v, out NavigationType n) {
    n = None; var face = v.Face; if (!face.IsValid()) return false;
    if (!MainParty.IsCurrentlyAtSea && NavigationHelper.IsPositionValidForNavigationType(v, Naval)) return false;
    if (MainParty.IsCurrentlyAtSea)
      n = (MainParty.HasNavalNavigationCapability && NavigationHelper.IsPositionValidForNavigationType(v, Naval)) ? Naval : MainParty.NavigationCapability;
    else n = Default;
    var invalid = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(n);
    if (invalid.Contains(v.Face.FaceGroupIndex)) return false;
    if (!v.IsOnLand && MainParty.IsCurrentlyAtSea) return true;
    return Campaign.Current.MapSceneWrapper.GetPathDistanceBetweenAIFaces(MainParty.CurrentNavigationFace, v.Face, MainParty.Position.ToVec2(), v.ToVec2(), 0.3f, Campaign.PathFindingMaxCostLimit, ref num, invalid, MainParty.GetRegionSwitchCostFromLandToSea(), MainParty.GetRegionSwitchCostFromSeaToLand());
  }
}

NOTE: in the installed v1.4.6 `IMapScene.GetPathDistanceBetweenAIFaces`, the 7th arg is `out float distance` (the NavalDLC ilspy render shows `ref num` but the real signature is `out`). TAOM passes `out float _`. Confirm the argument ORDER matches: (startFace, endFace, startPos, endPos, agentRadius=0.3f, distanceLimit=PathFindingMaxCostLimit, out dist, invalidIds, regionSwitchCostLandToSea, regionSwitchCostSeaToLand).

KNOWN SUSPECTS -- confirm or dispute each:

1. PORT FIDELITY of CanPlayerNavigateToPosition. Compare TaomPartyNavigationModel.CanPlayerNavigateToPosition line-by-line to the NavalDLC reference above. Flag ANY divergence in branch logic, the GetPathDistanceBetweenAIFaces argument order, or the land-to-sea vs sea-to-land cost order. A swapped region-switch-cost order would silently mis-cost embark/disembark pathing.

2. ARMY / ATTACHED-PARTY CAPABILITY MISMATCH. TAOM's HasNavalNavigationCapability returns CanPartySail(party.IsMainParty): main party gated by ApplyToPlayer, every other party gated by ApplyToAi. NavalDLC instead grants capability to a party attached to a capable army even if that party has no ship. Scenario: ApplyToAi=false (player-only) and the player leads an army containing AI companion/garrison parties. The main party gains Naval capability; the attached AI parties do NOT (ApplyToAi=false). When the player sails, the engine propagates IsCurrentlyAtSea to attached parties (MobileParty setter copies it to attached parties) but their NavigationCapability lacks Naval. Does this desync the army at sea (attached parties stuck / unable to path / NRE)? Examine MobileParty's at-sea propagation + how attached-party pathing uses NavigationCapability. If this is a real risk, propose the minimal guard (e.g. treat a party whose army leader can sail as capable).

3. GAMEMODEL REPLACEMENT. Confirm `campaignStarter.AddModel(new TaomPartyNavigationModel(...))` in Main/SubModule.cs REPLACES the base PartyNavigationModel (so Campaign.Current.Models.PartyNavigationModel returns our instance) rather than adding a duplicate or being shadowed. Confirm there is no second registration of a PartyNavigationModel anywhere.

4. DISABLED-PATH EQUIVALENCE. When EnableNavalTravel is off, HasNavalNavigationCapability and CanPlayerNavigateToPosition fall through to base, but IsTerrainTypeValidForNavigationType and GetInvalidTerrainTypesForNavigationType are NOT gated (always naval-aware). Confirm this is harmless: when disabled, no party ever has Naval/All capability, so those terrain methods are only ever queried with Default and return the vanilla land set. Flag any code path where a non-naval-capable party queries the Naval/All terrain set while the feature is disabled and gets non-vanilla behavior.

5. TERRAIN SET SEMANTICS. The default navalTerrainTypeIds = [8,10,11,18,19,23,24,25] (Lake/Water/River/CoastalSea/OpenSea/LandRestriction/SeaRestriction/UnderBridge), copied verbatim from NavalDLC. Is including 23 (LandRestriction) and 25 (UnderBridge) as ship-navigable correct/faithful, or could it let parties path through restricted-land or under-bridge faces while at sea in a way the base land model would not? Confirm it matches NavalDLC and note any gameplay oddity.

6. CONSTRUCTOR-SNAPSHOT vs LIVE config. NavalTravelService snapshots navalTerrainTypeIds into a HashSet in its constructor (Singleton). The three booleans (IsEnabled/ApplyToPlayer/ApplyToAi) are read live per-call from TaomSettings.Instance. Confirm the snapshot is correct (terrain ids are JSON-only, process-lifetime cached, never change at runtime) and that the live MCM toggles still take effect immediately without restart. Flag if a player would expect changing the terrain-id JSON mid-session to take effect (it does not -- requires app restart; confirm docs say so).

FILES TO REVIEW:
Core: Main/Features/NavalTravel/Models/TaomPartyNavigationModel.cs
Service: Main/Features/NavalTravel/NavalTravelService.cs, INavalTravelService.cs
Settings: Main/Features/NavalTravel/INavalTravelSettingsProvider.cs, NavalTravelSettingsProvider.cs
Config: Main/Features/NavalTravel/NavalTravelConfig.cs, INavalTravelConfigProvider.cs, NavalTravelConfigProvider.cs, Main/_Module/ModuleData/naval_travel/naval_travel_config.json
IoC/registration: Main/Features/NavalTravel/NavalTravelIoC.cs, Main/IoC.cs (NavalTravel line), Main/SubModule.cs (TaomPartyNavigationModel AddModel line), Main/Features/TaomSettings.cs (World/Naval Travel group)
Tests: TAOM.Tests/Features/NavalTravel/NavalTravelServiceTests.cs, NavalTravelConfigProviderTests.cs

REQUIRED SECTIONS in your output:
A. KNOWN SUSPECTS -- one CONFIRMED/DISPUTED verdict per suspect 1-6, with the specific source lines you used.
B. PORT-FIDELITY DIFF -- an explicit line-by-line comparison of TAOM CanPlayerNavigateToPosition + the terrain methods vs the NavalDLC reference above. State EXACTLY where they match and where they differ.
C. CONFIG CROSS-REFERENCE -- confirm every JSON key maps to a consumer and the validation (FiniteFloatValidator threshold range, Enum.IsDefined terrain ids) is sound; flag any parsed-but-unused field.
D. ADDITIONAL FINDINGS -- anything the above suspects did not cover (null-safety, threading, save-compat, AI-routing risk, etc).
E. FINDINGS OR OBSERVATIONS -- if you find nothing actionable in a section, say so explicitly; do NOT invent issues to fill space, and do NOT flag vanilla-matching code as a bug.

QUALITY GATES:
- Cite TAOM file:line for every claim about TAOM code.
- For every "X is missing/wrong" claim, show the code you read that proves it.
- Do not flag code that faithfully matches the NavalDLC reference as a bug -- faithful porting is the goal.
- Severity each finding HIGH/MED/LOW with a one-line justification.

PRIOR REVIEW LESSONS:
SUCCESSES: vanilla decompilation caught missing gates; lifecycle tracing caught stale caches; config cross-ref caught dead values.
FAILURES: Codex has flagged vanilla-matching code as bugs; Codex has skipped hard analysis sections; Codex has assumed APIs without verifying. Verify against the actual installed v1.4.6 signatures and the reference code above.

Write your review to docs/reviews/codex-adversarial-navaltravel-2026-06-24.md (this file is already the prompt; output is captured separately).
