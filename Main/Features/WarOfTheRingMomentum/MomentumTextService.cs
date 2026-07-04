using TaleWorlds.Localization;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Boundary implementation: resolves {=taom_wotr_desc_*} templates (source file
/// taom_wotr_strings.xml) with inline English defaults. Resolved strings are attached
/// to momentum events and persist in the save as-is.
/// </summary>
public class MomentumTextService : IMomentumTextService
{
    public string BattleWonDescription(string winnerFactionName, string winnerLeaderName,
        string loserFactionName, string loserLeaderName, int casualties)
    {
        var text = new TextObject("{=taom_wotr_desc_battle}Army of {WINNER_FACTION}, led by {WINNER_LEADER}, destroyed the army of {LOSER_FACTION}, led by {LOSER_LEADER}, killing {CASUALTIES}.");
        text.SetTextVariable("WINNER_FACTION", winnerFactionName);
        text.SetTextVariable("WINNER_LEADER", winnerLeaderName);
        text.SetTextVariable("LOSER_FACTION", loserFactionName);
        text.SetTextVariable("LOSER_LEADER", loserLeaderName);
        text.SetTextVariable("CASUALTIES", casualties);
        return text.ToString();
    }

    public string SiegeDescription(string factionName, string leaderName, string settlementName)
    {
        var text = new TextObject("{=taom_wotr_desc_siege}Army of {FACTION}, led by {LEADER}, captured {SETTLEMENT}.");
        text.SetTextVariable("FACTION", factionName);
        text.SetTextVariable("LEADER", leaderName);
        text.SetTextVariable("SETTLEMENT", settlementName);
        return text.ToString();
    }

    public string RaidDescription(string partyName, string factionName, string settlementName)
    {
        var text = new TextObject("{=taom_wotr_desc_raid}{PARTY} of {FACTION} raided the village of {SETTLEMENT}.");
        text.SetTextVariable("PARTY", partyName);
        text.SetTextVariable("FACTION", factionName);
        text.SetTextVariable("SETTLEMENT", settlementName);
        return text.ToString();
    }

    public string ArmyGatheredDescription(string leaderName)
    {
        var text = new TextObject("{=taom_wotr_desc_army}An army led by {LEADER} has gathered.");
        text.SetTextVariable("LEADER", leaderName);
        return text.ToString();
    }

    public string StrengthDescription(int percentStronger)
    {
        var text = new TextObject("{=taom_wotr_desc_strength}We are {PERCENTAGE}% stronger than our enemies!");
        text.SetTextVariable("PERCENTAGE", percentStronger);
        return text.ToString();
    }
}
