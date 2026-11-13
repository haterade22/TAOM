using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The opening split of the defenders (Mike, 2026-11-12). Before the first contact of a wave, <c>TacticDefendCastle</c> sends every infantry formation to the gate: with no lane active it defends the
/// lanes that have a gate (TacticDefendCastle.cs:1005-1016), and it consolidates the infantry to that one lane (CarryOutDefense, :427 onward). So the split takes the defenders off AI control in four
/// groups and orders each with the shapes of vanilla's ready order: a <c>TacticalPosition</c>'s position, direction and width for the wall and the gate (BehaviorDefendCastleKeyPosition), an
/// archer position with Scatter, fire at will and a wide form for the archers (BehaviorShootFromCastleWalls). Scatter joins every defender archer point within 40 m of the order position on each
/// ground axis, at any height (the <c>strategic_archer_point</c> prefab sets <c>_distanceToCheck</c> 40 and <c>_ignoreHeight</c>), with or without AI control (ArrangementOrder.TickOccasionally). A
/// formation off AI control keeps its units: ConsolidateFormations skips it, and a tactic's transfer out of it is refused (Formation.TransferUnitsAux, not splittable by AI while the player is general).
/// It can still gain units, since consolidation ranks it first as a target. The director hands the groups back at the first contact, and the tactic then defends the lanes from where they stand. The
/// engine also hands them back at the stalemate charge, at a new body for the lord and at the lord's fall (Team.DelegateCommandToAI); the director splits again after the last two. Main thread.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    // The gate keeps the foot formation the garrison spawns into, and the lower wall the ranged one; the wall and the high walls take two formations nothing else fills before the tactic splits.
    private const FormationClass GateClass = FormationClass.Infantry;
    private const FormationClass WallClass = FormationClass.HeavyInfantry;
    private const FormationClass LowClass = FormationClass.Ranged;
    private const FormationClass HighClass = FormationClass.HorseArcher;

    // The span the wall group's line may take at most, so a long wall does not draw the line thinner than one rank.
    private const float MaxWallWidth = 60f;
    private const float DefaultWidth = 10f;

    // A held group whose movement order still points within this distance of the split's place was not ordered by the player.
    private const float OwnOrderTolerance = 2f;

    private readonly struct SplitPlace
    {
        public SplitPlace(WorldPosition position, Vec2 direction, float width, FormationAI.BehaviorSide side)
        {
            Position = position;
            Direction = direction;
            Width = width;
            Side = side;
        }

        public WorldPosition Position { get; }
        public Vec2 Direction { get; }
        public float Width { get; }
        public FormationAI.BehaviorSide Side { get; }
    }

    private readonly List<(Formation Formation, Vec2 At, string Name, bool Archers)> _held = new();

    // The soldiers this hold has placed. A split again inside the hold keeps them where they are and places only the others, so a pack joins the group below its share.
    private readonly HashSet<Agent> _placed = new();

    public LdSplitResult SplitDefenders(SplitShares shares)
    {
        var team = _mission.DefenderTeam;
        if (team == null)
            return Refuse("the mission has no defender team");

        var gate = GatePlace();
        var wall = WallPlace(gate?.Side);
        var low = ArcherPlace(false, wall);
        var high = ArcherPlace(true, wall);
        var splitFoot = wall != null && gate != null;
        var splitArchers = low != null && high != null;
        if (!splitFoot && !splitArchers)
            return Refuse($"the scene gives no pair of places (wall {Found(wall)}, gate {Found(gate)}, lower archers {Found(low)}, high archers {Found(high)})");

        var foot = new List<Agent>();
        var archers = new List<Agent>();
        var main = _mission.MainAgent;
        var posts = team.GetFormation(PostsClass);
        foreach (var agent in team.ActiveAgents)
        {
            if (!agent.IsHuman || !agent.IsActive() || agent.IsRunningAway || agent == main || agent.HasMount || agent.Formation == posts || _postedByAgent.ContainsKey(agent))
                continue;
            (agent.IsRangedCached ? archers : foot).Add(agent);
        }

        var before = _held.Select(h => h.Formation).ToList();
        _held.Clear();
        int wallCount = 0, gateCount = 0, lowCount = 0, highCount = 0;
        if (splitFoot)
        {
            wallCount = Divide(foot, shares.WallFoot, wall!.Value, team.GetFormation(WallClass), team.GetFormation(GateClass));
            gateCount = foot.Count - wallCount;
            Order(team.GetFormation(WallClass), wall.Value, "wall", false);
            Order(team.GetFormation(GateClass), gate!.Value, "gate", false);
        }

        if (splitArchers)
        {
            lowCount = Divide(archers, shares.LowArchers, low!.Value, team.GetFormation(LowClass), team.GetFormation(HighClass));
            highCount = archers.Count - lowCount;
            Order(team.GetFormation(LowClass), low.Value, "low", true);
            Order(team.GetFormation(HighClass), high!.Value, "high", true);
        }

        HandBack(before);
        return new LdSplitResult("", wallCount, gateCount, lowCount, highCount, PointOf(wall), PointOf(gate), PointOf(low), PointOf(high));
    }

    // A refused split holds nothing, so the groups the last split still held go back to the AI, as at a release.
    private LdSplitResult Refuse(string problem)
    {
        ReleaseSplit();
        return LdSplitResult.Refused(problem);
    }

    // A formation the last split held that this one does not hold again goes back to the AI, so a pair a re-split could not place is not left off AI control with nothing to release it.
    private void HandBack(List<Formation> before)
    {
        foreach (var formation in before)
        {
            if (!formation.IsAIControlled && !_held.Any(h => ReferenceEquals(h.Formation, formation)))
                formation.SetControlledByAI(true);
        }
    }

    private static string Found(SplitPlace? place) => place == null ? "none" : "found";

    private static LdPoint? PointOf(SplitPlace? place) => place is { } p ? Point(p.Position.GetGroundVec3()) : null;

    // A soldier this hold placed and who still stands in one of the pair's groups stays there. The others fill the first group up to its share, nearest its place first, so the fewest walk
    // across the fortress, and the rest go to the second. On a fresh hold nobody is placed yet, so every soldier is divided by distance.
    private int Divide(List<Agent> agents, float share, SplitPlace first, Formation firstFormation, Formation secondFormation)
    {
        var inFirst = agents.Count(a => _placed.Contains(a) && ReferenceEquals(a.Formation, firstFormation));
        var at = first.Position.AsVec2;
        var others = agents.Where(a => !_placed.Contains(a) || !(ReferenceEquals(a.Formation, firstFormation) || ReferenceEquals(a.Formation, secondFormation)))
            .OrderBy(a => a.Position.AsVec2.DistanceSquared(at)).ToList();
        var toFirst = Math.Max(0, DefenderSplit.FirstGroup(agents.Count, share) - inFirst);
        for (var i = 0; i < others.Count; i++)
        {
            var target = i < toFirst ? firstFormation : secondFormation;
            if (!ReferenceEquals(others[i].Formation, target))
                others[i].Formation = target;
            _placed.Add(others[i]);
        }

        return inFirst + Math.Min(toFirst, others.Count);
    }

    // Off AI control first: an AI formation's behaviour sends its own order again at its next occasional tick (Formation.Tick calls AI.Tick, and BehaviorDefendCastleKeyPosition and
    // BehaviorShootFromCastleWalls order from TickOccasionally). The shapes are those of vanilla's ready order; vanilla also holds a wait position before contact and forms a shield wall later, and the split does neither.
    private void Order(Formation formation, SplitPlace place, string name, bool archers)
    {
        if (formation.CountOfUnits == 0)
            return;

        formation.SetControlledByAI(false);
        formation.AI.Side = place.Side;
        formation.SetMovementOrder(MovementOrder.MovementOrderMove(place.Position));
        formation.SetFacingOrder(FacingOrder.FacingOrderLookAtDirection(place.Direction));
        if (archers)
        {
            formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderScatter);
            formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
            formation.SetFormOrder(FormOrder.FormOrderWide);
        }
        else
        {
            formation.SetArrangementOrder(ArrangementOrder.ArrangementOrderLine);
            formation.SetFormOrder(FormOrder.FormOrderCustom(place.Width));
        }

        _held.Add((formation, place.Position.AsVec2, name, archers));
    }

    // The outer gate (BreakHiddenGates keeps the visible gates, the outer one first): where vanilla's gate lane defenders stand ready, its MiddlePosition, else its MiddleFrame.
    private SplitPlace? GatePlace()
    {
        var gate = _gates.FirstOrDefault();
        return gate == null ? null : KeyPlace(gate.MiddlePosition, gate.MiddleFrame, gate.DefenseSide);
    }

    // The wall's lane segments (a set DefenseSide, not the gate's lane). One segment gives its own place. Two or more give the middle of the two farthest apart, facing their mean direction, with
    // the span between them as the width, so one line covers every lane of that wall (at Helm's Deep, both Deeping Wall lanes). A middle with no navmesh falls back to the segment nearest it.
    private SplitPlace? WallPlace(FormationAI.BehaviorSide? gateSide)
    {
        var segments = _mission.ActiveMissionObjects.FindAllWithType<WallSegment>()
            .Where(w => w.DefenseSide != FormationAI.BehaviorSide.BehaviorSideNotSet && w.DefenseSide != gateSide)
            .Select(w => KeyPlace(w.MiddlePosition, w.MiddleFrame, w.DefenseSide))
            .Where(p => p != null)
            .Select(p => p!.Value)
            .ToList();
        if (segments.Count == 0)
            return null;
        if (segments.Count == 1)
            return segments[0];

        var pair = (from a in segments from b in segments select (A: a, B: b)).OrderByDescending(p => p.A.Position.AsVec2.DistanceSquared(p.B.Position.AsVec2)).First();
        var middle = (pair.A.Position.GetGroundVec3() + pair.B.Position.GetGroundVec3()) * 0.5f;
        var facing = pair.A.Direction + pair.B.Direction;
        facing = facing.LengthSquared > 1e-4f ? facing.Normalized() : pair.A.Direction;
        var width = Math.Min(MaxWallWidth, pair.A.Position.AsVec2.Distance(pair.B.Position.AsVec2) + (pair.A.Width + pair.B.Width) * 0.5f);
        var line = new WorldPosition(_mission.Scene, middle);
        if (Usable(line))
            return new SplitPlace(line, facing, width, pair.A.Side);

        var nearest = segments.OrderBy(s => s.Position.AsVec2.DistanceSquared(middle.AsVec2)).First();
        return new SplitPlace(nearest.Position, nearest.Direction, nearest.Width, nearest.Side);
    }

    private SplitPlace? KeyPlace(TacticalPosition? tactical, WorldFrame frame, FormationAI.BehaviorSide side)
    {
        if (tactical != null && Usable(tactical.Position))
            return new SplitPlace(tactical.Position, tactical.Direction, tactical.Width > 0f ? tactical.Width : DefaultWidth, side);

        var origin = frame.Origin;
        return Usable(origin) ? new SplitPlace(origin, frame.Rotation.f.AsVec2.Normalized(), DefaultWidth, side) : null;
    }

    // A place must have a navmesh and lie inside the boundary: otherwise MovementOrder.GetPosition substitutes a point that follows the formation (CreateNewOrderWorldPositionMT), and the release would
    // read the group as one the player ordered elsewhere.
    private bool Usable(WorldPosition position) => position.IsValid && position.GetNavMesh() != UIntPtr.Zero && _mission.IsPositionInsideBoundaries(position.AsVec2);

    // The defenders' archer positions (TeamAISiegeDefender.ArcherPositions) on the walls below the high line, or on the high walls above it. The lower one nearest the wall's place, so the lower archers
    // stand over the wall the infantry hold; the high one nearest it too, which keeps a scene with several high positions on the side the attack comes from.
    private SplitPlace? ArcherPlace(bool high, SplitPlace? wall)
    {
        var positions = (_mission.DefenderTeam?.TeamAI as TeamAISiegeDefender)?.ArcherPositions;
        if (positions == null)
            return null;

        var picked = positions.Where(p => p.Entity != null && (p.Entity.GlobalPosition.z >= _highZ) == high).ToList();
        if (picked.Count == 0)
            return null;

        var archer = wall is { } w ? picked.OrderBy(p => p.Entity.GlobalPosition.AsVec2.DistanceSquared(w.Position.AsVec2)).First() : picked[0];
        var side = archer.GetArcherPositionClosestSide();
        var tactical = archer.TacticalArcherPosition;
        if (tactical != null && Usable(tactical.Position))
            return new SplitPlace(tactical.Position, tactical.Direction, tactical.Width, side);

        var frame = archer.Entity.GetGlobalFrame();
        var at = new WorldPosition(_mission.Scene, frame.origin);
        return Usable(at) ? new SplitPlace(at, frame.rotation.f.AsVec2.Normalized(), DefaultWidth, side) : null;
    }

    // The siege AI's own count of attackers at the walls, left, middle and right (SiegeQuerySystem.LocateAttackers): within 15 m of a side's defender origin and 3 m of its height, or on an
    // inside navmesh face (a face id that ends in 1, a docked tower's floors among them) in the side's region; refreshed every 5 s. With no right_defender_origin tag, as at Helm's Deep, the
    // right origin is the middle lane's, so the right count holds the gate. A lane's defense level is no contact: DetermineLaneState raises it when an attacker formation takes one of the
    // lane's engines, within a second of the wave's spawn.
    public IReadOnlyList<int> ReadCloseAttackers()
    {
        var query = TeamAISiegeComponent.QuerySystem;
        if (query == null)
            return Array.Empty<int>();

        // A new array each read: the director keeps the hold's read and compares later reads with it.
        return new[] { query.LeftCloseAttackerCount, query.MiddleCloseAttackerCount, query.RightCloseAttackerCount };
    }

    // QueryData.Expire sets each count's expiry to 0, so the next read runs LocateAttackers again (SiegeQuerySystem.Expire).
    public void RefreshCloseAttackers() => TeamAISiegeComponent.QuerySystem?.Expire();

    public LdSplitRelease ReleaseSplit()
    {
        int released = 0, kept = 0;
        foreach (var (formation, at, _, _) in _held)
        {
            if (formation.IsAIControlled || formation.CountOfUnits == 0)
                continue;

            ref readonly var order = ref formation.GetReadonlyMovementOrderReference();
            if (order.OrderEnum != MovementOrder.MovementOrderEnum.Move || order.GetPosition(formation).Distance(at) > OwnOrderTolerance)
            {
                kept++;
                continue;
            }

            formation.SetControlledByAI(true);
            released++;
        }

        _held.Clear();
        _placed.Clear();
        return new LdSplitRelease(released, kept);
    }

    public IReadOnlyList<LdSplitGroup> ReadSplit()
    {
        var groups = new List<LdSplitGroup>();
        foreach (var (formation, at, name, archers) in _held)
        {
            var distances = new List<float>();
            var high = 0;
            var main = _mission.MainAgent;
            formation.ApplyActionOnEachUnit(agent =>
            {
                if (!agent.IsActive() || agent == main)
                    return;
                distances.Add(agent.Position.AsVec2.Distance(at));
                if (agent.Position.z >= _highZ)
                    high++;
            });

            distances.Sort();
            var median = distances.Count == 0 ? float.NaN : distances[distances.Count / 2];
            groups.Add(new LdSplitGroup(name, distances.Count, median, high, archers));
        }

        return groups;
    }

    private void ClearSplit()
    {
        _held.Clear();
        _placed.Clear();
    }
}
