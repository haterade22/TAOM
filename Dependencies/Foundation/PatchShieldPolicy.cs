using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// The two pure decisions behind <see cref="PatchShield"/>'s rescue path, extracted so they can be
/// tested without Harmony or a running game. PatchShield keeps the plumbing; this keeps the policy.
/// </summary>
public static class PatchShieldPolicy
{
    /// <summary>
    /// Harmony owner-id prefixes PatchShield must never unpatch.
    ///
    /// Codex review 2026-05-27 S1 (HIGH): expanded from "TAOM" only to the full infrastructure
    /// owner set. Vendored BUTR/MCM Harmony ids ("Bannerlord.ButterLib.SaveSystem",
    /// "MCM.UI.Adapter.MCMv5", …) do NOT start with "TAOM" — the prior filter would have unpatched
    /// the entire BUTR stack on the first MissingMethodException, breaking every dependent mod.
    /// Mirrors the vendored DLLs in Dependencies/_Module/bin/Win64_Shipping_Client/ plus
    /// Lib.Harmony's own runtime types.
    /// </summary>
    public static readonly IReadOnlyList<string> CompiledProtectedOwnerPrefixes = new[]
    {
        "TAOM",
        "Bannerlord.ButterLib",
        "butterlib.",
        "Bannerlord.UIExtenderEx",
        // The id UIExtenderEx ACTUALLY registers. Verified 2026-07-31 against the vendored
        // Bannerlord.UIExtenderEx 2.13.2 source: it creates exactly two Harmony instances —
        // `bannerlord.uiextender.ex` (UIExtender.cs:28) and
        // `bannerlord.uiextender.ex.viewmodels.<module>` (ViewModelComponent.cs:50), the latter
        // being `bannerlord.uiextender.ex.viewmodels.TAOM` for us. Neither starts with
        // "Bannerlord.UIExtenderEx" — the real ids put a dot between "uiextender" and "ex" — so
        // that entry above matches nothing, and without this line PatchShield's rescue path would
        // happily unpatch TAOM's OWN UI mixins (CharacterDeveloperVM, MapInfoVM, …) after an
        // engine bump. This one prefix covers both ids.
        "bannerlord.uiextender.ex",
        "Bannerlord.MBOptionScreen",
        "Bannerlord.ModuleLoader",
        "Bannerlord.MCM",
        "bannerlord.mcm.",
        "MCM",
        "MCMv5",
        "MCM.UI.Adapter",
        "BUTR.",
        "HarmonyLib.",
        "0Harmony",
    };

    /// <summary>
    /// Unions the compiled defaults with any extra prefixes from <c>coop-modules.txt</c>. Union
    /// only, and blank entries are dropped: the config file must be incapable of REMOVING a
    /// compiled default, or a bad edit could unprotect the whole BUTR/MCM stack.
    /// </summary>
    public static IReadOnlyList<string> BuildEffectiveOwnerPrefixes(IEnumerable<string>? extraPrefixes)
    {
        var set = new HashSet<string>(CompiledProtectedOwnerPrefixes, StringComparer.OrdinalIgnoreCase);
        if (extraPrefixes != null)
        {
            foreach (var prefix in extraPrefixes)
            {
                if (!string.IsNullOrWhiteSpace(prefix)) set.Add(prefix.Trim());
            }
        }
        return set.ToList();
    }

    /// <summary>Case-insensitive prefix match of a Harmony owner id against the allowlist.</summary>
    public static bool IsProtectedOwner(string? owner, IEnumerable<string> protectedPrefixes)
    {
        if (string.IsNullOrEmpty(owner)) return false;
        if (protectedPrefixes == null) return false;

        foreach (var prefix in protectedPrefixes)
        {
            if (string.IsNullOrEmpty(prefix)) continue;
            if (owner!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// Whether PatchShield may strip a non-allowlisted owner's patches after the missing-API
    /// trinity. FALSE whenever a co-op module is active.
    ///
    /// Why co-op inverts this: unpatching is an irreversible, mid-session, process-global mutation
    /// of another mod's patch set. Under a host-authoritative co-op mod, removing one peer's copy
    /// of a sync patch produces no crash at all — it produces a silent divergence between two
    /// campaigns, which corrupts both saves and cannot be diagnosed from a log. The crash it was
    /// trying to prevent is the strictly better outcome, because it is visible and recoverable.
    ///
    /// PatchShield's SWALLOW half — the part that actually keeps the session alive — is unaffected;
    /// only the strip is withheld, and the call site still logs what it would have done.
    /// </summary>
    public static bool ShouldUnpatchForeignOwners(bool coopActive) => !coopActive;
}
