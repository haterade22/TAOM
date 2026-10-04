using TaleWorlds.Localization;

namespace TAOM.Features.RaceAbilities.Hooks;

// The player-facing name of each shipped ability, and the two message-log lines. An ability added only in
// race_abilities.json shows its id until it gets a row here.
public static class RaceAbilityNames
{
    public static TextObject Name(string abilityId) => abilityId switch
    {
        "berserk" => new TextObject("{=taom_race_ability_berserk}Berserk"),
        "bloodlust" => new TextObject("{=taom_race_ability_bloodlust}Bloodlust"),
        "stand_fast" => new TextObject("{=taom_race_ability_stand_fast}Stand Fast"),
        "swiftness" => new TextObject("{=taom_race_ability_swiftness}Swiftness of the Eldar"),
        "swarm" => new TextObject("{=taom_race_ability_swarm}Swarm"),
        "scurry" => new TextObject("{=taom_race_ability_scurry}Scurry"),
        "iron_discipline" => new TextObject("{=taom_race_ability_iron_discipline}Iron Discipline"),
        "hunters_rush" => new TextObject("{=taom_race_ability_hunters_rush}Hunter's Rush"),
        "necromancer_shadow" => new TextObject("{=taom_race_ability_necromancer_shadow}Shadow of the Necromancer"),
        "citadel_guard" => new TextObject("{=taom_race_ability_citadel_guard}Guard of the Citadel"),
        "forth_eorlingas" => new TextObject("{=taom_race_ability_forth_eorlingas}Forth Eorlingas"),
        "bards_aim" => new TextObject("{=taom_race_ability_bards_aim}Bard's Aim"),
        "hillclan_fury" => new TextObject("{=taom_race_ability_hillclan_fury}Hill-clan Fury"),
        "serpent_venom" => new TextObject("{=taom_race_ability_serpent_venom}Serpent's Venom"),
        "wainrider_wall" => new TextObject("{=taom_race_ability_wainrider_wall}Wainrider Wall"),
        "corsair_raid" => new TextObject("{=taom_race_ability_corsair_raid}Corsair Raid"),
        "variag_ferocity" => new TextObject("{=taom_race_ability_variag_ferocity}Variag Ferocity"),
        "shadow_servants" => new TextObject("{=taom_race_ability_shadow_servants}Servants of the Shadow"),
        _ => new TextObject(abilityId),
    };

    public static TextObject Wave(string abilityId, int count, bool playerSide)
    {
        var line = playerSide
            ? new TextObject("{=taom_race_ability_wave_ally}{ABILITY}: {COUNT} of your soldiers")
            : new TextObject("{=taom_race_ability_wave_enemy}Enemy {ABILITY}: {COUNT} soldiers");
        line.SetTextVariable("ABILITY", Name(abilityId));
        line.SetTextVariable("COUNT", count);
        return line;
    }
}
