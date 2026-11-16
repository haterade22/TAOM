using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.CheatMenu;

/// <summary>The player's words for a <see cref="CheatOutcome"/> (rows taom_cheat_* in taom_module_strings.xml).</summary>
public static class CheatMenuTexts
{
    public static void Show(CheatOutcome outcome) =>
        InformationManager.DisplayMessage(new InformationMessage(Describe(outcome).ToString(), outcome.Ok ? Colors.Green : Colors.Yellow));

    public static TextObject Describe(CheatOutcome o) => o.Kind switch
    {
        CheatOutcomeKind.CoopRefused => new TextObject("{=taom_cheat_coop}Cheats are not available in co-op."),
        CheatOutcomeKind.BadAmount => new TextObject("{=taom_cheat_bad_amount}That amount is not allowed. The limit is {MAX}.")
            .SetTextVariable("MAX", o.Number.ToString("0")),
        CheatOutcomeKind.UnknownTarget => new TextObject("{=taom_cheat_unknown}Nothing called \"{NAME}\" was found.")
            .SetTextVariable("NAME", o.Name ?? string.Empty),
        CheatOutcomeKind.NothingToDo => new TextObject("{=taom_cheat_nothing}Nothing changed: your hero has nothing this cheat can give to."),
        CheatOutcomeKind.TeleportRefused => Refusal(o.Refusal),
        CheatOutcomeKind.NoBattle => new TextObject("{=taom_cheat_battle_none}Start a battle first. Battle cheats work only while a battle is on."),
        _ => (o.Given switch
        {
            CheatGiven.Gold => new TextObject("{=taom_cheat_done_gold}Gold given. You now have {NUMBER} denars."),
            CheatGiven.Influence => new TextObject("{=taom_cheat_done_influence}Gained {NUMBER} influence."),
            CheatGiven.Renown => new TextObject("{=taom_cheat_done_renown}Gained {NUMBER} renown."),
            CheatGiven.Heal => new TextObject("{=taom_cheat_done_heal}Your party is healed. Soldiers healed: {NUMBER}."),
            CheatGiven.Relation => new TextObject("{=taom_cheat_done_relation}Lords of {NAME} whose relation changed: {NUMBER}."),
            CheatGiven.Teleport => new TextObject("{=taom_cheat_done_teleport}You are at the gate of {NAME}."),
            _ => new TextObject("{=taom_cheat_done}Done: {NAME} {NUMBER}"),
        })
            .SetTextVariable("NAME", o.Name ?? string.Empty)
            .SetTextVariable("NUMBER", o.Number.ToString("#,0.##")),
    };

    private static TextObject Refusal(TeleportRefusal r) => r switch
    {
        TeleportRefusal.InMission => new TextObject("{=taom_cheat_tp_mission}You cannot teleport during a battle or a scene."),
        TeleportRefusal.InSettlement => new TextObject("{=taom_cheat_tp_settlement}Leave the settlement first, then teleport."),
        TeleportRefusal.InBattleOrEncounter => new TextObject("{=taom_cheat_tp_encounter}Finish the battle or encounter first."),
        TeleportRefusal.InSiege => new TextObject("{=taom_cheat_tp_siege}End the siege first."),
        TeleportRefusal.InArmyNotLeader => new TextObject("{=taom_cheat_tp_army}Leave the army first. Only an army's leader can teleport it."),
        TeleportRefusal.Prisoner => new TextObject("{=taom_cheat_tp_prisoner}You cannot teleport while you are a prisoner."),
        TeleportRefusal.PartyParked => new TextObject("{=taom_cheat_tp_parked}You cannot teleport while you serve in another lord's party."),
        TeleportRefusal.AtSea => new TextObject("{=taom_cheat_tp_sea}Go ashore first, then teleport."),
        TeleportRefusal.InFieldCamp => new TextObject("{=taom_cheat_tp_camp}Break camp first, then teleport."),
        _ => new TextObject("{=taom_cheat_tp_nocampaign}Start or load a campaign first."),
    };
}
