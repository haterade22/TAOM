using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.DevConsole;

/// <summary>
/// The dispatch shell every <c>taom.*</c> console command routes through: cheat gate, then help,
/// then the command body inside a catch-all.
///
/// Why a shared shell rather than repeating four lines per command. The engine dispatches console
/// commands across a native reverse-P/Invoke — <c>Managed_CallCommandlineFunction</c> →
/// <c>CommandLineFunctionality.CallFunction</c> → the delegate — with no try/catch at any level and
/// no <c>AppDomain.UnhandledException</c> handler anywhere in the decompiled managed tree. An
/// escaping exception therefore unwinds into TaleWorlds.Native.dll, and what happens next is not
/// readable from the managed side. A console command that dies must die as a printed string.
///
/// A read-only command is NOT a safe command: these walk <c>Settlement.All</c>, <c>Mission.Agents</c>
/// and the like, and TaleWorlds computed getters routinely throw before your null check
/// (<c>.claude/rules/adapters.md</c>).
///
/// Simplicity note, so this stays re-evaluable: at one or two commands this helper would be a
/// REJECT under <c>.claude/rules/simplicity-criterion.md</c> — a tiny win for a new indirection. It
/// earns its place only because the suite is ~20 commands (so it is net line-negative against the
/// per-command <c>_errorType</c> field plus two guard lines it replaces) and because the downside it
/// contains is unbounded. If the suite ever shrinks back to a handful, inline it again.
/// </summary>
internal static class TaomConsole
{
    /// <summary>Commands touching campaign state: settlements, clans, heroes, the map.</summary>
    internal static string RunInCampaign(List<string> args, string usage, Func<List<string>, string> body) =>
        Run(args, usage, DevConsoleGuard.CheckCampaignCheatUsage, body);

    /// <summary>Commands operating on an open mission. Works in custom battles (no campaign required).</summary>
    internal static string RunInMission(List<string> args, string usage, Func<List<string>, string> body) =>
        Run(args, usage, DevConsoleGuard.CheckMissionCheatUsage, body);

    /// <summary>Commands inspecting process-level state — config, registries, the Harmony patch table.</summary>
    internal static string RunAnywhere(List<string> args, string usage, Func<List<string>, string> body) =>
        Run(args, usage, DevConsoleGuard.CheckCheatMode, body);

    private static string Run(List<string> args, string usage, DevConsoleGuard.GateCheck gate, Func<List<string>, string> body)
    {
        // The gate is inside the try for a concrete reason: vanilla's CampaignCheats.CheckCheatUsage
        // tests `Campaign.Current == null` and then dereferences `Game.Current.CheatMode` with no
        // null check of its own. Any state with a live Campaign and a torn-down Game NREs inside the
        // engine's gate, before our code runs.
        try
        {
            var error = string.Empty;
            if (!gate(ref error)) return error;

            // Vanilla's convention — `<command> help` prints usage. Safe with no campaign: it is a
            // pure list check. Gate-before-help matches vanilla: every CampaignCheats command runs
            // CheckCheatUsage first, so `<command> help` with no campaign prints the gate error.
            var arguments = args ?? new List<string>();

            // Null-coalesced for the same reason the body result is: a null crosses the native
            // reverse-P/Invoke return unchanged (CallFunction returns the delegate's value verbatim),
            // and no return path from this shell may be undefended.
            if (CampaignCheats.CheckHelp(arguments)) return usage ?? string.Empty;

            return body(arguments) ?? string.Empty;
        }
        catch (Exception ex)
        {
            return Report(ex, usage);
        }
    }

    private static string Report(Exception exception, string usage)
    {
        // Logging is best-effort by design: in a test host (and before IoC.Configure runs) the
        // container is null, and a logger failure must never replace the diagnostic the user asked
        // for with a second, less useful exception.
        try
        {
            IoC.Resolve<IModLogger>().LogError($"[DevConsole] Command failed: {exception}");
        }
        catch
        {
            // Intentionally swallowed — see above.
        }

        return $"Command failed: {exception.GetType().Name}: {exception.Message}\n"
             + "Full stack trace is in the taom_debug log.\n"
             + usage;
    }
}
