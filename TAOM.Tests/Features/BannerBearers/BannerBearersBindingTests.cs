using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BannerBearers.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BannerBearers;

/// <summary>
/// Drift-guards for Patch63's engine bindings (issue #360). The spawn-guard prefix
/// reimplements <c>BannerBearerLogic.SpawnBannerBearer</c> and reaches its PRIVATE nested
/// <c>FormationBannerController</c> plus two private methods via cached reflection — a rename
/// silently deactivates the guard (fail-open by design) and re-exposes the slot-4 AV CTD.
/// These tests redden offline instead.
/// </summary>
[TestClass]
public class BannerBearersBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch63_ReflectedMembers_ResolveAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsTrue(BannerBearerLogicReflection.TryResolve(out var missing),
            "Patch63 bindings unresolved — the spawn guard would deactivate (fail-open) and the " +
            "reinforcement slot-4 AV (issue #360) would be reachable again: " + string.Join(", ", missing));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnBannerBearer_SignatureMatchesPrefix()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var target = AccessTools.Method("TaleWorlds.MountAndBlade.BannerBearerLogic:SpawnBannerBearer");
        Assert.IsNotNull(target, "BannerBearerLogic.SpawnBannerBearer did not resolve — Patch63 target gone.");

        // The prefix declares every parameter by name; Harmony matches by name, so a rename is a
        // silent no-bind. Pin the exact list.
        var expected = new[]
        {
            "troopOrigin", "isPlayerSide", "formation", "spawnWithHorse", "isReinforcement",
            "formationTroopCount", "formationTroopIndex", "isAlarmed", "wieldInitialWeapons",
            "initialPosition", "initialDirection", "specialActionSetSuffix", "useTroopClassForSpawn",
        };
        CollectionAssert.AreEqual(expected, target!.GetParameters().Select(p => p.Name).ToArray(),
            "SpawnBannerBearer parameter list drifted — update Patch63's Prefix signature to match.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Mission_SpawnTroop_OverloadUsedByPrefix_Resolves()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // The prefix calls the public 15-parameter overload (with bannerItem + formationIndex
        // defaults) directly — compile-time bound, but the parameter COUNT is engine-owned and a
        // new overload/reshuffle would redirect the call. Pin the shape the prefix relies on.
        var spawnTroop = AccessTools.Method("TaleWorlds.MountAndBlade.Mission:SpawnTroop");
        Assert.IsNotNull(spawnTroop, "Mission.SpawnTroop did not resolve.");
        Assert.AreEqual(15, spawnTroop!.GetParameters().Length,
            "Mission.SpawnTroop parameter count drifted — re-verify Patch63's plain-spawn and banner-spawn calls.");
        Assert.AreEqual("bannerItem", spawnTroop.GetParameters()[12].Name,
            "Mission.SpawnTroop no longer takes bannerItem at position 13 — re-verify Patch63.");
    }
}
