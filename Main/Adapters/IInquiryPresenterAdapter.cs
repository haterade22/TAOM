using System;

namespace TAOM.Adapters;

/// <summary>
/// Boundary over <c>InformationManager.ShowInquiry</c>/<c>ShowTextInquiry</c> and
/// <c>TextInquiryData</c>/<c>InquiryData</c>/<c>TextObject</c> (ADR-007) — every localization
/// concern for Battlefield Promotions lives here, never in the service.
///
/// Bug fix (d): every message is a FRESH <c>TextObject</c> built with a <c>{=taom_fc_*}</c> key +
/// inline English fallback, never <c>GameTexts.FindText(id)</c> — mutating a variable
/// (<c>SetTextVariable</c>) on a shared/cached <c>TextObject</c> returned by <c>FindText</c> would
/// corrupt it for every other call site that fetches the same id expecting the untouched default.
///
/// Bug fix (b): <see cref="ShowRenamePrompt"/> uses the verified 1.4.7 <c>TextInquiryData</c> ctor
/// order — <c>(titleText, text, affirmativeShown, negativeShown, affirmativeText, negativeText,
/// affirmativeAction, negativeAction, shouldInputBeObfuscated=false, textCondition=null,
/// soundEventPath="", defaultInputText="")</c>. The donor mod put the troop's fallback name in the
/// <c>soundEventPath</c> slot (position 11) and left <c>defaultInputText</c> (position 12) empty,
/// so the rename box never pre-filled the soldier's name. Pinned by
/// <c>FieldCommissionBindingTests.TextInquiryData_CtorBindingResolves_WithVerifiedParameterOrder</c>.
/// </summary>
public interface IInquiryPresenterAdapter
{
    /// <summary>"Promote &lt;troopName&gt;?" — accept/decline.</summary>
    void ShowPromotionOffer(string troopName, Action onAccept, Action onDecline);

    /// <summary>Informational: no companion room. Single acknowledge button.</summary>
    void ShowNoCompanionRoom(string troopName, Action onAcknowledge);

    /// <summary>Text-entry rename prompt pre-filled with <paramref name="troopName"/>.
    /// <paramref name="onConfirm"/> receives the raw entered text — the caller decides
    /// blank/whitespace fallback behavior.</summary>
    void ShowRenamePrompt(string troopName, Action<string> onConfirm, Action onSkip);
}
