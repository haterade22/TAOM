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
    // Every declared method of 43 campaign types gets a finalizer that binds __originalMethod, so
    // Harmony's wrapper pays GetMethodFromHandle + try/catch per call — the #331 mechanism, on the
    // campaign hot path. These four rows pin the gate so it cannot regress in either direction.

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
        // (PatchShield.Install logs when added > 0 || alreadyShielded == 0).
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
