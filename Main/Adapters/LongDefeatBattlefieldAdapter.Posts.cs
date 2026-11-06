using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Adapters;

/// <summary>
/// The high towers' posted archers (phase3-plan.md D20, D30a, spike S6, Slice 7). Each is a real defender spawned at a recorded post through <c>Spawn(..., place:)</c>, which takes a position with a direction (E20),
/// into the defenders' Skirmisher formation (index 4, which ProperDefense leaves empty on the A23 boot lines), and held there by a scripted position (<c>Agent.SetScriptedPositionAndDirection</c>). The archers are
/// kept per tower, in lists held by reference and gated with <c>IsLive</c> (never <c>Agent.Index</c>, never a registry role), as Fords keeps its gunners. The tactic can hand the formation back to AI and split or merge it
/// (E22): <see cref="HoldPosts"/> re-asserts control and membership once a second, and the fill clears the formation of strangers before it spawns. The hold is chosen by <see cref="PostsVariant"/>. Main thread: the
/// engine declares the shot callback <see cref="NotePostShot"/> hears with <c>[MBCallback(null, false)]</c>, so it is not multithread callable (Mission.cs:4982).
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter
{
    // FormationClass.Skirmisher is index 4; the boot line of two gate runs shows ProperDefense split the ranged line into indices 1 to 3 and leave 4 empty (A23).
    private const FormationClass PostsClass = FormationClass.Skirmisher;

    /// <summary>One posted archer: the agent, the tower it stands on, which of the tower's posts is his and where it is. Held by reference; his liveness is read with <see cref="IsLive"/> before every use.</summary>
    private sealed class PostArcher
    {
        public PostArcher(Agent agent, string towerId, int post, LdPlace place)
        {
            Agent = agent;
            TowerId = towerId;
            Post = post;
            Place = place;
        }

        public Agent Agent { get; }
        public string TowerId { get; }
        public int Post { get; }
        public LdPlace Place { get; }
    }

    // The counts are per tower and cumulative, so an archer's death does not lose them; the set holds the towers a fill has named.
    private readonly List<PostArcher> _posted = new();
    private readonly Dictionary<Agent, PostArcher> _postedByAgent = new();
    private readonly Dictionary<string, int> _towerShots = new();
    private readonly HashSet<string> _postTowers = new();
    private PostsVariant? _variant;

    public PostsVariant PostsVariant => _variant ??= ChooseVariant();

    public int PostsFormationIndex => (int)PostsClass;

    // The environment names the variant so a proof run can try each without a rebuild (spike S6); the wrapper's --posts-variant sets it. Logged once, with where it came from.
    private PostsVariant ChooseVariant()
    {
        var named = Environment.GetEnvironmentVariable(PostsVariants.EnvironmentName);
        var variant = PostsVariants.Parse(named);
        var chosen = variant ?? PostsVariants.Default;
        _logger.LogInfo($"[LongDefeat][Posts] hold variant {chosen} ({(variant != null ? "from " + PostsVariants.EnvironmentName : "the default")}), posts formation index {(int)PostsClass}");
        if (variant == null && !string.IsNullOrWhiteSpace(named))
            _logger.LogWarning($"[LongDefeat][Posts] {PostsVariants.EnvironmentName}='{named}' names no variant (A, B or C); the default {PostsVariants.Default} stays");
        return chosen;
    }

    // The tactic splits units into any empty formation index (E21), so a soldier who is not a post archer can stand in the posts formation at a fill (the hold runs only while a wave's watches are open): he is moved to the
    // Ranged formation as the hold does, and the fill goes on. The fill reports how many it moved. A post the cap leaves empty is short; a spawn that throws, or a troop the game does not know, is failed.
    public LdPostsFilled FillPosts(LdPostFill fill, IReadOnlyList<LdTower> towers)
    {
        var team = _mission.DefenderTeam;
        var formation = team?.GetFormation(PostsClass);
        if (team == null || formation == null)
        {
            _logger.LogWarning("[LongDefeat][Posts] the defenders have no posts formation; no post was manned");
            return LdPostsFilled.Nothing;
        }

        var variant = PostsVariant;
        PruneDead();
        var movedOut = MoveStrangersOut(formation);
        var troop = MBObjectManager.Instance.GetObject<BasicCharacterObject>(fill.Troop);
        var room = Math.Max(0, fill.Room);
        var fills = new List<LdTowerFill>();
        var spawnedNow = new List<PostArcher>();
        foreach (var tower in towers)
        {
            var wanted = Math.Min(Math.Max(0, fill.Posts), tower.Posts.Count);
            var taken = new HashSet<int>(_posted.Where(p => p.TowerId == tower.Id).Select(p => p.Post));
            _postTowers.Add(tower.Id);
            var plan = PostsFillPlan.For(wanted, taken, room);
            var spawned = 0;
            var failed = 0;
            foreach (var post in plan.Spawn)
            {
                var archer = troop == null ? null : SpawnPostArcher(troop, formation, tower, post, fill.HealthFactor);
                if (archer == null)
                {
                    failed++;
                    continue;
                }

                spawnedNow.Add(archer);
                spawned++;
                room--;
            }

            fills.Add(new LdTowerFill(tower.Id, wanted, taken.Count, spawned, plan.Short, failed));
        }

        if (spawnedNow.Count > 0)
        {
            ControlFormation(formation, variant, true);
            foreach (var archer in spawnedNow)
                Hold(archer, variant);
        }

        if (troop == null)
            _logger.LogWarning($"[LongDefeat][Posts] no troop '{fill.Troop}'; no post was manned");
        return new LdPostsFilled(fills, movedOut);
    }

    // The mounted lord's recipe (SpawnMounted): the first spawn positions the formation at the place, so the engine finds it positioned and never asks the deployment plan for a plan nobody added troops to
    // (Mission.cs:4214-4226). The fill then takes the formation off AI (D30a) once and holds each archer spawned. A spawn that throws is not a post: the fill names it failed and the run goes on.
    private PostArcher? SpawnPostArcher(BasicCharacterObject troop, Formation formation, LdTower tower, int post, float healthFactor)
    {
        var place = LdPlace.From(tower.Posts[post]);
        try
        {
            if (!formation.HasBeenPositioned)
                formation.SetPositioning(new WorldPosition(_mission.Scene, new Vec3(place.X, place.Y, place.Z)), FacingOf(place));

            formation.BeginSpawn(1, false);
            Agent agent;
            try
            {
                agent = Spawn(troop, _mission.DefenderTeam, true, PostsClass, true, 1, 0, false, place: place);
            }
            finally
            {
                formation.EndSpawn();
            }

            ScaleHealth(agent, healthFactor);
            var archer = new PostArcher(agent, tower.Id, post, place);
            _posted.Add(archer);
            _postedByAgent[agent] = archer;
            _logger.LogInfo($"[LongDefeat][Posts] {tower.Id} post {post}: spawned {troop.StringId} at ({place.X:F1}, {place.Y:F1}, {place.Z:F1}), standing at ({agent.Position.x:F1}, {agent.Position.y:F1}, {agent.Position.z:F1}), face {agent.GetCurrentNavigationFaceId()}");
            return archer;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongDefeat][Posts] {tower.Id} post {post} could not be manned ({ex.GetType().Name}: {ex.Message})");
            return null;
        }
    }

    // E19: the scripted hold without NoAttack, so a post archer may still shoot (A18, variant A); variant B adds a speed limit of 0 so he cannot walk.
    // The position goes in as the Fords garrison does (a navmesh of none, an unsure height: the engine settles it), and the direction is the post's recorded facing.
    private void Hold(PostArcher archer, PostsVariant variant)
    {
        var agent = archer.Agent;
        if (variant == PostsVariant.B)
            agent.SetMaximumSpeedLimit(0f, false);

        var at = new Vec3(archer.Place.X, archer.Place.Y, archer.Place.Z);
        var hold = new WorldPosition(_mission.Scene, UIntPtr.Zero, at, false);
        agent.SetScriptedPositionAndDirection(ref hold, FacingOf(archer.Place).RotationInRadians, false, Agent.AIScriptedFrameFlags.ConsiderRotation);
    }

    // D30a. Variants A and B take the formation off AI control (Formation.cs:808-834). Long Defeat makes the player the general (LongDefeatMissionLauncher.cs:112, Team.SetPlayerRole), so a formation off AI is not AI-owned
    // (Formation.cs:332-342) and the tactic's split and consolidation leave it alone (TacticComponent.cs:191-201, :243-256); the once-a-second re-assert is still needed after a hand-back to AI (Team.DelegateCommandToAI,
    // Team.cs:479-485; the last-unit rule, Formation.cs:2376-2378) and against TacticDefendCastle.CarryOutDefense, which moves a ranged formation's units without asking who owns it (TacticDefendCastle.cs:824).
    // Variant C asks for the engine's own not-splittable switch: SetControlledByAI returns at once when the flag already holds (:810-813), so AI is dropped first and raised again with the switch (:828-831);
    // IsAIOwned reads false while the switch holds (:330, :346), so a formation that reads AI-owned has lost it. Returns whether it had to act.
    private static bool ControlFormation(Formation formation, PostsVariant variant, bool first)
    {
        if (variant == PostsVariant.C)
        {
            if (!first && !formation.IsAIOwned)
                return false;

            formation.SetControlledByAI(false);
            formation.SetControlledByAI(true, true);
            return !first;
        }

        if (!formation.IsAIControlled)
            return false;

        formation.SetControlledByAI(false);
        return true;
    }

    public LdPostsHold HoldPosts()
    {
        var formation = _mission.DefenderTeam?.GetFormation(PostsClass);
        if (formation == null)
            return LdPostsHold.Nothing;

        var variant = PostsVariant;
        var live = PruneDead();
        if (live.Count == 0)
            return LdPostsHold.Nothing;

        var regained = ControlFormation(formation, variant, false);
        var movedOut = MoveStrangersOut(formation);
        var movedBack = 0;
        foreach (var archer in live)
        {
            if (!ReferenceEquals(archer.Agent.Formation, formation))
            {
                archer.Agent.Formation = formation;
                movedBack++;
            }

            Hold(archer, variant);
        }

        return new LdPostsHold(movedOut, movedBack, regained);
    }

    // Every soldier in the posts formation who is not a post archer goes to the Ranged formation (the one the defenders' archers belong to); the count is returned. Detached units are covered by ApplyActionOnEachUnit.
    private int MoveStrangersOut(Formation formation)
    {
        var strangers = new List<Agent>();
        formation.ApplyActionOnEachUnit(agent =>
        {
            if (!_postedByAgent.ContainsKey(agent))
                strangers.Add(agent);
        });

        if (strangers.Count == 0)
            return 0;

        var ranged = _mission.DefenderTeam.GetFormation(FormationClass.Ranged);
        foreach (var stranger in strangers)
            stranger.Formation = ranged;
        return strangers.Count;
    }

    public LdPostsReading ReadPosts()
    {
        var live = PruneDead();
        var readings = new List<LdTowerPosts>();
        foreach (var id in _postTowers)
        {
            int standing = 0, held = 0;
            float maxDrift = 0f, maxDz = 0f;
            foreach (var archer in live)
            {
                if (archer.TowerId != id)
                    continue;

                standing++;
                var at = archer.Agent.Position;
                var drift = new Vec2(at.x - archer.Place.X, at.y - archer.Place.Y).Length;
                var dz = Math.Abs(at.z - archer.Place.Z);
                if (PostsRules.Held(drift, dz))
                    held++;
                maxDrift = Math.Max(maxDrift, drift);
                maxDz = Math.Max(maxDz, dz);
            }

            readings.Add(new LdTowerPosts(id, standing, held, maxDrift, maxDz, _towerShots.TryGetValue(id, out var shots) ? shots : 0));
        }

        return new LdPostsReading(readings);
    }

    // OnAgentShootMissile is raised for an agent's own shot only (E15): a lookup by reference, nothing else.
    public void NotePostShot(object shooter)
    {
        if (!(shooter is Agent agent) || !_postedByAgent.TryGetValue(agent, out var archer))
            return;

        _towerShots[archer.TowerId] = (_towerShots.TryGetValue(archer.TowerId, out var n) ? n : 0) + 1;
    }

    // A dead or recycled archer leaves the lists: the handle is gated with IsLive before anything touches native state. Returns the archers still standing.
    private List<PostArcher> PruneDead()
    {
        for (var i = _posted.Count - 1; i >= 0; i--)
        {
            if (IsLive(_posted[i].Agent))
                continue;

            _postedByAgent.Remove(_posted[i].Agent);
            _posted.RemoveAt(i);
        }

        return _posted;
    }

    private void ClearPosts()
    {
        _posted.Clear();
        _postedByAgent.Clear();
        _towerShots.Clear();
        _postTowers.Clear();
        _variant = null;
    }
}
