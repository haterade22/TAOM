using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Adapters;

/// <summary>
/// The engine work of the Long Defeat fortifications (phase3-plan.md section 4), kept apart from <see cref="ILongDefeatBattlefieldAdapter"/> so the director's other fake stays small. The battlefield adapter
/// implements both, and the run host hands the director the same instance as each (<c>LongDefeatFortWiringTests</c>). Takes and returns domain types only (ADR-007). Main thread. Slice 5 holds the gates, the fort
/// points and the A23 boot line; Slice 6 adds the Elven Volley's reads and its fire, and the posts join with their slice.
/// </summary>
internal interface ILongDefeatFortAdapter
{
    /// <summary>
    /// The visible gates as the field reads them now, in the order of the boot list, read from the gate objects themselves (a fallen gate has left the active list, so never from it). Empty before the boot.
    /// </summary>
    IReadOnlyList<LdGateFacts> ReadGates();

    /// <summary>
    /// A gate level (D18): every visible gate's maximum becomes its vanilla maximum times <paramref name="factor"/>, and a standing gate keeps its share of hit points (<see cref="GateMath.KeepShare"/>); a
    /// broken gate stays broken. Writes only gates, never a ram or a tower (D19). Returns each gate before and after.
    /// </summary>
    LdGateWrite SetGateLevel(float factor);

    /// <summary>
    /// The repair (D17): every visible gate missing at least <see cref="GateMath.MinMissingShare"/> of its maximum is made whole at its vanilla maximum times <paramref name="factor"/>, closed and enabled, by
    /// vanilla's own mission reset. A gate that is whole is left alone, and the hidden gates stay broken. Returns each gate before and after, with what the check reads.
    /// </summary>
    LdGateWrite RepairGates(float factor);

    /// <summary>
    /// How many standing attackers are within <see cref="GateRules.BehindDepth"/> behind a standing visible gate's plane (the inside is the side <paramref name="inside"/> lies on), within the door's width and
    /// <see cref="GateRules.HeightBand"/> of its height, summed over the gates. -1 when no standing gate had a plane to measure (no inside point, a frame with no direction, every gate down): the sample is then not
    /// counted. The 5-second sample of <c>gate-attacked</c>'s <c>through=</c>.
    /// </summary>
    int AttackersBehindGates(LdPoint? inside);

    /// <summary>Whether each point has a navmesh and lies inside the mission boundary, in the order given: the <c>fort-points</c> boot check.</summary>
    IReadOnlyList<LdPointFacts> PointsUsable(IReadOnlyList<LdPlace> points);

    /// <summary>One fragment for the boot line (A23): <c>archer_positions=N formations=&lt;index:count,...&gt;</c>, the engine's own count of archer positions and the units in each defender formation.</summary>
    string DescribeArcherPositions();

    /// <summary>
    /// The arrow item a volley looses, looked up once and logged with its thrust, speed and reach against <paramref name="range"/> (the Fords guard, <c>FordsTowerAdapter.TowerArrow</c>). Null when the game did not
    /// load it. The aim needs its speed (<see cref="VolleyAim.Plan"/>) and <see cref="VolleyOrder"/> carries none.
    /// </summary>
    LdVolleyItem? VolleyItem(string itemId, float range);

    /// <summary>
    /// The attackers a volley may aim at: standing, human, not running away, and within <paramref name="range"/> of the nearest of the <paramref name="origins"/> raised by <paramref name="originHeight"/>,
    /// measured in 3D from the raised origin to the chest, which is the test <see cref="VolleyAim.Plan"/> applies again (so this is never narrower than it). Each carries the registry key, his chest point and his
    /// horizontal velocity.
    /// </summary>
    IReadOnlyList<LdVolleyTarget> ReadVolleyTargets(IReadOnlyList<LdPoint> origins, float originHeight, float range);

    /// <summary>The first point the scene stops a ray from <paramref name="origin"/> along the unit <paramref name="direction"/> within <paramref name="range"/> (<c>Scene.RayCastForClosestEntityOrTerrain</c>), or null when it reaches the end.</summary>
    Vec3f? RayHit(Vec3f origin, Vec3f direction, float range);

