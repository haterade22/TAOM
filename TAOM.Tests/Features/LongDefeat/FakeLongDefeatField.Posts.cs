using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The posted archers' half of the field fake (Slice 7): each tower keeps a count of live archers, a fill spawns the free posts inside the room it is given with the real <see cref="PostsFillPlan"/> and adds them to the
/// standing defenders (a post archer is a defender, D20), a reading reports them with the drift and arrows a test sets, and the hold counts the calls and answers what a test arranged.
/// </summary>
internal sealed partial class FakeLongDefeatField
{
    /// <summary>The way the field holds its archers; a test sets another to see it logged and judged.</summary>
    public PostsVariant PostsVariantValue { get; set; } = PostsVariant.A;

    public PostsVariant PostsVariant => PostsVariantValue;

    public int PostsFormationIndex => 4;

    /// <summary>Soldiers who are not post archers standing in the posts formation when the next fill runs (the tactic split them in, E21); the fill moves them out and reports the count.</summary>
    public int PostsStrangers { get; set; }

    /// <summary>The next fill's first spawns that fail (the engine throws): they are reported failed, not short.</summary>
    public int PostsSpawnFailures { get; set; }

    /// <summary>The live archers of each tower; a fill raises them, <see cref="KillArchers"/> lowers them.</summary>
    public Dictionary<string, int> Archers { get; } = new();

    /// <summary>The arrows each tower's archers have loosed (cumulative, as the real counter is).</summary>
    public Dictionary<string, int> TowerShots { get; } = new();

    /// <summary>How far each live archer stands from his post and above or below it, as the reading reports; the held count follows <see cref="PostsRules.Held"/>.</summary>
    public float ArcherDrift { get; set; } = 0.3f;

    public float ArcherDz { get; set; } = 0.2f;

    /// <summary>What each hold answers, in order; the last repeats. Empty: nothing moved.</summary>
    public List<LdPostsHold> HoldResults { get; } = new();

    /// <summary>At most this many archers a fill may spawn (a cap tighter than the run knew); null is no limit.</summary>
    public int? PostsSpawnLimit { get; set; }

    public List<LdPostFill> PostFills { get; } = new();

    public List<IReadOnlyList<LdTower>> PostTowers { get; } = new();

    public List<object> PostShotsNoted { get; } = new();

    public int HoldCalls { get; private set; }

    public LdPostsFilled FillPosts(LdPostFill fill, IReadOnlyList<LdTower> towers)
    {
        Calls.Add($"FillPosts:{fill.Posts}:{fill.Room}");
        PostFills.Add(fill);
        PostTowers.Add(towers);
        var movedOut = PostsStrangers;
        PostsStrangers = 0;
        var room = PostsSpawnLimit is { } limit ? Math.Min(fill.Room, limit) : fill.Room;
        var fills = new List<LdTowerFill>();
        foreach (var tower in towers)
        {
            var have = Archers.TryGetValue(tower.Id, out var n) ? n : 0;
            var wanted = Math.Min(fill.Posts, tower.Posts.Count);
            var plan = PostsFillPlan.For(wanted, Enumerable.Range(0, have).ToList(), room);
            var failed = Math.Min(PostsSpawnFailures, plan.Spawn.Count);
            PostsSpawnFailures -= failed;
            var spawned = plan.Spawn.Count - failed;
            Archers[tower.Id] = have + spawned;
            room -= spawned;
            Defenders += spawned;
            fills.Add(new LdTowerFill(tower.Id, wanted, have, spawned, plan.Short, failed));
        }

        return new LdPostsFilled(fills, movedOut);
    }

    public LdPostsReading ReadPosts()
    {
        Calls.Add("ReadPosts");
        var towers = Archers.Select(a =>
        {
            var held = PostsRules.Held(ArcherDrift, ArcherDz) ? a.Value : 0;
            return new LdTowerPosts(a.Key, a.Value, held, a.Value > 0 ? ArcherDrift : 0f, a.Value > 0 ? ArcherDz : 0f,
                TowerShots.TryGetValue(a.Key, out var shots) ? shots : 0);
        }).ToList();
        return new LdPostsReading(towers);
    }

    public LdPostsHold HoldPosts()
    {
        Calls.Add("HoldPosts");
        var at = Math.Min(HoldCalls++, HoldResults.Count - 1);
        return at < 0 ? LdPostsHold.Nothing : HoldResults[at];
    }

    public void NotePostShot(object shooter)
    {
        Calls.Add("PostShot");
        PostShotsNoted.Add(shooter);
        if (shooter is string tower && Archers.ContainsKey(tower))
            TowerShots[tower] = (TowerShots.TryGetValue(tower, out var n) ? n : 0) + 1;
    }

    /// <summary>A tower's archers fall: the standing defenders fall with them.</summary>
    public void KillArchers(string tower, int count)
    {
        var have = Archers.TryGetValue(tower, out var n) ? n : 0;
        var killed = Math.Min(count, have);
        Archers[tower] = have - killed;
        Defenders -= killed;
    }
}
