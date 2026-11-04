using TAOM.Features.LongDefeat.Diagnostics;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The two #1079 <c>[Check]</c> ids that only the client can write (<c>tools/ingame_checks/1079.toml</c> reads them), and the verdict
/// of each. Kept out of <see cref="LongDefeatHudView"/> so a test can call them without loading a <c>MissionView</c>.
/// </summary>
internal static class LongDefeatClientChecks
{
    internal const string HudId = "taom.long-defeat.hud";
    internal const string OrdersId = "taom.long-defeat.orders";

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

    internal static CheckVerdict Orders(int sets) =>
        LongDefeatChecks.Verdict(sets > 0, sets > 0 ? "sets=" + sets : "sets=" + sets + " hint=friendly_mission_or_provider");
}
