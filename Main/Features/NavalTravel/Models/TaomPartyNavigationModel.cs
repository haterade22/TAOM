using System;
using System.Collections.Generic;
using Helpers;
using TAOM.Core.Logging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;

namespace TAOM.Features.NavalTravel.Models;

/// <summary>
/// Unlocks the base engine's native naval-travel system for everyone, without the Naval DLC.
///
/// The whole campaign-map sailing system (water pathing, embark/disembark transitions, native
/// party-as-ship rendering, sea-state, ports) ships in the base engine but is switched OFF by
/// <see cref="DefaultPartyNavigationModel.HasNavalNavigationCapability"/> returning <c>false</c>.
/// This override is a faithful port of NavalDLC's internal <c>NavalPartyNavigationModel</c> — same
/// naval terrain rules + embark threshold + <c>CanPlayerNavigateToPosition</c> — except the
/// ship-ownership capability gate is replaced by <see cref="INavalTravelService.CanPartySail"/>
/// (config + independent player/AI MCM toggles), so any party can sail with no ship and no DLC.
///
/// Thin entry point (ADR-002): pure decisions (naval terrain set, sail permission, threshold) live
/// in <see cref="INavalTravelService"/>; only the inherently engine-coupled navigation glue stays
/// here, mirroring how <c>TaomPartySpeedModel</c> keeps its boundary code in the model.
/// </summary>
public class TaomPartyNavigationModel : DefaultPartyNavigationModel
{
    private readonly INavalTravelService _service;
    private readonly IModLogger _logger;
    private readonly Dictionary<MobileParty.NavigationType, int[]> _invalidTerrainCache = new();

    // Held to deliberately set sail (click water while held → embark). Auto-pathing never picks a sea
    // route over a land/bridge route, so the player initiates sailing explicitly. Parsed once from config.
    private readonly InputKey _sailModifierKey;

    // Diagnostics (temporary — strip after in-game sign-off).
    private bool? _lastMainCapability;
    private (bool targetOnLand, bool mainAtSea, bool result)? _lastCanNav;

    public TaomPartyNavigationModel(INavalTravelService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _sailModifierKey = Enum.TryParse<InputKey>(_service.SailModifierKey, ignoreCase: true, out var key)
            ? key
            : InputKey.LeftAlt;
        BuildInvalidTerrainCache();
        _logger.LogInfo($"[NavalTravel][diag] TaomPartyNavigationModel constructed + registered as PartyNavigationModel " +
            $"(isEnabled={_service.IsEnabled}, embarkThreshold={_service.EmbarkThresholdDistance}, " +
            $"invalidForAll=[{string.Join(",", _invalidTerrainCache[MobileParty.NavigationType.All])}]).");
    }

    public override float GetEmbarkDisembarkThresholdDistance() =>
        _service.IsEnabled ? _service.EmbarkThresholdDistance : base.GetEmbarkDisembarkThresholdDistance();

    public override bool IsTerrainTypeValidForNavigationType(TerrainType terrainType, MobileParty.NavigationType navigationType) =>
        ComputeTerrainValid(terrainType, navigationType);

    public override int[] GetInvalidTerrainTypesForNavigationType(MobileParty.NavigationType navigationType) =>
        _invalidTerrainCache.TryGetValue(navigationType, out var ids) ? ids : Array.Empty<int>();

    /// <summary>
    /// The single gate that flips naval travel on. Boundary: extracts the engine values the pure
    /// <see cref="INavalTravelService.HasNavalCapability"/> decision needs (is-main, at-sea, and the
    /// army leader's capability) and delegates. Replaces NavalDLC's "party owns a ship" check with the
    /// TAOM config/MCM decision so sailing needs no ship and no DLC. An at-sea party keeps capability
    /// (reach land, never strand mid-voyage); an attached party inherits its leader's capability so a
    /// player-led army sails together even when ApplyToAi is off.
    /// </summary>
    public override bool HasNavalNavigationCapability(MobileParty mobileParty)
    {
        if (mobileParty == null)
            return base.HasNavalNavigationCapability(mobileParty);
        var attachedLeaderCanSail = mobileParty.AttachedTo?.HasNavalNavigationCapability ?? false;
        var result = _service.HasNavalCapability(mobileParty.IsMainParty, mobileParty.IsCurrentlyAtSea, attachedLeaderCanSail);

        // [diag] main party's capability, logged only when it changes (this method is hot).
        if (mobileParty.IsMainParty && _lastMainCapability != result)
        {
            _lastMainCapability = result;
            _logger.LogInfo($"[NavalTravel][diag] main party HasNavalNavigationCapability={result} " +
                $"(atSea={mobileParty.IsCurrentlyAtSea}, isEnabled={_service.IsEnabled}).");
        }
        return result;
    }

    /// <summary>
    /// Faithful port of NavalDLC's <c>CanPlayerNavigateToPosition</c> — decides whether a player
    /// map click initiates land/sea movement (the base engine's <c>NavigationHelper</c> calls this).
    /// Falls through to vanilla when the feature is disabled, EXCEPT while the main party is already
    /// at sea (a live toggle-off mid-voyage) — then it stays naval-aware so the player can sail to
    /// shore instead of soft-locking (base <c>CanPlayerNavigateToPosition</c> rejects any move from a
    /// non-land position).
    /// </summary>
    public override bool CanPlayerNavigateToPosition(CampaignVec2 vec2, out MobileParty.NavigationType navigationType)
    {
        var result = CanPlayerNavigateToPositionCore(vec2, out navigationType);

        // [diag] log each distinct (targetOnLand, mainAtSea, result) combo once — shows whether a water click is accepted.
        var key = (vec2.IsOnLand, MobileParty.MainParty?.IsCurrentlyAtSea ?? false, result);
        if (_lastCanNav != key)
        {
            _lastCanNav = key;
            _logger.LogInfo($"[NavalTravel][diag] CanPlayerNavigateToPosition: targetOnLand={key.Item1} mainAtSea={key.Item2} " +
                $"-> result={result} navType={navigationType}.");
        }
        return result;
    }

