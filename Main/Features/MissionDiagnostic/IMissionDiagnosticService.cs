namespace TAOM.Features.MissionDiagnostic;

// Diagnostic capture surface for crash investigations. The MissionLogic boundary
// (MissionDiagnosticBehavior) calls into this service to log structured snapshots
// to the TAOM debug log — so user-uploaded `taom_debug_*.log` files contain
// everything we need to identify mod-conflict bugs (BehaviorType-Logic null casts,
// action-set mismatches, etc.) without asking the user to attach a debugger.
public interface IMissionDiagnosticService
{
    // Called once per session, on OnSessionLaunched.
    void LogSessionSnapshot();

    // Called from a MissionLogic boundary on the first OnMissionTick, after vanilla
    // and all mods have added their MissionBehaviors. Receives the list and the
    // null-detected MissionLogics view so we can name the offender.
    void LogMissionStartSnapshot(
        string sceneName,
        System.Collections.Generic.IReadOnlyList<TaleWorlds.MountAndBlade.MissionBehavior> behaviors,
        System.Collections.Generic.IReadOnlyList<TaleWorlds.MountAndBlade.MissionLogic> missionLogics);

    // Called from the same boundary for each unique action_set name seen on an
    // agent in the first 5 seconds. Service deduplicates internally — no need
    // to gate per-agent at the boundary.
    void LogActionSetSeen(string actionSetName, string raceName, bool isFemale, string agentName, string characterId, string monsterId);

    // Resets per-mission state (action-set dedup set, first-tick flag).
    void ResetForNewMission();
}
