using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace TAOM.Features.BattleBalance.Hooks;

/// <summary>
/// #1283: the wage and starvation lines of a garrison of a realm in SiegeDefence.UpkeepFreeGarrisons
/// (<see cref="GarrisonUpkeep"/>). The models stay branch-free and TaomPartyWageModel stays within its line baseline,
/// so the wage line resolves the config itself, as FactionUIPatchContext does.
/// </summary>
internal static class FreeGarrisonUpkeepLine
{
    private static IBattleBalanceConfigProvider _config;
    private static IBattleBalanceConfigProvider Config => _config ??= IoC.Resolve<IBattleBalanceConfigProvider>();

    // The base's own starvation label (DefaultPartyHealingModel._starvingText), so the refund merges into that line.
    private static TextObject _starvingText;
    private static TextObject StarvingText => _starvingText ??= new TextObject("{=jZYUdkXF}Starving");

    /// <summary>
    /// A free realm's garrison costs nothing. TotalWage feeds the owner clan's daily garrison expense, the garrison's wage
    /// budget (PaymentLimit - TotalWage - 5, which caps its daily growth and auto-recruitment) and the wage gate on its
    /// troop upgrades (PartyUpgraderCampaignBehavior), whose gold the owner clan's leader still pays.
    /// </summary>
    public static ExplainedNumber Wage(MobileParty party, ExplainedNumber wage, TextObject label)
    {
        if (party != null && party.IsGarrison
            && GarrisonUpkeep.IsUpkeepFree(Config?.GetConfig()?.SiegeDefence, party.CurrentSettlement?.MapFaction?.Culture?.StringId))
            wage.LimitMax(0f, label);
        return wage;
    }

    /// <summary>
    /// A free realm's garrison takes no starvation wounds (<see cref="GarrisonUpkeep.StarvationRefund"/>). The config is
    /// read only for a garrison on the starving branch, the one negative value.
    /// </summary>
    public static ExplainedNumber Healing(IBattleBalanceConfigProvider config, PartyBase party, ExplainedNumber healing)
    {
        var garrison = party?.MobileParty;
        if (healing.ResultNumber < 0f && garrison != null && garrison.IsGarrison)
        {
            var refund = GarrisonUpkeep.StarvationRefund(
                GarrisonUpkeep.IsUpkeepFree(config?.GetConfig()?.SiegeDefence, garrison.CurrentSettlement?.MapFaction?.Culture?.StringId),
                isGarrison: true, healing.ResultNumber);
            if (refund > 0f)
                healing.Add(refund, StarvingText);
        }
        return healing;
    }
}
