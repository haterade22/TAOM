using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Validation;

namespace TAOM.Features.CreatureSiegeRole.Domain;

/// <summary>Why the creature siege role is, or is not, active for a mission. Everything but <see cref="Active"/> is "inert".</summary>
public enum ActivationVerdict
{
    Active,
    NotSiege,
    SallyOutOrRelief,
    ClientOrReplay,
    Disabled,
    NoCreatureRace,
    NoOuterGate,
}

/// <summary>
/// The decisions of the creature siege role (docs/features/creature-siege-role.md), as pure functions: no engine types and
/// no state. The service reads the engine through adapters and asks these what to do; the hooks the game models call reduce
/// their engine arguments to primitives and ask these too. Every float the engine hands in is gated as a positive
/// requirement, so a NaN fails the gate and never becomes an owned verdict
/// (csharp-architecture.md "Engine-Float Decision Gates"). The role, slot and anchor rules are in
/// <c>CreatureSiegeRules.Roles.cs</c>.
/// </summary>
public static partial class CreatureSiegeRules
{
    /// <summary>The reconcile runs once per this many seconds of mission time.</summary>
    public const float PassStrideSeconds = 0.5f;

    /// <summary>
    /// Face ids one creature is excluded from. Native keeps one set per exact ordered id list and marks each face with one
    /// byte. Bit 0 is the engine's no-exclusion bit (the re-mark at 0x401B20 starts from index 1) and bit 7 is the scene's
    /// base navmesh boundary, so bits 1 to 6 give six sets that are clean for certain (TaleWorlds.Native.dll, v1.5.4 0x3FD4A4,
    /// 0x4DD0B5 and 0x401B20). Whether the registry's entry 0 is pre-seeded is unverified, so 6 is safe either way. Never
    /// raise this: a seventh set would rewrite the base bit across the scene.
    /// </summary>
    public const int MaxExcludedFaceGroups = 6;

    /// <summary>A gate has to stand open this long, unbroken, before a creature may count on it.</summary>
    public const float GateOpenDebounceSeconds = 2f;

    /// <summary>A battering ram counts as at work this long after a formation was last assigned to it.</summary>
    public const float RamActiveGraceSeconds = 20f;

    /// <summary>Distance from a gate at which a striking creature stands (a hill troll's capsule radius 1.2 m plus its reach).</summary>
    public const float StrikeStandOff = 2.2f;

    /// <summary>Lateral spacing of the strike slots in a row.</summary>
    public const float StrikeLateral = 2.8f;

    /// <summary>How far each further row of slots sits behind the one in front of it.</summary>
    public const float SlotRowDepth = 2.8f;

    /// <summary>Distance from the gate of the stand-off slots, which keep a creature out of the ram's way.</summary>
    public const float StandOffDistance = 10f;

    /// <summary>Lateral spacing of the stand-off slots in a row.</summary>
    public const float StandOffLateral = 4f;

    /// <summary>Lateral spacing of a hold's slots around its anchor.</summary>
    public const float HoldLateral = 2.8f;

    /// <summary>Slots per row. The columns are the centre, then one to each side.</summary>
    public const int SlotColumns = 3;

    /// <summary>Rows of slots before the service starts reusing them.</summary>
    public const int SlotRows = 4;

    public const int SlotsPerRole = SlotColumns * SlotRows;

    /// <summary>A creature on the inner side of the outer gate within this many metres of it counts as through the gate.</summary>
    public const float PastGateRadius = 15f;

    /// <summary>A creature on the inner side of the outer gate counts as through it only within this many metres of the gate's axis.</summary>
    public const float PastGateLateral = 4f;

    /// <summary>
    /// A gate's <c>middle_pos</c> farther than this (in XY) from the gate itself is not an anchor (mordor_town_minas 119 m,
    /// Osgiliath 88 to 93 m, Dale 27 m). Where the outer gate's is dropped, the hold falls to the next candidate: in
    /// mordor_town_minas and Dale that is the inner gate's middle, so the trolls hold the inner gate there by design.
    /// </summary>
    public const float MaxMiddleAnchorDistance = 15f;

    /// <summary>A creature that found no place for a role tries that role again after this many passes (2 s), or at once if its role decision changes.</summary>
    public const int PlaceRetryPasses = 4;

    /// <summary>A hold anchor's ground may sit this far above or below its gate's base before it counts as a wall top.</summary>
    public const float AnchorHeightTolerance = 4f;

    /// <summary>The computed hold anchor sits this far behind the outer gate.</summary>
    public const float InsideAnchorDistance = 6f;

    // A race table holds a few dozen races; an id past this is poisoned input, and would size a huge array.
    private const int MaxRaceId = 1023;

    // A gate frame's axis shorter than this carries no direction (a zero-scale frame).
    private const float MinAxisLength = 1e-3f;

