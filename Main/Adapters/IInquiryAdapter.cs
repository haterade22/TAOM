using System;

namespace TAOM.Adapters;

/// <summary>
/// Generic two-option popup boundary for the Enlistment duty system (11 interactive duties
/// + 3 incidents share ONE presenter — <c>InteractiveDutyPresenter</c> — over this one
/// adapter). Every <c>TextObject</c> construction and the <c>InformationManager.ShowInquiry</c>/
/// <c>DisplayMessage</c> calls live here (ADR-007); the presenter passes plain key/fallback
/// strings, never a <c>TextObject</c>, so it stays 100%-testable without touching
/// <c>TaleWorlds.Localization</c>.
///
/// Verified 1.4.7 <c>InquiryData</c> ctor order: (titleText, text, isAffirmativeShown,
/// isNegativeShown, affirmativeText, negativeText, affirmativeAction, negativeAction, ...).
/// Every key/fallback pair follows the TAOM convention <c>{=key}fallback</c>. NOTE: these
/// keys are built from data-row ids at runtime (<c>taom_enlist_duty_{id}_title</c> etc.) —
/// static localization extraction tooling that greps for LITERAL <c>{=key}</c> strings will
/// NOT find them; see the feature doc for the follow-up needed before <c>/localize</c>.
/// </summary>
public interface IInquiryAdapter
{
    void ShowTwoOptionInquiry(
        string titleKey, string titleFallback,
        string bodyKey, string bodyFallback,
        string optionAKey, string optionAFallback,
        string optionBKey, string optionBFallback,
        Action onOptionA, Action onOptionB);

    /// <summary>Fire-and-forget toast. <paramref name="variableName"/>/<paramref name="variableValue"/> are optional — pass null to skip the substitution.</summary>
    void ShowMessage(string key, string fallback, string variableName, string variableValue);
}
