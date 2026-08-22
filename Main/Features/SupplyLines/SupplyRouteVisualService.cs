using System;
using System.Collections.Generic;
using SandBox.View.Map;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.SupplyLines.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TAOM.Features.SupplyLines;

/// <summary>Draws the on-map arrow trail for in-transit supply caravans. Engine boundary.</summary>
public interface ISupplyRouteVisualService
{
    /// <summary>Per-frame update; cheap when nothing changed. Gates itself on the setting with a
    /// clear-once latch so a toggle-off removes the entities exactly once.</summary>
    void Update();

    /// <summary>Removes every arrow entity. Safe to call with none present.</summary>
    void ClearAll();
}

/// <summary>
/// Implementation ported from the source module's SupplyRouteVisual, throttled: the path is
/// resampled every 0.25 game-hours (source cadence) and arrows are only re-tinted when the travel
/// fraction moved at least a step; between those, a frame does no engine work. The dead handcart
/// mover branch is not ported.
/// </summary>
public sealed class SupplyRouteVisualService : ISupplyRouteVisualService
{
    private sealed class RouteEntry
    {
        public readonly List<GameEntity> Arrows = new List<GameEntity>();
        public List<RouteSample> Samples = new List<RouteSample>();
        public double LastPathRefreshHours = double.MinValue;
        public float LastTintedFraction = float.MinValue;
        public MobileParty Caravan;
    }

    private readonly struct RouteSample
    {
        public RouteSample(float x, float y, float angle)
        {
            X = x;
            Y = y;
            Angle = angle;
        }

        public readonly float X;
        public readonly float Y;
        public readonly float Angle;
    }

    private const string ArrowAsset = "map_track_arrow";
    private const float ArrowSpacing = 1.6f;
    private const int MaxArrows = 40;
    private const float ArrowScale = 0.27f;
    private const double PathRefreshHours = 0.25;

    // Arrows are re-tinted (and only re-tinted) when the fraction moved at least this much.
    private const float TintFractionStep = 0.01f;

    private const int EntityRemoveReason = 111;

    private readonly ISupplyOrderService _orders;
    private readonly ISupplyLinesSettingsProvider _settings;
    private readonly IModLogger _logger;

    private readonly Dictionary<string, RouteEntry> _entries = new Dictionary<string, RouteEntry>();
    private bool _clearedWhileHidden;
    private bool _assetWarned;

    public SupplyRouteVisualService(
        ISupplyOrderService orders,
        ISupplyLinesSettingsProvider settings,
        IModLogger logger)
    {
        _orders = orders;
        _settings = settings;
        _logger = logger;
    }

    public void Update()
    {
        if (!_settings.ShowRouteVisual)
        {
            if (!_clearedWhileHidden)
            {
                ClearAll();
                _clearedWhileHidden = true;
            }
            return;
        }
        _clearedWhileHidden = false;

        var scene = MapScreen.Instance?.MapScene;
        var mainParty = MobileParty.MainParty;
        if (scene == null || mainParty == null)
            return;

        var active = _orders.ActiveOrders;
        if (active.Count == 0)
        {
            if (_entries.Count > 0)
                ClearAll();
            return;
        }

        var seen = new HashSet<string>();
        double nowHours = CampaignTime.Now.ToHours;

        foreach (var order in active)
        {
            if (order.StatusEnum != SupplyOrderStatus.InTransit)
                continue;
            if (!_entries.TryGetValue(order.OrderId, out var entry))
            {
                entry = new RouteEntry();
                _entries[order.OrderId] = entry;
            }

            if (entry.Samples.Count == 0 || nowHours - entry.LastPathRefreshHours >= PathRefreshHours)
            {
                var caravan = ResolveCaravan(entry, order);
                if (caravan == null)
                    continue; // party gone; the stale sweep below removes the arrows
                entry.Samples = SampleRoute(caravan, mainParty);
                entry.LastPathRefreshHours = nowHours;
                SyncArrows(scene, entry);
                entry.LastTintedFraction = float.MinValue; // arrows moved, force a retint
            }
            seen.Add(order.OrderId);

            float fraction = ClampFraction(order.ElapsedFraction());
            if (Math.Abs(fraction - entry.LastTintedFraction) >= TintFractionStep)
            {
                uint color = FractionToColor(fraction);
                foreach (var arrow in entry.Arrows)
                    TintArrow(arrow, color);
                entry.LastTintedFraction = fraction;
            }
        }

        List<string> stale = null;
        foreach (var pair in _entries)
        {
            if (!seen.Contains(pair.Key))
                (stale ?? (stale = new List<string>())).Add(pair.Key);
        }
        if (stale != null)
        {
            foreach (var key in stale)
            {
                RemoveEntry(_entries[key]);
                _entries.Remove(key);
            }
        }
    }

