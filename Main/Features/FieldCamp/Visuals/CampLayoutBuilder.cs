using System;
using System.Collections.Generic;
using SandBox.View.Map.Visuals;
using TAOM.Features.FieldCamp.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.DotNet;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TAOM.Features.FieldCamp.Visuals;

/// <summary>
/// Places and removes the campaign-map entities for one camp. Prefab-first: the shipped tpac
/// meshes (<see cref="FieldCampPrefabMesh"/>, <see cref="PalisadeRingMesh"/>) are tried before
/// the procedural vanilla-mesh layout, so the feature still renders something sensible when the
/// art half of the module is absent. Every engine call is null-tolerant and exception-guarded
/// because a missing mesh must degrade to the fallback (or to nothing), never crash the map.
///
/// <para>Only <see cref="CampVisualService"/> calls this; the per-party bookkeeping lives there.
/// The one piece of static state here is the banner-cloth registry, which must outlive a single
/// Place call so <see cref="TickWind"/> can keep the command-tent flags waving.</para>
/// </summary>
internal static class CampLayoutBuilder
{
    internal const string TentMeshName = "map_icon_siege_camp_tent";
    internal const string BarricadeMeshName = "map_icon_siege_camp_1";
    internal const string FieldCampPrefabMesh = "fieldcamp_camp_a";
    internal const string PalisadeRingMesh = "fieldcamp_palisade_ring";
    internal const string BannerMeshName = "campaign_flag";

    internal const float FieldCampPrefabScale = 3f;
    internal const float PalisadeRingScale = 3.8f;
    internal const float BigTentScale = 1.7f;
    internal const float BigTentForwardOffset = 0.55f;
    internal const int FieldMinTents = 2;
    internal const int FieldMaxTents = 10;
    internal const int FortifiedMinTents = 7;
    internal const int FortifiedMaxTents = 18;
    internal const float FieldTentRadius = 0.9f;
    internal const float FortifiedTentRadius = 1.15f;
    internal const int FortifiedBarricadeCount = 8;
    internal const float FortifiedBarricadeRadius = 1.8f;

    // The source used reason 0 ("removed by user"); camp entities are decorative, any reason works.
    private const int EntityRemoveReason = 0;

    private const float FlagWindStrength = 6f;
    private static readonly Vec3 FlagWindDir = new Vec3(0.6f, 0.8f, 0f, -1f);

    // Command-tent banner cloths that still need per-tick forced wind. Dead handles are pruned
    // both by Remove (by entity) and by TickWind (a throw or a null native pointer drops the row).
    private static readonly List<KeyValuePair<GameEntity, ClothSimulatorComponent>> _bannerCloths =
        new List<KeyValuePair<GameEntity, ClothSimulatorComponent>>();

