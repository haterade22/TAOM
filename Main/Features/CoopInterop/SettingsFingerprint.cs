using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace TAOM.Features.CoopInterop;

/// <summary>
/// A short code summarising the simulation-affecting settings, so two co-op peers can find out
/// they disagree instead of discovering it as drift.
/// </summary>
/// <remarks>
/// <para><b>Per group, not one number.</b> A single global hash answers "something differs",
/// which sends a player through 105 checkboxes. A hash per MCM group answers "Battle Tactics
/// differs", which is one screen. The global hash is kept as the cheap equality test.</para>
///
/// <para><b>Culture is the trap.</b> <c>0.25f.ToString()</c> is <c>"0,25"</c> on a Spanish
/// Windows and <c>"0.25"</c> on an English one — two peers with identical settings would hash
/// differently and the warning would fire on everyone with a comma. Every value goes through
/// <see cref="CultureInfo.InvariantCulture"/>, and floats use the round-trip format so
/// 0.1f + 0.2f never collapses onto a neighbour.</para>
///
/// <para><b>Reflection, deliberately.</b> Hard-coding the property list would drift the first
/// time someone adds a setting. Reading the type means a new knob is covered on the day it is
/// added; <see cref="CoopSettingsRelevance"/> is what removes the ones that must not count.
/// The MCM group attribute is read BY NAME rather than by type so this stays testable against
/// a synthetic settings class with no MCM dependency.</para>
///
/// <para>This computes and compares. It does not decide when to ask, where to store the
/// answer, or how to tell the player — those need a co-op session and belong to the caller.</para>
/// </remarks>
public static class SettingsFingerprint
{
    public const string Ungrouped = "(ungrouped)";
    private const int DisplayLength = 12;

    /// <summary>Hash the simulation-relevant settings on <paramref name="settings"/>.</summary>
    public static FingerprintReport Compute(object settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        var relevant = settings.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(CoopSettingsRelevance.IsSimulationRelevant)
            // Ordinal, and group first: the canonical text must not depend on the order
            // reflection happens to return members in, which is not specified.
            .OrderBy(GroupOf, StringComparer.Ordinal)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        var perGroup = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var p in relevant)
        {
            var group = GroupOf(p);
            if (!perGroup.TryGetValue(group, out var sb))
            {
                perGroup[group] = sb = new StringBuilder();
                counts[group] = 0;
            }
            sb.Append(p.Name).Append('=').Append(Render(Read(p, settings))).Append('\n');
            counts[group]++;
        }

        var groupHashes = perGroup.ToDictionary(kv => kv.Key, kv => Sha256(kv.Value.ToString()),
                                                StringComparer.Ordinal);

        // The global hash covers the group hashes, in name order — so it changes when any group
        // changes, and does not depend on how the groups were enumerated.
        var globalText = string.Concat(groupHashes.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                                  .Select(kv => kv.Key + ":" + kv.Value + "\n"));

        return new FingerprintReport(Sha256(globalText), groupHashes, counts, relevant.Count);
    }

    /// <summary>A property read must never take the session down; an unreadable one hashes as unreadable.</summary>
    private static object Read(PropertyInfo p, object target)
    {
        try { return p.GetValue(target, null); }
        catch (Exception ex) { return "<unreadable:" + ex.GetType().Name + ">"; }
    }

    /// <summary>MCM's group attribute, resolved by name so tests need no MCM reference.</summary>
    internal static string GroupOf(PropertyInfo p)
    {
        foreach (var attr in p.GetCustomAttributes(false))
        {
            var t = attr.GetType();
            if (t.Name.IndexOf("SettingPropertyGroup", StringComparison.Ordinal) < 0) continue;
            var value = (t.GetProperty("GroupName") ?? t.GetProperty("Name"))?.GetValue(attr, null) as string;
            if (string.IsNullOrEmpty(value)) continue;
            // "Battle Tactics/Smart Cavalry" -> "Battle Tactics": a subgroup is a UI nicety,
            // and reporting the root is what a player can act on.
            var slash = value.IndexOf('/');
            return slash < 0 ? value : value.Substring(0, slash);
        }
        return Ungrouped;
    }

    /// <summary>Culture-invariant, round-trip-exact rendering. See the class remarks.</summary>
    internal static string Render(object value)
    {
        switch (value)
        {
            case null: return "<null>";
            case bool b: return b ? "true" : "false";
            case float f: return f.ToString("R", CultureInfo.InvariantCulture);
            case double d: return d.ToString("R", CultureInfo.InvariantCulture);
            case string s: return s;
        }

        // MCM's Dropdown<T> renders its whole option list through ToString(); only the
        // selection is a setting, and only the selection may enter the hash.
        var type = value.GetType();
        if (type.Name.IndexOf("Dropdown", StringComparison.Ordinal) >= 0)
        {
            var selected = (type.GetProperty("SelectedValue") ?? type.GetProperty("SelectedIndex"))
                ?.GetValue(value, null);
            if (selected != null) return Render(selected);
        }

        return value is IFormattable f2
            ? f2.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString() ?? "<null>";
    }

    private static string Sha256(string text)
    {
        using (var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }

    /// <summary>Fingerprints of one peer's settings: one per MCM group, plus a global.</summary>
    public sealed class FingerprintReport
    {
        internal FingerprintReport(string global, IReadOnlyDictionary<string, string> byGroup,
                                   IReadOnlyDictionary<string, int> countsByGroup, int covered)
        {
            Global = global;
            ByGroup = byGroup;
            CountsByGroup = countsByGroup;
            Covered = covered;
        }

        public string Global { get; }
        public IReadOnlyDictionary<string, string> ByGroup { get; }
        public IReadOnlyDictionary<string, int> CountsByGroup { get; }

        /// <summary>How many settings entered the hash — the number to log, so a future
        /// version bump that silently drops half the settings is visible.</summary>
        public int Covered { get; }

        public string ShortGlobal => Global.Substring(0, Math.Min(DisplayLength, Global.Length));

        /// <summary>
        /// Groups whose settings differ from <paramref name="other"/>, in name order.
        /// A group present on one side only counts as differing: a peer running a build without
        /// that feature is exactly the mismatch worth reporting.
        /// </summary>
        public IReadOnlyList<string> DivergentGroups(FingerprintReport other)
        {
            if (other == null) return Array.Empty<string>();
            var names = new SortedSet<string>(ByGroup.Keys, StringComparer.Ordinal);
            names.UnionWith(other.ByGroup.Keys);
            var divergent = new List<string>();
            foreach (var g in names)
            {
                var hereHas = ByGroup.TryGetValue(g, out var here);
                var thereHas = other.ByGroup.TryGetValue(g, out var there);
                if (!hereHas || !thereHas || !string.Equals(here, there, StringComparison.Ordinal))
                    divergent.Add(g);
            }
            return divergent;
        }

        public bool Matches(FingerprintReport other) =>
            other != null && string.Equals(Global, other.Global, StringComparison.Ordinal);
    }
}
