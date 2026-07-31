using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.CoopInterop.Diagnostics;

/// <summary>
/// Turns a snapshot of Harmony's runtime registry into the `[HarmonyCensus]` log block.
///
/// WHY THIS EXISTS. Making TAOM coexist with a third-party co-op mod requires knowing what that mod
/// patches. Its package ships an explicit no-decompile / no-AI-analysis policy from its copyright
/// holders, so TAOM does not read its assembly. Instead this reports what HarmonyLib itself already
/// knows about every mod in the process: owner ids, patch kinds, and the reflection metadata of the
/// patched target. That answers the questions the interop work actually turns on — which engine
/// methods both mods patch, where two transpilers meet, and what the other mod's Harmony id is.
///
/// It is also the verification that both mods share ONE Harmony instance. HarmonyLib's patch
/// registry is per-assembly-instance static state, and the co-op mod ships its own 0Harmony.dll at
/// the same version TAOM deploys. If its owner id never appears here while the game plainly runs
/// its patches, there are two Harmony instances and TAOM's view of the process is blind.
///
/// Pure and injectable; the Harmony walk lives in the collector.
/// </summary>
public sealed class HarmonyCensusReportBuilder
{
    private const string Prefix = "[HarmonyCensus]";
    private const string UnknownOwner = "(unknown)";

    /// <summary>
    /// Max CONTESTED rows printed, so a badly-stacked modlist can't flood the log. Transpiler
    /// conflicts are deliberately uncapped — they are rare and each one is individually worth
    /// reading, whereas contested rows can run to hundreds.
    /// </summary>
    public const int MaxDetailRows = 60;

    public IReadOnlyList<string> BuildReport(
        IReadOnlyList<HarmonyCensusMethod>? methods,
        IReadOnlyList<string>? coopModuleIds)
    {
        var lines = new List<string>();
        var all = methods ?? Array.Empty<HarmonyCensusMethod>();
        var coop = (coopModuleIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();

        var ownerCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ownerNamespaces = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var method in all)
        {
            foreach (var patch in method.Patches ?? Array.Empty<HarmonyCensusPatch>())
            {
                var owner = Normalize(patch?.Owner);
                ownerCounts.TryGetValue(owner, out var count);
                ownerCounts[owner] = count + 1;

                if (!ownerNamespaces.TryGetValue(owner, out var namespaces))
                    ownerNamespaces[owner] = namespaces = new Dictionary<string, int>(StringComparer.Ordinal);
                var ns = string.IsNullOrWhiteSpace(method.TypeNamespace) ? "(global)" : method.TypeNamespace;
                namespaces.TryGetValue(ns, out var nsCount);
                namespaces[ns] = nsCount + 1;
            }
        }

        lines.Add($"{Prefix} totalPatchedMethods={all.Count}, owners={ownerCounts.Count}, " +
                  $"coopActive={(coop.Count > 0 ? "true" : "false")}, coopIds=[{string.Join(", ", coop)}]");

        foreach (var kv in ownerCounts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var namespaces = ownerNamespaces[kv.Key]
                .OrderByDescending(n => n.Value).ThenBy(n => n.Key, StringComparer.Ordinal)
                .Take(6)
                .Select(n => $"{n.Key}({n.Value})");
            lines.Add($"{Prefix}   owner={kv.Key} patches={kv.Value}  targets: {string.Join(" ", namespaces)}");
        }

        AppendContested(lines, all);
        AppendTranspilerConflicts(lines, all);

        return lines;
    }

    /// <summary>
    /// Methods where a TAOM FEATURE patch and a foreign patch meet — the rows a co-op triager
    /// actually wants.
    ///
    /// TAOM's infrastructure shields are excluded from the comparison, not merely from the
    /// "all-TAOM" test. PatchShield attaches a finalizer to essentially every patched method in the
    /// process, so counting it as an owner made every foreign-patched method "contested": the
    /// section saturated its row cap with noise and answered "who patched anything" instead of
    /// "where do the two mods collide".
    /// </summary>
    private static void AppendContested(List<string> lines, IReadOnlyList<HarmonyCensusMethod> all)
    {
        var rows = 0;
        var suppressed = 0;

        foreach (var method in all)
        {
            var patches = method.Patches ?? Array.Empty<HarmonyCensusPatch>();
            var owners = patches.Select(p => Normalize(p?.Owner))
                                .Where(o => !IsInfrastructureOwner(o))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
            if (owners.Count < 2) continue;
            if (owners.All(IsTaomOwner)) continue;

            // Cap checked BEFORE building the detail string — a suppressed row must cost a counter
            // increment, not a full join over every patch on the method.
            if (rows >= MaxDetailRows) { suppressed++; continue; }
            rows++;

            var detail = string.Join(", ", patches
                .Where(p => !IsInfrastructureOwner(Normalize(p?.Owner)))
                .Select(p => $"{Normalize(p?.Owner)}:{p?.PatchType ?? "?"}"));
            lines.Add($"{Prefix}   contested {method.MethodFullName}  [{detail}]");
        }

        // Never let a cap read as "nothing more to see".
        if (suppressed > 0)
            lines.Add($"{Prefix}   ... {suppressed} further contested method(s) not listed (row cap {MaxDetailRows})");
    }

    /// <summary>
    /// Two transpilers from different owners on one method — the highest-risk Harmony conflict
    /// class, because IL rewrites compound and neither author wrote against the other's output.
    /// </summary>
    private static void AppendTranspilerConflicts(List<string> lines, IReadOnlyList<HarmonyCensusMethod> all)
    {
        foreach (var method in all)
        {
            var transpilerOwners = (method.Patches ?? Array.Empty<HarmonyCensusPatch>())
                .Where(p => string.Equals(p?.PatchType, "Transpiler", StringComparison.OrdinalIgnoreCase))
                .Select(p => Normalize(p?.Owner))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (transpilerOwners.Count < 2) continue;

            lines.Add($"{Prefix}   TRANSPILER CONFLICT {method.MethodFullName}  " +
                      $"[{string.Join(", ", transpilerOwners)}]");
        }
    }

    private static string Normalize(string? owner) =>
        string.IsNullOrWhiteSpace(owner) ? UnknownOwner : owner!;

    /// <summary>
    /// TAOM's own Harmony ids: "com.taom.mod" (Main) plus the "TAOM.*" ids owned by
    /// TAOM.Dependencies, and the per-module id UIExtenderEx registers on TAOM's behalf
    /// ("bannerlord.uiextender.ex.viewmodels.TAOM") — without that last one, a method carrying only
    /// a TAOM mixin plus a TAOM patch reads as a cross-mod conflict.
    /// </summary>
    private static bool IsTaomOwner(string owner) =>
        owner.StartsWith("TAOM", StringComparison.OrdinalIgnoreCase) ||
        owner.StartsWith("com.taom.", StringComparison.OrdinalIgnoreCase) ||
        owner.EndsWith(".viewmodels.TAOM", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// TAOM's blanket shields, which attach to methods they know nothing about. They are owners in
    /// Harmony's registry but never a party to a feature-level conflict, so they are removed before
    /// the contested comparison rather than counted and then filtered.
    /// </summary>
    private static bool IsInfrastructureOwner(string owner) =>
        owner.StartsWith("TAOM.Dependencies.Foundation.", StringComparison.OrdinalIgnoreCase);
}
