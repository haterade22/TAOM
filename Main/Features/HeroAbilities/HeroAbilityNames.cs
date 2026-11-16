using TaleWorlds.Localization;

namespace TAOM.Features.HeroAbilities;

// The player-facing name of each shipped hero ability and the cast line. An ability added only in
// hero_abilities.json shows its id until it gets a row here.
public static class HeroAbilityNames
{
    public static TextObject Name(string abilityId) => abilityId switch
    {
        "aragorn_for_frodo" => new TextObject("{=taom_hero_ability_aragorn_for_frodo}For Frodo!"),
        "aragorn_isildurs_heir" => new TextObject("{=taom_hero_ability_aragorn_isildurs_heir}Heir of Isildur"),
        "gimli_baruk_khazad" => new TextObject("{=taom_hero_ability_gimli_baruk_khazad}Baruk Khazâd!"),
        "gimli_still_standing" => new TextObject("{=taom_hero_ability_gimli_still_standing}Still Standing"),
        "legolas_rapid_fire" => new TextObject("{=taom_hero_ability_legolas_rapid_fire}Rapid Fire"),
        "legolas_many_arrows" => new TextObject("{=taom_hero_ability_legolas_many_arrows}Many Arrows"),
        "legolas_archers_eye" => new TextObject("{=taom_hero_ability_legolas_archers_eye}Archer's Eye"),
        _ => new TextObject(abilityId),
    };

    // playerSide is the player's team or an ally's (Team.IsPlayerAlly).
    public static TextObject Cast(TextObject hero, string abilityId, bool playerSide)
    {
        var line = playerSide
            ? new TextObject("{=taom_hero_ability_cast_ally}{HERO}: {ABILITY}")
            : new TextObject("{=taom_hero_ability_cast_enemy}Enemy {HERO}: {ABILITY}");
        line.SetTextVariable("HERO", hero);
        line.SetTextVariable("ABILITY", Name(abilityId));
        return line;
    }
}
