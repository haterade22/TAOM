using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight.Hooks;

/// Party screen transfers in the weighted frame (#1430): the own-limit verdict recorded when the screen opens, and
/// Transfer All's member budget.
public interface IOnPartyScreenLogicTransfer
{
    void OnInitialize(PartyScreenLogic logic);

    int OnTransferAllBudget(int vanillaBudget, PartyScreenLogic logic, PartyScreenLogic.PartyCommand command);
}
