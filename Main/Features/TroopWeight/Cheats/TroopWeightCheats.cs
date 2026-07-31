using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.TroopWeight.Cheats;

/// <summary>
/// `taom.print_party_size` — the whole weight-deflation chain for the main party.
///
/// Why it exists: the 2026-07-11 rework moved the elite tax off the member COUNT and onto the
/// party-size LIMIT, so every count reads raw and the limit shrinks instead. In-game that is
/// invisible — the player sees "150/240" with nothing to say which number the weight touched, which
/// is precisely why the original count-inflation bug went unnoticed for so long.
/// </summary>
public static class TroopWeightCheats
{
    private const string Usage =
        "Format is \"taom.print_party_size\".\n"
        + "Prints the main party's raw and weighted member counts, its true (pre-penalty) size limit,\n"
        + "the weight-deflation penalty, and the final clamped limit.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_party_size", "taom")]
    public static string PrintPartySize(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var party = MobileParty.MainParty;
            if (party?.Party == null) return "No main party.";

            var service = IoC.Resolve<ITroopWeightService>();

            var raw = party.Party.NumberOfAllMembers;
            var weighted = (int)Math.Ceiling(service.CalculateWeightedMemberCount(party.Party));
            var trueBase = service.GetTrueBaseSizeLimit(party.Party);

            // Read the deflated limit through the engine so the report shows what the game shows,
            // rather than re-deriving it and risking a number nothing else agrees with.
            var finalLimit = party.Party.PartySizeLimit;

            // Same read as TroopWeightService's own gate, so the report can never disagree with the
            // service about whether the feature is on.
            var weightEnabled = TaomSettings.Instance?.EnableTroopWeight ?? true;

            return FormatPartySize(
                party.Name?.ToString() ?? "Main party",
                raw, weighted, trueBase, finalLimit, weightEnabled);
        });

    /// <summary>
    /// Pure. A <c>penalty=0</c> reading MUST say why: <see cref="TroopWeightService.ComputeSizePenalty"/>
    /// returns 0 both when the party is light (nothing to tax) and when <c>baseLimit &lt; 2</c> — the
    /// guard added after a NaN-poisoned <c>(int)ResultNumber</c> cast defeated the clamp. Those two
    /// zeroes mean opposite things, and rendering them identically would hide the exact failure the
    /// guard exists to catch.
    /// </summary>
    internal static string FormatPartySize(
        string partyName, int rawCount, int weightedCount, int trueBaseLimit, int finalLimit, bool weightEnabled)
    {
        var penalty = TroopWeightService.ComputeSizePenalty(rawCount, weightedCount, trueBaseLimit);

        string note;
        if (penalty > 0)
            note = penalty == trueBaseLimit - 1 && weightedCount - rawCount > penalty
                ? " (clamped at baseLimit-1 — the weight surplus exceeds the whole limit)"
                : string.Empty;
        else if (trueBaseLimit < 2)
            note = " (degenerate base limit — penalty suppressed, NOT a light party)";
        else
            note = " (party under weight — nothing to tax)";

        return $"[TroopWeight] {partyName}: raw={rawCount} weighted={weightedCount} "
             + $"trueBase={trueBaseLimit} penalty={penalty} finalLimit={finalLimit}{note}\n"
             + $"[TroopWeight] EnableTroopWeight={weightEnabled}";
    }
}
