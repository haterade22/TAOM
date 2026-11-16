using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.BattleBalance.Models;

/// <summary>
/// #1283: free garrison growth by realm culture. GarrisonRecruitmentCampaignBehavior.TickGarrisonChangeForTown adds
/// this many of the realm's basic troop (town.MapFaction.BasicTroop) each day, whole troops, only while the
/// fortification is not in a battle or a siege, capped at the garrison size limit and at the wage budget divided by
/// that troop's wage. A realm in UpkeepFreeGarrisons pays no garrison wage, so its budget stays at the fief's wage
/// limit minus 5; a growth row without it is wage-capped and its troops cost wages. Vanilla's base adds +2 to rebel
/// settlements and the issue effects.
/// </summary>
public class TaomSettlementGarrisonModel : DefaultSettlementGarrisonModel
{
    private readonly IBattleBalanceConfigProvider _configProvider;

    public TaomSettlementGarrisonModel(IBattleBalanceConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    public override ExplainedNumber CalculateBaseGarrisonChange(Settlement settlement, bool includeDescriptions = false)
    {
        var result = base.CalculateBaseGarrisonChange(settlement, includeDescriptions);
        var growth = GarrisonUpkeep.DailyGrowth(_configProvider?.GetConfig()?.SiegeDefence, settlement?.MapFaction?.Culture?.StringId);
        if (growth > 0f)
            result.Add(growth, GameTexts.FindText("str_culture"));
        return result;
    }
}
