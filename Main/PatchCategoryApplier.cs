using System;
using System.Collections.Generic;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM;

/// <summary>
/// Applies one Harmony patch category at a time so a category whose target no longer resolves
/// costs only that category. Harmony's PatchCategory has no catch: the first class whose target is
/// missing throws a HarmonyException out of the caller, which in OnSubModuleLoad fails the module
/// load and in OnGameInitializationFinished skips every later category of the batch. Harmony does
/// not roll back, so the failing category's earlier classes stay patched. A class whose attributes
/// cannot be read (one naming a type the engine no longer has) would fail Harmony's assembly-wide
/// category index and so every category; PatchCategoryIndex skips that class instead, and
/// RecordSkippedClasses reports it here. Each failure or skipped class is logged at Error with its
/// full cause and remembered until the phase summary is taken.
/// The apply delegate keeps HarmonyLib out of this class, and its only engine type is the
/// TextObject it builds (never rendered here), so it is unit-testable.
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
    /// Logs each class the category index skipped because its attributes could not be read, and
    /// records it by name for the next phase summary.
    /// </summary>
    internal void RecordSkippedClasses(IEnumerable<KeyValuePair<Type, Exception>> skipped)
    {
        foreach (var entry in skipped)
        {
            var name = entry.Key.FullName ?? entry.Key.Name;
            _failed.Add(name);
            _logger.LogError(
                $"[PatchApply] {name} SKIPPED (its attributes cannot be read, so its patches are off; every other class still applies): {entry.Value}");
        }
    }

    /// <summary>
    /// One player-facing line naming every category that failed since the last call, or null when
    /// none did. Clears the list, so each phase reports only its own failures. A localized
    /// TextObject built here but rendered by the caller; the category ids stay literal.
    /// </summary>
    internal TextObject? TakeFailureSummary(TextObject phase)
    {
        if (_failed.Count == 0) return null;

        var summary = new TextObject("{=taom_patch_apply_failed}TAOM: patch groups failed to apply during {PHASE}: {GROUPS}. Some fixes in those groups are off this session; the TAOM log names the cause.")
            .SetTextVariable("PHASE", phase)
            .SetTextVariable("GROUPS", string.Join(", ", _failed));
        _failed.Clear();
        return summary;
    }
}
