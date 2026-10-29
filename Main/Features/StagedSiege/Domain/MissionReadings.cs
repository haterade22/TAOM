namespace TAOM.Features.StagedSiege.Domain;

/// <summary>Units of each side standing inside one capture zone.</summary>
public readonly struct ZoneCount
{
    public ZoneCount(int attackers, int defenders)
    {
        Attackers = attackers;
        Defenders = defenders;
    }

    public int Attackers { get; }
    public int Defenders { get; }
}

/// <summary>A side's troops on the field and in reserve, as the spawn logic counts them.</summary>
public readonly struct SpawnCounts
{
    public SpawnCounts(int active, int remaining)
    {
        Active = active;
        Remaining = remaining;
    }

    public int Active { get; }
    public int Remaining { get; }
}

/// <summary>The detachment's size, whether the AI drives it, and where its units stand on average.</summary>
public readonly struct DetachmentReading
{
    public DetachmentReading(int units, bool driven, float x, float y)
    {
        Units = units;
        Driven = driven;
        X = x;
        Y = y;
    }

    public int Units { get; }
    public bool Driven { get; }
    public float X { get; }
    public float Y { get; }
}
