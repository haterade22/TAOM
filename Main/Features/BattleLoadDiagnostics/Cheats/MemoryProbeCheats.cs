using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics.Domain;
using TAOM.Features.DevConsole;

namespace TAOM.Features.BattleLoadDiagnostics.Cheats;

/// <summary>
/// On-demand engine memory attribution: "the process holds 20 GB — what does the ENGINE think they
/// are for?". Fills the per-station rows of the commit-attribution matrix in
/// docs/investigations/native-commit-audit-2026-08.md, which [MemSample] alone cannot (it reports
/// OS totals on a timer, not engine categories on demand).
///
/// SHAPE IS LOAD-BEARING, NOT STYLE. A [CommandLineArgumentFunction] whose signature is not exactly
/// `public static string Name(List&lt;string&gt;)` throws inside the engine's UNGUARDED discovery
/// loop, past a native boundary with no managed backstop — that is a startup hazard, not a broken
/// console. This class also deliberately has NO static field initializers: anything touching engine
/// or IoC state at type-init time runs during that same discovery pass. Every resolve happens inside
/// the lambda. Pinned by ConsoleCommandBindingTests.
/// </summary>
public static class MemoryProbeCheats
{
    private const string GpuToken = "gpu";
    private const string LogsDirectory = "Logs";

    private const string MemoryUsage =
        "Format is \"taom.print_memory [label] [gpu]\".\n"
        + "Prints the engine's own memory accounting plus the OS process/commit numbers.\n"
        + "Optional label (max 32 chars, A-Z a-z 0-9 _ . : -) names the station in the log, e.g. B-menu.\n"
        + "Add \"gpu\" to also write a GPU memory dump into the Logs folder.\n"
        + "Also written to taom_debug under [MemProbe], so a matrix run is self-recording.";

    /// <summary>
    /// RunAnywhere, not a campaign gate: process memory is meaningful at the main menu too. Note the
    /// consequence for the matrix — RunAnywhere still returns "Campaign was not started." before a
    /// game loads, so this command CANNOT produce the menu station. That is why
    /// MemoryPressureSampler.Start() was moved to OnBeforeInitialModuleScreenSetAsRoot: [MemSample]
    /// covers menu, print_memory covers map and battle. The two are complementary by construction.
    /// </summary>
    [CommandLineFunctionality.CommandLineArgumentFunction("print_memory", "taom")]
    public static string PrintMemory(List<string> strings) =>
        TaomConsole.RunAnywhere(strings, MemoryUsage, args =>
        {
            var wantsGpuDump = args.Any(a => string.Equals(a, GpuToken, StringComparison.OrdinalIgnoreCase));
            var label = args.FirstOrDefault(a => !string.Equals(a, GpuToken, StringComparison.OrdinalIgnoreCase));

            if (!MemoryProbeReportFormatter.IsValidLabel(label))
                return $"Rejected label '{label}'.\n{MemoryUsage}";

            var reader = IoC.Resolve<IEngineMemoryStatsReader>();
            var engine = reader.Read(wantsGpuDump ? LogsDirectory : null);

            // A read failure omits the OS block rather than printing zeros.
            var osRead = MemorySampleReader.TryRead(out var sample);
            MemorySample? os = osRead ? sample : (MemorySample?)null;

            var report = MemoryProbeReportFormatter.Format(engine, os, osRead, label);

            // Mirror into taom_debug so the operator does not have to transcribe console output into
            // the matrix. LogInfo, so it is flushed synchronously and survives a subsequent CTD —
            // which is exactly the scenario this probe is used to investigate.
            try { IoC.Resolve<IModLogger>()?.LogInfo(report); }
            catch { /* console output is the primary channel; the log copy is a convenience */ }

            return report;
        });
}
