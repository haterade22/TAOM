using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.StoryMissions;

/// <summary>
/// The story offer's lines (#1105 slice 4, #1326), as raw <c>{=KEY}English</c> text that may name {TARGET}: the popup for the sides the
/// player may take, and the menu body before the mission and after its result. A player who may only attack is never told to "ride to
/// its walls and fight", and a storming host is never "the enemy" to its own captain.
/// </summary>
internal static class StoryOfferText
{
    /// <summary>The popup's question for the sides on offer.</summary>
    public static string PopupBody(StoryChoices choices, bool hero = false)
    {
        if (hero)
            return HeroPopupBody(choices);
        if (choices.Defend && choices.Attack)
            return "{=taom_story_offer_body_both}The hour of {TARGET} has come. Will you ride there and choose your side of its walls, or let fate decide?";
        return choices.Attack
            ? "{=taom_story_offer_body_attack}The hour of {TARGET} has come. Will you march on its walls and lead the assault yourself, or let fate decide?"
            : "{=taom_story_offer_body}The hour of {TARGET} has come. Will you ride to its walls and fight the battle yourself, or let fate decide?";
    }

    /// <summary>The popup's accept button. It only opens the menu: the side is chosen there.</summary>
    public static string PopupAccept(StoryChoices choices, bool hero = false) => hero
        ? "{=taom_story_breaking_go}Go to Parth Galen"
        : choices.Defend
            ? "{=taom_story_offer_ride}Ride to {TARGET}"
            : "{=taom_story_offer_march}March on {TARGET}";

    /// <summary>The lines of the Breaking of the Fellowship (#1105 slice 3), the one hero story: no walls and no {TARGET}.</summary>
    private static string HeroPopupBody(StoryChoices choices)
    {
        if (choices.Defend && choices.Attack)
            return "{=taom_story_breaking_body_both}The Uruk-hai of the White Hand close on the Company at Parth Galen. Will you go there and choose your side, or let fate decide?";
        return choices.Attack
            ? "{=taom_story_breaking_body_attack}The Company rests at Parth Galen, and the Uruk-hai of the White Hand are near. Will you lead the hunt yourself, or let fate decide?"
            : "{=taom_story_breaking_body}The Uruk-hai of the White Hand close on the Company at Parth Galen. Will you stand with the Fellowship yourself, or let fate decide?";
    }

    private static string HeroMenu(StorySide side, StoryResult? result)
    {
        var attacked = side == StorySide.Attacking;
        return result switch
        {
            StoryResult.HeroLives => attacked
                ? "{=taom_story_breaking_escaped}Your band is broken at Parth Galen, and Boromir lives."
                : "{=taom_story_breaking_saved}The Uruk-hai are thrown back at Parth Galen, and Boromir lives.",
            StoryResult.HeroDies => attacked
                ? "{=taom_story_breaking_slain}Boromir is slain at Parth Galen, and the Company is scattered."
                : "{=taom_story_breaking_fallen}Boromir has fallen at Parth Galen, pierced by many arrows.",
            null => "{=taom_story_breaking_menu}The horn of Gondor sounds in the woods of Parth Galen. Time stands until you choose.",
            _ => "{=taom_story_breaking_undecided}The fight at Parth Galen is not decided. The call may come again.",
        };
    }

    /// <summary>
    /// The story offer menu's body for its state: the open offer before the mission, then the applied result for the side the player
    /// took. Held and Fell report the battle; Undecided and Failed say the call may come again (both snooze); Unchanged means the window
    /// closed meanwhile.
    /// </summary>
    public static string Menu(StoryChoices? choices, StorySide side, StoryResult? result, bool hero = false)
    {
        if (choices == null || result == StoryResult.Unchanged)
            return "{=taom_story_offer_none}The moment has passed.";
        if (hero)
            return HeroMenu(side, result);

        var attacked = side == StorySide.Attacking;
        return result switch
        {
            StoryResult.Held => attacked
                ? "{=taom_story_offer_repulsed}Your assault on {TARGET} is thrown back, and its walls hold."
                : "{=taom_story_offer_won}The walls of {TARGET} held, and the host breaks against them.",
            StoryResult.Fell => attacked
                ? "{=taom_story_offer_stormed}The walls of {TARGET} are stormed, and the fortress falls."
                : "{=taom_story_offer_lost}The walls of {TARGET} are lost.",
            null => choices.Value.Attack
                ? "{=taom_story_offer_menu_host}A host gathers before the walls of {TARGET}. Time stands until you choose."
                : "{=taom_story_offer_menu}The host of the enemy gathers before {TARGET}. Time stands until you choose.",
            _ => "{=taom_story_offer_undecided}The battle for {TARGET} is not decided. The call may come again.",
        };
    }
}
