using System;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// A run on a field scene with no walls (#1105 slice 2). No deployment plan is built: each formation is positioned at its side's start place before every batch
/// (<see cref="SetMusterFrame"/>) and each soldier spawns into his own formation, the mechanism the recorded muster places already use for the defenders of a siege.
/// A start place is the mission's recorded one, else a point of the scene's own spawn path (<c>Mission.GetSpawnPathFrame</c>, Mission.cs:4021-4032), and a place
/// with no navmesh, or outside the mission boundary, is no place. Both sides share the path's pivot, so offset 0 is one point for both; a negative offset moves the defender toward the path's start and
/// the attacker toward its end (<c>SpawnPathData.ConvertRelativePathOffsetToPathDistance</c>, and the attacker's path is the inverted one, BattleSpawnPathSelector.cs:36-38).
/// The attackers' second place is a positive attacker offset of three half gaps: one gap beyond the defenders, on their far side. The engine clamps it to the path's
/// start, so a place that ends within half a gap of the defenders is dropped. The director never calls the gate, engine and lane methods on a field run.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    /// <summary>How far each side starts from the spawn path's pivot, in metres along the path, when the mission records no place: the two sides start twice this apart.</summary>
    private const float PathHalfGap = 40f;

    private readonly FieldPlaces? _fieldPlan;
    private FieldPlaces? _fieldStarts;

    private bool IsField => _fieldPlan != null;

    public FieldPlaces? FieldStarts() => _fieldStarts;

    private void ResolveFieldStarts()
    {
        var defenders = RecordedOrPath(_fieldPlan!.Defenders, BattleSideEnum.Defender);
        var attackers = RecordedOrPath(_fieldPlan.Attackers, BattleSideEnum.Attacker);
        var behind = RecordedOrPath(_fieldPlan.AttackersBehind, BattleSideEnum.Attacker, PathHalfGap * 3f);
        if (_fieldPlan.AttackersBehind == null && behind != null && defenders != null && Flat(behind, defenders) < PathHalfGap)
            behind = null;

        _fieldStarts = new FieldPlaces(
            _fieldPlan.Defenders == null ? FacingTheOtherSide(defenders, attackers) : defenders,
            _fieldPlan.Attackers == null ? FacingTheOtherSide(attackers, defenders) : attackers,
            _fieldPlan.AttackersBehind == null ? FacingTheOtherSide(behind, defenders) : behind);
        _logger.LogInfo($"[LongDefeat][Field] attackers' second start: {(_fieldStarts.AttackersBehind is { } b ? At(new Vec3(b.X, b.Y, b.Z)) : "none")}");
    }

    private static float Flat(MusterFrame a, MusterFrame b) => (float)Math.Sqrt((double)(a.X - b.X) * (a.X - b.X) + (double)(a.Y - b.Y) * (a.Y - b.Y));

    private MusterFrame? RecordedOrPath(MusterFrame? recorded, BattleSideEnum side, float pathOffset = -PathHalfGap)
    {
        if (recorded != null)
        {
            if (Standable(new Vec3(recorded.X, recorded.Y, recorded.Z)))
                return recorded;

            _logger.LogWarning($"[LongDefeat][Field] the recorded {side} start {At(new Vec3(recorded.X, recorded.Y, recorded.Z))} has no navmesh or lies outside the mission boundary");
            return null;
        }

        if (!_mission.HasSpawnPath)
        {
            _logger.LogWarning($"[LongDefeat][Field] the scene has no spawn path, and the mission records no {side} start");
            return null;
        }

        var frame = _mission.GetSpawnPathFrame(side, pathOffset);
        if (!frame.IsValid)
        {
            _logger.LogWarning($"[LongDefeat][Field] the scene's spawn path gave no {side} frame");
            return null;
        }

        var at = frame.Origin.GetGroundVec3();
        var look = frame.Rotation.f.AsVec2;
        if (!at.IsValid || !Standable(at) || !MusterFrame.TryCreate(at.x, at.y, at.z, look.x, look.y, out var made))
        {
            _logger.LogWarning($"[LongDefeat][Field] the scene's {side} spawn path frame {At(at)} has no navmesh, lies outside the mission boundary or has no direction");
            return null;
        }

        return made;
    }

    /// <summary>The place turned to look at the other side's place. Unchanged when either is missing or the two coincide: <c>TryCreate</c> refuses a look with no length.</summary>
    private static MusterFrame? FacingTheOtherSide(MusterFrame? from, MusterFrame? to)
    {
        if (from == null || to == null)
            return from;

        return MusterFrame.TryCreate(from.X, from.Y, from.Z, to.X - from.X, to.Y - from.Y, out var turned) ? turned : from;
    }

    /// <summary>A place a side can start at: on the navmesh and inside the mission boundary. A lord who spawns outside the boundary starts the retreat countdown, and a retreat reads as a loss.</summary>
    private bool Standable(Vec3 at) => _mission.Scene.GetNavigationMeshForPosition(in at) != UIntPtr.Zero && _mission.IsPositionInsideBoundaries(at.AsVec2);

    // The lord of a field run spawns alone at his side's start place, as a hero does (SpawnHero): the formation is positioned there first, so the spawn never asks the
    // deployment plan, which a field run does not build, and the given position skips the formation's layout (Mission.cs:4227).
    private Agent SpawnAtStart(BasicCharacterObject player, Team team, Equipment? kit, MusterFrame start)
    {
        var place = LdPlace.From(start);
        var formation = team.GetFormation(FormationClass.Infantry);
        if (!formation.HasBeenPositioned)
            formation.SetPositioning(new TaleWorlds.Engine.WorldPosition(_mission.Scene, new Vec3(place.X, place.Y, place.Z)), FacingOf(place));

        formation.BeginSpawn(1, false);
        try
        {
            return Spawn(player, team, true, FormationClass.Infantry, true, 1, 0, false, kit: kit, place: place);
        }
        finally
        {
            formation.EndSpawn();
        }
    }
}
