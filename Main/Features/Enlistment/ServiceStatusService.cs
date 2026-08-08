using TAOM.Adapters;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Features.Enlistment;

/// <summary>
/// Builds the wait menu's live status and re-pushes the text only when it actually changed.
///
/// THE REAL ROOT CAUSE OF THE FROZEN TEXT, verified against installed v1.4.7 rather than assumed:
/// it was never a caching problem. <c>GameMenuVM.IsMenuTextChanged</c> compares
/// <c>_menuTextAttributes.Count</c> (an <c>int</c>) against <c>_menuText?.Attributes?.Count</c> (an
/// <c>int?</c>); our menu TextObject is built by <c>new TextObject(text)</c> with no attributes, so
/// that comparison is <c>0 != null</c> — always TRUE under C#'s lifted equality. The menu therefore
/// re-renders EVERY frame and <c>.ToString()</c> re-resolves the global variable each time. The
/// sentence looked frozen because <c>RefreshWaitText</c> ran only on menu init and its one token
/// was the commander's name, which does not change all term. Nothing new was ever computed.
///
/// So the fix is cadence plus varying content — NOT the <c>MenuContext.GameMenu.GetText()</c>
/// plumbing an earlier design called for. That change would have been real code aimed at a bug this
/// menu does not have.
/// </summary>
public interface IServiceStatusService
{
    /// <summary>Current status, or null when not enlisted.</summary>
    ServiceStatusModel Build();

    /// <summary>
    /// Rebuild and push the menu text if anything changed. Cheap on the unchanged path — one
    /// snapshot read and a value comparison, no allocation of a TextObject.
    /// </summary>
    bool RefreshIfChanged();

    /// <summary>Drop the cached model so the next refresh always pushes (menu init, discharge).</summary>
    void Invalidate();
}

public class ServiceStatusService : IServiceStatusService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _content;
    private readonly ICommanderLordAdapter _commander;
    private readonly IServiceStatusTextWriter _writer;

    private ServiceStatusModel _last;

    public ServiceStatusService(
        IEnlistmentStore store,
        IEnlistmentContentStore content,
        ICommanderLordAdapter commander,
        IServiceStatusTextWriter writer)
    {
        _store = store;
        _content = content;
        _commander = commander;
        _writer = writer;
    }

    public ServiceStatusModel Build()
    {
        var record = _store.Record;
        if (!record.IsEnlisted)
            return null;

        var commander = _commander.GetSnapshot(record.CommanderHeroId) ?? CommanderSnapshot.Missing;
        var content = _content.Record;

        return new ServiceStatusModel(
            commanderName: commander.Name,
            activity: ResolveActivity(commander),
            settlementName: commander.SettlementName,
            rank: content.Rank,
            assignment: content.Assignment,
            daysServed: content.DaysServed,
            trust: content.Trust,
            deferredWages: content.DeferredWages,
            activeDutyId: content.ActiveDutyId);
    }

    /// <summary>
    /// Order matters: a battle outranks a siege outranks being in a settlement. A commander
    /// assaulting a town is in all three states at once, and "in battle" is the one the player
    /// needs to see.
    /// </summary>
    private static CommanderActivity ResolveActivity(CommanderSnapshot commander)
    {
        if (!commander.Exists || !commander.IsAlive || commander.IsPrisoner || !commander.HasParty)
            return CommanderActivity.Unavailable;
        if (commander.PartyIsInMapEvent)
            return CommanderActivity.InBattle;
        if (commander.PartyIsBesieging)
            return CommanderActivity.Besieging;
        if (commander.PartyIsInSettlement)
            return CommanderActivity.InSettlement;
        return CommanderActivity.Marching;
    }

    public bool RefreshIfChanged()
    {
        var current = Build();
        if (current == null)
            return false;
        if (current.Equals(_last))
            return false;

        _last = current;
        _writer.Write(current);
        return true;
    }

    public void Invalidate() => _last = null;
}
