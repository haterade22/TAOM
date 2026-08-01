using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Features.Dependencies;

/// <summary>
/// #370 — the co-op presence decision. Two invariants matter more than the happy path: unknown
/// fails CLOSED (an uninspectable session behaves as unmodded), and the force flag only ever ADDS
/// presence.
/// </summary>
[TestClass]
public class CoopPresencePolicyTests
{
    private static readonly string[] Known = { "BannerlordTogether", "BattleLinkMPClient", "Coop" };

    private static string[] Resolve(string[] active, bool flag = false) =>
        CoopPresencePolicy.ResolveActiveIds(active, Known, flag).ToArray();

    // --- Detection ------------------------------------------------------------------------

    [TestMethod]
    public void ResolveActiveIds_KnownModuleActive_ReturnsIt()
    {
        var result = Resolve(new[] { "Native", "SandBox", "BannerlordTogether", "TAOM" });

        CollectionAssert.AreEqual(new[] { "BannerlordTogether" }, result);
    }

    [TestMethod]
    public void ResolveActiveIds_MultipleKnownModulesActive_ReturnsAll()
    {
        var result = Resolve(new[] { "BannerlordTogether", "Coop" });

        CollectionAssert.AreEquivalent(new[] { "BannerlordTogether", "Coop" }, result);
    }

    [TestMethod]
    public void ResolveActiveIds_NoCoopModuleActive_ReturnsEmpty()
    {
        Assert.AreEqual(0, Resolve(new[] { "Native", "SandBox", "TAOM" }).Length);
    }

    [TestMethod]
    public void ResolveActiveIds_MatchIsCaseInsensitive()
    {
        // The launcher's casing is not ours to rely on.
        var result = Resolve(new[] { "bannerlordtogether" });

        Assert.AreEqual(1, result.Length);
    }

    [TestMethod]
    public void ResolveActiveIds_RepeatedIds_AreDeduplicated()
    {
        var result = Resolve(new[] { "Coop", "Coop", "coop" });

        Assert.AreEqual(1, result.Length);
    }

    // --- Fail closed ----------------------------------------------------------------------

    [TestMethod]
    public void ResolveActiveIds_ModuleListUnavailable_ReturnsEmpty()
    {
        // Empty means UNKNOWN, not "none". Treating unknown as co-op would make every session that
        // fails to introspect itself silently drop TAOM's diplomacy vetoes.
        Assert.AreEqual(0, Resolve(Array.Empty<string>()).Length);
    }

    [TestMethod]
    public void ResolveActiveIds_NullModuleList_ReturnsEmpty()
    {
        Assert.AreEqual(0, CoopPresencePolicy.ResolveActiveIds(null, Known, false).Count);
    }

    [TestMethod]
    public void ResolveActiveIds_NullKnownIds_ReturnsEmpty()
    {
        Assert.AreEqual(
            0, CoopPresencePolicy.ResolveActiveIds(new[] { "BannerlordTogether" }, null, false).Count);
    }

    // --- The force flag only ever ADDS ----------------------------------------------------

    [TestMethod]
    public void ResolveActiveIds_ModuleListUnavailable_FlagPresent_ForcesMarker()
    {
        // The reason the flag exists: detection is broken, the player knows co-op is running.
        var result = Resolve(Array.Empty<string>(), flag: true);

        CollectionAssert.AreEqual(new[] { CoopPresencePolicy.ForcedMarkerId }, result);
    }

    [TestMethod]
    public void ResolveActiveIds_RenamedCoopModule_FlagPresent_ForcesMarker()
    {
        // A renamed BT build or an unknown fork — present in the list, but not a known id.
        var result = Resolve(new[] { "Native", "BannerlordTogether_Fork_v2" }, flag: true);

        CollectionAssert.AreEqual(new[] { CoopPresencePolicy.ForcedMarkerId }, result);
    }

    [TestMethod]
    public void ResolveActiveIds_KnownModuleActive_FlagPresent_ReturnsRealIdNotMarker()
    {
        // The flag must not mask a real detection — the census and logs need the true id.
        var result = Resolve(new[] { "BannerlordTogether" }, flag: true);

        CollectionAssert.AreEqual(new[] { "BannerlordTogether" }, result);
        CollectionAssert.DoesNotContain(result, CoopPresencePolicy.ForcedMarkerId);
    }

    [TestMethod]
    public void ResolveActiveIds_FlagAbsent_NeverRemovesADetectedModule()
    {
        // There is deliberately no way to force co-op OFF: a stray file must not be able to take a
        // live co-op session out of co-op mode, which would resurrect the D1 divergence.
        var result = Resolve(new[] { "BannerlordTogether" }, flag: false);

        CollectionAssert.AreEqual(new[] { "BannerlordTogether" }, result);
    }

    [TestMethod]
    public void ForcedMarkerId_IsNotAPlausibleModuleId()
    {
        // It appears in logs and ActiveCoopModuleIds; it must be unmistakable for a real id.
        StringAssert.Contains(CoopPresencePolicy.ForcedMarkerId, "(");
        Assert.IsFalse(
            CoopPresencePolicy.ForcedMarkerId.All(char.IsLetterOrDigit),
            "marker must not look like a module id");
    }
}
