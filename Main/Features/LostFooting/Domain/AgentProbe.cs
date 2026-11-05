namespace TAOM.Features.LostFooting.Domain;

/// <summary>What a stranded agent is, for the counts. One kind per agent, the most specific that fits.</summary>
public enum AgentKind
{
    /// <summary>An AI human in a formation.</summary>
    Soldier,

    /// <summary>A soldier running away (<c>Agent.IsRunningAway</c>).</summary>
    Router,

    /// <summary>A mount with nobody on it.</summary>
    RiderlessMount,

    /// <summary>A TAOM creature: it carries a behavior tree that is not a race-ability tree.</summary>
    Creature,

    /// <summary>Using a game object (a ladder, a machine, a door).</summary>
    UsingObject,

    /// <summary>Anything else, such as a human outside every formation.</summary>
    Other,
}

/// <summary>The three ways an agent can be stranded. An agent can be in more than one.</summary>
public enum StrandClass
{
    /// <summary>Standing where the navigation mesh has no face.</summary>
    OffMesh,

    /// <summary>Movement mode not Land (water surface or diving), still, with somewhere to go.</summary>
    WaterStill,

    /// <summary>In land mode and still, but below the scene's water level.</summary>
    LandBelowWater,
}

/// <summary>
/// One agent's facts at one sighting, read by the adapter and judged by the rules. Facts only: no engine type,
/// and a number the engine could not give is NaN. <see cref="Handle"/> is the opaque token for the live agent.
/// </summary>
public sealed class AgentProbe
{
    public object Handle { get; set; } = new object();

    /// <summary>The player and any agent a person controls; the census never counts these.</summary>
    public bool IsPlayerControlled { get; set; }

    public bool IsHuman { get; set; } = true;

    public bool InFormation { get; set; } = true;

    public bool IsRouter { get; set; }

    public bool IsMount { get; set; }

    public bool IsCreature { get; set; }

    public bool IsUsingObject { get; set; }

    /// <summary>The movement mode is not Land: the test the engine uses to withhold the formation frame (<c>Agent.cs:2089</c>).</summary>
    public bool InWaterMode { get; set; }

    /// <summary>The cheap test: the agent's own world position carries no navigation mesh.</summary>
    public bool FaceMiss { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Z { get; set; }

    /// <summary>Metres a second, from the agent's current velocity.</summary>
    public float Speed { get; set; } = float.NaN;

    /// <summary>The water level at the agent's spot (<c>Mission.GetWaterLevelAtPosition</c>).</summary>
    public float WaterLevelHere { get; set; } = float.NaN;

    /// <summary>Distance to the formation's order position for this agent, or NaN when it has none.</summary>
    public float DistToFormation { get; set; } = float.NaN;

    /// <summary>Distance to the AI's move destination (<c>Agent.GetAIMoveDestination</c>), or NaN when the engine has none.</summary>
    public float DistToDestination { get; set; } = float.NaN;

    /// <summary>The engine flags the agent as unreachable via the navigation mesh.</summary>
    public bool Unreachable { get; set; }
}