    public void ClearAll()
    {
        foreach (var entry in _entries.Values)
            RemoveEntry(entry);
        _entries.Clear();
    }

    // --- caravan resolution (only at resample cadence, never per frame) ---

    private static MobileParty ResolveCaravan(RouteEntry entry, SupplyOrder order)
    {
        var cached = entry.Caravan;
        if (cached != null && cached.IsActive && cached.StringId == order.CaravanPartyId)
            return cached;
        entry.Caravan = null;
        if (string.IsNullOrEmpty(order.CaravanPartyId))
            return null;
        // Linear scan, but reached at most once per 0.25 game-hours per order and only after the
        // cached reference went stale (spawn, load, respawn).
        foreach (var party in MobileParty.All)
        {
            if (party != null && party.StringId == order.CaravanPartyId)
            {
                entry.Caravan = party;
                return party;
            }
        }
        return null;
    }

    // --- path sampling ---

    private static List<RouteSample> SampleRoute(MobileParty caravan, MobileParty mainParty)
    {
        var points = SupplyCaravanService.ComputeNavPathPoints(caravan.GetPosition2D, mainParty.GetPosition2D);
        return SampleByArcLength(points, ArrowSpacing, MaxArrows);
    }

    private static List<RouteSample> SampleByArcLength(List<Vec2> waypoints, float spacing, int maxPoints)
    {
        var samples = new List<RouteSample>();
        if (waypoints == null || waypoints.Count == 0)
            return samples;
        if (waypoints.Count == 1)
        {
            samples.Add(new RouteSample(waypoints[0].x, waypoints[0].y, 0f));
            return samples;
        }
        if (spacing <= 0f)
            spacing = 1f;

        samples.Add(new RouteSample(waypoints[0].x, waypoints[0].y, Angle(waypoints[0], waypoints[1])));
        float carried = 0f;
        float step = spacing;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            Vec2 a = waypoints[i];
            Vec2 b = waypoints[i + 1];
            float segment = a.Distance(b);
            if (segment <= 0f)
                continue;
            float angle = Angle(a, b);
            float dirX = (b.x - a.x) / segment;
            float dirY = (b.y - a.y) / segment;
            for (step = spacing - carried; step <= segment; step += spacing)
            {
                if (samples.Count >= maxPoints)
                    return samples;
                samples.Add(new RouteSample(a.x + dirX * step, a.y + dirY * step, angle));
            }
            carried = segment - (step - spacing);
        }