    // --- activation -----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether the role is active for a mission, and if not, the first reason in the order of cost. Active only in a siege
    /// battle that is no sally-out or relief force (both carry a <c>SallyOutMissionController</c>), on a peer that runs the AI
    /// (not a network client or replay), with the MCM switch on, a creature race to look for, and an outer gate to strike.
    /// </summary>
    public static ActivationVerdict Activate(bool isSiegeBattle, bool hasSallyOutController, bool isClientOrReplay,
        bool roleEnabled, bool hasCreatureRaces, bool hasOuterGate)
    {
        if (!isSiegeBattle) return ActivationVerdict.NotSiege;
        if (hasSallyOutController) return ActivationVerdict.SallyOutOrRelief;
        if (isClientOrReplay) return ActivationVerdict.ClientOrReplay;
        if (!roleEnabled) return ActivationVerdict.Disabled;
        if (!hasCreatureRaces) return ActivationVerdict.NoCreatureRace;
        if (!hasOuterGate) return ActivationVerdict.NoOuterGate;
        return ActivationVerdict.Active;
    }

    /// <summary>
    /// The race ids to look for, as a lookup array indexed by race id: <c>mask[race]</c> is true for a creature race. A fresh
    /// array every call, so the snapshot that keeps it is the only holder. Negative ids and ids no race table could hold are
    /// ignored; null or no ids gives an empty mask.
    /// </summary>
    public static bool[] BuildRaceMask(IReadOnlyList<int>? raceIds)
    {
        if (raceIds == null) return Array.Empty<bool>();

        var highest = -1;
        foreach (var id in raceIds)
        {
            if (id >= 0 && id <= MaxRaceId && id > highest)
                highest = id;
        }

        if (highest < 0) return Array.Empty<bool>();

        var mask = new bool[highest + 1];
        foreach (var id in raceIds)
        {
            if (id >= 0 && id <= MaxRaceId)
                mask[id] = true;
        }

        return mask;
    }

    // --- the detachment cost ----------------------------------------------------------------------------------------------

    /// <summary>
    /// The cost the engine multiplies into a creature's distance to a standing point: +Infinity, so a creature is never the
    /// lowest-cost candidate for a ram, ladder, tower, lever, stone pile or siege engine. The engine's selection starts at
    /// <c>float.MaxValue</c> and takes a candidate only on a strict greater-than, so +Infinity loses, and so does the NaN of
    /// 0 x +Infinity. <c>float.MaxValue</c> itself would not do: the product stays under it for any distance under 1 m. Any
    /// other agent keeps the engine's own value, a NaN included.
    /// </summary>
    public static float DetachmentCost(bool isCreature, float baseCost) => isCreature ? float.PositiveInfinity : baseCost;

    // --- gate damage ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A creature's melee blow on a gate, multiplied (the caller has already checked it is a hit on one of the mission's
    /// gates). The blow is handed back unchanged unless every condition holds: no friendly fire, not a mount's, not a missile's, by a creature, from a finite
    /// positive damage and a finite multiplier of at least 1, and the product is finite (a runaway product would hand the
    /// engine an infinity).
    /// </summary>
    public static float ScaleGateDamage(float multiplier, float damage, bool isFriendlyFire,
        bool isAttackerMount, bool isMissile, bool attackerIsCreature)
    {
        if (isFriendlyFire || isAttackerMount || isMissile || !attackerIsCreature) return damage;
        if (!(damage > 0f) || !FiniteFloatValidator.IsFinite(damage)) return damage;
        if (!FiniteFloatValidator.IsFinite(multiplier) || !(multiplier >= 1f)) return damage;

        var scaled = damage * multiplier;
        return FiniteFloatValidator.IsFinite(scaled) ? scaled : damage;
    }

    // --- ladders and towers: the exclusion ids -------------------------------------------------------------------------

    /// <summary>
    /// The navmesh face ids a creature is excluded from, in the one fixed order the engine's set registry needs: every
    /// tower's ground entrance (<c>DynamicStart + 2</c>) first, then the distinct ladder ids ascending, then the towers'
    /// bridges, which the cap drops first and without a warning. Ids of 0 or less are scene face groups or unset (a tower with no navmesh prefab has start 0, and +2 would be
    /// the scene's own face group 2), so they are left out and named. Duplicates count once, at their first tier. Past
    /// <paramref name="cap"/> the later ids are dropped and named, never the earlier ones.
    /// </summary>
    public static ExclusionPlan BuildExclusionIds(IReadOnlyList<TowerFaces>? towers, IReadOnlyList<int>? ladderIds, int cap)
    {
        var ids = new List<int>();
        var skipped = new List<SkippedFace>();
        var dropped = new List<int>();
        var droppedBridges = 0;
        var seen = new HashSet<int>();

        void Offer(int id, ExclusionTier tier)
        {
            if (id <= 0)
            {
                skipped.Add(new SkippedFace(tier, id));
                return;
            }

            if (!seen.Add(id)) return;

            if (ids.Count < cap)
            {
                ids.Add(id);
            }
            else
            {
                dropped.Add(id);
                if (tier == ExclusionTier.TowerBridge)
                    droppedBridges++;
            }
        }

        if (towers != null)
        {
            foreach (var tower in towers)
            {
                if (tower.DynamicStart <= 0 || tower.DynamicStart > int.MaxValue - 2)
                    skipped.Add(new SkippedFace(ExclusionTier.TowerEntrance, tower.DynamicStart));
                else
                    Offer(tower.DynamicStart + 2, ExclusionTier.TowerEntrance);
            }
        }

        if (ladderIds != null)
        {
            foreach (var id in ladderIds.OrderBy(i => i))
                Offer(id, ExclusionTier.Ladder);
        }

        if (towers != null)
        {
            foreach (var tower in towers)
                Offer(tower.BridgeId, ExclusionTier.TowerBridge);
        }

        return new ExclusionPlan(ids, skipped, dropped, droppedBridges);
    }

