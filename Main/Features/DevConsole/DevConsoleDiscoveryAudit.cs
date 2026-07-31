using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.DevConsole;

/// <summary>
/// Asks the engine, at startup, whether it actually registered TAOM's console commands.
///
/// Why this is not paranoia. The engine's <c>CollectCommandLineFunctions</c> is invoked from
/// TaleWorlds.Native.dll, so whether that pass runs before or after TAOM's assembly loads is not
/// answerable from the managed side, and a native disassembly would not generalise (module load
/// order is data-dependent). The assumption has been carried as "[Likely]" since the first console
/// command shipped (<c>docs/reviews/rca-specialresources-cheat-2026-07-30.md</c>).
/// <see cref="CommandLineFunctionality.HasFunctionForCommand"/> is public, so we can simply ask.
///
/// The vanilla control command is the part that makes the answer decisive. On its own, "our command
/// is not registered" cannot distinguish "discovery has not run yet" from "discovery ran and dropped
/// it" — and those demand opposite reactions. With the control, all four readings are distinct.
///
/// Beyond the one-time proof this is the only mechanism that surfaces a silent duplicate-name drop
/// (the engine keeps the first of a colliding pair, logs nothing) on a player's machine, and it
/// re-proves the assumption after every engine bump.
///
/// Fails open in every path: a diagnostic must never be the reason a session does not start.
/// </summary>
internal static class DevConsoleDiscoveryAudit
{
    /// <summary>
    /// A vanilla cheat that has existed for the life of the game. If the engine does not know this
    /// one, the discovery pass has not run yet — the reading says nothing about TAOM.
    /// </summary>
    internal const string VanillaControlCommand = "campaign.add_gold_to_hero";

    internal enum Severity { Info, Warning, Error }

    internal readonly struct Verdict
    {
        internal Severity Level { get; }
        internal string Message { get; }

        /// <summary>False only when discovery demonstrably has not run yet, so a later call may retry.</summary>
        internal bool IsConclusive { get; }

        internal Verdict(Severity level, string message, bool isConclusive)
        {
            Level = level;
            Message = message;
            IsConclusive = isConclusive;
        }
    }

    // Unsynchronised by design. OnBeforeInitialModuleScreenSetAsRoot runs on the engine's main
    // thread, and the worst case if that ever stopped being true is a duplicate log line.
    private static bool _settled;

    // The attributed-command set is fixed for the assembly's lifetime, so the reflection scan is
    // cached. Without this, a build that declares zero commands never reaches a conclusive verdict
    // and re-scans every type in TAOM on every return to the main menu, forever.
    private static IReadOnlyList<string> _declaredCommands;

    /// <summary>
    /// Runs the audit and logs the verdict. Safe to call more than once — it goes quiet once the
    /// answer is conclusive, so a return to the main menu does not re-log it.
    /// </summary>
    internal static void Run(IModLogger logger)
    {
        if (_settled) return;

        try
        {
            var declared = _declaredCommands ?? (_declaredCommands = CollectDeclaredCommands());
            var controlFound = CommandLineFunctionality.HasFunctionForCommand(VanillaControlCommand);
            var missing = declared.Where(c => !CommandLineFunctionality.HasFunctionForCommand(c)).ToList();

            var verdict = BuildVerdict(controlFound, declared, missing);
            _settled = verdict.IsConclusive;

            switch (verdict.Level)
            {
                case Severity.Error:
                    logger?.LogError(verdict.Message);
                    break;
                case Severity.Warning:
                    logger?.LogWarning(verdict.Message);
                    break;
                default:
                    logger?.LogInfo(verdict.Message);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Never block startup over a diagnostic. Do not set _settled — a transient failure
            // should not permanently silence the audit.
            try { logger?.LogWarning($"[DevConsole] Discovery audit failed: {ex.GetType().Name}: {ex.Message}"); }
            catch { /* intentionally swallowed */ }
        }
    }

    /// <summary>
    /// The four readings, pure and testable. Split out from <see cref="Run"/> deliberately: the
    /// engine-querying half needs a live game, but the interpretation is where the value is.
    /// </summary>
    internal static Verdict BuildVerdict(bool controlFound, IReadOnlyList<string> declared, IReadOnlyList<string> missing)
    {
        var declaredCount = declared?.Count ?? 0;
        var missingCount = missing?.Count ?? 0;

        if (declaredCount == 0)
        {
            // Not a clean bill of health — the attribute scan found nothing to check.
            return new Verdict(Severity.Warning,
                "[DevConsole] Discovery audit found no attributed taom.* commands in the TAOM assembly. "
                + "Either every command was removed, or the reflection scan failed silently.",
                isConclusive: false);
        }

        if (controlFound && missingCount == 0)
        {
            return new Verdict(Severity.Info,
                $"[DevConsole] Console discovery PROVEN: the engine registered all {declaredCount} taom.* commands "
                + $"(vanilla control '{VanillaControlCommand}' present).",
                isConclusive: true);
        }

        if (controlFound)
        {
            return new Verdict(Severity.Error,
                $"[DevConsole] Console discovery ran (vanilla control '{VanillaControlCommand}' present) but DROPPED "
                + $"{missingCount} of {declaredCount} taom.* commands: {string.Join(", ", missing)}. "
                + "Most likely a duplicate command name (the engine silently keeps the first of a colliding pair) "
                + "or an aborted discovery pass. See docs/features/dev-console.md.",
                isConclusive: true);
        }

        if (missingCount == declaredCount)
        {
            return new Verdict(Severity.Warning,
                $"[DevConsole] Console discovery audit inconclusive: neither the vanilla control "
                + $"'{VanillaControlCommand}' nor any of the {declaredCount} taom.* commands are registered yet, "
                + "so the engine's collect pass has not run at this point in startup. Re-checks on the next "
                + "return to the main menu — a single uninterrupted session will not produce a second reading.",
                isConclusive: false);
        }

        return new Verdict(Severity.Warning,
            $"[DevConsole] Console discovery audit is reading oddly: {declaredCount - missingCount} of {declaredCount} "
            + $"taom.* commands are registered, but the vanilla control '{VanillaControlCommand}' is not. "
            + "The control command was most likely renamed by an engine update — update VanillaControlCommand.",
            isConclusive: true);
    }

    private static IReadOnlyList<string> CollectDeclaredCommands()
    {
        // Mirrors the engine's own GetTypesSafe(): some TAOM types fail to load in some hosts, and a
        // hard GetTypes() would throw before reaching the commands.
        Type[] types;
        try
        {
            types = typeof(DevConsoleDiscoveryAudit).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray();
        }

        return types
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            .Select(m => m.GetCustomAttributes(typeof(CommandLineFunctionality.CommandLineArgumentFunction), false))
            .Where(a => a.Length > 0)
            .Select(a => (CommandLineFunctionality.CommandLineArgumentFunction)a[0])
            .Select(a => $"{a.GroupName}.{a.Name}")
            .Distinct()
            .ToList();
    }
}
