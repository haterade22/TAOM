using System;
using System.Collections.Generic;
using DryIoc;
using TAOM.Core.Logging;

namespace TAOM.Composition;

/// <summary>
/// Runs one lifecycle step over every feature module in list order, isolating each module. A module
/// that throws is logged, marked faulted and skipped in every later step of the session (half-wired
/// is worse than absent), and the next module still runs. A module that owns save data fails CLOSED
/// instead in the steps that decide whether its SyncData runs (service registration, static
/// initialisation, campaign start): the throw propagates, because a campaign that runs without the
/// behavior persisting its data can lose that data on the next save. Parked modules get only their
/// service registration. Engine-free and unit-tested; the engine-facing loops are in
/// <see cref="FeatureModuleHooks"/>.
/// </summary>
internal sealed class ModuleRunner
{
    private readonly IReadOnlyList<ITaomFeatureModule> _modules;
    private readonly Func<IModLogger?> _logger;
    private readonly HashSet<string> _faulted = new(StringComparer.Ordinal);
    private readonly List<string> _unreported = new();

    internal ModuleRunner(IReadOnlyList<ITaomFeatureModule> modules, Func<IModLogger?> logger)
    {
        _modules = modules ?? throw new ArgumentNullException(nameof(modules));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal bool IsFaulted(string moduleId) => _faulted.Contains(moduleId);

    internal void RegisterServices(IRegistrator registrator) =>
        Run("service registration", includeParked: true, failClosed: true, m => m.RegisterServices(registrator));

    internal void InitializeStatics(IResolver resolver) =>
        Run("static initialisation", includeParked: false, failClosed: true, m => m.InitializeStatics(resolver));

    /// <summary>
    /// Applies each module's categories for <paramref name="phase"/> through the kernel's guarded
    /// applier (a failed category is the applier's to log and report, not a module fault), then
    /// calls the module's OnPhase.
    /// </summary>
    internal void RunPhase(ApplyPhase phase, Func<string, bool> tryPatchCategory, IResolver resolver) =>
        Run(phase.ToString(), includeParked: false, failClosed: false, m =>
        {
            foreach (var decl in m.PatchCategories)
            {
                if (decl.Phase == phase)
                    tryPatchCategory(decl.Category);
            }

            m.OnPhase(phase, resolver);
        });

    internal void Run(string step, bool includeParked, bool failClosed, Action<ITaomFeatureModule> action)
    {
        foreach (var module in _modules)
        {
            if (!includeParked && module.State == FeatureState.Parked) continue;
            if (_faulted.Contains(module.Id)) continue;

            try
            {
                action(module);
            }
            catch (Exception ex)
            {
                _faulted.Add(module.Id);
                _unreported.Add($"{module.Id} ({step})");
                Log($"[Module] {module.Id} failed in {step}: {ex}");
                if (failClosed && module.OwnsSaveData)
                    throw;
            }
        }
    }

    /// <summary>
    /// One player-facing line naming every module that faulted since the last call, or null when none
    /// did. Clears the list, so each report point shows only new faults.
    /// </summary>
    internal string? TakeFaultSummary()
    {
        if (_unreported.Count == 0) return null;

        var summary = "TAOM: feature modules failed and are off this session: "
            + string.Join(", ", _unreported) + ". The TAOM log names the cause.";
        _unreported.Clear();
        return summary;
    }

    private void Log(string message)
    {
        try
        {
            _logger()?.LogError(message);
        }
        catch
        {
            // The fault record must never be the thing that breaks the step.
        }
    }
}
