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
    public static string PopupBody(StoryChoices choices)
    {
        if (choices.Defend && choices.Attack)
            return "{=taom_story_offer_body_both}The hour of {TARGET} has come. Will you ride there and choose your side of its walls, or let fate decide?";
        return choices.Attack
            ? "{=taom_story_offer_body_attack}The hour of {TARGET} has come. Will you march on its walls and lead the assault yourself, or let fate decide?"
            : "{=taom_story_offer_body}The hour of {TARGET} has come. Will you ride to its walls and fight the battle yourself, or let fate decide?";
    }

    /// <summary>The popup's accept button. It only opens the menu: the side is chosen there.</summary>
    public static string PopupAccept(StoryChoices choices) => choices.Defend
        ? "{=taom_story_offer_ride}Ride to {TARGET}"
        : "{=taom_story_offer_march}March on {TARGET}";

    /// <summary>
    /// The story offer menu's body for its state: the open offer before the mission, then the applied result for the side the player
    /// took. Held and Fell report the battle; Undecided and Failed say the call may come again (both snooze); Unchanged means the window
    /// closed meanwhile.
    /// </summary>
    public static string Menu(StoryChoices? choices, StorySide side, StoryResult? result)
    {
        if (choices == null || result == StoryResult.Unchanged)
            return "{=taom_story_offer_none}The moment has passed.";

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
