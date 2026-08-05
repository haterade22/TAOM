using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Entry-point-layer presenter for the wait menu's text and the service-status inquiry.
/// Exists so the menu behavior stays under the ADR-002 ceiling and so NOTHING allocates
/// per menu tick: the wait TextObject is built once per menu init (commander names don't
/// change mid-service), never per frame.
/// </summary>
public interface IEnlistmentWaitMenuPresenter
{
    /// <summary>Resolve the commander name once and push the wait text. Called from menu init only.</summary>
    void RefreshWaitText();

    void ShowServiceStatus();
}

public sealed class EnlistmentWaitMenuPresenter : IEnlistmentWaitMenuPresenter
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;

    private readonly TextObject _waitText = new TextObject(
        "{=taom_enlist_wait_text}You serve in {COMMANDER}'s company. The column moves at your commander's pace.");

    public EnlistmentWaitMenuPresenter(IEnlistmentStore store, ICommanderLordAdapter commander)
    {
        _store = store;
        _commander = commander;
    }

    public void RefreshWaitText()
    {
        var commanderName = _commander.GetSnapshot(_store.Record.CommanderHeroId).Name ?? "";
        _waitText.SetTextVariable("COMMANDER", commanderName);
        MBTextManager.SetTextVariable("TAOM_ENLISTMENT_WAIT_TEXT", _waitText);
    }

    public void ShowServiceStatus()
    {
        var record = _store.Record;
        var commanderName = _commander.GetSnapshot(record.CommanderHeroId).Name ?? "";
        var body = new TextObject(
            "{=taom_enlist_status_body}Commander: {COMMANDER}. Enlisted on day {ENLISTED_DAY}; contract ends on day {CONTRACT_END}.");
        body.SetTextVariable("COMMANDER", commanderName);
        body.SetTextVariable("ENLISTED_DAY", ((int)(record.EnlistedAtDay ?? 0)).ToString());
        body.SetTextVariable("CONTRACT_END", ((int)(record.ContractEndDay ?? 0)).ToString());
        InformationManager.ShowInquiry(new InquiryData(
            new TextObject("{=taom_enlist_status_title}Service record").ToString(),
            body.ToString(),
            true, false,
            new TextObject("{=taom_enlist_ok}Understood").ToString(), null,
            null, null));
    }
}
