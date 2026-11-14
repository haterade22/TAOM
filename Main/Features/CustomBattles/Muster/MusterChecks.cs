using System;

namespace TAOM.Features.CustomBattles.Muster;

// The [Check] lines for #1184: the muster screen starts a battle in which the player serves as a captain.
// tools/ingame_checks/1184.toml reads them. The key of each call is the game type, so one line per type and result.
internal static class MusterChecks
{
    internal const string CaptainStartId = "taom.muster.captain-start";

    // A siege starts without a general (the engine does not read one there), so "none" is a valid value.
    internal static string StartedDetail(string gameType, string? generalId)
        => "type=" + gameType + " general=" + (generalId ?? "none");

    // The engine's own throw may arrive wrapped, so name the innermost type.
    internal static string FailedDetail(string gameType, Exception ex)
        => "type=" + gameType + " reason=" + ex.GetBaseException().GetType().Name;
}
