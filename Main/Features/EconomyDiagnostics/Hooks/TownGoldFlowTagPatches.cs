using HarmonyLib;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.EconomyDiagnostics.Hooks;

/// <summary>
/// The four flow-tag patches, grouped because they are one mechanism expressed four times: each is a
/// two-line prefix/postfix pair that claims <see cref="TownGoldFlowScope"/> for the duration of a
/// known gold-moving call, so the single recorder on <c>SettlementComponent.ChangeGold</c> can name
/// the caller. Splitting one shared idiom across four near-identical files would obscure that they
/// must stay consistent with each other.
///
/// Ownership is outermost-wins (see <see cref="TownGoldFlowScope.TryEnter"/>), so a caravan sale
/// keeps its label while passing through the generic <c>SellItemsAction</c> underneath.
///
/// Deliberately not tagged: <c>WorkshopsCampaignBehavior</c>, the player's own market trades, and
/// the bandit-loot return — all land in <see cref="TownGoldFlow.Other"/>. A large <c>Other</c> is
/// therefore NOT synonymous with "workshops"; check your own trading first. Adding a fifth tag
/// before the data says it matters would be speculative, and the correct target if it ever is would
/// be the public <c>InventoryLogic.DoneLogic</c>, never the private nested listener.
/// </summary>
internal static class TownGoldFlowTagPatches
{
    /// <summary>
    /// The daily mean-reversion mint — `0.25 * (base + prosperity*12 - gold)`. Tagged so the report
    /// can show income and drain side by side; a net figure alone cannot distinguish "not enough is
    /// coming in" from "too much is going out", which is the whole question.
    /// </summary>
    [HarmonyPatch(typeof(ItemConsumptionBehavior), "UpdateTownGold")]
    [HarmonyPatchCategory("Patch68_EconomyDiagnostics")]
    public static class ItemConsumptionBehavior_UpdateTownGold_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(out bool __state) => __state = TownGoldFlowScope.TryEnter(TownGoldFlow.DailyMint);

        [HarmonyPostfix]
        public static void Postfix(bool __state) => TownGoldFlowScope.Exit(__state);
    }

    /// <summary>
    /// Residents buying shelf stock — the town's second income stream, and the one that dies when a
    /// broke town's workshops stop producing goods for the shelf.
    /// </summary>
    [HarmonyPatch(typeof(ItemConsumptionBehavior), "MakeConsumption")]
    [HarmonyPatchCategory("Patch68_EconomyDiagnostics")]
    public static class ItemConsumptionBehavior_MakeConsumption_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(out bool __state) =>
            __state = TownGoldFlowScope.TryEnter(TownGoldFlow.ResidentConsumption);

        [HarmonyPostfix]
        public static void Postfix(bool __state) => TownGoldFlowScope.Exit(__state);
    }

    /// <summary>
    /// The prime suspect. `SellGoodsForTradeAction` spends `min(qty, town.Gold / price)` per item
    /// across the villager's whole roster with no reserve, so one convoy can legally zero the pool.
    /// Patched on the public `ApplyByVillagerTrade` rather than the private `ApplyInternal` — same
    /// coverage (it is the only caller), less binding-drift surface.
    /// </summary>
    [HarmonyPatch(typeof(SellGoodsForTradeAction), nameof(SellGoodsForTradeAction.ApplyByVillagerTrade))]
    [HarmonyPatchCategory("Patch68_EconomyDiagnostics")]
    public static class SellGoodsForTradeAction_ApplyByVillagerTrade_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(out bool __state) =>
            __state = TownGoldFlowScope.TryEnter(TownGoldFlow.VillagerDelivery);

        [HarmonyPostfix]
        public static void Postfix(bool __state) => TownGoldFlowScope.Exit(__state);
    }

    /// <summary>
    /// AI trade in both directions — caravan sales/purchases, AI party food and horse purchases, and
    /// AI lord loot sales. All seven callers of this method gate on `IsMainParty`, so the player's own
    /// market trades are NOT here; they bypass `SellItemsAction` entirely (see `TownGoldFlow.Other`).
    /// One bucket on purpose: telling the AI flows apart needs a tag per caller, and the measurement
    /// comes first.
    /// </summary>
    [HarmonyPatch(typeof(SellItemsAction), nameof(SellItemsAction.Apply))]
    [HarmonyPatchCategory("Patch68_EconomyDiagnostics")]
    public static class SellItemsAction_Apply_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(out bool __state) => __state = TownGoldFlowScope.TryEnter(TownGoldFlow.Trade);

        [HarmonyPostfix]
        public static void Postfix(bool __state) => TownGoldFlowScope.Exit(__state);
    }
}