    // --- gates --------------------------------------------------------------------------------------------------------

    /// <summary>
    /// The gate a role uses, from the candidates the adapter listed with the engine's own choice first. A gate is usable
    /// when it is visible and not disabled; the first usable candidate wins. TAOM scenes hide inner gates and duplicate
    /// them, and vanilla takes the first tagged gate whether it is hidden or not, so a hidden canonical gate is stepped over.
    /// Null when no candidate is usable.
    /// </summary>
    public static SiegeGateReading? ResolveGate(IReadOnlyList<SiegeGateReading>? candidates)
    {
        if (candidates == null) return null;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate != null && candidate.IsUsable)
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Advances a gate's record by one observation. Destruction is latched for good. An open door keeps the time it was
    /// first seen open and loses it the moment the door shuts, so a gate a lever flaps never accumulates two seconds. A
    /// non-finite clock starts no stretch: the open stretch begins at the first real time.
    /// </summary>
    public static GateTrack Track(GateTrack previous, bool destroyedNow, bool openNow, float now)
    {
        if (previous.Destroyed || destroyedNow) return new GateTrack(true, float.NaN);
        if (!openNow) return GateTrack.Closed;

        var since = FiniteFloatValidator.IsFinite(previous.OpenSince) ? previous.OpenSince : now;
        return new GateTrack(false, since);
    }

    /// <summary>
    /// Whether a creature may count on the gate being open: destroyed (latched), or open continuously for at least
    /// <see cref="GateOpenDebounceSeconds"/>. A re-closed intact gate is impassable at once. A NaN clock never passes.
    /// </summary>
    public static bool IsPassable(GateTrack track, float now) =>
        track.Destroyed || now - track.OpenSince >= GateOpenDebounceSeconds;

    /// <summary>
    /// Whether a creature is already through the outer gate: its navmesh face id ends in 1 (the engine's own inside test,
    /// <c>InsideCastleNavMeshID</c> being 1), or it stands on the inner side of the gate's forward axis within
    /// <see cref="PastGateRadius"/> of the gate and within <see cref="PastGateLateral"/> of its axis (a creature beside the wall
    /// is not in the doorway). A creature through a gate that has shut behind it skips the outer-gate role.
    /// </summary>
    public static bool IsPastOuterGate(int navigationFaceId, float agentX, float agentY, float gateX, float gateY,
        float forwardX, float forwardY)
    {
        if (navigationFaceId % 10 == 1) return true;
        if (!TryUnit(forwardX, forwardY, out var unitX, out var unitY)) return false;

        var dx = agentX - gateX;
        var dy = agentY - gateY;
        var along = dx * unitX + dy * unitY;
        var lateral = dx * unitY - dy * unitX;
        return along < 0f && dx * dx + dy * dy <= PastGateRadius * PastGateRadius && Math.Abs(lateral) <= PastGateLateral;
    }

    /// <summary>
    /// Whether a battering ram is at work, which is when a creature waits off the gate instead of standing in the ram's way.
    /// The ram is not spent, and agents stand at its points now or a formation was assigned to it within
    /// <see cref="RamActiveGraceSeconds"/>. <paramref name="secondsSinceUsed"/> is NaN for a ram never seen assigned.
    /// </summary>
    public static bool RamWorking(bool present, bool deactivated, int userCount, float secondsSinceUsed)
    {
        if (!present || deactivated) return false;
        return userCount > 0 || secondsSinceUsed <= RamActiveGraceSeconds;
    }

    // The unit vector of a gate frame's axis, or false when the axis is not finite or too short to carry a direction.
    private static bool TryUnit(float x, float y, out float unitX, out float unitY)
    {
        unitX = float.NaN;
        unitY = float.NaN;
        if (!FiniteFloatValidator.IsFinite(x) || !FiniteFloatValidator.IsFinite(y)) return false;

        var lengthSquared = x * x + y * y;
        if (!FiniteFloatValidator.IsFinite(lengthSquared) || !(lengthSquared > MinAxisLength * MinAxisLength)) return false;

        var length = (float)Math.Sqrt(lengthSquared);
        unitX = x / length;
        unitY = y / length;
        return true;
    }
}
