using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins PatchShield's two decisions that a co-op session depends on.
///
/// PatchShield itself is static and Harmony-bound, so it cannot be exercised in the test host.
/// The two decisions that actually matter were extracted into pure predicates in
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
}
