namespace TAOM.Features.LostFooting.Domain;

/// <summary>How a stranded agent is moved (#894 phase 2).</summary>
public enum RescueMove
{
    None,

    /// <summary>To the nearest standing spot on the navigation mesh at about the agent's own height.</summary>
    Near,

    /// <summary>To the agent's own spot in its formation, only out of the player's sight range.</summary>
    Formation,
}

/// <summary>Why an agent is moved.</summary>
public enum RescueCause
{
    OffMesh,
    Water,
}

/// <summary>The rule's answer for one entry at one sighting.</summary>
public readonly struct RescuePlan
{
    public RescuePlan(RescueMove move, RescueCause cause, bool needLand)
    {
        Move = move;
        Cause = cause;
        NeedLand = needLand;
    }

    public static RescuePlan Nothing => default;

    public RescueMove Move { get; }

    public RescueCause Cause { get; }

    /// <summary>The spot must be dry land: the agent is stranded in water.</summary>
    public bool NeedLand { get; }
}

/// <summary>What the adapter did. A refusal names its reason and moves nothing.</summary>
public sealed class RescueResult
{
    /// <summary>The refusal that a later try can overcome: the player stood near one end of a formation move.</summary>
    public const string PlayerNear = "player-near";

    public bool Moved { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Metres between the agent and the spot, flat; NaN when no spot was found.</summary>
    public float Meters { get; set; } = float.NaN;

    public float ToX { get; set; } = float.NaN;

    public float ToY { get; set; } = float.NaN;

    public float ToZ { get; set; } = float.NaN;

    public static RescueResult Refused(string reason) => new RescueResult { Reason = reason };
}
