using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Features.CaravanTrade.Diagnostics;
using TAOM.Features.DevConsole;

namespace TAOM.Features.CaravanTrade.Cheats;

/// <summary>
/// `taom.print_caravans [settlement]` — why each caravan is or is not moving.
///
/// Why it exists: "caravans are sitting in my city" has many distinct engine causes — see
/// <see cref="CaravanGate"/> for the current set — and every one is a silent early-return with no
/// log line, no event, and no visible difference between them. They do not share a fix. The first
/// time this was reported it was read as "the town has no money", which is the one thing that does
/// NOT directly park a caravan, so this command exists to make the guess unnecessary.
///
/// Read-only: every field it touches is a public engine getter and nothing here mutates campaign
/// state. It deliberately does NOT invoke the engine's private <c>ThinkNextDestination</c> to answer
/// the zero-score question — that call mutates a scratch cache, and a diagnostic must not perturb
/// the thing it is measuring. It reports the two inputs that make a zero score inevitable instead.
/// </summary>
public static class CaravanTradeCheats
{
    private const string Usage =
        "Format is \"taom.print_caravans [SettlementName/SettlementId]\".\n"
        + "With no argument, reports every caravan currently inside a settlement.\n"
        + "Prints each caravan's blocking gate, wounded fraction, trade gold, cargo and target.";

    /// <summary>A whole-map dump of every caravan would be unreadable and slow; this bounds it.</summary>
    private const int MaxCaravansReported = 40;

    [CommandLineFunctionality.CommandLineArgumentFunction("print_caravans", "taom")]
    public static string PrintCaravans(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            Settlement settlement = null;
            if (args.Count > 0)
            {
                if (!CampaignCheats.TryGetObject(CampaignCheats.ConcatenateString(args), out settlement, out var error))
                    return error + "\n" + Usage;
            }

            var caravans = CollectCaravans(settlement);
            if (caravans.Count == 0)
            {
                return settlement == null
                    ? "[Caravans] No caravans are currently inside a settlement."
                    : $"[Caravans] No caravans are currently inside '{settlement.Name}'.";
            }

            var service = new CaravanGateDiagnosticsService();
            var verdicts = caravans
                .Take(MaxCaravansReported)
                .Select(c => (Snapshot: c, Verdict: service.Evaluate(c)))
                .ToList();

            return CaravanGateReportFormatter.Format(
                settlement?.Name?.ToString(), verdicts, caravans.Count, MaxCaravansReported);
        });

    /// <summary>
    /// Scoped to caravans inside a settlement — a caravan on the open map is by definition moving,
    /// and including them would bury the parked ones this command exists to find.
    /// </summary>
    private static List<CaravanGateSnapshot> CollectCaravans(Settlement settlement)
    {
        var result = new List<CaravanGateSnapshot>();

        foreach (var party in MobileParty.AllCaravanParties)
        {
            // Computed TaleWorlds getters throw before a null check reaches them (adapters.md), so
            // every hop off the party is null-conditional.
            var current = party?.CurrentSettlement;
            if (current == null) continue;
            if (settlement != null && current != settlement) continue;

            result.Add(Capture(party, current));
        }

        return result;
    }

    /// <summary>
    /// The ADR-007 boundary: sealed engine types are read here and never cross into the service.
    /// </summary>
    private static CaravanGateSnapshot Capture(MobileParty party, Settlement current)
    {
        var ai = party.Ai;
        var roster = party.MemberRoster;

        return new CaravanGateSnapshot
        {
            CaravanId = party.StringId,
            CaravanName = party.Name?.ToString() ?? party.StringId,
            CurrentSettlementId = current.StringId,
            TargetSettlementId = party.TargetSettlement?.StringId,
            IsInFortification = current.IsFortification,
            IsCurrentSettlementUnderSiege = current.IsUnderSiege,
            IsBlockadeActive = current.SiegeEvent?.IsBlockadeActive ?? false,
            HasNavalNavigationCapability = party.HasNavalNavigationCapability,
            HasMapEvent = party.MapEvent != null,
            IsPartyTradeActive = party.IsPartyTradeActive,
            DoNotMakeNewDecisions = ai?.DoNotMakeNewDecisions ?? false,
            IsAiDisabled = ai?.IsDisabled ?? false,
            IsAlerted = ai?.IsAlerted ?? false,
            IsFleeing = party.ShortTermBehavior == AiBehavior.FleeToPoint,
            IsOnHold = party.ShortTermBehavior == AiBehavior.Hold,
            IsCurrentlyUsedByAQuest = party.IsCurrentlyUsedByAQuest,
            TotalManCount = roster?.TotalManCount ?? 0,
            TotalWounded = roster?.TotalWounded ?? 0,
            PartyTradeGold = party.PartyTradeGold,
            CargoItemCount = party.ItemRoster?.Count ?? 0,
        };
    }
}
