namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Which branch of the lane decision order produced a command. None is an empty lane or a lane with nothing to do.</summary>
internal enum LaneBranch
{
    None,
    FallBack,
    Charge,
    AttackTower,
    Hold,
    March,
    Seize,

    /// <summary>An enemy inside the faction's close range: the lane closes on it as one formation (<see cref="LaneTactic"/>).</summary>
    Close,
}
