namespace TAOM.Features.BattleBalance;

/// <summary>
/// Garrison upkeep by realm culture (#1283, <see cref="BattleBalanceConfig.SiegeDefenceSection"/>). A realm in
/// UpkeepFreeGarrisons pays no garrison wages (TaomPartyWageModel), its garrisons eat no town food
/// (TaomSettlementFoodModel) and take no starvation wounds (TaomPartyHealingModel); DailyGarrisonGrowth gives its
/// fortifications free troops (TaomSettlementGarrisonModel). Each model keys on the fortification's realm
/// (Settlement.MapFaction), as the siege defence bonus does.
/// </summary>
public static class GarrisonUpkeep
{
    public static bool IsUpkeepFree(BattleBalanceConfig.SiegeDefenceSection section, string realmCultureId) =>
        section?.IsUpkeepFree(realmCultureId) ?? false;

    public static float DailyGrowth(BattleBalanceConfig.SiegeDefenceSection section, string realmCultureId) =>
        section?.GetDailyGarrisonGrowth(realmCultureId) ?? 0f;

    /// <summary>
    /// The food a free garrison's town gets back: the base's unscaled garrison term, members / men per food
    /// (DefaultSettlementFoodModel line 48). The base scales its charge: a governor with Generosity +1 or +2 raises it
    /// by 10%, so that garrison still eats a tenth; FoodConsumption buildings and, in a siege, a Gourmet governor lower
    /// it, so there the refund is larger than the charge.
    /// </summary>
    public static float GarrisonFoodRefund(bool upkeepFree, int garrisonMembers, int menPerFood) =>
        upkeepFree && garrisonMembers > 0 && menPerFood > 0 ? garrisonMembers / (float)menPerFood : 0f;

    /// <summary>
    /// Cancels a free garrison's starvation wounds. The base's regulars healing is negative only on its starving branch
    /// (DefaultPartyHealingModel lines 132 to 139), which excludes the healing branch, so the result becomes 0.
    /// PartyHealCampaignBehavior.ReduceHpMemberRegulars turns a negative value into wounded regulars, not deaths.
    /// </summary>
    public static float StarvationRefund(bool upkeepFree, bool isGarrison, float healing) =>
        upkeepFree && isGarrison && healing < 0f ? -healing : 0f;
}