        Vec2 last = waypoints[waypoints.Count - 1];
        RouteSample lastSample = samples[samples.Count - 1];
        if (samples.Count < maxPoints
            && (Math.Abs(lastSample.X - last.x) > 0.001f || Math.Abs(lastSample.Y - last.y) > 0.001f))
        {
            samples.Add(new RouteSample(last.x, last.y, Angle(waypoints[waypoints.Count - 2], last)));
        }
        return samples;
    }

    private static float Angle(Vec2 a, Vec2 b) => (float)Math.Atan2(b.y - a.y, b.x - a.x);

    // --- entity plumbing ---

    private void SyncArrows(Scene scene, RouteEntry entry)
    {
        while (entry.Arrows.Count < entry.Samples.Count)
        {
            var arrow = CreateArrow(scene);
            if (arrow == null)
                break;
            entry.Arrows.Add(arrow);
        }
        while (entry.Arrows.Count > entry.Samples.Count)
        {
            int last = entry.Arrows.Count - 1;
            RemoveArrow(entry.Arrows[last]);
            entry.Arrows.RemoveAt(last);
        }

        var mapScene = Campaign.Current?.MapSceneWrapper;
        for (int i = 0; i < entry.Arrows.Count; i++)
        {
            RouteSample sample = entry.Samples[i];
            float height = 0f;
            var normal = new Vec3(0f, 0f, 1f);
            if (mapScene != null)
            {
                try
                {
                    mapScene.GetTerrainHeightAndNormal(new Vec2(sample.X, sample.Y), out height, out normal);
                }
                catch (Exception)
                {
                    // A point off the navigable terrain keeps the flat default frame.
                }
            }

            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = new Vec3(sample.X, sample.Y, height);
            var forward = new Vec3((float)Math.Cos(sample.Angle), (float)Math.Sin(sample.Angle), 0f);
            frame.rotation.u = normal;
            frame.rotation.s = Vec3.CrossProduct(forward, frame.rotation.u);
            frame.rotation.s.Normalize();
            frame.rotation.f = Vec3.CrossProduct(frame.rotation.u, frame.rotation.s);
            frame.rotation.f.Normalize();
            frame.rotation.u.Normalize();
            frame.rotation.s *= ArrowScale;
            frame.rotation.f *= ArrowScale;
            frame.rotation.u *= ArrowScale;
            try
            {
                entry.Arrows[i].SetGlobalFrame(in frame);
            }
            catch (Exception)
            {
                // A dead entity handle is recreated on the next sync; nothing to do this frame.
            }
        }
    }

    private GameEntity CreateArrow(Scene scene)
    {
        try
        {
            var entity = GameEntity.Instantiate(scene, ArrowAsset, MatrixFrame.Identity);
            if (entity == null && !_assetWarned)
            {
                _assetWarned = true;
                _logger.LogWarning($"[SupplyLines] prefab '{ArrowAsset}' unavailable; route arrows disabled");
            }
            return entity;
        }
        catch (Exception ex)
        {
            if (!_assetWarned)
            {
                _assetWarned = true;
                _logger.LogWarning($"[SupplyLines] route arrow instantiate failed: {ex.Message}");
            }
            return null;
        }
    }

    private static void TintArrow(GameEntity entity, uint color)
    {
        if (entity == null)
            return;
        try
        {
            if (entity.GetComponentAtIndex(0, GameEntity.ComponentType.Decal) is Decal decal)
            {
                decal.SetFactor1(color);
                return;
            }
        }
        catch (Exception)
        {
            // Fall through to the whole-entity tint below.
        }
        try
        {
            entity.SetFactorColor(color);
        }
        catch (Exception)
        {
            // Untintable entity; the arrow still renders in its default colour.
        }
    }

    private static void RemoveArrow(GameEntity entity)
    {
        try
        {
            entity?.Remove(EntityRemoveReason);
        }
        catch (Exception)
        {
            // Already gone with its scene; nothing to release.
        }
    }

    private static void RemoveEntry(RouteEntry entry)
    {
        foreach (var arrow in entry.Arrows)
            RemoveArrow(arrow);
        entry.Arrows.Clear();
        entry.Samples.Clear();
    }

    // --- colour ---

    private static float ClampFraction(float fraction)
    {
        if (!FiniteFloatValidator.IsFinite(fraction))
            return 1f;
        if (fraction < 0f)
            return 0f;
        return fraction > 1f ? 1f : fraction;
    }

    /// <summary>Far-to-near gradient: red at 0, yellow at 0.5, green at 1 (source module ramp).</summary>
    private static uint FractionToColor(float fraction)
    {
        int r;
        int g;
        int b;
        if (fraction <= 0.5f)
        {
            float t = fraction / 0.5f;
            r = 224;
            g = LerpByte(48, 192, t);
            b = 48;
        }
        else
        {
            float t = (fraction - 0.5f) / 0.5f;
            r = LerpByte(224, 48, t);
            g = 192;
            b = LerpByte(48, 64, t);
        }
        return (uint)(-16777216 | (r << 16) | (g << 8) | b);
    }

    private static int LerpByte(int a, int b, float t)
    {
        int value = (int)(a + (b - a) * t + 0.5f);
        if (value < 0)
            return 0;
        return value > 255 ? 255 : value;
    }
}
