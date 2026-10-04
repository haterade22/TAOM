using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins PatchShield's two decisions that a co-op session depends on, plus its hot-layer target
/// exclusion list (#331, plan 007) and the shield-pass log line.
///
/// PatchShield itself is static and Harmony-bound, so it cannot be exercised in the test host.
/// The decisions that actually matter were extracted into pure predicates in
/// <see cref="PatchShieldPolicy"/>; the Harmony plumbing stays a thin entry point.
///
/// The unpatch gate is the highest-severity item in the co-op work. PatchShield's rescue path
/// strips a foreign owner's prefixes/postfixes/transpilers from a method permanently and
/// mid-session. In singleplayer that converts a crash into a survivable degradation. Under a
/// host-authoritative co-op mod it does something far worse: removing one peer's copy of a sync
/// patch does not crash anything, it silently desynchronises two campaigns — and a desync corrupts
/// both players' saves undiagnosably, whereas a crash is visible and recoverable.
/// </summary>
[TestClass]
public class PatchShieldPolicyTests
{
    [TestMethod]
    public void IsProtectedOwner_TaomOwner_ReturnsTrue()
    {
        Assert.IsTrue(PatchShieldPolicy.IsProtectedOwner(
            "TAOM.Dependencies.Foundation.SaveShield", PatchShieldPolicy.CompiledProtectedOwnerPrefixes));
    }

    [TestMethod]
    public void IsProtectedOwner_VendoredButrOwner_MatchesCaseInsensitively()
    {
        Assert.IsTrue(PatchShieldPolicy.IsProtectedOwner(
            "bannerlord.butterlib.savesystem", PatchShieldPolicy.CompiledProtectedOwnerPrefixes));
    }

    [TestMethod]
    public void IsProtectedOwner_UiExtenderExRealOwnerIds_ReturnTrue()
    {
        // Regression: the list carried "Bannerlord.UIExtenderEx", which matches NEITHER id
        // UIExtenderEx actually registers — the real ones put a dot between "uiextender" and "ex".
        // Unprotected, PatchShield's rescue path would strip TAOM's own UI mixins after an engine
        // bump. Verified against the vendored 2.13.2 source; both ids must be protected.
        foreach (var owner in new[] { "bannerlord.uiextender.ex", "bannerlord.uiextender.ex.viewmodels.TAOM" })
        {
            Assert.IsTrue(
                PatchShieldPolicy.IsProtectedOwner(owner, PatchShieldPolicy.CompiledProtectedOwnerPrefixes),
                $"UIExtenderEx owner '{owner}' must be protected from PatchShield's unpatch path");
        }
    }

    [TestMethod]
    public void IsProtectedOwner_UnknownThirdPartyOwner_ReturnsFalse()
    {
        Assert.IsFalse(PatchShieldPolicy.IsProtectedOwner(
            "com.example.somemod", PatchShieldPolicy.CompiledProtectedOwnerPrefixes));
    }

