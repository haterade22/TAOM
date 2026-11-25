using TAOM.Features.Warg;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal partial interface ICommandTrialHost
{
    /// <summary>
    /// The player's riders on a warg (a warg-family Monster id): the living ones among those seen so far, how many still sit on a living warg, the
    /// highest planar speed of a living warg and of a living mount of another kind under a rider of the team, and a living warg's speed limit
    /// and agent scale. It reads agents: call it after the in-mission-tick marker.
    /// </summary>
    WargReading ReadWargs();
}
