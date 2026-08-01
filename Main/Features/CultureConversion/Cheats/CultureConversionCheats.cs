using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.CultureConversion.Cheats;

/// <summary>
/// `taom.requeue_settlement <settlement>` — a live regression guard for #333.
///
/// #333 was a conversion-timer restart: capturing a fief and then being granted it by the kingdom
/// fires `OnSettlementConquered` twice, and the second fire used to reset the hold. Reproducing it
/// today means raising an army, taking a cross-culture fief, and waiting for the grant. This fires
/// the same path twice on demand and reports whether the pending timer moved.
///
/// Tier B: it drives an existing code path with the settlement's real current owner rather than
/// forcing an outcome, so re-running it is idempotent by the very guard it tests. No new service
/// methods — <see cref="ICultureConversionService.OnSettlementConquered"/> and
/// <see cref="ICultureConversionStore.TryGet"/> are enough.
/// </summary>
public static class CultureConversionCheats
{
    private const string Usage =
        "Format is \"taom.requeue_settlement [SettlementName/SettlementId]\".\n"
        + "Fires the owner-changed path twice, as a fief grant after a capture does, and reports\n"
        + "whether the conversion hold timer restarted. A restart is bug #333.";

    [CommandLineFunctionality.CommandLineArgumentFunction("requeue_settlement", "taom")]
    public static string RequeueSettlement(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            if (args.Count == 0) return "Expected a settlement name or id.\n" + Usage;

            Settlement settlement;
            if (!CampaignCheats.TryGetObject(CampaignCheats.ConcatenateString(args), out settlement, out var error))
                return error + "\n" + Usage;

            if (!settlement.IsFortification)
                return $"'{settlement.Name}' is not a town or castle — only fortifications have conversion timers.";

            var settings = IoC.Resolve<ICultureConversionSettingsProvider>();
            if (!settings.IsEnabled)
                return "Culture conversion is disabled — enable it in MCM first, or this reports nothing "
                     + "because the service no-ops rather than because the guard held.";

            var service = IoC.Resolve<ICultureConversionService>();
            var store = IoC.Resolve<ICultureConversionStore>();

            // Refuse when the settlement has no record yet. Without this the FIRST internal fire would
            // call StartPending and Put — arming a real, persisted conversion timer that did not exist
            // before the diagnostic ran, which the next daily tick would eventually complete into an
            // actual culture flip. That is a Tier C mutation from a command documented as a read-mostly
            // regression guard. With a record already present, both fires hit the guard this command
            // exists to test and nothing new is armed.
            if (!store.TryGet(settlement.StringId, out _))
                return $"'{settlement.Name}' has no conversion record yet, so firing the owner-changed "
                     + "path would ARM a new timer rather than test the guard. Capture or be granted the "
                     + "fief first — this command verifies an existing timer, it does not create one.";

            var now = CampaignTime.Now.ToDays;

            service.OnSettlementConquered(settlement.StringId, now);
            var first = ReadPending(store, settlement.StringId, out var target);

            // The second fire is the fief-grant that follows a capture — the exact double-fire #333
            // was about. A tiny day delta mirrors the real gap between the two events.
            service.OnSettlementConquered(settlement.StringId, now + 0.01);
            var second = ReadPending(store, settlement.StringId, out _);

            return FormatRequeue(settlement.StringId, settlement.Name?.ToString() ?? settlement.StringId,
                target, first, second);
        });

    private static double? ReadPending(ICultureConversionStore store, string settlementId, out string targetCultureId)
    {
        targetCultureId = null;
        if (!store.TryGet(settlementId, out var record) || record == null) return null;
        targetCultureId = record.PendingTargetCultureId;
        return record.PendingStartDays;
    }

    /// <summary>
    /// Pure. Four outcomes, all distinguishable: the guard held, the guard regressed, nothing was
    /// queued at all, or the timer appeared only on the second fire. Collapsing "nothing queued" into
    /// "unchanged" would report a passing regression test for a case that never exercised the guard.
    /// </summary>
    internal static string FormatRequeue(
        string settlementId, string settlementName, string targetCultureId, double? firstPending, double? secondPending)
    {
        var header = $"[CultureConversion] {settlementId} {settlementName}";

        if (!firstPending.HasValue && !secondPending.HasValue)
            return header + ": no conversion was queued by either fire — the owner's culture most likely "
                 + "already matches, so the #333 guard was never exercised.";

        if (!firstPending.HasValue)
            return header + $": a timer appeared only after the second fire (day {secondPending:0.##}, "
                 + $"target {targetCultureId}). The first fire queued nothing — worth investigating separately.";

        var target = string.IsNullOrEmpty(targetCultureId) ? "?" : targetCultureId;
        var firstLine = $"{header}: pending -> {target} @ day {firstPending:0.##}";

        if (secondPending.HasValue && secondPending.Value > firstPending.Value)
            return firstLine + $"\n[CultureConversion]   second fire: day {secondPending:0.##} — RESTARTED. "
                 + "Bug #333 has regressed: the hold timer resets on a fief grant after capture.";

        return firstLine + $"\n[CultureConversion]   second fire: day {secondPending:0.##} — UNCHANGED. "
             + "The #333 guard holds.";
    }
}
