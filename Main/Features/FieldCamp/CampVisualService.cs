using System;
using System.Collections.Generic;
using SandBox.View.Map;
using TAOM.Core.Logging;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.FieldCamp.Visuals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TAOM.Features.FieldCamp;

/// <summary>
/// Engine boundary for the camp map visuals. Owns the per-party entity lists and the banner-wind
/// ticker; the actual mesh placement lives in <see cref="CampLayoutBuilder"/>.
///
/// <para><see cref="Show"/> is safe to call every frame: once a party's visuals exist it
/// short-circuits, and while the map scene is not live yet (the save-load window) it returns
/// false so the camp service keeps retrying. That retry loop is how the source's
/// recreate-after-load path is expressed through this API.</para>
///
/// <para>The service is an IoC singleton, so it outlives a campaign session. The behavior calls
/// <see cref="ClearAll"/> at session end; without that, entity handles from a dead map scene
/// would leak into the next campaign.</para>
/// </summary>
public sealed class CampVisualService : ICampVisualService
{
    private sealed class CampVisual
    {
        // An ambush camp is legitimately "shown" with zero entities, so the flag is explicit
        // rather than derived from the list.
        public readonly List<GameEntity> Entities = new List<GameEntity>();
        public bool Shown;
    }

    // Forced wind is reapplied at most this often, in real milliseconds. The source ticked it
    // every frame; cloth wind does not need anywhere near that cadence.
    private const int WindTickIntervalMs = 500;

    private readonly IModLogger _logger;
    private readonly Dictionary<string, CampVisual> _visuals =
        new Dictionary<string, CampVisual>(StringComparer.Ordinal);

    private int _lastWindTickMs;
    private bool _sceneWaitLogged;

    public CampVisualService(IModLogger logger)
    {
        _logger = logger;
    }

    public bool Show(string partyId, CampType type, Vec2 position)
    {
        TickWindThrottled();

        if (string.IsNullOrEmpty(partyId))
            return false;

        if (_visuals.TryGetValue(partyId, out var existing) && existing.Shown)
            return true;

        // MapScreen.MapScene is null until the map screen finishes initializing after a load;
        // returning false tells the camp service to retry on a later frame.
        var scene = MapScreen.Instance?.MapScene;
        if (scene == null)
        {
            if (!_sceneWaitLogged)
            {
                _sceneWaitLogged = true;
                _logger.LogDebug("[FieldCamp] camp visuals waiting for the map scene (will retry)");
            }
            return false;
        }
        _sceneWaitLogged = false;

        var visual = existing;
        if (visual == null)
        {
            visual = new CampVisual();
            _visuals[partyId] = visual;
        }

        // A previous half-failed attempt may have left entities behind; never double-place.
        CampLayoutBuilder.Remove(visual.Entities);

        if (type != CampType.Ambush)
        {
            var party = ResolveParty(partyId);
            int troops = party?.MemberRoster?.TotalManCount ?? 0;
            var center = new Vec3(position.x, position.y, 0f, -1f);
            CampLayoutBuilder.Place(scene, in center, type, troops, visual.Entities, ResolveBanner(party, type));
            _logger.LogDebug(
                $"[FieldCamp] placed {visual.Entities.Count} {type} camp entities for '{partyId}' (troops {troops})");
        }

        visual.Shown = true;
        return true;
    }

    public bool IsShown(string partyId)
    {
        // The camp service polls this at frame cadence once the camp is up, which makes it the
        // steady-state driver for the wind ticker (Show covers the raising window).
        TickWindThrottled();

        return !string.IsNullOrEmpty(partyId)
            && _visuals.TryGetValue(partyId, out var visual)
            && visual.Shown;
    }

    public void Remove(string partyId)
    {
        if (string.IsNullOrEmpty(partyId) || !_visuals.TryGetValue(partyId, out var visual))
            return;
        CampLayoutBuilder.Remove(visual.Entities);
        _visuals.Remove(partyId);
    }

    public void ClearAll()
    {
        foreach (var visual in _visuals.Values)
            CampLayoutBuilder.Remove(visual.Entities);
        _visuals.Clear();
        _sceneWaitLogged = false;
    }

    private void TickWindThrottled()
    {
        // Environment.TickCount wraps after ~25 days; unchecked subtraction keeps the comparison
        // correct across the wrap.
        int now = Environment.TickCount;
        if (unchecked(now - _lastWindTickMs) < WindTickIntervalMs)
            return;
        _lastWindTickMs = now;
        CampLayoutBuilder.TickWind();
    }

    private static MobileParty? ResolveParty(string partyId)
    {
        try
        {
            var main = MobileParty.MainParty;
            if (main != null && string.Equals(main.StringId, partyId, StringComparison.Ordinal))
                return main;

            // Only reached for a non-main party, which today does not happen (camps are
            // player-only); the scan keeps the service correct if AI camps ever land.
            foreach (var party in MobileParty.All)
            {
                if (party != null && string.Equals(party.StringId, partyId, StringComparison.Ordinal))
                    return party;
            }
        }
        catch
        {
            // Campaign mid-teardown; an unresolved party just means a bannerless default layout.
        }
        return null;
    }

    private static Banner? ResolveBanner(MobileParty? party, CampType type)
    {
        // Matches the source: only the two tented camp types fly the clan banner. A lookout is a
        // single unmarked tent and an ambush shows nothing at all.
        if (type != CampType.Field && type != CampType.Fortified)
            return null;

        try
        {
            if (party != null && party == MobileParty.MainParty)
                return Clan.PlayerClan?.Banner;
            return party?.LeaderHero?.Clan?.Banner;
        }
        catch
        {
            return null;
        }
    }
}
