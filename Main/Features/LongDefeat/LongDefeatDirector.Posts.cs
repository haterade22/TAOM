using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>The high towers' posted archers at the director (phase3-plan.md D20, D30a): the fill, the once-a-second hold, and one <see cref="PostsWatch"/> per tower and wave for the <c>posts</c> line.</summary>
internal sealed partial class LongDefeatDirector
{
    private readonly List<PostsWatch> _postsWatches = new();
    private readonly Dictionary<string, LdTowerFill> _lastFill = new();
    private bool _postShots;
    private LdPostsReading _posts = LdPostsReading.None;
    private float _postsClock;
    private int _postsMovedOut;
    private int _postsMovedBack;
    private int _postsHoldLogged;

    /// <summary>Whether a wave with posted archers is running. Read in the engine's shot callback (not multithread callable, Mission.cs:4982), before anything else is done for a shot.</summary>
    public bool WantsPostShots => _postShots;

    /// <summary>A shot was loosed in the mission (the logic forwards every agent's own shot while a posts wave runs). The field counts it only for a post archer.</summary>
    public void OnPostShot(object shooter)
    {
        if (_started && _postShots)
            _fort?.NotePostShot(shooter);
    }

    /// <summary>The fill the run handed over: the field mans the free posts (moving out any stranger the tactic split into the formation), and the run books what was spawned, left short (the cap) and failed.</summary>
    private void ApplyFillPosts(LdPostFill fill)
    {
        if (_fort == null)
            return;

        var result = _fort.FillPosts(fill, _run.Towers);
        _run.PostsFilled(result);
        foreach (var tower in result.Towers)
            _lastFill[tower.TowerId] = tower;

        _logger.LogInfo($"{Tag}[Posts] fill: {fill.Troop}, room {fill.Room}, {string.Join("; ", result.Towers.Select(t => $"{t.TowerId} {t.Standing} standing + {t.Spawned} spawned of {t.Wanted}, short {t.Short}, failed {t.Failed}"))}"
            + (result.MovedOut > 0 ? $"; {result.MovedOut} soldier(s) that were not post archers moved out of the posts formation" : string.Empty));
        _logger.LogInfo($"{Tag}[Posts] after the fill: {_fort.DescribeArcherPositions()}");
    }

    /// <summary>The posts as they stand, read before the run advances: the clear's row needs the count from before its own refill.</summary>
    private void ReadPostsTick()
    {
        if (_fort == null || (_run.TowerLevel <= 0 && _postsWatches.Count == 0))
            return;

        _posts = _fort.ReadPosts();
        _run.NotePosts(_posts.Standing);
    }

    /// <summary>A wave begins: one watch per tower that should hold archers, from the reading taken just before the tick that started it.</summary>
    private void OpenPostsWatches(int wave)
    {
        _postsWatches.Clear();
        _postsMovedOut = 0;
        _postsMovedBack = 0;
        _postsClock = 0f;
        if (_fort == null || _run.TowerLevel <= 0)
            return;

        foreach (var tower in _run.Towers)
        {
            var wanted = _run.PostsWanted(tower);
            if (wanted <= 0)
                continue;

            var last = _lastFill.TryGetValue(tower.Id, out var fill) ? fill : default;
            var reading = ReadingOf(tower.Id);
            var expected = System.Math.Max(0, wanted - last.Short);
            _postsWatches.Add(new PostsWatch(tower.Id, wave, _run.TowerLevel, _fort.PostsVariant, _fort.PostsFormationIndex, tower.Posts.Count, expected, last.Short, last.Failed, reading.Standing, reading.Shots));
        }

        _postShots = _postsWatches.Count > 0;
        if (_postShots)
            _logger.LogInfo($"{Tag}[Posts] at the wave start: {_fort.DescribeArcherPositions()}");
    }

    private LdTowerPosts ReadingOf(string towerId)
    {
        foreach (var tower in _posts.Towers)
        {
            if (tower.TowerId == towerId)
                return tower;
        }

        return default;
    }

    /// <summary>Each tick of a wave with posts: the hold's once-a-second re-assert, and the held sample 30 s in.</summary>
    private void TickPosts(float dt)
    {
        if (_fort == null || _postsWatches.Count == 0)
            return;

        _postsClock += dt;
        if (_postsClock >= PostsRules.ReassertSeconds)
        {
            _postsClock = 0f;
            if (_posts.Standing > 0)
                HoldPostsOnce();
        }

        foreach (var watch in _postsWatches)
        {
            watch.Seconds += dt;
            if (!watch.Sampled && watch.Seconds >= PostsRules.SampleSeconds)
                watch.Sample(ReadingOf(watch.TowerId));
        }
    }

    private void HoldPostsOnce()
    {
        var hold = _fort!.HoldPosts();
        _postsMovedOut += hold.MovedOut;
        _postsMovedBack += hold.MovedBack;
        if ((hold.Moved > 0 || hold.AiRegained) && _postsHoldLogged++ < 5)
            _logger.LogInfo($"{Tag}[Posts] hold: AI regained {(hold.AiRegained ? "yes, taken off again" : "no")}, {hold.MovedOut} agent(s) moved out of the posts formation, {hold.MovedBack} post archer(s) moved back");
    }

    /// <summary>Writes each open watch's <c>posts</c> line and its hold line, once: at the clear, or at the run's end (<paramref name="endedRun"/>). The reading is the last tick's, from before any refill.</summary>
    private void JudgePosts(bool endedRun)
    {
        _postShots = false;
        _postsHoldLogged = 0;
        foreach (var watch in _postsWatches)
        {
            watch.Count(ReadingOf(watch.TowerId).Shots);
            watch.MovedOut = _postsMovedOut;
            watch.MovedBack = _postsMovedBack;
            var verdict = LongDefeatPostsChecks.Posts(watch, endedRun);
            LongDefeatChecks.Record(_outcomes, LongDefeatChecks.PostsId, $"{watch.TowerId}-w{watch.Wave}", verdict);
            _logger.LogInfo($"{Tag}[Posts] {watch.TowerId} w{watch.Wave} {verdict.Result}: {verdict.Detail}");
        }

        _postsWatches.Clear();
    }
}
