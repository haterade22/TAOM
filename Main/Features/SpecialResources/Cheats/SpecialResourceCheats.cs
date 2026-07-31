using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.SpecialResources.Cheats;

/// <summary>
/// Console cheat for the Special Resources economy — the counterpart to vanilla's
/// <c>campaign.add_gold_to_hero</c>. Discovered by reflection: <see cref="CommandLineFunctionality"/>
/// scans every loaded assembly that references TaleWorlds.Library for static methods carrying
/// <see cref="CommandLineFunctionality.CommandLineArgumentFunction"/> and returning <c>string</c>,
/// so this needs no registration in <c>SubModule.cs</c> or <c>IoC.cs</c>. Requires cheat mode.
///
/// Thin entry point (ADR-002): parses/validates console text, then delegates the whole grant to
/// <see cref="ISpecialResourceService.GrantAmount"/>. Service-locator resolution is unavoidable and
/// intended here — a static console method has no constructor to inject through (same precedent as
/// <c>SpecialResourceMapBarMixin</c>). Only strings cross into the service, so no adapter is needed.
///
/// The cheat gate, the help branch and the catch-all live in <see cref="TaomConsole.RunInCampaign"/>
/// with every other <c>taom.*</c> command; see <c>docs/features/dev-console.md</c>.
/// </summary>
public static class SpecialResourceCheats
{
    private const float DefaultAmount = 1000f;

    private const string Usage =
        "Format is \"taom.add_special_resources [amount]\".\n"
        + "If amount is not specified, 1000 is added. Negative amounts deduct (floored at 0).\n"
        + "Adds to the special resource your kingdom/culture currently grants, clamped to its cap.";

    private const string DumpUsage =
        "Format is \"taom.print_special_resources\".\n"
        + "Read-only: the resource your kingdom/culture grants, your balance, cap and tier.";

    /// <summary>
    /// Shared so the add and print commands cannot disagree about what "nothing is granted" reads
    /// like. <see cref="FormatResult"/> appends its own action-specific clause.
    /// </summary>
    internal const string NoResourceMessage = "Your kingdom/culture grants no special resource";

    [CommandLineFunctionality.CommandLineArgumentFunction("add_special_resources", "taom")]
    public static string AddSpecialResources(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var amount = DefaultAmount;
            if (!CampaignCheats.CheckParameters(args, 0))
            {
                if (!CampaignCheats.CheckParameters(args, 1))
                    return "Expected at most one argument.\n" + Usage;
                // TryParseAmount rejects "NaN"/"Infinity", which float.TryParse accepts and which
                // would poison the saved balance.
                if (!DevConsoleArgs.TryParseAmount(args[0], out amount, out var parseError))
                    return parseError + "\n" + Usage;
            }

            var hero = Hero.MainHero;
            if (hero == null) return "No main hero.";

            var result = IoC.Resolve<ISpecialResourceService>().GrantAmount(
                hero.StringId,
                hero.Clan?.Kingdom?.StringId,
                hero.Culture?.StringId,
                amount);

            return FormatResult(result, amount);
        });

    /// <summary>
    /// Read-only counterpart to <see cref="AddSpecialResources"/>.
    ///
    /// Deliberately NOT implemented as <c>GrantAmount(..., 0f)</c>: that would look like a free read,
    /// but it clamps to the cap and writes back, so on a save whose balance predates a lowered cap a
    /// command named "print" would silently mutate the balance.
    /// </summary>
    [CommandLineFunctionality.CommandLineArgumentFunction("print_special_resources", "taom")]
    public static string PrintSpecialResources(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, DumpUsage, args =>
        {
            var hero = Hero.MainHero;
            if (hero == null) return "No main hero.";

            var kingdomId = hero.Clan?.Kingdom?.StringId;
            var cultureId = hero.Culture?.StringId;

            var service = IoC.Resolve<ISpecialResourceService>();
            var resource = service.ResolveResource(kingdomId, cultureId);
            if (resource == null)
                return FormatDump(null, null, 0f, 0f, 0, 0, 0f);

            return FormatDump(
                resource.DisplayName,
                resource.Id,
                service.GetCurrentAmount(hero.StringId, kingdomId, cultureId),
                resource.Cap,
                service.GetCurrentTierLevel(hero.StringId, kingdomId, cultureId),
                resource.TierThresholds?.Count ?? 0,
                service.GetAvailableAfterPending(hero.StringId, kingdomId, cultureId));
        });

    /// <summary>
    /// Pure. Tier and pending clauses are omitted rather than rendered as zeroes — "tier 0 of 3" and
    /// a pending figure equal to the balance both read as information when they are noise.
    /// </summary>
    internal static string FormatDump(
        string displayName, string resourceId, float amount, float cap,
        int tierLevel, int tierCount, float availableAfterPending)
    {
        if (string.IsNullOrEmpty(resourceId))
            return NoResourceMessage + ".";

        var line = $"{displayName} ({resourceId}): {amount:0.##} / {cap:0.##}";

        if (tierCount > 0)
            line += tierLevel > 0 ? $"  [tier {tierLevel} of {tierCount}]" : "  [no tier reached]";

        // Only worth showing when an open party-screen session is holding spend against the balance.
        if (Math.Abs(availableAfterPending - amount) > 0.005f)
            line += $"  (spendable after pending: {availableAfterPending:0.##})";

        return line;
    }

    /// <summary>
    /// Builds the console echo. Reports from the UNCLAMPED result (<c>Before + amount</c>) rather
    /// than the sign of the request: a save whose balance predates a lowered cap clamps on a
    /// NEGATIVE grant too (550 − 10 → 500, not 540), and an <c>amount &gt; 0</c> test would hide
    /// exactly that case. Internal so the branches are testable without a running campaign.
    /// </summary>
    internal static string FormatResult(ResourceGrantResult result, float amount)
    {
        if (!result.Resolved)
            return NoResourceMessage + " — nothing to add.";

        var natural = result.Before + amount;
        var change = $"{result.DisplayName}: {result.Before:0.##} -> {result.After:0.##}";
        if (natural > result.Cap) return $"{change} (clamped at cap {result.Cap:0.##})";
        if (natural < 0f) return $"{change} (floored at 0)";
        return $"{change} (cap {result.Cap:0.##})";
    }
}