    /// <summary>
    /// Re-applies the forced wind to every registered banner cloth. Map-scene cloths do not keep
    /// a forced wind on their own, so without a periodic reapply the flags hang limp shortly
    /// after placement. Called by the visual service on a real-time throttle, never per frame.
    /// </summary>
    internal static void TickWind()
    {
        if (_bannerCloths.Count == 0)
            return;

        Vec3 wind = FlagWindDir * FlagWindStrength;
        for (int i = _bannerCloths.Count - 1; i >= 0; i--)
        {
            ClothSimulatorComponent cloth = _bannerCloths[i].Value;
            if ((NativeObject)cloth == null)
            {
                _bannerCloths.RemoveAt(i);
                continue;
            }
            try
            {
                cloth.SetForcedWind(wind, false);
            }
            catch
            {
                // The native cloth died with its scene; forget it rather than throw every tick.
                _bannerCloths.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Places the full layout for one camp into <paramref name="outEntities"/>. Ambush camps
    /// place nothing by design: an ambush is hidden, a visible tent would advertise it.
    /// </summary>
    internal static void Place(
        Scene? scene, in Vec3 center, CampType type, int troops,
        List<GameEntity>? outEntities, Banner? commandBanner)
    {
        if (scene == null || outEntities == null)
            return;

        switch (type)
        {
            case CampType.Field:
                if (!PlaceCenteredPrefab(scene, in center, FieldCampPrefabMesh, FieldCampPrefabScale, outEntities))
                {
                    PlaceCommandTent(scene, in center, BigTentScale, commandBanner, outEntities);
                    PlaceTentRing(
                        scene, in center,
                        CampLayoutMath.ScaledTentCount(troops, FieldMinTents, FieldMaxTents),
                        FieldTentRadius, outEntities);
                }
                break;

            case CampType.Fortified:
                if (PlaceCenteredPrefab(scene, in center, FieldCampPrefabMesh, FieldCampPrefabScale, outEntities))
                {
                    // Prefab camp stands; ring it. The bespoke palisade first, vanilla barricades
                    // only when that mesh is missing too.
                    if (!PlaceCenteredPrefab(scene, in center, PalisadeRingMesh, PalisadeRingScale, outEntities))
                        PlaceBarricadeRing(scene, in center, FortifiedBarricadeCount, FortifiedBarricadeRadius, outEntities);
                }
                else
                {
                    PlaceCommandTent(scene, in center, BigTentScale, commandBanner, outEntities);
                    PlaceTentRing(
                        scene, in center,
                        CampLayoutMath.ScaledTentCount(troops, FortifiedMinTents, FortifiedMaxTents),
                        FortifiedTentRadius, outEntities);
                    PlaceBarricadeRing(scene, in center, FortifiedBarricadeCount, FortifiedBarricadeRadius, outEntities);
                }
                break;

            case CampType.Lookout:
                PlaceCommandTent(scene, in center, BigTentScale, commandBanner, outEntities);
                break;

            case CampType.Ambush:
                break;
        }
    }

    /// <summary>Removes every entity in the list and unregisters their banner cloths.</summary>
    internal static void Remove(List<GameEntity>? entities)
    {
        if (entities == null)
            return;

        for (int i = 0; i < entities.Count; i++)
        {
            GameEntity entity = entities[i];
            if (entity != null)
                _bannerCloths.RemoveAll(kv => kv.Key == entity);
            try
            {
                entity?.Remove(EntityRemoveReason);
            }
            catch
            {
                // Already gone with its scene (save load, map screen teardown); nothing to release.
            }
        }
        entities.Clear();
    }

    // The granular placement helpers below are internal (not private) because RefugeVisualService
    // composes its own bigger layout from them: the Refuge prefabs use different meshes and scales,
    // and its fallback rings carry per-tent scaling that the camp-level Place entry does not.
    internal static void PlaceCommandTent(
        Scene scene, in Vec3 center, float scale, Banner? banner, List<GameEntity> outEntities)
    {
        // The command tent sits slightly forward of center so the tent ring reads as around it.
        float x = center.x;
        float y = center.y + BigTentForwardOffset;

        if (banner != null)
        {
            PlaceTentWithBanner(scene, x, y, center.z, scale, banner, outEntities);
            return;
        }

        MatrixFrame frame = MatrixFrame.Identity;
        frame.origin = new Vec3(x, y, TerrainHeight(x, y, center.z), -1f);
        frame.rotation.ApplyScaleLocal(scale);
        PlaceMesh(scene, TentMeshName, in frame, outEntities);
    }

    internal static void PlaceTentWithBanner(
        Scene scene, float x, float y, float zFallback, float tentScale, Banner banner,
        List<GameEntity> outEntities)
    {
        try
        {
            MetaMesh? tentMesh = MetaMesh.GetCopy(TentMeshName, showErrors: false, mayReturnNull: true);
            if ((NativeObject?)tentMesh == null)
                return;

            GameEntity entity = GameEntity.CreateEmpty(scene);
            if (entity == null)
                return;
            entity.AddMultiMesh(tentMesh);

            MetaMesh? bannerMesh = MobilePartyVisual.GetBannerOfCharacter(banner, BannerMeshName);
            if ((NativeObject?)bannerMesh != null)
            {
                // The banner is a child mesh of the tent entity, so its frame is local: raised a
                // touch, turned side-on, and counter-scaled so the flag stays flag-sized however
                // big the tent is drawn.
                MatrixFrame bannerFrame = MatrixFrame.Identity;
                bannerFrame.origin.z += 0.1f;
                bannerFrame.rotation.RotateAboutUp((float)Math.PI / 2f);
                bannerFrame.rotation.ApplyScaleLocal(tentScale > 0f ? 0.16f / tentScale : 0.16f);
                bannerMesh.Frame = bannerFrame;

                // AddMultiMesh may or may not spawn a cloth component (the mesh decides). Diffing
                // the component count before/after is the only way to find OUR cloth rather than
                // one belonging to another mesh on the entity.
                int clothsBefore = entity.GetComponentCount(GameEntity.ComponentType.ClothSimulator);
                entity.AddMultiMesh(bannerMesh);
                if (entity.GetComponentCount(GameEntity.ComponentType.ClothSimulator) > clothsBefore)
                {
                    GameEntityComponent component =
                        entity.GetComponentAtIndex(clothsBefore, GameEntity.ComponentType.ClothSimulator);
                    if (component is ClothSimulatorComponent cloth)
                    {
                        try
                        {
                            cloth.SetForcedWind(FlagWindDir * FlagWindStrength, false);
                        }
                        catch
                        {
                            // Wind is cosmetic; a limp flag beats a dropped tent.
                        }
                        _bannerCloths.Add(new KeyValuePair<GameEntity, ClothSimulatorComponent>(entity, cloth));
                    }
                }
            }

            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = new Vec3(x, y, TerrainHeight(x, y, zFallback), -1f);
            frame.rotation.ApplyScaleLocal(tentScale);
            entity.SetGlobalFrame(in frame);
            outEntities.Add(entity);
        }
        catch
        {
            // Any native failure here means this one tent is skipped; the rest of the camp stands.
        }
    }

    internal static bool PlaceCenteredPrefab(
        Scene scene, in Vec3 center, string meshName, float scale, List<GameEntity> outEntities)
    {
        try
        {
            MetaMesh? mesh = MetaMesh.GetCopy(meshName, showErrors: false, mayReturnNull: true);
            if ((NativeObject?)mesh == null)
                return false;

            GameEntity entity = GameEntity.CreateEmpty(scene);
            if (entity == null)
                return false;

            entity.AddMultiMesh(mesh);
            MatrixFrame frame = MatrixFrame.Identity;
            frame.rotation.ApplyScaleLocal(scale);
            frame.origin = new Vec3(center.x, center.y, TerrainHeight(center.x, center.y, center.z), -1f);
            entity.SetGlobalFrame(in frame);
            outEntities.Add(entity);
            return true;
        }
        catch
        {
            // Treat a native failure exactly like a missing mesh: the caller runs the fallback.
            return false;
        }
    }

    private static void PlaceTentRing(
        Scene scene, in Vec3 center, int count, float radius, List<GameEntity> outEntities)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = CampLayoutMath.TentSlotAngle(i, count);
            float distance = CampLayoutMath.TentSlotDistance(i, radius);
            float x = center.x + distance * (float)Math.Cos(angle);
            float y = center.y + distance * (float)Math.Sin(angle);

            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = new Vec3(x, y, TerrainHeight(x, y, center.z), -1f);
            frame.rotation.RotateAboutUp(CampLayoutMath.TentSlotFacing(i, count));
            PlaceMesh(scene, TentMeshName, in frame, outEntities);
        }
    }

    internal static void PlaceBarricadeRing(
        Scene scene, in Vec3 center, int count, float radius, List<GameEntity> outEntities)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = CampLayoutMath.BarricadeSlotAngle(i, count);
            float distance = CampLayoutMath.BarricadeSlotDistance(i, radius);
            float x = center.x + distance * (float)Math.Cos(angle);
            float y = center.y + distance * (float)Math.Sin(angle);

            MatrixFrame frame = MatrixFrame.Identity;
            frame.origin = new Vec3(x, y, TerrainHeight(x, y, center.z), -1f);
            // Barricades face the camp center (angle + pi), evenly spaced, no jitter: a defensive
            // ring should read as built, where the tent ring reads as pitched.
            frame.rotation.RotateAboutUp(angle + (float)Math.PI);
            PlaceMesh(scene, BarricadeMeshName, in frame, outEntities);
        }
    }

    internal static void PlaceMesh(Scene scene, string meshName, in MatrixFrame frame, List<GameEntity> outEntities)
    {
        try
        {
            MetaMesh? mesh = MetaMesh.GetCopy(meshName, showErrors: false, mayReturnNull: true);
            if ((NativeObject?)mesh == null)
                return;

            GameEntity entity = GameEntity.CreateEmpty(scene);
            if (entity == null)
                return;

            entity.AddMultiMesh(mesh);
            entity.SetGlobalFrame(in frame);
            outEntities.Add(entity);
        }
        catch
        {
            // One missing ring piece is invisible in play; a throw here would kill the whole camp.
        }
    }

    /// <summary>Snaps a layout point to the map terrain so tents sit on slopes, not in them.</summary>
    internal static float TerrainHeight(float x, float y, float fallback)
    {
        float height = fallback;
        try
        {
            var point = new CampaignVec2(new Vec2(x, y), isOnLand: true);
            Campaign.Current?.MapSceneWrapper?.GetHeightAtPoint(point, ref height);
        }
        catch
        {
            height = fallback;
        }
        return height;
    }
}

