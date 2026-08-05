using TAOM.Adapters;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment;

public class EnlistmentStateQuery : IEnlistmentStateQuery
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;

    public EnlistmentStateQuery(IEnlistmentStore store, ICommanderLordAdapter commander)
    {
        _store = store;
        _commander = commander;
    }

    public bool IsEnlisted => _store.Record.IsEnlisted;

    public EnlistmentState State => _store.Record.State;

    public string CommanderHeroId => _store.Record.CommanderHeroId;

    public string EnlistedHeroId => _store.Record.EnlistedHeroId;

    public double? ContractEndDay => _store.Record.ContractEndDay;

    public bool IsCommanderParty(string partyId)
    {
        if (string.IsNullOrEmpty(partyId))
            return false;
        var record = _store.Record;
        if (!record.IsEnlisted || string.IsNullOrEmpty(record.CommanderHeroId))
            return false;

        var partyIdOfCommander = _commander.GetSnapshot(record.CommanderHeroId).PartyId;
        return partyIdOfCommander == partyId;
    }
}