    private bool CanPlayerNavigateToPositionCore(CampaignVec2 vec2, out MobileParty.NavigationType navigationType)
    {
        if (!_service.IsEnabled && !(MobileParty.MainParty?.IsCurrentlyAtSea ?? false))
            return base.CanPlayerNavigateToPosition(vec2, out navigationType);

        navigationType = MobileParty.NavigationType.None;
        if (!vec2.Face.IsValid())
            return false;

        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
            return false;

        var targetIsNaval = NavigationHelper.IsPositionValidForNavigationType(vec2, MobileParty.NavigationType.Naval);

        // From land, a water target is normally not directly clickable. Holding the sail modifier key
        // overrides that (a deliberate "set sail") so the player can route toward the water — the party
        // walks to the coast and the engine's embark transition fires there. Without the modifier it
        // stays vanilla (reject water-from-land), so auto-pathing never picks a sea route on its own.
        if (!mainParty.IsCurrentlyAtSea && targetIsNaval && !IsSailModifierHeld())
            return false;

        if (mainParty.IsCurrentlyAtSea)
        {
            navigationType = mainParty.HasNavalNavigationCapability && targetIsNaval
                ? MobileParty.NavigationType.Naval
                : mainParty.NavigationCapability;
        }
        else if (targetIsNaval)
        {
            // Deliberate set-sail (modifier held): path toward the water with full (All) capability so
            // the route can cross the coast; the land→sea embark transition fires at the edge.
            navigationType = mainParty.NavigationCapability;
        }
        else
        {
            navigationType = MobileParty.NavigationType.Default;
        }

        var invalidTerrainTypes = GetInvalidTerrainTypesForNavigationType(navigationType);
        if (Array.IndexOf(invalidTerrainTypes, vec2.Face.FaceGroupIndex) >= 0)
            return false;
        if (!vec2.IsOnLand && mainParty.IsCurrentlyAtSea)
            return true;

        var mapScene = Campaign.Current.MapSceneWrapper;
        var position = mainParty.Position;
        return mapScene.GetPathDistanceBetweenAIFaces(
            mainParty.CurrentNavigationFace,
            vec2.Face,
            position.ToVec2(),
            vec2.ToVec2(),
            0.3f,
            Campaign.PathFindingMaxCostLimit,
            out float _,
            invalidTerrainTypes,
            mainParty.GetRegionSwitchCostFromLandToSea(),
            mainParty.GetRegionSwitchCostFromSeaToLand());
    }

    /// <summary>
    /// Whether the player is holding the configured set-sail modifier key. Boundary read of raw input
    /// (engine-coupled, ADR-008). [diag] logs the down/up transition so we can confirm the key is seen.
    /// </summary>
    private bool IsSailModifierHeld()
    {
        // The map's active input layer owns/consumes key routing, so a poll from outside a layer
        // (this GameModel, called during the map tick) sees the buffered IsKeyDown as false even
        // while the key is physically held. IsKeyDownImmediate reads the raw device state and
        // bypasses that gate. Accept EITHER source so the modifier works regardless of which the
        // engine populates in this context. [diag] logs both so the input path is verifiable in-game.
        var immediate = Input.IsKeyDownImmediate(_sailModifierKey);
        var buffered = Input.IsKeyDown(_sailModifierKey);
        var held = immediate || buffered;
        _logger.LogInfo($"[NavalTravel][diag] sail modifier ({_sailModifierKey}): immediate={immediate} buffered={buffered} -> held={held}.");
        return held;
    }

    private void BuildInvalidTerrainCache()
    {
        var invalidForDefault = new List<int>();
        var invalidForNaval = new List<int>();
        var invalidForAll = new List<int>();

        foreach (TerrainType terrainType in Enum.GetValues(typeof(TerrainType)))
        {
            if (!ComputeTerrainValid(terrainType, MobileParty.NavigationType.Default))
                invalidForDefault.Add((int)terrainType);
            if (!ComputeTerrainValid(terrainType, MobileParty.NavigationType.Naval))
                invalidForNaval.Add((int)terrainType);
            if (!ComputeTerrainValid(terrainType, MobileParty.NavigationType.All))
                invalidForAll.Add((int)terrainType);
        }

        _invalidTerrainCache[MobileParty.NavigationType.Default] = invalidForDefault.ToArray();
        _invalidTerrainCache[MobileParty.NavigationType.Naval] = invalidForNaval.ToArray();
        _invalidTerrainCache[MobileParty.NavigationType.All] = invalidForAll.ToArray();
    }

    /// <summary>
    /// Non-virtual terrain-validity core (so the ctor cache build doesn't dispatch into the
    /// override). Naval = the configured naval terrain set; All = naval OR vanilla land; everything
    /// else defers to the base model.
    /// </summary>
    private bool ComputeTerrainValid(TerrainType terrainType, MobileParty.NavigationType navigationType)
    {
        if (navigationType == MobileParty.NavigationType.Naval)
            return _service.IsNavalTerrain((int)terrainType);
        if (navigationType == MobileParty.NavigationType.All)
            return _service.IsNavalTerrain((int)terrainType) || base.IsTerrainTypeValidForNavigationType(terrainType, navigationType);
        return base.IsTerrainTypeValidForNavigationType(terrainType, navigationType);
    }
}
