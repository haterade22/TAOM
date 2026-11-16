using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The two #1079 <c>[Check]</c> ids that only the client can write (<c>tools/ingame_checks/1079.toml</c> reads them), and the verdict
/// of each. Kept out of <see cref="LongDefeatHudView"/> so a test can call them without loading a <c>MissionView</c>.
/// </summary>
internal static class LongDefeatClientChecks
{
    internal const string HudId = "taom.long-defeat.hud";
    internal const string OrdersId = "taom.long-defeat.orders";
    internal const string UpgradeNoticeId = "taom.long-defeat.upgrade-notice";

    /// <summary>
    /// Both movies must have loaded: a missing prefab skips the load without an exception and leaves <c>IsLoaded</c> false, so the wave
    /// text alone proves only the view model.
    /// </summary>
    internal static CheckVerdict Hud(string? waveText, bool hudLoaded, bool panelLoaded)
    {
        var textSet = !string.IsNullOrEmpty(waveText);
        var detail = "movie=LongDefeatHud wave_text=" + (textSet ? "set" : "empty") + " hud_loaded=" + hudLoaded + " panel_loaded=" + panelLoaded;
        return LongDefeatChecks.Verdict(textSet && hudLoaded && panelLoaded, detail);
    }

    /// <summary>An upgrade buy or refund the HUD said (#1388): passes when the line it said has text.</summary>
    internal static CheckVerdict Notice(UpgradeNotice notice, string line)
    {
        var detail = "kind=" + notice.Kind.ToString().ToLowerInvariant()
            + " from=" + notice.Step.FromTroop + " to=" + notice.Step.ToTroop + " count=" + notice.Count + " left=" + notice.Left + " text=" + (line.Length > 0 ? "set" : "empty");
        return LongDefeatChecks.Verdict(line.Length > 0, detail);
    }

    /// <summary>Writes the check for an upgrade notice the panel said and the view has not yet recorded (key <c>&lt;view&gt;n&lt;serial&gt;</c>); returns the serial recorded so far.</summary>
    internal static int RecordNotice(IOutcomeLog? log, LongDefeatHudVM vm, string key, int recorded)
    {
        var notice = vm.SaidUpgrade;
        if (notice == null || notice.Serial == recorded)
            return recorded;

        LongDefeatChecks.Record(log, UpgradeNoticeId, key + "n" + notice.Serial, Notice(notice, vm.SaidUpgradeLine));
        return notice.Serial;
    }

    internal static CheckVerdict Orders(int sets) =>
        LongDefeatChecks.Verdict(sets > 0, sets > 0 ? "sets=" + sets : "sets=" + sets + " hint=friendly_mission_or_provider");
}
