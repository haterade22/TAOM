using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TAOM.Features.DevConsole;
using TAOM.Features.FieldCommission.Domain;

namespace TAOM.Features.FieldCommission.Cheats;

/// <summary>
/// `taom.fc_grant_merit &lt;troopId&gt; &lt;amount&gt;` and `taom.fc_status` — Battlefield Promotions
/// diagnostics, routed through <see cref="TaomConsole"/> (see docs/features/dev-console.md).
/// </summary>
public static class FieldCommissionCheats
{
    private const string GrantUsage =
        "Format is \"taom.fc_grant_merit [troopId] [amount]\".\n"
        + "Directly adds merit to a troop type's bank, no kill/battle context needed — for testing\n"
        + "promotion offers without playing out a battle. Example: taom.fc_grant_merit taom_soldier 8";

    private const string StatusUsage =
        "Format is \"taom.fc_status\". Reports config, banked merit per troop, promoted-companion\n"
        + "count, and whether an offer is queued/currently showing.";

    [CommandLineFunctionality.CommandLineArgumentFunction("fc_grant_merit", "taom")]
    public static string GrantMerit(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, GrantUsage, args =>
        {
            if (args.Count < 2) return "Expected a troop id and an amount.\n" + GrantUsage;

            var troopId = args[0];
            if (MBObjectManager.Instance?.GetObject<CharacterObject>(troopId) == null)
                return $"Unknown troop '{troopId}'. Check ModuleData/troops for the id.";

            if (!DevConsoleArgs.TryParseCount(args[1], 1, 100000, out var amount, out var error))
                return error + "\n" + GrantUsage;

            var merit = IoC.Resolve<IFieldCommissionMeritService>();
            merit.GrantMerit(troopId, amount);
            return FormatGrant(troopId, amount, merit.GetMerit(troopId));
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("fc_status", "taom")]
    public static string Status(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StatusUsage, args =>
        {
            var config = IoC.Resolve<IFieldCommissionConfigProvider>().GetConfig();
            var merit = IoC.Resolve<IFieldCommissionMeritService>();
            var offerFlow = IoC.Resolve<IFieldCommissionOfferFlowService>();

            return FormatStatus(
                config,
                merit.ExportMerits(),
                merit.GetPromotedHeroIds().Count,
                merit.HasPendingOffers,
                offerFlow.IsShowingOffer);
        });

    internal static string FormatGrant(string troopId, int amount, int newTotal) =>
        $"[FieldCommission] granted {amount} merit to {troopId} — now banked at {newTotal}.";

    internal static string FormatStatus(
        FieldCommissionConfig config, Dictionary<string, int> merits, int promotedCount, bool hasPendingOffers, bool isShowingOffer)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"[FieldCommission] enabled={config.Enabled} ratioThreshold={config.RatioThreshold:0.00} " +
            $"meritPerKill={config.MeritPerKill} meritThreshold={config.MeritThreshold} retainerAllowance={config.RetainerAllowance}");
        sb.AppendLine(
            $"[FieldCommission] promoted companions so far: {promotedCount}; offer pending: {hasPendingOffers}; offer showing now: {isShowingOffer}");

        if (merits == null || merits.Count == 0)
            sb.Append("[FieldCommission] no banked merit.");
        else
            sb.Append("[FieldCommission] banked merit: ")
              .Append(string.Join(", ", merits.OrderByDescending(m => m.Value).Select(m => $"{m.Key}={m.Value}")));

        return sb.ToString();
    }
}