    [TestMethod]
    public void IsProtectedOwner_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(PatchShieldPolicy.IsProtectedOwner(null, PatchShieldPolicy.CompiledProtectedOwnerPrefixes));
        Assert.IsFalse(PatchShieldPolicy.IsProtectedOwner("", PatchShieldPolicy.CompiledProtectedOwnerPrefixes));
    }

    [TestMethod]
    public void IsProtectedOwner_ConfigAddedPrefix_ReturnsTrue()
    {
        var effective = PatchShieldPolicy.BuildEffectiveOwnerPrefixes(new[] { "com.example.coop" });

        Assert.IsTrue(PatchShieldPolicy.IsProtectedOwner("com.example.coop.sync", effective));
    }

    [TestMethod]
    public void BuildEffectiveOwnerPrefixes_NullExtras_ReturnsCompiledDefaults()
    {
        var effective = PatchShieldPolicy.BuildEffectiveOwnerPrefixes(null);

        CollectionAssert.AreEquivalent(
            PatchShieldPolicy.CompiledProtectedOwnerPrefixes.ToArray(), effective.ToArray());
    }

    [TestMethod]
    public void BuildEffectiveOwnerPrefixes_HostileExtras_StillContainsEveryCompiledDefault()
    {
        // The config file is user-editable and feeds this list. It must be incapable of REMOVING a
        // default — unprotecting the BUTR/MCM stack would let PatchShield strip it on the first
        // missing-API exception, breaking every dependent mod.
        var hostile = new[] { "", "   ", null!, "\0", "TAOM" };

        var effective = PatchShieldPolicy.BuildEffectiveOwnerPrefixes(hostile);

        foreach (var required in PatchShieldPolicy.CompiledProtectedOwnerPrefixes)
        {
            Assert.IsTrue(
                effective.Any(p => string.Equals(p, required, StringComparison.OrdinalIgnoreCase)),
                $"compiled default '{required}' was lost from the effective allowlist");
        }
    }

    [TestMethod]
    public void BuildEffectiveOwnerPrefixes_ExtraDuplicatingADefault_DoesNotDuplicateIt()
    {
        var effective = PatchShieldPolicy.BuildEffectiveOwnerPrefixes(new[] { "TAOM" });

        Assert.AreEqual(1, effective.Count(p => string.Equals(p, "TAOM", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ShouldUnpatchForeignOwners_CoopModuleActive_ReturnsFalse()
    {
        Assert.IsFalse(PatchShieldPolicy.ShouldUnpatchForeignOwners(coopActive: true));
    }

    [TestMethod]
    public void ShouldUnpatchForeignOwners_NoCoopModule_ReturnsTrue()
    {
        // Vanilla singleplayer behaviour is unchanged — the rescue path stays armed.
        Assert.IsTrue(PatchShieldPolicy.ShouldUnpatchForeignOwners(coopActive: false));
    }

    // --- ShouldInstall: full flag x coop truth table ---------------------------------------------
    //
    // Player-reported 2026-08-02: shielding BannerlordCoop's AutoSync surface collapsed frame rate.
    // Every declared method of 43 campaign types got a finalizer that took __originalMethod until
    // plan 034, so Harmony's wrapper paid a GetMethodFromHandle per call (the #331 mechanism) on the
    // campaign hot path. The finalizer now takes only __exception (a stand-in finalizer of that shape added
    // about 3.5 ns per call in plan 034's Debug benchmark, 5.4 against 1.9 ns); the gate stands as decided.
    // These four rows pin it so it cannot regress in either direction.

    [TestMethod]
    public void ShouldInstall_NoCoopNoFlag_ReturnsTrue()
    {
        // The row that matters most: ordinary singleplayer must be completely unaffected.
        Assert.IsTrue(PatchShieldPolicy.ShouldInstall(coopActive: false, disabledByFlag: false));
    }

    [TestMethod]
    public void ShouldInstall_CoopActive_ReturnsFalse()
    {
        Assert.IsFalse(PatchShieldPolicy.ShouldInstall(coopActive: true, disabledByFlag: false));
    }

    [TestMethod]
    public void ShouldInstall_FlagSet_ReturnsFalse()
    {
        // patchshield-disabled.flag keeps working on its own, independent of co-op.
        Assert.IsFalse(PatchShieldPolicy.ShouldInstall(coopActive: false, disabledByFlag: true));
    }

    [TestMethod]
    public void ShouldInstall_CoopActiveAndFlagSet_ReturnsFalse()
    {
        // Either condition alone suppresses; together they must not cancel out.
        Assert.IsFalse(PatchShieldPolicy.ShouldInstall(coopActive: true, disabledByFlag: true));
    }

    [TestMethod]
    public void ShouldInstall_IsTheOnlyPathThatSuppressesTheSwallowHalf()
    {
        // Documents the division of labour, so a future reader does not "simplify" one into the
        // other: ShouldUnpatchForeignOwners withholds the STRIP under co-op while the shield is
        // still installed; ShouldInstall withholds the whole shield, which is what removes the
        // per-call tax. Under co-op both are now false, by different mechanisms.
        Assert.IsFalse(PatchShieldPolicy.ShouldUnpatchForeignOwners(coopActive: true));
        Assert.IsFalse(PatchShieldPolicy.ShouldInstall(coopActive: true, disabledByFlag: false));
    }

    // --- IsExcludedTargetNamespace: the hot-layer exclusion list ---------------------------------

    [TestMethod]
    public void IsExcludedTargetNamespace_GauntletAndTwoDimensionLayers_ReturnsTrue()
    {
        foreach (var ns in new[]
                 {
                     "TaleWorlds.GauntletUI",
                     "TaleWorlds.GauntletUI.PrefabSystem",
                     "TaleWorlds.TwoDimension",
                     "TaleWorlds.MountAndBlade.GauntletUI.Widgets",
                 })
        {
            Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetNamespace(ns), $"'{ns}' must be excluded (#331)");
        }
    }

    [TestMethod]
    public void IsExcludedTargetNamespace_GameplayNamespaces_ReturnsFalse()
    {
        // The list is not co-op-scoped: widening it to gameplay code would drop the shield in solo play.
        foreach (var ns in new[] { "TaleWorlds.CampaignSystem", "TaleWorlds.MountAndBlade", "SandBox", "TaleWorlds.Core" })
        {
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(ns), $"'{ns}' must stay shielded");
        }
    }

    [TestMethod]
    public void IsExcludedTargetNamespace_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(null));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(string.Empty));
    }

    [TestMethod]
    public void IsExcludedTargetNamespace_ManagedCallbacksShims_ReturnsTrue()
    {
        // Native2ManagedPatcher (Main/Features/CrashReport/Hooks) puts a crash-capture finalizer on
        // every static method of ManagedCallbacks.{Library,Core,Engine}CallbacksGenerated (247 in
        // v1.5.3). Re-shielding them cost about 46 s of the first game start's loading screen on a
        // machine paying about 186 ms per Harmony.Patch, for no practical rescue: the known patches
        // there are TAOM's finalizers and ButterLib's blank transpilers (a protected owner).
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetNamespace("ManagedCallbacks"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void IsExcludedTargetNamespace_InstalledCallbackShimTypes_ReturnsTrue()
    {
        // The string test above cannot see an engine bump that moves the shims out of
        // ManagedCallbacks: the exclusion would silently stop matching and the ~247 Harmony.Patch
        // calls would return to the first loading screen. Select the types the way
        // Native2ManagedPatcher does (*CallbacksGenerated in these three assemblies) and check each.
        foreach (var asmName in new[] { "TaleWorlds.MountAndBlade.AutoGenerated", "TaleWorlds.Engine.AutoGenerated", "TaleWorlds.DotNet.AutoGenerated" })
        {
            Assembly asm;
            try { asm = Assembly.Load(asmName); }
            catch (Exception ex) { Assert.Inconclusive($"{asmName} not loadable in the test host: {ex.GetType().Name}"); return; }

            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException tle) { types = tle.Types.Where(t => t != null).ToArray(); }

            var shims = types.Where(t => t.Name.EndsWith("CallbacksGenerated", StringComparison.Ordinal)).ToList();
            Assert.AreNotEqual(0, shims.Count, $"{asmName}: no *CallbacksGenerated type; re-check Native2ManagedPatcher's selector");
            foreach (var t in shims)
            {
                Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetNamespace(t.Namespace), $"{t.FullName} would be re-shielded by PatchShield pass 2");
            }
        }
    }

    // --- IsExcludedTargetMethod: Patch92's per-unit Formation hot members -------------------------
    // The positive case is Patch92BindingTests.EveryPatch92Target_IsOnPatchShieldsHotMethodList, which walks
    // Patch92's real targets instead of repeating the list's strings.

    [TestMethod]
    public void IsExcludedTargetMethod_AnotherFormationMethod_ReturnsFalse()
    {
        // The exclusion is per-method, not per-type: Formation stays a normally-shielded type otherwise.
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Formation", "SetMovementOrder"));
    }

    [TestMethod]
    public void IsExcludedTargetMethod_MissionOnTickAndOnPreTick_ReturnsFalse()
    {
        // Maintainer decision D13 (2026-10-03): plan 028 had excluded both; neither is on the list now, so the
        // shield attaches to them whenever something patches them, and a foreign patch's missing-API throw on
        // Mission.OnTick is swallowed and the patch stripped instead of unwinding the application tick. That does
        // not make an interrupted Mission.OnTick safe (the hazard in PatchShieldPolicy.ExcludedTargetMethods).
        // Only the agent tick stays excluded, so a swallow there cannot skip tickCompleted = true;
        // MissionTickProfilerBindingTests pins that one against the real patch target.
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", "OnTick"));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", "OnPreTick"));
    }

    [TestMethod]
    public void IsExcludedTargetMethod_NullOrEmptyParts_ReturnsFalse()
    {
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(null, "get_UnitDiameter"));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Formation", null));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(null, null));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(string.Empty, "get_UnitDiameter"));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Formation", string.Empty));
    }

    // --- Mission tick targets: which stay off the shield (maintainer decision D13, 2026-10-03) -----
    // The shield costs about 5 ns per call once plan 034's finalizer change is in (64 ns and 241 bytes as the
    // finalizer ships on this branch), so cost alone no longer keeps a target off it:
    // Mission.OnTick, Mission.OnPreTick and Mission.SpawnAgent are shielded again. The three that stay would
    // break the frame if a swallow skipped their body (or, for the script tick, stay as built); the first two stop
    // a swallow only at their own method, since their callers are shielded. The binding
    // tests (MissionTickProfilerBindingTests, HitchProbeBindingTests) check the same split against the
    // installed engine's real targets; these two need no game.

    [TestMethod]
    public void IsExcludedTargetMethod_MissionTickTargetsKeptOffTheShield_ReturnTrue()
    {
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", "TickAgentsAndTeamsImp"));
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", "WaitTickCompletion"));
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.Engine.ManagedScriptHolder", "TickComponents"));
    }

    [TestMethod]
    public void IsExcludedTargetMethod_MissionOnTickOnPreTickAndSpawnAgent_ReturnFalse()
    {
        foreach (var name in new[] { "OnTick", "OnPreTick", "SpawnAgent" })
        {
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", name),
                $"Mission.{name} was excluded for cost alone and is shielded again (D13)");
        }
    }

    // --- FormatHotMethodSkip: the once-per-method diag.log reason line ----------------------------

    [TestMethod]
    public void FormatHotMethodSkip_NamesTheTargetItsOwnersAndWhatIsGivenUp()
        => Assert.AreEqual(
            "not shielding TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp (ExcludedTargetMethods, a hot target), patched by com.taom.mod, other.mod: "
            + "a MissingMethod, MissingField or TypeLoad exception from a patch on it is not swallowed, and that patch is not stripped",
            PatchShieldPolicy.FormatHotMethodSkip("TaleWorlds.MountAndBlade.Mission", "TickAgentsAndTeamsImp", new[] { "com.taom.mod", "other.mod" }));

    [TestMethod]
    public void FormatHotMethodSkip_NoOwnersKnown_SaysUnknown()
        => StringAssert.Contains(
            PatchShieldPolicy.FormatHotMethodSkip("TaleWorlds.MountAndBlade.Mission", "TickAgentsAndTeamsImp", null),
            "(ExcludedTargetMethods, a hot target), patched by unknown: ");

    // --- FormatShieldPassSummary: the diag.log shield-pass line ----------------------------------

    [TestMethod]
    public void FormatShieldPassSummary_WithAttaches_AppendsElapsedAndPerAttach()
    {
        var line = PatchShieldPolicy.FormatShieldPassSummary(added: 372, alreadySeen: 46, skipped: 19, seenTotal: 437, attachedTotal: 399, elapsedMs: 69300);

        // Seen and attached are separate counts: a pass decides on far more methods than it
        // shields (TAOM's own, the excluded hot layers, SaveShield's targets). The layout is the
        // one docs/migration/dr3-maintenance.md shows.
        StringAssert.StartsWith(line, "shield pass: +372 new, 46 already-seen, 19 skipped (seen: 437, attached: 399)");
        StringAssert.Contains(line, "in 69300 ms");
        StringAssert.Contains(line, "186.3 ms/attach");
    }

    [TestMethod]
    public void FormatShieldPassSummary_NoAttaches_DoesNotDivideByZero()
    {
        // The only zero-attach line PatchShield logs is a first pass that attached nothing
        // (PatchShield.Install logs when added > 0 || alreadySeen == 0).
        var line = PatchShieldPolicy.FormatShieldPassSummary(added: 0, alreadySeen: 0, skipped: 16, seenTotal: 16, attachedTotal: 0, elapsedMs: 3);

        StringAssert.Contains(line, "(seen: 16, attached: 0)");
        StringAssert.Contains(line, "in 3 ms");
        StringAssert.EndsWith(line, "(no new attaches)");
        Assert.IsFalse(line.Contains("ms/attach"), line);
        Assert.IsFalse(line.Contains("NaN") || line.Contains("Infinity") || line.Contains("∞"), line);
    }

    [TestMethod]
    public void FormatShieldPassSummary_CommaDecimalCulture_UsesInvariantDecimalPoint()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            var line = PatchShieldPolicy.FormatShieldPassSummary(added: 372, alreadySeen: 46, skipped: 19, seenTotal: 437, attachedTotal: 399, elapsedMs: 69300);
            StringAssert.Contains(line, "186.3 ms/attach");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }
}
