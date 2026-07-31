using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TAOM.Features.DevConsole;

/// <summary>
/// Renders the declared-vs-applied Harmony patch report for `taom.print_patches`. Pure — the
/// reflection walk lives in <see cref="HarmonyPatchInspector"/>, so every branch here is testable.
///
/// Two behaviours are deliberately encoded rather than flagged as faults, because both are correct
/// by design and a report that cried wolf on them would be ignored within a week:
/// manual patches carry no <c>[HarmonyPatchCategory]</c> and get their own bucket, and some
/// categories (e.g. <c>Patch0_BattleScenes</c>) are deliberately never registered, so
/// declared-but-not-applied is sometimes the right answer. The report states the facts and lets the
/// reader apply the registry.
/// </summary>
internal static class PatchReportFormatter
{
    internal static string Format(
        IReadOnlyList<string> declared,
        IReadOnlyDictionary<string, int> appliedCounts,
        IReadOnlyList<string> uncategorized,
        IReadOnlyDictionary<string, int> otherOwners,
        int totalPatchedMethods,
        string filter)
    {
        declared = declared ?? new string[0];
        appliedCounts = appliedCounts ?? new Dictionary<string, int>();
        uncategorized = uncategorized ?? new string[0];
        otherOwners = otherOwners ?? new Dictionary<string, int>();

        var matching = string.IsNullOrWhiteSpace(filter)
            ? declared.OrderBy(c => c, StringComparer.Ordinal).ToList()
            : declared.Where(c => c.IndexOf(filter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                      .OrderBy(c => c, StringComparer.Ordinal).ToList();

        var appliedCount = matching.Count(c => appliedCounts.ContainsKey(c));
        var missingCount = matching.Count - appliedCount;

        var sb = new StringBuilder();
        sb.AppendLine(
            $"[Patches] {matching.Count} declared categories: {appliedCount} applied, {missingCount} not applied "
            + $"| {totalPatchedMethods} patched methods total"
            + (string.IsNullOrWhiteSpace(filter) ? string.Empty : $" | filter '{filter}'"));

        foreach (var category in matching)
        {
            sb.AppendLine(appliedCounts.TryGetValue(category, out var count)
                ? $"[Patches]   {category}: APPLIED ({count} methods)"
                : $"[Patches]   {category}: NOT APPLIED");
        }

        if (uncategorized.Count > 0)
        {
            sb.AppendLine($"[Patches] {uncategorized.Count} uncategorized TAOM patch methods (manual patches carry no category by design):");
            foreach (var method in uncategorized.OrderBy(m => m, StringComparer.Ordinal))
                sb.AppendLine($"[Patches]   {method}");
        }

        if (otherOwners.Count > 0)
        {
            sb.AppendLine("[Patches] patches owned by other mods:");
            foreach (var owner in otherOwners.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
                sb.AppendLine($"[Patches]   {owner.Key}: {owner.Value} methods");
        }

        if (missingCount > 0)
            sb.Append("[Patches] NOTE: some categories are deliberately never registered "
                + "(see docs/reference/harmony-patch-registry.md) — NOT APPLIED is not automatically a fault.");

        return sb.ToString().TrimEnd();
    }
}
