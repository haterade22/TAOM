using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.Messengers;

/// <summary>
/// Notices of the arrival that need no answer from the player. Engine UI only: the rule that picks a notice is
/// MessengerService.ClassifyArrival, and the once-only latch is on the PendingMessenger.
/// </summary>
internal static class MessengerArrivalNotices
{
    /// <summary>
    /// The target is nowhere on the map until the engine makes it Active again (#1133). A log line, not an inquiry: the
    /// hourly tick must not pause the game for a wait.
    /// </summary>
    internal static void Waiting(Hero target)
    {
        var text = new TextObject("{=taom_messenger_waiting}Your messenger cannot find {HERO_NAME} yet and will wait until they can be reached.");
        text.SetTextVariable("HERO_NAME", target.Name);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }
}
