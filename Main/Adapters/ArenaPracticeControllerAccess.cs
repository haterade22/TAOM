using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The four private members of vanilla's practice controller (<see cref="ArenaPracticeFightMissionController"/>) that the wave fight
/// reaches (docs/features/training-waves.md), bound once. Each is a Category B row in
/// docs/reference/taleworlds-api-snapshot/reflection-sites.md and a <c>[DataRow]</c> in <c>ReflectionSiteBindingTests</c>, so an engine
/// rename turns the gate red. A member that did not bind makes its call return false and the wave fight refunds the stake: it never
/// guesses. Nothing here holds the controller; every call takes it.
/// </summary>
internal static class ArenaPracticeControllerAccess
{
    private static readonly FieldInfo? ParticipantAgents =
        AccessTools.Field(typeof(ArenaPracticeFightMissionController), "_participantAgents");

    private static readonly FieldInfo? SpawnedOpponentCount =
        AccessTools.Field(typeof(ArenaPracticeFightMissionController), "_spawnedOpponentAgentCount");

    private static readonly MethodInfo? StartPracticeMethod =
        AccessTools.Method(typeof(ArenaPracticeFightMissionController), "StartPractice");

    private static readonly MethodInfo? SpawnNearMasterMethod =
        AccessTools.Method(typeof(ArenaPracticeFightMissionController), "SpawnPlayerNearTournamentMaster");

    /// <summary>All four members bound.</summary>
    internal static bool IsBound =>
        ParticipantAgents != null && SpawnedOpponentCount != null && StartPracticeMethod != null && SpawnNearMasterMethod != null;

    /// <summary>A copy of the controller's practice fighters, so a fade (which removes each from the original) never walks a changing list. Null when unbound.</summary>
    internal static List<Agent>? ParticipantsCopy(ArenaPracticeFightMissionController controller) =>
        ParticipantAgents?.GetValue(controller) is List<Agent> list ? new List<Agent>(list) : null;

    /// <summary>Writes the controller's count of spawned opponents; false when the field did not bind.</summary>
    internal static bool SetSpawnedCount(ArenaPracticeFightMissionController controller, int value)
    {
        if (SpawnedOpponentCount == null)
            return false;
        SpawnedOpponentCount.SetValue(controller, value);
        return true;
    }

    /// <summary>The count of spawned opponents; -1 when the field did not bind.</summary>
    internal static int SpawnedCount(ArenaPracticeFightMissionController controller) =>
        SpawnedOpponentCount?.GetValue(controller) is int value ? value : -1;

    /// <summary>Vanilla's reset of the yard to its idle state; false when the method did not bind.</summary>
    internal static bool StartPractice(ArenaPracticeFightMissionController controller)
    {
        if (StartPracticeMethod == null)
            return false;
        StartPracticeMethod.Invoke(controller, null);
        return true;
    }

    /// <summary>Vanilla's spawn of the player's civilian body next to the master; false when the method did not bind.</summary>
    internal static bool SpawnPlayerNearMaster(ArenaPracticeFightMissionController controller)
    {
        if (SpawnNearMasterMethod == null)
            return false;
        SpawnNearMasterMethod.Invoke(controller, null);
        return true;
    }
}
