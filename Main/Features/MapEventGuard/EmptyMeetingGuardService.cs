using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// A meeting nobody can speak in opens a one-option Leave menu instead of vanilla's conversation, which would throw on
/// a null partner and then on every frame (#1099, Patch143). The encounter stays open until the player leaves, because
/// <c>RestartPlayerEncounter</c> reads <c>PlayerEncounter.Current</c> after the meeting returns.
/// </summary>
public sealed class EmptyMeetingGuardService
{
    internal const string CheckId = "taom.map-event-guard.empty-meeting";
    internal const string MenuId = "taom_empty_meeting";

    private readonly IEmptyMeetingAdapter _meeting;
    private readonly IGameMenuAdapter _menus;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;

    public EmptyMeetingGuardService(
        IEmptyMeetingAdapter meeting, IGameMenuAdapter menus, IOutcomeLog outcomes, IModLogger logger)
    {
        _meeting = meeting;
        _menus = menus;
        _outcomes = outcomes;
        _logger = logger;
    }

    /// <summary>True when the Leave menu replaced the meeting; false leaves the meeting to vanilla.</summary>
    public bool ShouldDivertMeeting()
    {
        var partyId = _meeting.FindPartnerWithNoSpeaker();
        if (partyId == null)
            return false;

        if (!_menus.EnsureMenuOpen(MenuId))
        {
            _logger.LogError($"[MapEventGuard] empty-meeting guard could not open its menu, the meeting goes to vanilla: party={partyId}");
            return false;
        }

        _outcomes.Pass(CheckId, partyId, $"party={partyId}");
        _logger.LogWarning($"[MapEventGuard] stopped a meeting with nobody to speak for the party: party={partyId}");
        return true;
    }

    public void Leave() => _meeting.LeaveEncounter();
}
