using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The identity of the Fords of War mission: the name the launcher opens it under (the client's view creator and the dread aura
/// gate both match on it) and the test for "this mission is a Fords match".
/// </summary>
internal static class FordsOfWarMission
{
    internal const string MissionName = "TaomFordsOfWar";

    /// <summary>
    /// True when <paramref name="mission"/> is the mission the engine's current state opened under <see cref="MissionName"/>.
    /// The state's name is the only place the launcher's name is kept, and reading it needs no behavior list.
    /// </summary>
    internal static bool IsActive(Mission? mission)
    {
        var state = MissionState.Current;
        return mission != null && state != null && ReferenceEquals(state.CurrentMission, mission) && state.MissionName == MissionName;
    }
}
