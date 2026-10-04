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

    // Called from the same boundary for an agent whose TryMarkActionSetKey was true. Logs once per
    // (action set name, race name, sex) per mission, naming the first agent seen with it.
    void LogActionSetSeen(string actionSetName, string raceName, bool isFemale, string agentName, string characterId, string monsterId);

    // Called from the boundary for every agent in the action-set window before any name is read: true the
    // first time this mission sees the (action set index, race id, sex) combination. The boundary reads the
    // names and calls LogActionSetSeen only on true, so a combination already seen costs no native string
    // marshal. One index has one name and one race id one cached race name, so this filter never hides a line.
    bool TryMarkActionSetKey(int actionSetIndex, int raceId, bool isFemale);

    // One INFO header when the action-set window opens (its length and how the census keys its lines), and one
    // INFO summary when it closes (or the mission ends first): the pre-filter checks, the new keys and the lines
    // written. Totals reset with ResetForNewMission.
    void LogActionSetCensusOpened(float windowSeconds);
    void LogActionSetCensusClosed();

    // Resets per-mission state (both action-set dedup sets and the census check count).
    void ResetForNewMission();
}