/// <summary>
/// The deterministic layout maths, split from the builder so it is testable without any engine
/// type. The sin-hash noise is seeded purely by slot index, which is what makes a camp's layout
/// stable across re-shows and save loads without storing anything.
/// </summary>
internal static class CampLayoutMath
{
    internal const int TroopsPerSmallTent = 8;

    private const float TwoPi = (float)(Math.PI * 2.0);

    /// <summary>Deterministic pseudo-random in [0, 1): the classic sin-hash, seeded by index only.</summary>
    internal static float Noise(int i)
    {
        double value = Math.Sin(i * 12.9898) * 43758.5453;
        return (float)(value - Math.Floor(value));
    }

    /// <summary>One small tent per <see cref="TroopsPerSmallTent"/> troops, clamped to [min, max].</summary>
    internal static int ScaledTentCount(int troops, int min, int max)
    {
        int tents = troops / TroopsPerSmallTent;
        if (tents < min)
            return min;
        return tents > max ? max : tents;
    }

    /// <summary>Slot angle around the ring, jittered up to half a radian so the ring looks pitched.</summary>
    internal static float TentSlotAngle(int i, int count)
        => TwoPi * i / count + (Noise(i) - 0.5f) * 1f;

    /// <summary>Slot distance from center, jittered in [0.65, 1.25] of the base radius.</summary>
    internal static float TentSlotDistance(int i, float radius)
        => radius * (0.65f + Noise(i + 101) * 0.6f);

    /// <summary>Tent facing: roughly back toward center (slot angle + pi), 0.4 rad of wobble.</summary>
    internal static float TentSlotFacing(int i, int count)
        => TentSlotAngle(i, count) + (float)Math.PI + (Noise(i + 202) - 0.5f) * 0.8f;

    /// <summary>Barricade slots are evenly spaced; the defensive ring carries no angular jitter.</summary>
    internal static float BarricadeSlotAngle(int i, int count)
        => TwoPi * i / count;

    /// <summary>Barricade distance jitters only in [0.9, 1.1] of the radius to keep the ring closed.</summary>
    internal static float BarricadeSlotDistance(int i, float radius)
        => radius * (0.9f + Noise(i + 303) * 0.2f);
}
