using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.Namegiving.UI;

/// <summary>
/// The three prompts of a rename (docs/features/namegiving.md): a picker of the selected fief and its villages (each row shows its
/// price; a row that cannot be renamed is disabled with its reason), a text box prefilled with the current name whose OK button
/// is greyed with the reason while the name is invalid, and a confirmation. The last step asks <see cref="NamegivingService.Apply"/>,
/// which re-checks every gate against the engine's state at that moment. Every callback runs inside the mixin's
/// <see cref="ScreenFaultLog"/> (one log line per site per screen): they run from the inquiry popups, where a throw would take the popup down.
/// </summary>
internal sealed class NamegivingPrompts
{
    private readonly NamegivingService _service;
    private readonly ScreenFaultLog _faults;
    private readonly Action<string> _onRenamed;

    internal NamegivingPrompts(NamegivingService service, ScreenFaultLog faults, Action<string> onRenamed)
    {
        _service = service;
        _faults = faults;
        _onRenamed = onRenamed;
    }

    /// <summary>Opens the picker for the selected fief.</summary>
    internal void Start(string fiefId)
    {
        var targets = _service.Targets(fiefId);
        if (targets.Count == 0)
            return;
        var elements = targets.Select(t => new InquiryElement(
            t, NamegivingTexts.Entry(t.Name, t.Cost), null, t.Block == RenameBlock.None,
            t.Block == RenameBlock.None ? null : NamegivingTexts.Block(t.Block, t.Detail))).ToList();

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            titleText: NamegivingTexts.PickTitle(),
            descriptionText: NamegivingTexts.PickBody(),
            inquiryElements: elements,
            isExitShown: true,
            minSelectableOptionCount: 1,
            maxSelectableOptionCount: 1,
            affirmativeText: GameTexts.FindText("str_ok").ToString(),
            negativeText: GameTexts.FindText("str_cancel").ToString(),
            affirmativeAction: chosen => _faults.Run("picker", () =>
            {
                if (chosen != null && chosen.Count > 0 && chosen[0].Identifier is RenameTarget target)
                    AskName(fiefId, target);
            }),
            negativeAction: _ => { }), pauseGameActiveState: true);
    }

    private void AskName(string fiefId, RenameTarget target)
    {
        // One snapshot of the other names per prompt: the check runs on every keystroke, and Apply re-reads them at the end.
        var others = _service.OtherNames(target.Id);
        InformationManager.ShowTextInquiry(new TextInquiryData(
            NamegivingTexts.InputTitle(target.Name),
            NamegivingTexts.InputBody(target.Cost),
            true,
            true,
            GameTexts.FindText("str_ok").ToString(),
            GameTexts.FindText("str_cancel").ToString(),
            typed => _faults.Run("name", () => Confirm(fiefId, target, typed)),
            () => { },
            shouldInputBeObfuscated: false,
            textCondition: typed => Condition(typed, target.Name, others),
            soundEventPath: string.Empty,
            defaultInputText: target.Name), pauseGameActiveState: true);
    }

    // Runs on every keystroke and when the box opens, so the prefilled current name starts with OK greyed ("already its name").
    private Tuple<bool, string> Condition(string typed, string current, IEnumerable<string> others)
    {
        try
        {
            var problem = NamegivingService.ValidateName(typed, current, others);
            return Tuple.Create(problem == NameProblem.None, NamegivingTexts.Problem(problem));
        }
        catch (Exception ex)
        {
            _faults.LogOnce("condition", ex);
            return Tuple.Create(false, "");
        }
    }

    private void Confirm(string fiefId, RenameTarget target, string typed)
    {
        InformationManager.ShowInquiry(new InquiryData(
            NamegivingTexts.ConfirmTitle(),
            NamegivingTexts.ConfirmBody(target.Name, typed, target.Cost),
            true,
            true,
            GameTexts.FindText("str_yes").ToString(),
            GameTexts.FindText("str_no").ToString(),
            () => _faults.Run("apply", () => Apply(fiefId, target, typed)),
            () => { }), pauseGameActiveState: true);
    }

    private void Apply(string fiefId, RenameTarget target, string typed)
    {
        var result = _service.Apply(target.Id, typed);
        if (result.Success)
        {
            InformationManager.DisplayMessage(new InformationMessage(NamegivingTexts.Done(target.Name, typed)));
            _onRenamed(fiefId);
            return;
        }

        var why = result.Problem != NameProblem.None ? NamegivingTexts.Problem(result.Problem) : NamegivingTexts.Block(result.Block, result.Detail);
        if (why.Length > 0)
            InformationManager.DisplayMessage(new InformationMessage(why));
    }
}
