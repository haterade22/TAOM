using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.CheatMenu.Battle;

/// <summary>The player's words for the battle cheat menu (rows taom_cheat_battle_* in taom_module_strings.xml).</summary>
public static class BattleCheatTexts
{
    /// <summary>The Esc-menu item that opens the list.</summary>
    public static TextObject Item() => new("{=taom_cheat_battle_item}Cheats");

    /// <summary>The list entry's words; the god mode entry shows whether god mode is on now.</summary>
    public static TextObject Label(BattleCheatAction action, bool godModeOn = false) => action switch
    {
        BattleCheatAction.HealSelf => new TextObject("{=taom_cheat_battle_opt_heal_self}Heal yourself"),
        BattleCheatAction.HealSide => new TextObject("{=taom_cheat_battle_opt_heal_side}Heal your side"),
        BattleCheatAction.GodMode => godModeOn
            ? new TextObject("{=taom_cheat_battle_opt_god_on}God mode: on")
            : new TextObject("{=taom_cheat_battle_opt_god_off}God mode: off"),
        BattleCheatAction.Rout => new TextObject("{=taom_cheat_battle_opt_rout}Rout the enemy"),
        BattleCheatAction.Kill => new TextObject("{=taom_cheat_battle_opt_kill}Kill all enemies"),
        BattleCheatAction.SpawnAllies => new TextObject("{=taom_cheat_battle_opt_spawn_ally}+10 allied troops"),
        _ => new TextObject("{=taom_cheat_battle_opt_spawn_enemy}+10 enemy troops"),
    };

    public static void Show(BattleCheatAction action, CheatOutcome outcome) =>
        InformationManager.DisplayMessage(new InformationMessage(Describe(action, outcome).ToString(), outcome.Ok ? Colors.Green : Colors.Yellow));

    /// <summary>A done or nothing-to-do outcome in the action's own words; every refusal as the campaign menu words it.</summary>
    public static TextObject Describe(BattleCheatAction action, CheatOutcome o)
    {
        if (o.Kind == CheatOutcomeKind.NothingToDo)
            return new TextObject("{=taom_cheat_battle_nothing}Nothing changed: there is nobody in this battle this cheat can act on.");
        if (!o.Ok) return CheatMenuTexts.Describe(o);
        var text = action switch
        {
            BattleCheatAction.HealSelf => new TextObject("{=taom_cheat_battle_done_heal_self}You are back to full health, and so is your mount if you ride one."),
            BattleCheatAction.HealSide => new TextObject("{=taom_cheat_battle_done_heal_side}{NUMBER} soldiers and mounts of your side are back to full health."),
            BattleCheatAction.GodMode => o.Number > 0
                ? new TextObject("{=taom_cheat_battle_done_god_on}God mode is on. You and any mount you ride take no damage until you turn it off or the battle ends.")
                : new TextObject("{=taom_cheat_battle_done_god_off}God mode is off. You and any mount you ride take damage again."),
            BattleCheatAction.Rout => new TextObject("{=taom_cheat_battle_done_rout}{NUMBER} enemies are fleeing."),
            BattleCheatAction.Kill => new TextObject("{=taom_cheat_battle_done_kill}{NUMBER} enemies were killed."),
            BattleCheatAction.SpawnAllies => new TextObject("{=taom_cheat_battle_done_spawn_ally}{NUMBER} allied troops joined the battle."),
            _ => new TextObject("{=taom_cheat_battle_done_spawn_enemy}{NUMBER} enemy troops joined the battle."),
        };
        return text.SetTextVariable("NUMBER", o.Number.ToString("0"));
    }
}
