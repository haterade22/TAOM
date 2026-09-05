using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.FieldCommission.Hooks;

/// <summary>
/// Thin dialogue registration (ADR-002) for dismissing a promoted companion in person (#540).
/// The line sits under <c>hero_main_options</c> for any promoted companion who qualifies, in a
/// keep or tavern scene as much as on the map; vanilla's own fire line hides itself whenever
/// <c>Settlement.CurrentSettlement</c> is set, which is why players never found it in a town.
///
/// The removal runs on <c>ConversationEnded</c>, not in the farewell line's consequence. Vanilla
/// never removes a conversation partner from inside a scene conversation (its fire line is
/// map-only), so the one place this behavior removes a hero is after the conversation has closed,
/// where <c>KillCharacterAction</c> already handles a hero standing in the current settlement.
/// The hand-off is a one-shot field consumed on every conversation end, whichever line won.
/// </summary>
public class FieldCommissionDismissDialogBehavior : CampaignBehaviorBase
{
    private const int Priority = 111;

    private readonly IFieldCommissionDismissService _dismiss;
    private readonly ICoopSessionProvider _coopSession;
    private readonly IModLogger _logger;

    private string _pendingDismissHeroId;

    public FieldCommissionDismissDialogBehavior(
        IFieldCommissionDismissService dismiss,
        ICoopSessionProvider coopSession,
        IModLogger logger)
    {
        _dismiss = dismiss;
        _coopSession = coopSession;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        // Process-lifetime singleton (the Enlistment precedent): a hand-off must not cross sessions.
        _pendingDismissHeroId = null;

        starter.AddPlayerLine(
            "taom_fc_dismiss",
            "hero_main_options",
            "taom_fc_dismiss_answer",
            "{=taom_fc_dismiss}Your commission is ended. Return to the ranks.",
            PartnerIsDismissable,
            null,
            Priority);

        starter.AddDialogLine(
            "taom_fc_dismiss_answer",
            "taom_fc_dismiss_answer",
            "taom_fc_dismiss_confirm",
            "{=taom_fc_dismiss_answer}Back to the line, then. What I carry goes with the commission; the ranks do not hand it back. You are certain of this?",
            null,
            null,
            Priority);

        starter.AddPlayerLine(
            "taom_fc_dismiss_confirm",
            "taom_fc_dismiss_confirm",
            "taom_fc_dismiss_done",
            "{=taom_fc_dismiss_confirm}I am certain. Return to the {TROOP_NAME} ranks.",
            SetTroopName,
            null,
            Priority);

        starter.AddPlayerLine(
            "taom_fc_dismiss_cancel",
            "taom_fc_dismiss_confirm",
            "close_window",
            "{=taom_fc_dismiss_cancel}No. Keep your commission; forget I spoke.",
            null,
            null,
            Priority);

        // The removal is armed on the farewell, the last line before close_window, as vanilla
        // arms its own fire on companion_fire_farewell.
        starter.AddDialogLine(
            "taom_fc_dismiss_done",
            "taom_fc_dismiss_done",
            "close_window",
            "{=taom_fc_dismiss_done}Then I go back to the line. It was an honor to carry the rank.",
            null,
            OnDismissConfirmed,
            Priority);
    }

    // Boundary conversion only: the conversation partner's id.
    private static string PartnerId() => Hero.OneToOneConversationHero?.StringId;

    // Condition AND text setup: the confirm line names the troop, and a condition is the only
    // hook that runs before a line renders. Hidden on a co-op client outright, rather than shown
    // and silently ignored at the end: a whole are-you-sure exchange that does nothing is worse
    // than no line (deep-review data-flow finding, 2026-09-04).
    private bool PartnerIsDismissable()
    {
        if (!_coopSession.IsAuthority)
            return false;

        var candidate = _dismiss.Evaluate(PartnerId());
        if (!candidate.IsDismissable)
            return false;

        MBTextManager.SetTextVariable("TROOP_NAME", candidate.TroopName ?? string.Empty);
        return true;
    }

    // Re-pushed rather than trusted from the first line: the variable is a global that any line
    // in between could have overwritten.
    private bool SetTroopName()
    {
        MBTextManager.SetTextVariable("TROOP_NAME", _dismiss.Evaluate(PartnerId()).TroopName ?? string.Empty);
        return true;
    }

    private void OnDismissConfirmed() => _pendingDismissHeroId = PartnerId();

    private void OnConversationEnded(IEnumerable<CharacterObject> characters)
    {
        var heroId = _pendingDismissHeroId;
        _pendingDismissHeroId = null;
        if (string.IsNullOrEmpty(heroId) || !_coopSession.IsAuthority)
            return;

        _logger?.LogInfo($"[FieldCommission] conversation closed; discharging {heroId} to the ranks");
        _dismiss.DismissAndReport(heroId);
    }
}
