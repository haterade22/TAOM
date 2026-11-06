namespace TAOM.Features.LongDefeat.Domain;

/// <summary>One tower's posted archers over one wave, kept for its <c>posts</c> check line (phase3-plan.md section 6): the fill's <see cref="Expected"/> against the <see cref="Manned"/> at the wave's start, the held sample and the arrows.</summary>
internal sealed class PostsWatch
{
    public PostsWatch(string towerId, int wave, int level, PostsVariant variant, int formation, int posts, int expected, int shortBy, int failed, int manned, int shotsAtStart)
    {
        TowerId = towerId;
        Wave = wave;
        Level = level;
        Variant = variant;
        Formation = formation;
        Posts = posts;
        Expected = expected;
        Short = shortBy;
        Failed = failed;
        Manned = manned;
        ShotsAtStart = shotsAtStart;
    }

    public string TowerId { get; }

    public int Wave { get; }

    /// <summary>The tower level in force at the wave's start.</summary>
    public int Level { get; }

    public PostsVariant Variant { get; }

    /// <summary>The posts formation's index.</summary>
    public int Formation { get; }

    /// <summary>The posts recorded for the tower.</summary>
    public int Posts { get; }

    /// <summary>The archers the fill should have put on the tower: the level's posts (or the recorded ones, if fewer) less <see cref="Short"/>. A failed spawn is not taken off, so it leaves the tower short of this.</summary>
    public int Expected { get; }

    /// <summary>The posts the garrison cap left empty at the last fill.</summary>
    public int Short { get; }

    /// <summary>The spawns of the last fill that threw or had no troop to spawn: named apart from the cap, and counted in <see cref="Expected"/>.</summary>
    public int Failed { get; }

    /// <summary>The archers standing on the tower when the wave began.</summary>
    public int Manned { get; }

    public int ShotsAtStart { get; }

    /// <summary>Seconds this wave has run, set by the director as it ticks.</summary>
    public float Seconds { get; set; }

    public bool Sampled { get; private set; }

    /// <summary>The archers standing at the sample, and those of them within the hold distance of their own post.</summary>
    public int SampleStanding { get; private set; }

    public int SampleHeld { get; private set; }

    public float MaxDrift { get; private set; }

    public float MaxDz { get; private set; }

    /// <summary>Arrows the tower's archers loosed this wave (the cumulative counter less its value at the wave's start).</summary>
    public int Shots { get; private set; }

    /// <summary>Archers the re-assert took out of the posts formation, and post archers it put back, since the wave began (the whole posts hold, not this tower's alone).</summary>
    public int MovedOut { get; set; }

    public int MovedBack { get; set; }

    /// <summary>Takes the held sample from the tower's reading.</summary>
    public void Sample(LdTowerPosts reading)
    {
        Sampled = true;
        SampleStanding = reading.Standing;
        SampleHeld = reading.Held;
        MaxDrift = reading.MaxDrift;
        MaxDz = reading.MaxDz;
    }

    /// <summary>Takes the arrows loosed so far from the tower's cumulative counter.</summary>
    public void Count(int cumulativeShots) => Shots = cumulativeShots - ShotsAtStart;
}
