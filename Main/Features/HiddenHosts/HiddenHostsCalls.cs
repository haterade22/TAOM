using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.HiddenHosts;

/// <summary>The service and adapter Patch105 reaches, handed over by the module's static initialisation, and the hooks' fault budget.</summary>
internal static class HiddenHostsCalls
{
    /// <summary>The faults the hooks tolerate before they stop for the rest of the process.</summary>
    internal const int FaultBudget = 32;

    private static readonly HashSet<string> Reported = new();
    private static int _faults;

    internal static HiddenHostsService? Service { get; private set; }

    internal static IPartyIntelAdapter? Adapter { get; private set; }

    private static IModLogger? Logger { get; set; }

    /// <summary>
    /// The label of a settlement tooltip's defenders row, the text vanilla passed to that row. A seam so a test needs no
    /// game text; production reads the game's own text through <see cref="HiddenHostsTexts.DefendersLabel"/>.
    /// </summary>
    internal static Func<string> DefendersLabel { get; private set; } = () => HiddenHostsTexts.DefendersLabel;

    /// <summary>The label of a party tooltip's troops row, the text vanilla passed for it. A seam, like <see cref="DefendersLabel"/>.</summary>
    internal static Func<string> TroopsLabel { get; private set; } = () => HiddenHostsTexts.TroopsLabel;

    /// <summary>The "Counted ... ago" line for a number of hours. A seam so a test needs no game text.</summary>
    internal static Func<int, string> CountedAgo { get; private set; } = HiddenHostsTexts.CountedAgo;

    /// <summary>True once the fault budget is spent: every hook returns at once, leaving vanilla's text.</summary>
    internal static bool Stopped => _faults >= FaultBudget;

    internal static void Initialize(HiddenHostsService? service, IPartyIntelAdapter? adapter, IModLogger? logger = null,
        Func<string>? defendersLabel = null, Func<string>? troopsLabel = null, Func<int, string>? countedAgo = null)
    {
        Service = service;
        Adapter = adapter;
        Logger = logger;
        DefendersLabel = defendersLabel ?? (() => HiddenHostsTexts.DefendersLabel);
        TroopsLabel = troopsLabel ?? (() => HiddenHostsTexts.TroopsLabel);
        CountedAgo = countedAgo ?? HiddenHostsTexts.CountedAgo;
        Reported.Clear();
        _faults = 0;
    }

    /// <summary>
    /// A hook that failed shows vanilla's text for that frame. A fault that repeats every frame would flood the log and
    /// cost a throw per plate, so one line per hook and exception type goes to the log, never one per frame, and the
    /// <see cref="FaultBudget"/>th fault stops the hooks (one "from now on" line); the hooks read <see cref="Stopped"/>.
    /// </summary>
    internal static void ReportFault(string hook, Exception ex)
    {
        bool wasSpent = _faults >= FaultBudget;
        if (!wasSpent) _faults++;
        bool spent = _faults >= FaultBudget;
        // #1185: the player is told once, outside the try, so a log that fails cannot hide it.
        if (spent && !wasSpent) SelfDisables.Report(HiddenHostsModule.PatchCategory, FaultBudget + " hook faults");
        try
        {
            if (Reported.Add(hook + ": " + ex.GetType().Name))
                Logger?.LogError($"[HiddenHosts] {hook} fell back to vanilla: {ex}");
            if (spent && !wasSpent)
                Logger?.LogError($"[HiddenHosts] {FaultBudget} faults: the hooks are off from now on, vanilla counts show until the game restarts.");
        }
        catch
        {
            // The log itself failed; vanilla text is already showing.
        }
    }
}
