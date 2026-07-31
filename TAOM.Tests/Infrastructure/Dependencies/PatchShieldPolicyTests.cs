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
}
