using System;
using System.Collections.Generic;
using SandBox.View.Map;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.FieldCamp.Visuals;
using TAOM.Features.Refuge.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TAOM.Features.Refuge.Visuals;

/// <summary>
/// Engine boundary for the refuge map visuals (CampVisualService shape). Owns the per-refuge
/// entity lists; mesh placement composes <see cref="CampLayoutBuilder"/>'s internal helpers, since
/// a refuge is drawn from different prefabs at different scales than a camp, and its fallback
/// layout carries per-tent scaling the camp-level Place entry does not.
///
/// <para><see cref="Show"/> is safe to call every frame: once a refuge's visuals exist for its
/// current tier + fortification it short-circuits, and while the map scene is not live yet (the
/// save-load window) it returns false so the refuge service keeps retrying. A tier change
/// (stronghold upgrade completing) re-places the layout in the same call, so the service does not
/// need a separate remove-then-show dance after an upgrade.</para>
/// </summary>
public sealed class RefugeVisualService : IRefugeVisualService
{
    private sealed class RefugeVisual
    {
        public readonly List<GameEntity> Entities = new List<GameEntity>();
        public bool Shown;

        // The layout differs per tier and fortification; remembering what was placed is what lets
        // Show be both idempotent and self-refreshing when the refuge upgrades.
        public RefugeTier Tier;
        public bool Fortified;
    }

    // Bespoke refuge tpac meshes; the military fallback runs when they are absent (art half of
    // the module not shipped). Scales are the source module's values verbatim.
    internal const string RefugeCampMesh = "refuge_camp_a";
    internal const string RefugeRingMesh = "refuge_palisade_ring";
    internal const float RefugeCampScale = 4f;
    internal const float RefugeCampScaleStronghold = 4.8f;
    internal const float RefugeRingScale = 4.6f;
    internal const float RefugeRingScaleStronghold = 5.4f;

    // Decorative large-tent mesh used by the fallback ring; a scene prop scaled far down for the
    // campaign map, exactly as the source module drew it.
    internal const string DecorTentMesh = "camp_tent";
    internal const float DecorTentScale = 0.17f;

    // Accent recolor for every second banner tent in the fallback layout (source values). The
    // Banner(Banner, uint, uint) copy ctor is guarded because banner data is player-authored.
    private const uint BannerAccentColor1 = 4280237343u;
    private const uint BannerAccentColor2 = 4292397984u;

    // Same cadence as CampVisualService: cloth wind does not need frame rate.
    private const int WindTickIntervalMs = 500;

    private readonly IModLogger _logger;
    private readonly IRefugeSettingsProvider _settings;
    private readonly Dictionary<string, RefugeVisual> _visuals =
        new Dictionary<string, RefugeVisual>(StringComparer.Ordinal);

    private int _lastWindTickMs;
    private bool _sceneWaitLogged;

