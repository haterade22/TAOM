using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Result of parsing <c>coop-modules.txt</c>. Both lists are already unioned with the compiled
/// defaults and de-duplicated case-insensitively.
/// </summary>
public sealed class CoopModuleListResult
{
    public CoopModuleListResult(IReadOnlyList<string> moduleIds, IReadOnlyList<string> ownerPrefixes, bool truncated)
    {
        ModuleIds = moduleIds;
        OwnerPrefixes = ownerPrefixes;
        Truncated = truncated;
    }

    /// <summary>Module ids that identify a co-op mod when active in the launcher.</summary>
    public IReadOnlyList<string> ModuleIds { get; }

    /// <summary>Harmony owner-id prefixes to add to PatchShield's protected-owner allowlist.</summary>
    public IReadOnlyList<string> OwnerPrefixes { get; }

    /// <summary>True when the file exceeded <see cref="CoopModuleList.MaxEntriesPerSection"/>.</summary>
    public bool Truncated { get; }
}

/// <summary>
/// Parser for the shipped, user-editable <c>coop-modules.txt</c> in the TAOM.Dependencies module
/// directory. Plain text rather than JSON deliberately: this assembly has no JSON dependency and
/// should not acquire one for a two-section id list. The format mirrors the
/// <c>last-good-modlist.txt</c> that IncompatibleModDetector already writes to the same folder.
///
/// <code>
/// # comment
/// [modules]
/// BannerlordTogether
/// [harmony-owner-prefixes]
/// com.example.coop
/// </code>
///
/// Entries before any section header are treated as module ids. Unknown section headers cause
/// their entries to be ignored.
///
/// THE INVARIANT THIS FILE EXISTS TO GUARANTEE: parsing is UNION-ONLY. The result feeds
/// PatchShield's protected-owner allowlist, so a corrupt or hostile file must never be able to
/// REMOVE a compiled default — doing so would unprotect the BUTR/MCM stack and let PatchShield
/// unpatch it on the first missing-API exception. Pinned by CoopModuleListTests.
/// </summary>
public static class CoopModuleList
{
    /// <summary>Per-section cap on entries read from the file. Compiled defaults are never capped.</summary>
    public const int MaxEntriesPerSection = 200;

    private const string ModulesSection = "modules";
    private const string OwnerPrefixSection = "harmony-owner-prefixes";

    public static CoopModuleListResult Parse(
        IEnumerable<string>? lines,
        IReadOnlyList<string> compiledModuleDefaults,
        IReadOnlyList<string> compiledOwnerDefaults)
    {
        // Seed from the compiled defaults FIRST so no parsing path can drop them.
        var modules = NewSet(compiledModuleDefaults);
        var owners = NewSet(compiledOwnerDefaults);

        if (lines == null)
            return new CoopModuleListResult(modules.ToList(), owners.ToList(), truncated: false);

        var section = ModulesSection;
        var truncated = false;
        var moduleEntries = 0;
        var ownerEntries = 0;

        foreach (var raw in lines)
        {
            var line = raw?.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line!.StartsWith("#", StringComparison.Ordinal)) continue;

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                continue;
            }

            if (string.Equals(section, ModulesSection, StringComparison.Ordinal))
            {
                if (moduleEntries >= MaxEntriesPerSection) { truncated = true; continue; }
                moduleEntries++;
                modules.Add(line);
            }
            else if (string.Equals(section, OwnerPrefixSection, StringComparison.Ordinal))
            {
                if (ownerEntries >= MaxEntriesPerSection) { truncated = true; continue; }
                ownerEntries++;
                owners.Add(line);
            }
            // Unknown section: entries are ignored by design.
        }

        return new CoopModuleListResult(modules.ToList(), owners.ToList(), truncated);
    }

    private static HashSet<string> NewSet(IReadOnlyList<string>? seed)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (seed == null) return set;
        foreach (var entry in seed)
        {
            if (!string.IsNullOrWhiteSpace(entry)) set.Add(entry.Trim());
        }
        return set;
    }
}