    /// <summary>The main agent's eye and look direction (<c>GetEyeGlobalPosition</c>, <c>LookDirection</c>), or null when he is not standing: the crosshair of a person.</summary>
    LdVolleyRay? EyeRay();

    /// <summary>
    /// Looses the <paramref name="shots"/> as scripted missiles of <paramref name="itemId"/> (<c>Mission.AddCustomMissile</c>), one per shot, owned by a defender chosen by <see cref="VolleyOwnerRule"/> and read for
    /// liveness at the call. <paramref name="towerId"/> is empty for the Elven Volley, from a wall origin; a tower's id names a post archer of that tower as the owner. Adds no agent. When there is no owner or no item it
    /// adds nothing and says why.
    /// </summary>
    LdVolleyFire FireVolley(string itemId, IReadOnlyList<LdVolleyShot> shots, string towerId);

    /// <summary>The way posted archers are held for this mission (spike S6): the plan's first choice, or the variant the process environment names (<see cref="PostsVariants.EnvironmentName"/>).</summary>
    PostsVariant PostsVariant { get; }

    /// <summary>The posts formation's index (<c>FormationClass.Skirmisher</c>, 4: the A23 boot line shows ProperDefense leaves it empty).</summary>
    int PostsFormationIndex { get; }

    /// <summary>
    /// Mans the towers' posts (D20): for each tower, up to <see cref="LdPostFill.Posts"/> posts (the recorded ones, if fewer) that no live archer holds get a new one of <see cref="LdPostFill.Troop"/>, spawned at the
    /// post facing its direction (the spawn takes a position with a direction, E20), until <see cref="LdPostFill.Room"/> soldiers are out; the rest are short (the cap), and a spawn that fails is failed. Each is held by a
    /// scripted position (<see cref="PostsVariant"/>). Soldiers who are not post archers and stand in the posts formation (the tactic splits units into any empty index) are moved out first, and counted in the answer.
    /// </summary>
    LdPostsFilled FillPosts(LdPostFill fill, IReadOnlyList<LdTower> towers);

    /// <summary>Each tower's post archers as they stand now: how many are live, how many are within the hold distance of their own post, the worst drift, and the arrows loosed so far.</summary>
    LdPostsReading ReadPosts();

    /// <summary>
    /// The D30a re-assert, run once a second while posts exist: the posts formation is kept off AI control (variant C: kept not splittable), any agent in it that is not a post archer is moved out, a post archer the
    /// tactic took away is moved back, and each live archer's scripted position is set again.
    /// </summary>
    LdPostsHold HoldPosts();

    /// <summary>An agent loosed an arrow (the logic's <c>OnAgentShootMissile</c>, which the engine declares not multithread callable, Mission.cs:4982). Counted against the tower whose post archer he is; any other agent is ignored.</summary>
    void NotePostShot(object shooter);

    /// <summary>
    /// The opening split (Mike, 2026-11-12): every standing defender on foot who is not the player or a post archer goes to one of four formations, the infantry to the wall or the gate and the archers
    /// to the lower or the high walls by the shares, each group nearest its place. The places come from the scene's own siege objects. Each group is taken off AI control and ordered to its place with
    /// the shapes of vanilla's ready order. A pair whose two places the scene does not give is not split. Safe to call again inside a hold: the soldiers it placed keep their group, and the others fill the group below its share.
    /// </summary>
    LdSplitResult SplitDefenders(SplitShares shares);

    /// <summary>The attackers at the walls on each side, left, middle and right, as the siege AI counts them (<c>SiegeQuerySystem</c>'s close attacker counts). Empty when the mission has no siege query system.</summary>
    IReadOnlyList<int> ReadCloseAttackers();

    /// <summary>Drops the siege AI's 5 s cache of those counts (<c>SiegeQuerySystem.Expire</c>), so the next read counts the attackers again. Does nothing when the mission has no siege query system.</summary>
    void RefreshCloseAttackers();

    /// <summary>The split's held groups as they stand now, in the order wall, gate, low, high; a group the last split did not make is absent. Empty when nothing is held.</summary>
    IReadOnlyList<LdSplitGroup> ReadSplit();

    /// <summary>Hands the split's groups back to AI control, except a group whose order the player changed. Forgets the groups.</summary>
    LdSplitRelease ReleaseSplit();
}
