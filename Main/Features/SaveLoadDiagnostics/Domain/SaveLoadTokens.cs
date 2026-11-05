using System;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;

namespace TAOM.Features.SaveLoadDiagnostics.Domain;

// What the campaign that ended before the last load looks like now (plan 114, MEMORY-05).
internal enum PreviousCampaignState
{
    None,
    Collected,
    Alive,
}

// Pure formatting for the save-growth probes' [SaveLoad] tokens (plan 114). Part of the log contract.
// An unknown value prints as `?` or is left out, never as a made-up 0 or false.
internal static class SaveLoadTokens
{
    public static string LoadRequested(string saveName, bool inGame, int loads, int inGameLoads, string memory) =>
        $"name='{saveName}' inGame={Bool(inGame)} loadsThisProcess={Int(loads)} inGameLoads={Int(inGameLoads)} {memory}".TrimEnd();

    public static string ProcessContext(int loads, int inGameLoads, PreviousCampaignState prev, int gen2Since, string memory)
    {
        var state = prev switch
        {
            PreviousCampaignState.Collected => "collected",
            PreviousCampaignState.Alive => "alive",
            _ => "none",
        };
        var gen2 = prev == PreviousCampaignState.None ? string.Empty : $" gen2Since={Int(gen2Since)}";
        return $"loadsThisProcess={Int(loads)} inGameLoads={Int(inGameLoads)} prevCampaign={state}{gen2} {memory}".TrimEnd();
    }

    public static string RuntimeHost(string framework, string clr, bool? serverGc, string latency, bool? is64Bit) =>
        $"framework='{framework}' clr={clr} serverGC={Bool(serverGc)} latency={latency} is64Bit={Bool(is64Bit)}";

    // Reads each value on its own: a runtime host missing one member costs only that token.
    public static string RuntimeHost()
    {
        var framework = "?";
        var clr = "?";
        bool? serverGc = null;
        var latency = "?";
        bool? is64Bit = null;
        try { framework = RuntimeInformation.FrameworkDescription; } catch { /* unknown stays ? */ }
        try { clr = Environment.Version.ToString(); } catch { /* unknown stays ? */ }
        try { serverGc = GCSettings.IsServerGC; } catch { /* unknown stays ? */ }
        try { latency = GCSettings.LatencyMode.ToString(); } catch { /* unknown stays ? */ }
        try { is64Bit = Environment.Is64BitProcess; } catch { /* unknown stays ? */ }
        return RuntimeHost(framework, clr, serverGc, latency, is64Bit);
    }

    private static string Bool(bool? value) => value == null ? "?" : value.Value ? "true" : "false";

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
}
