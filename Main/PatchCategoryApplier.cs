using System;
using System.Collections.Generic;
using TAOM.Core.Logging;

namespace TAOM;

/// <summary>
/// Applies one Harmony patch category at a time so a category whose target no longer resolves
/// costs only that category. Harmony's PatchCategory has no catch: the first class whose target is
/// missing throws a HarmonyException out of the caller, which in OnSubModuleLoad fails the module
/// load and in OnGameInitializationFinished skips every later category of the batch. Harmony does
/// not roll back, so the failing category's earlier classes stay patched. One case is NOT
/// category-local: Harmony builds its category index once per assembly by reading every type's
/// attributes, so an attribute naming a type the engine no longer has makes every call fail. Each
/// failure is logged at Error with its full cause and remembered until the phase summary is taken.
/// The apply delegate keeps HarmonyLib and the engine out of this class, so it is unit-testable.
/// </summary>
internal sealed class PatchCategoryApplier
{
    private readonly Action<string> _apply;
    private readonly IModLogger _logger;
    private readonly List<string> _failed = new();

    internal PatchCategoryApplier(Action<string> apply, IModLogger logger)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Applies the category; on a throw, logs it, records it and returns false.</summary>
    internal bool TryApply(string category)
    {
        try
        {
            _apply(category);
            return true;
        }
        catch (Exception ex)
        {
            _failed.Add(category);
            _logger.LogError(
                $"[PatchApply] {category} FAILED (Harmony stops a category at its first failing class): {ex}");
            return false;
        }
    }

    /// <summary>
    /// One player-facing line naming every category that failed since the last call, or null when
    /// none did. Clears the list, so each phase reports only its own failures.
    /// </summary>
    internal string? TakeFailureSummary(string phase)
    {
        if (_failed.Count == 0) return null;

        var summary = $"TAOM: patch groups failed to apply during {phase}: {string.Join(", ", _failed)}. "
            + "Some fixes in those groups are off this session; the TAOM log names the cause.";
        _failed.Clear();
        return summary;
    }
}
