using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.SettlementEconomy.Cheats;

/// <summary>
/// `taom.print_town_economy [town]` — a town's market gold against both TAOM's and vanilla's math.
///
/// Why it exists: #317 is "towns run out of gold and never recover". Confirming the fix means
/// watching a broke town for 4–8 in-game days, and then repeating it with the MCM toggle off to tell
/// TAOM's model from vanilla's. Both columns print here at once, and the vanilla one costs nothing:
/// <see cref="ISettlementEconomyService.ComputeTownGoldChange"/> with a null config computes with the
/// vanilla constants, so the report cannot drift from the engine's own numbers the way a duplicated
/// formula would.
/// </summary>
public static class SettlementEconomyCheats
{
    private const string Usage =
        "Format is \"taom.print_town_economy [TownName/TownId]\".\n"
        + "With no argument, uses the town you are currently in.\n"
        + "Prints market gold, prosperity, TAOM's equilibrium target and daily change, the vanilla\n"
        + "figures for comparison, and which model is actually in effect.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_town_economy", "taom")]
    public static string PrintTownEconomy(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var settlement = ResolveSettlement(args, out var error);
            if (settlement == null) return error + "\n" + Usage;

            // Castles never reach TaomSettlementEconomyModel: its sole engine caller
            // (ItemConsumptionBehavior.UpdateTownGold) iterates Town.AllTowns. Computing a number for
            // a castle would actively mislead the #317 investigation, so refuse instead.
            if (settlement.Town == null || !settlement.IsTown)
                return $"'{settlement.Name}' is not a town. Castles never reach the town-gold model "
                     + "(the engine only ticks Town.AllTowns), so there is nothing to report.";

            var town = settlement.Town;
            var service = IoC.Resolve<ISettlementEconomyService>();
            var config = IoC.Resolve<ISettlementEconomyConfigProvider>().GetConfig();

            var taomChange = service.ComputeTownGoldChange(town.Prosperity, town.Gold, config);
            var vanillaChange = service.ComputeTownGoldChange(town.Prosperity, town.Gold, null);
            var target = config.TownGoldBase + town.Prosperity * config.TownGoldPerProsperity;

            return FormatTownEconomy(
                settlement.StringId, settlement.Name?.ToString() ?? settlement.StringId,
                town.Gold, town.Prosperity, taomChange, vanillaChange,
                target, config.TownGoldRegenRate,
                TaomSettings.Instance?.EnableSettlementEconomyTuning ?? true);
        });

    private static Settlement ResolveSettlement(List<string> args, out string error)
    {
        error = string.Empty;

        if (args.Count == 0)
        {
            var current = Settlement.CurrentSettlement;
            if (current != null) return current;
            error = "Not in a settlement. Pass a town name or id.";
            return null;
        }

        // Vanilla's resolver: StringId then name, across eight case/space permutations, with a ready
        // error string. Reusing it keeps TAOM's id handling identical to campaign.* commands.
        Settlement settlement;
        return CampaignCheats.TryGetObject(CampaignCheats.ConcatenateString(args), out settlement, out error)
            ? settlement
            : null;
    }

    /// <summary>
    /// Pure. The vanilla column is the point — #317's real question is "is the buff doing anything",
    /// and a single TAOM number cannot answer it.
    /// </summary>
    internal static string FormatTownEconomy(
        string townId, string townName, int gold, float prosperity,
        int taomChange, int vanillaChange, float taomTarget, float rate, bool enabled)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[TownEconomy] {townId} {townName}: gold={gold:N0} prosperity={prosperity:N0}");
        sb.AppendLine($"[TownEconomy]   TAOM:    target={taomTarget:N0}  dailyChange={taomChange:+#,##0;-#,##0;0}");
        sb.AppendLine($"[TownEconomy]   vanilla: dailyChange={vanillaChange:+#,##0;-#,##0;0}");
        sb.AppendLine($"[TownEconomy]   model in effect: {(enabled ? "TAOM" : "vanilla (EnableSettlementEconomyTuning is off)")}");
        sb.Append($"[TownEconomy]   {DescribeTrajectory(gold, taomChange, taomTarget, rate)}");
        return sb.ToString();
    }

    // Kept separate because every branch here is a trap: a zero rate would divide by zero, and a town
    // ABOVE its target mean-reverts downward, where a "days to target" figure would be nonsense.
    private static string DescribeTrajectory(int gold, int change, float target, float rate)
    {
        if (rate <= 0f)
            return "regen frozen (TownGoldRegenRate is 0 — gold will not move)";

        if (gold >= target)
            return "gold is above target — the model mean-reverts DOWNWARD from here, no recovery estimate applies";

        if (change <= 0)
            return "below target but the daily change is not positive — check the config";

        var days = (int)Math.Ceiling((target - gold) / change);
        return $"below target, roughly {days} days to converge at the current rate";
    }
}
