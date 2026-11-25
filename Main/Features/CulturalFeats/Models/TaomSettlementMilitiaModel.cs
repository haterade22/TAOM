using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.FactionPower.Hooks;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomSettlementMilitiaModel : DefaultSettlementMilitiaModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly IFactionPowerEconomyHook _factionPower;

    public TaomSettlementMilitiaModel(ICulturalFeatsService feats)
        : this(feats, IoC.Resolve<IFactionPowerEconomyHook>())
    {
    }

    internal TaomSettlementMilitiaModel(ICulturalFeatsService feats, IFactionPowerEconomyHook factionPower)
    {
        _feats = feats;
        _factionPower = factionPower;
    }

    public override ExplainedNumber CalculateMilitiaChange(Settlement settlement, bool includeDescriptions = false)
    {
        var result = base.CalculateMilitiaChange(settlement, includeDescriptions);
        _factionPower.ApplyMilitiaChange(settlement, ref result);
        return result;
    }

    public override ExplainedNumber CalculateVeteranMilitiaSpawnChance(Settlement settlement)
    {
        var result = base.CalculateVeteranMilitiaSpawnChance(settlement);
        _feats.ApplyVeteranMilitiaFeats(CultureFeatAdapter.FromOrNull(settlement.OwnerClan?.Culture), ref result);
        return result;
    }
}
