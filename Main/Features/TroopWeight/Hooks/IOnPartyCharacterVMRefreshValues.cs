using TaleWorlds.CampaignSystem.ViewModelCollection.Party;

namespace TAOM.Features.TroopWeight.Hooks;

/// Per-row weight tag on the party screen, so a header reading "19 / 20" over ten visible bodies is
/// self-explanatory instead of looking like a miscount.
public interface IOnPartyCharacterVMRefreshValues
{
    void OnPartyCharacterRefreshValues(PartyCharacterVM character);
}
