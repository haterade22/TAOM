namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Composes the display strings attached to momentum events (breakdown tooltips).
/// Boundary implementation resolves {=taom_wotr_*} TextObjects; services stay
/// TaleWorlds-free. Strings are resolved at event creation and persist as-is in the
/// save (frozen in write-time language — documented limitation).
/// </summary>
public interface IMomentumTextService
{
    string BattleWonDescription(string winnerFactionName, string winnerLeaderName,
        string loserFactionName, string loserLeaderName, int casualties);
    string SiegeDescription(string factionName, string leaderName, string settlementName);
    string RaidDescription(string partyName, string factionName, string settlementName);
    string ArmyGatheredDescription(string leaderName);
    string StrengthDescription(int percentStronger);
}