    public RefugeVisualService(IRefugeSettingsProvider settings, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool Show(string refugeId, RefugeTier tier, bool fortified, Vec2 position)
    {
        TickWindThrottled();

        if (string.IsNullOrEmpty(refugeId))
            return false;

        if (_visuals.TryGetValue(refugeId, out var existing)
            && existing.Shown && existing.Tier == tier && existing.Fortified == fortified)
        {
            return true;
        }

        // MapScreen.MapScene is null until the map screen finishes initializing after a load;
        // returning false tells the refuge service to retry on a later frame.
        var scene = MapScreen.Instance?.MapScene;
        if (scene == null)
        {
            if (!_sceneWaitLogged)
            {
                _sceneWaitLogged = true;
                _logger.LogDebug("[Refuge] visuals waiting for the map scene (will retry)");
            }
            return false;
        }
        _sceneWaitLogged = false;

        var visual = existing;
        if (visual == null)
        {
            visual = new RefugeVisual();
            _visuals[refugeId] = visual;
        }

        // A previous half-failed attempt (or the pre-upgrade layout) may have left entities
        // behind; never double-place.
        CampLayoutBuilder.Remove(visual.Entities);

        int troops = ResolveParty(refugeId)?.MemberRoster?.TotalManCount ?? 0;
        var center = new Vec3(position.x, position.y, 0f, -1f);
        Place(scene, in center, tier, fortified, troops, visual.Entities,
            _settings.BuildingMesh, _settings.BuildingScale);

        visual.Shown = true;
        visual.Tier = tier;
        visual.Fortified = fortified;
        _logger.LogDebug(
            $"[Refuge] placed {visual.Entities.Count} {tier} entities for '{refugeId}' (fortified {fortified}, troops {troops})");
        return true;
    }

    public void Remove(string refugeId)
    {
        if (string.IsNullOrEmpty(refugeId) || !_visuals.TryGetValue(refugeId, out var visual))
            return;
        CampLayoutBuilder.Remove(visual.Entities);
        _visuals.Remove(refugeId);
    }

    public void ClearAll()
    {
        foreach (var visual in _visuals.Values)
            CampLayoutBuilder.Remove(visual.Entities);
        _visuals.Clear();
        _sceneWaitLogged = false;
    }

    private static void Place(
        Scene scene, in Vec3 center, RefugeTier tier, bool fortified, int troops,
        List<GameEntity> outEntities, string buildingMesh, float buildingScale)
    {
        bool stronghold = tier == RefugeTier.Stronghold;
        float campScale = stronghold ? RefugeCampScaleStronghold : RefugeCampScale;

        if (!CampLayoutBuilder.PlaceCenteredPrefab(scene, in center, RefugeCampMesh, campScale, outEntities))
        {
            PlaceMilitaryLayout(scene, in center, stronghold, troops, outEntities, buildingMesh, buildingScale);
            return;
        }

        // Prefab refuge stands; only a fortified one gets the palisade ring (source behavior:
        // the ring reads as the fortification carried over from the fortified camp it grew from).
        if (fortified)
        {
            CampLayoutBuilder.PlaceCenteredPrefab(
                scene, in center, RefugeRingMesh,
                stronghold ? RefugeRingScaleStronghold : RefugeRingScale, outEntities);
        }
    }

    /// <summary>
    /// The source module's procedural fallback, ported over the shared builder: command tent with
    /// the clan banner, two jittered tent rings, a ring of large decorative tents, alternating
    /// clan/accent banner tents, and a closing barricade ring. Every count and radius steps up for
    /// a stronghold so the two tiers read differently at a glance.
    /// </summary>
    private static void PlaceMilitaryLayout(
        Scene scene, in Vec3 center, bool stronghold, int troops, List<GameEntity> outEntities,
        string buildingMesh, float buildingScale)
    {
        Banner? banner = null;
        try
        {
            banner = Clan.PlayerClan?.Banner;
        }
        catch
        {
            // Campaign mid-teardown; a bannerless layout still stands.
        }

        Banner? accentBanner = banner;
        if (banner != null)
        {
            try
            {
                accentBanner = new Banner(banner, BannerAccentColor1, BannerAccentColor2);
            }
            catch
            {
                accentBanner = banner;
            }
        }

        CampLayoutBuilder.PlaceCommandTent(
            scene, in center, stronghold ? 3.2f : 2.6f, banner, outEntities);

        // Source parity: an optional named building mesh beside the command tent (the source's
        // RefugeBuildingMesh/RefugeBuildingScale MCM knobs, offsets +1/-0.2 verbatim). Positive
        // finite gate on the scale: a degenerate value drops the prop, never a NaN frame.
        if (!string.IsNullOrEmpty(buildingMesh)
            && FiniteFloatValidator.IsFiniteInRange(buildingScale, 0.01f, 10f))
        {
            PlaceBuildingMesh(scene, center.x + 1f, center.y - 0.2f, center.z, buildingScale, buildingMesh, outEntities);
        }

        float tentScale = stronghold ? 1.5f : 1.3f;
        PlaceScaledTentRing(
            scene, in center, stronghold ? 8 : 6, stronghold ? 2.1f : 1.5f, tentScale, outEntities);
        PlaceScaledTentRing(
            scene, in center,
            CampLayoutMath.ScaledTentCount(troops, stronghold ? 10 : 8, stronghold ? 16 : 12),
            stronghold ? 3.2f : 2.3f, tentScale, outEntities);

        PlaceDecorTentRing(
            scene, in center, stronghold ? 5 : 4, stronghold ? 2.7f : 1.95f, outEntities);

        if (banner != null)
        {
            int bannerTents = stronghold ? 6 : 4;
            float bannerRadius = stronghold ? 2.5f : 1.85f;
            for (int i = 0; i < bannerTents; i++)
            {
                // Banner tents sit evenly spaced (no jitter): they are the deliberate, planted
                // markers of the layout where the plain tent rings read as pitched.
                float angle = (float)(Math.PI * 2.0 * i / bannerTents);
                float x = center.x + bannerRadius * (float)Math.Cos(angle);
                float y = center.y + bannerRadius * (float)Math.Sin(angle);
                CampLayoutBuilder.PlaceTentWithBanner(
                    scene, x, y, center.z, 1.2f, (i % 2 == 0 ? banner : accentBanner) ?? banner, outEntities);
            }
        }

        CampLayoutBuilder.PlaceBarricadeRing(
            scene, in center, stronghold ? 12 : 9, stronghold ? 3.9f : 2.9f, outEntities);
    }

    /// <summary>Like the builder's own tent ring but with a per-tent scale: refuge tents are drawn
    /// larger than camp tents so the settlement reads as permanent.</summary>
    private static void PlaceScaledTentRing(
        Scene scene, in Vec3 center, int count, float radius, float tentScale,
        List<GameEntity> outEntities)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = CampLayoutMath.TentSlotAngle(i, count);
            float distance = CampLayoutMath.TentSlotDistance(i, radius);
            float x = center.x + distance * (float)Math.Cos(angle);
            float y = center.y + distance * (float)Math.Sin(angle);

            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = new Vec3(x, y, CampLayoutBuilder.TerrainHeight(x, y, center.z), -1f);
            frame.rotation.RotateAboutUp(CampLayoutMath.TentSlotFacing(i, count));
            frame.rotation.ApplyScaleLocal(tentScale);
            CampLayoutBuilder.PlaceMesh(scene, CampLayoutBuilder.TentMeshName, in frame, outEntities);
        }
    }

    private static void PlaceDecorTentRing(
        Scene scene, in Vec3 center, int count, float radius, List<GameEntity> outEntities)
    {
        for (int i = 0; i < count; i++)
        {
            // Jittered angles and radii, the source's PlaceNamedMeshRing values verbatim
            // (noise seeds 401/411): the big set-piece tents read as pitched, not planted on a
            // compass rose. An earlier build de-jittered this ring; that was a port drift, not
            // design.
            float angle = (float)(Math.PI * 2.0 * i / count)
                + 0.4f + (CampLayoutMath.Noise(i + 401) - 0.5f) * 1f;
            float distance = radius * (0.7f + CampLayoutMath.Noise(i + 411) * 0.55f);
            float x = center.x + distance * (float)Math.Cos(angle);
            float y = center.y + distance * (float)Math.Sin(angle);

            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = new Vec3(x, y, CampLayoutBuilder.TerrainHeight(x, y, center.z), -1f);
            frame.rotation.RotateAboutUp(angle + (float)Math.PI);
            frame.rotation.ApplyScaleLocal(DecorTentScale);
            CampLayoutBuilder.PlaceMesh(scene, DecorTentMesh, in frame, outEntities);
        }
    }

    /// <summary>The source's CampLayoutBuilder.PlaceNamedMesh, expressed over the shared
    /// builder's PlaceMesh (identity frame at terrain height, uniform scale).</summary>
    private static void PlaceBuildingMesh(
        Scene scene, float x, float y, float zFallback, float scale, string meshName,
        List<GameEntity> outEntities)
    {
        MatrixFrame frame = MatrixFrame.Identity;
        frame.origin = new Vec3(x, y, CampLayoutBuilder.TerrainHeight(x, y, zFallback), -1f);
        frame.rotation.ApplyScaleLocal(scale);
        CampLayoutBuilder.PlaceMesh(scene, meshName, in frame, outEntities);
    }

    public void TickWind()
    {
        if (_visuals.Count == 0)
            return;
        TickWindThrottled();
    }

    private void TickWindThrottled()
    {
        // Environment.TickCount wraps after ~25 days; unchecked subtraction stays correct.
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
            foreach (var party in MobileParty.All)
            {
                if (party != null && string.Equals(party.StringId, partyId, StringComparison.Ordinal))
                    return party;
            }
        }
        catch
        {
            // Campaign mid-teardown; an unresolved party just means the minimum tent count.
        }
        return null;
    }
}
