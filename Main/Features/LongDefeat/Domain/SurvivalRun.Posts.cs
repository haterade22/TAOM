using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>The posted archers in the run (phase3-plan.md D20): the towers it sells against, what each fill spawned and left short, and the posts standing, for a wave's row.</summary>
internal sealed partial class SurvivalRun
{
    private IReadOnlyList<LdTower>? _usableTowers;
    private int _postsStanding;
    private int _postsFilledSince;
    private int _postsShortSince;
    private int _wavePostsFilled;
    private int _wavePostsShort;
    private int _wavePostsManned;

    /// <summary>The towers the shop sells against: the recorded ones, or the subset the boot check left usable.</summary>
    internal IReadOnlyList<LdTower> Towers => _usableTowers ?? _plan.Towers;

    /// <summary>
    /// The towers that passed the director's boot check (each post and volley spot with a navmesh and inside the boundary), in their recorded order and with only their usable points. A bad point is dropped, as
    /// the Points rule says; with no tower left the tower row is hidden. Called once, before any buy.
    /// </summary>
    public void LimitTowers(IReadOnlyList<LdTower> usable) => _usableTowers = usable;

    /// <summary>A fill the field made: the posts it spawned are filled for the next wave's row, and the posts it left empty (the cap) are the short count the next wave starts with.</summary>
    public void PostsFilled(LdPostsFilled filled)
    {
        _postsFilledSince += filled.Spawned;
        _postsShortSince = filled.Short;
    }

    /// <summary>The post archers standing now, read each tick before the run advances (a clear's row needs the count from before its own refill).</summary>
    public void NotePosts(int standing) => _postsStanding = Math.Max(0, standing);

    /// <summary>The posts a tower should hold at the level in force: the level's number, or the recorded ones if fewer. 0 before the first tower level.</summary>
    public int PostsWanted(LdTower tower)
    {
        var levels = _plan.Shop.Fort?.Towers?.Levels;
        return levels == null || _towerLevel <= 0 ? 0 : Math.Min(levels[Math.Min(_towerLevel, levels.Count) - 1].Posts, tower.Posts.Count);
    }

    /// <summary>A wave begins: the posts filled and left short since the last wave started, and the archers standing as it starts.</summary>
    private void InitPosts()
    {
        _wavePostsFilled = _postsFilledSince;
        _wavePostsShort = _postsShortSince;
        _wavePostsManned = _postsStanding;
        _postsFilledSince = 0;
    }

    private PostsWaveRecord PostsWaveNow() => new(_wavePostsFilled, _wavePostsShort, _wavePostsManned, Math.Max(0, _wavePostsManned - _postsStanding));
}
