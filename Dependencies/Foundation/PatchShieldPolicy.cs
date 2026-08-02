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
        // BannerlordCoop's four Harmony owner ids, read from its decompiled source 2026-08-01:
        // "Bannerlord.Coop" (GameInterfaceModule.HarmonyId — carries every explicit patch plus the
        // whole AutoSync transpiler engine), "Coop.UILoading" (CoopMod.cs:97), "Coop.BootFix"
        // (BootPatches.cs:58) and "CoopAutoRegistryFactory" (AutoRegistryFactory.cs:18, declared but
        // never used to patch). Listed explicitly rather than as a bare "Coop" prefix so the entry
        // stays self-documenting and cannot swallow an unrelated mod whose id merely starts "Coop".
        //
        // Belt-and-braces only: ShouldUnpatchForeignOwners already disables the strip path outright
        // whenever a co-op module is active. This matters for the window where the module-list probe
        // has not yet succeeded. Internals: docs/research/bannerlordcoop-internals.md
        "Bannerlord.Coop",
        "Coop.UILoading",
        "Coop.BootFix",
        "CoopAutoRegistryFactory",
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

    /// <summary>
    /// Should PatchShield install at all?
    ///
    /// NO under co-op, and this is a PERFORMANCE decision, not a correctness one. A shield finalizer
    /// binds <c>__originalMethod</c>, so Harmony's generated wrapper pays a
    /// <c>MethodBase.GetMethodFromHandle</c> plus a try/catch on EVERY CALL (~50 µs). That tax is
    /// what turned a millisecond tournament teardown into a measured 104–109 s freeze in #331, and
    /// co-op amplifies it far harder: BannerlordCoop's AutoSync transpiles every declared method and
    /// constructor of 43 campaign types (<c>MobileParty</c>, <c>Hero</c>, <c>Settlement</c>,
    /// <c>Clan</c>, <c>PartyBase</c>…), and those are the campaign hot path. Coop's <c>PatchAll</c>
    /// runs on connect, BEFORE TAOM's <c>OnGameInitializationFinished</c> pass, so pass 2 shields
    /// that entire surface. A player traced a co-op frame-rate collapse to exactly this, which
    /// answers the open question the 2026-08-01 deep review raised and could not measure.
    ///
    /// Extending <c>ExcludedTargetNamespacePrefixes</c> instead would have been wrong: adding
    /// <c>TaleWorlds.CampaignSystem</c> there excludes nearly everything TAOM shields IN SOLO PLAY
    /// TOO, because that list is not co-op-scoped.
    ///
    /// WHAT THIS GIVES UP: the swallow half — surviving a <c>MissingMethodException</c> /
    /// <c>MissingFieldException</c> / <c>TypeLoadException</c> from engine drift. Under co-op that
    /// is the right trade and matches SaveShield, which already RETHROWS save-load faults for the
    /// same reason: a visible crash beats a silent divergence between two campaigns. The unpatch
    /// half was already withheld (see <see cref="ShouldUnpatchForeignOwners"/>), so this removes the
    /// remaining half rather than changing the policy's direction.
    ///
    /// KNOWN COST: a player with the co-op module merely ENABLED but playing solo also loses the
    /// shield, for no benefit — Coop only calls <c>PatchAll</c> on connect. That is unavoidable
    /// here. Install runs at <c>OnSubModuleLoad</c> / <c>OnGameInitializationFinished</c>, before
    /// any session can exist, so there is nothing session-scoped to read.
    /// </summary>
    /// <param name="coopActive">
    /// <c>CoopPresence.IsActive</c> — module presence, NOT session/role. Presence is the correct
    /// signal at a patch-application site and the ONLY co-op fact safe to read there: TAOM's late
    /// patch batch is a process one-shot and one of its transpilers is non-idempotent, so a gate
    /// that varied per session could never be undone or re-run. This deliberately does NOT use
    /// <c>ICoopSessionProvider</c>. The "presence is not authority" rule from the 2026-08-01 Codex
    /// review governs WORLD-STATE decisions; it does not apply to install-time gating, and the two
    /// look alike enough that this comment exists to stop the next reader "fixing" it.
    /// </param>
    public static bool ShouldInstall(bool coopActive, bool disabledByFlag)
        => !disabledByFlag && !coopActive;
}
