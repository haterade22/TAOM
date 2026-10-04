using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// The pure decisions behind <see cref="PatchShield"/> (which targets to skip, which owners never
/// to unpatch, when to install, and the shield-pass log line), extracted so they can be tested
/// without Harmony or a running game. PatchShield keeps the plumbing; this keeps the policy.
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

    // Issue #331 round 2 (2026-07-09, measured): NEVER shield the Gauntlet/2D UI layer.
    // A shield finalizer binds __originalMethod, so Harmony's generated wrapper pays a
    // MethodBase.GetMethodFromHandle + try/catch on EVERY CALL (~50µs). The Gauntlet
    // prefab system contains per-widget-recursion methods that UIExtenderEx patches
    // (WidgetFactory.IsCustomType prefix, WidgetTemplate.OnRelease blank-transpiler);
    // a tournament's accumulated template tree calls them ~2 MILLION times at release,
    // so the shield tax amplified a milliseconds-scale teardown into a measured 104-109s
    // frozen exit (+8,276 gen0 GCs, invariant across sessions; stack-sampled proof in
    // docs/reviews/rca-tournament-exit-hang-2026-07-06.md round 2). Shield value there
    // is nil anyway: the only patcher of that layer is BUTR's own UIExtenderEx.
    public static readonly IReadOnlyList<string> ExcludedTargetNamespacePrefixes = new[]
    {
        "TaleWorlds.GauntletUI",
        "TaleWorlds.TwoDimension",
        // Round-2 compat review (2026-07-10): TAOM's own Patch38 target
        // (SettlementNameplateWidget.DetermineTargetAlphaValue, ~3000 calls/sec on the
        // campaign map) lives here and was silently paying the shield tax every frame.
        // Same rationale as above: hot widget/view layer, shield value nil.
        "TaleWorlds.MountAndBlade.GauntletUI",
        // Plan 007 (2026-09-23, measured from diag.log): the engine's native-to-managed callback
        // shims, ManagedCallbacks.{Library,Core,Engine}CallbacksGenerated. TAOM's own
        // Native2ManagedPatcher wraps the 16 allowlisted shims (plan 006, Native2ManagedTargets) with
        // a finalizer that, on its normal path, swallows the exception
        // (CrashReportPatchHelper.HandleAndSwallow). When capture is off, on re-entry, or when the
        // crash service is unresolved or throws, it hands the exception back with its throw site
        // kept (HandBack, RethrowStackPreserver). The other 231 shims carry no TAOM finalizer, so
        // on those nothing swallows the missing-API trinity.
        // Shielding them again cost one Harmony.Patch each at the first game start (about 46 s of a
        // 69 s pass 2 on a machine paying 186 ms per Patch) and stacked an __originalMethod wrapper
        // on engine callback hot paths: the #331 hot-layer rationale. Rescue value is nil in
        // practice: the known patches on the shims are TAOM's finalizers and ButterLib BEW's blank
        // transpilers on three tick shims (a protected owner); a third-party prefix, postfix or
        // transpiler on a shim loses the rescue.
        // The prefix is the whole namespace, not only the three shims: it also holds the engine's
        // 79 managed-to-native ScriptingInterfaceOf* wrappers plus CallbackManager and
        // ScriptingInterfaceObjects (88 classes in v1.5.3), which Native2Managed does NOT wrap.
        // They are excluded on the #331 per-call rationale alone, so a third-party patch on one of
        // them gets no shield. PatchShieldPolicyTests pins the shim half against the installed DLLs.
        "ManagedCallbacks",
    };

    /// <summary>Whether a patch target's declaring namespace is on the hot-layer exclusion list (ordinal prefix match).</summary>
    public static bool IsExcludedTargetNamespace(string? targetNamespace)
    {
        if (string.IsNullOrEmpty(targetNamespace)) return false;
        foreach (var prefix in ExcludedTargetNamespacePrefixes)
        {
            if (targetNamespace!.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// Method-level hot-target exclusion list: "&lt;FullTypeName&gt;.&lt;MethodName&gt;" entries for engine
    /// members whose declaring TYPE is not hot enough to exclude by namespace (Patch92's own targets sit
    /// in the otherwise-ordinary <c>TaleWorlds.MountAndBlade</c> namespace) but whose own call frequency
    /// makes a per-call <c>__originalMethod</c> finalizer tax unacceptable: <c>Formation.get_UnitDiameter</c>
    /// runs per unit per formation-positioning query, and the order preview, the deployment placement and
    /// the spawn frames all walk every unit through <c>GetUnitPositionWithIndexAccordingToNewOrder</c>
    /// (three overloads, one entry covers all of them) and <c>GetUnitSpawnFrameWithIndex</c>. Same rationale
    /// as the namespace list's #331 (Gauntlet UI) and Patch38 (SettlementNameplateWidget) entries, applied
    /// at method granularity instead of namespace granularity.
    ///
    /// PatchShield skips an excluded method for every owner, so a third-party patch on one of these methods
    /// also loses the rescue. Patch92BindingTests walks Patch92's real targets through
    /// <see cref="IsExcludedTargetMethod"/>, so a new or renamed target fails there rather than being shielded.
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedTargetMethods = new[]
    {
        "TaleWorlds.MountAndBlade.Formation.get_UnitDiameter",
        "TaleWorlds.MountAndBlade.Formation.GetUnitPositionWithIndexAccordingToNewOrder",
        "TaleWorlds.MountAndBlade.Formation.GetUnitSpawnFrameWithIndex",
        // Patch93's weapon-state guards (#692): read for every agent by AI, combat and UI, often on worker
        // threads. CreatureBanditsWiringTests.HotCreatureTargets_AreOnPatchShieldsExclusionList walks the real
        // targets through IsExcludedTargetMethod.
        "TaleWorlds.MountAndBlade.Agent.GetPrimaryWieldedItemIndex",
        "TaleWorlds.MountAndBlade.Agent.GetOffhandWieldedItemIndex",
        "TaleWorlds.MountAndBlade.Agent.GetMissileRange",
        // Patch93_CreatureBanditNoRout: CommonAIComponent.OnTickParallel asks it for every AI agent, horses
        // included, every 0.5 to 0.6 s on the TWParallel workers.
        "TaleWorlds.MountAndBlade.Mission.CanAgentRout",
        // Mission.TickAgentsAndTeamsImp carries Patch91's bracket for every player. With a finalizer on it, a
        // swallowed exception from the agent or team ticks would skip tickCompleted = true (Mission.cs:3629), so
        // the next WaitTickCompletion would spin forever; excluded, the exception leaves the method instead.
        // That is all this entry buys. On the asynchronous call it reaches the native job thread (the generated
        // shim Mission_TickAgentsAndTeams has no catch, and what native does then is UNVERIFIED); on the inline
        // call, which fast-forward makes (MissionState.TickMission passes asyncAITick false), it reaches
        // Mission.OnTick, see the hazard below. Given up: the missing-API swallow and the strip of the
        // offending patch, for any owner.
        // Mission.OnTick (Patch35's postfix for every player, Patch97's transpiler) and Mission.OnPreTick (TAOM
        // patches it only through Patch97; any patch on it attaches the shield) are deliberately NOT listed. Plan
        // 028 listed them under the per-frame rule (lessons/harmony-il.md, 2026-09-26 and 2026-09-28); the
        // maintainer took them off (decision D13, 2026-10-03), because plan 034 takes the per-call lookup off
        // the shield's no-exception path, and left unshielded a foreign patch's missing-API throw on
        // Mission.OnTick unwinds the whole application tick in a process's first game. The price: the strip is
        // not culprit-only and "com.taom.mod" is not a protected owner, so if the shield ever strips patches on
        // Mission.OnTick, TAOM's own Patch35 and Patch97 go with them, until the planned culprit-only fix.
        // KNOWN HAZARD, older than the profiler and NOT fixed here: no choice on this list makes an interrupted
        // Mission.OnTick safe. OnTick clears tickCompleted (Mission.cs:3756) before its OnMissionTick loop, and
        // only TickAgentsAndTeamsImp sets it again (:3629). An exception that escapes OnTick between the two (a
        // behaviour's OnMissionTick, which need not be a patch; a patch or transpiled call in that stretch; the
        // inline agent tick) and is swallowed by a finalizer above it, this shield's (missing-API only) or
        // Patch37's crash capture on Module.OnApplicationTick (any exception, while capture is on), leaves the
        // flag false, and every later WaitTickCompletion spins forever. A throw before the clear (a prefix) or
        // after the agent tick is launched (a postfix) does not leave the flag false. A throw before the clear
        // still costs the frame: one no patch made (a behaviour's OnPreDisplayMissionTick, Mission.cs:3750) that a
        // finalizer swallows skips the rest of OnTick, the agent tick launch included, on every frame it recurs.
        // This shield logs each missing-API swallow; Patch37's capture logs at occurrences 1, 2, 10, 100 and so on
        // (CrashBundleThrottle.IsLoggedOccurrence).
        // The PatchShield follow-up plan (the culprit-only strip, FOLLOW-UP L1, plus a completion-aware recovery)
        // takes it, with regression tests for an exception before the clear, between the clear and the launch,
        // and inside the inline agent tick. It takes this one too: a swallowed foreign prefix throw on
        // Mission.OnPreTick skips the whole body, whose first call is WaitTickCompletion, so that frame's OnTick
        // can run while the previous agent tick still runs (consequence UNVERIFIED); trunk shields a foreign patch
        // on it the same way, so that is older than the profiler as well.
        // MissionTickProfilerBindingTests walks the real targets in both directions.
        "TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp",
        // Per-frame campaign-map targets that only the Patch101 map profiler patches (2026-10-03),
        // unconditional like the entries above: with the profiler off no TAOM patch exists on them, but another
        // mod's patch on them loses the shield all the same. MapState.OnTick, Campaign.RealTick and
        // MapScreen.OnFrameTick, which Patch43, Patch89 and Patch36 patch for every player, keep the shield by
        // the maintainer's decision D13 (option B, 2026-10-03): their once-per-frame finalizer stays inside the
        // map profiler's numbers, and a swallow there also strips Patch101's pair on that method, which the
        // profiler finds at its next window or session start (MapSessionHooks.LostHooks) and stops measuring,
        // except on MapState.OnTick, whose strip removes the boundary that runs the window check, so there the
        // lines stop until the session end looks again.
        // Given up on the two below, for any owner: the swallow of a
        // missing-API exception thrown anywhere inside them (the tick-event listeners, the periodic and hourly
        // events of every mod's campaign behaviours); it unwinds to MapState.OnTick instead
        // (docs/features/map-perf-profiler.md, "PatchShield").
        // MapFrameProfilerBindingTests.ProfilerOnlyTargets_AreOnPatchShieldsExclusionList and
        // SharedMapTargets_StayUnderPatchShield walk the real targets.
        "TaleWorlds.CampaignSystem.Campaign.Tick",
        "TaleWorlds.CampaignSystem.CampaignEvents.Tick",
    };

    /// <summary>Whether a patch target's declaring type + method name is on the hot-method exclusion list.</summary>
    public static bool IsExcludedTargetMethod(string? declaringType, string? name)
    {
        if (string.IsNullOrEmpty(declaringType) || string.IsNullOrEmpty(name)) return false;
        var key = declaringType + "." + name;
        foreach (var entry in ExcludedTargetMethods)
        {
            if (string.Equals(entry, key, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    /// <summary>
    /// The diag.log reason line PatchShield writes once per process for each patched method it skips
    /// because it is on <see cref="ExcludedTargetMethods"/>: which method, whose patches sit on it, and
    /// what the skip gives up.
    /// </summary>
    public static string FormatHotMethodSkip(string? declaringType, string? name, IEnumerable<string>? owners)
    {
        var ownerList = owners == null ? string.Empty : string.Join(", ", owners.Where(o => !string.IsNullOrEmpty(o)));
        return "not shielding " + declaringType + "." + name + " (ExcludedTargetMethods, a hot target), patched by "
            + (ownerList.Length == 0 ? "unknown" : ownerList)
            + ": a MissingMethod, MissingField or TypeLoad exception from a patch on it is not swallowed, and that patch is not stripped";
    }

    /// <summary>
    /// The diag.log line for one shield pass. "seen" counts every method the passes so far decided on,
    /// skipped ones included; "attached" counts the methods carrying PatchShield's finalizer, the
    /// real coverage (they were one conflated "total" before 2026-09-24). The timing
    /// suffix exists because diag.log ships in every crash bundle, and the per-attach cost of
    /// Harmony.Patch is not stable: one desktop has logged both about 5 to 10 ms and about 186 ms per
    /// attach (diag.log, 2026-06 to 2026-09), a 30x swing that decides whether a pass costs about a
    /// second or tens of seconds of a player's loading screen.
    /// </summary>
    public static string FormatShieldPassSummary(int added, int alreadySeen, int skipped, int seenTotal, int attachedTotal, long elapsedMs)
    {
        var line = $"shield pass: +{added} new, {alreadySeen} already-seen, {skipped} skipped (seen: {seenTotal}, attached: {attachedTotal}) in {elapsedMs} ms";
        return added > 0
            ? line + " (" + ((double)elapsedMs / added).ToString("F1", CultureInfo.InvariantCulture) + " ms/attach)"
            : line + " (no new attaches)";
    }

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
