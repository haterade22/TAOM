namespace TAOM.Features.BattleLoadDiagnostics.Domain;

// Stable phase names for the [BattleLoad] lifecycle log. The LAST phase written before
// a hang localizes the freeze. These names are part of the log contract — users upload
// logs and we grep for them — so do NOT rename casually.
public enum BattleLoadPhase
{
    EncounterStart,
    MissionOpenNew,
    BattleSceneSelected,
    MissionInitialize,
    AgentEquipBegin,
    AgentEquipOk,
    BattlePlayable,
    StallWatchdog,
}
