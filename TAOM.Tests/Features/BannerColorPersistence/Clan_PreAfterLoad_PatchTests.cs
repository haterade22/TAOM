using System;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BannerColorPersistence.Hooks;

namespace TAOM.Tests.Features.BannerColorPersistence;

/// <summary>
/// The Clan.PreAfterLoad bracket the load-repaint skip reads. Run through real Harmony on local fakes, as the harmony
/// rules require for a prefix and finalizer pair: the flag is set only inside the call, cleared on a throw, kept for an
/// outer call when an inner one returns, and left alone by a finalizer whose prefix never ran (stripped).
/// </summary>
[TestClass]
public class Clan_PreAfterLoad_PatchTests
{
    public class FakeClan
    {
        public static bool SeenDuring;
        public static bool SeenAfterInner;
        public static bool Throw;
        public static Action? Inner;

        public void PreAfterLoad()
        {
            SeenDuring = Clan_PreAfterLoad_Patch.InClanLoad;
            var inner = Inner;
            Inner = null;
            inner?.Invoke();
            SeenAfterInner = Clan_PreAfterLoad_Patch.InClanLoad;
            if (Throw) throw new InvalidOperationException("load failed");
        }
    }

    public class FakeFinalizerOnly
    {
        public void PreAfterLoad() { }
    }

    private Harmony _harmony = null!;

    [TestInitialize]
    public void Setup()
    {
        FakeClan.SeenDuring = false;
        FakeClan.SeenAfterInner = false;
        FakeClan.Throw = false;
        FakeClan.Inner = null;
        _harmony = new Harmony("taom.tests.clan-preafterload." + Guid.NewGuid());
        _harmony.Patch(
            AccessTools.Method(typeof(FakeClan), nameof(FakeClan.PreAfterLoad)),
            prefix: new HarmonyMethod(typeof(Clan_PreAfterLoad_Patch), nameof(Clan_PreAfterLoad_Patch.Prefix)),
            finalizer: new HarmonyMethod(typeof(Clan_PreAfterLoad_Patch), nameof(Clan_PreAfterLoad_Patch.Finalizer)));
        _harmony.Patch(
            AccessTools.Method(typeof(FakeFinalizerOnly), nameof(FakeFinalizerOnly.PreAfterLoad)),
            finalizer: new HarmonyMethod(typeof(Clan_PreAfterLoad_Patch), nameof(Clan_PreAfterLoad_Patch.Finalizer)));
    }

    [TestCleanup]
    public void Cleanup() => _harmony.UnpatchAll(_harmony.Id);

    [TestMethod]
    public void InClanLoad_DuringTheCall_TrueAndFalseAfter()
    {
        Assert.IsFalse(Clan_PreAfterLoad_Patch.InClanLoad);

        new FakeClan().PreAfterLoad();

        Assert.IsTrue(FakeClan.SeenDuring);
        Assert.IsFalse(Clan_PreAfterLoad_Patch.InClanLoad);
    }

    [TestMethod]
    public void InClanLoad_TheCallThrows_ClearedAndTheExceptionStillPropagates()
    {
        FakeClan.Throw = true;

        Assert.ThrowsException<InvalidOperationException>(() => new FakeClan().PreAfterLoad());

        Assert.IsFalse(Clan_PreAfterLoad_Patch.InClanLoad);
    }

    [TestMethod]
    public void InClanLoad_NestedCallReturns_TheOuterCallStillInside()
    {
        FakeClan.Inner = () => new FakeClan().PreAfterLoad();

        new FakeClan().PreAfterLoad();

        Assert.IsTrue(FakeClan.SeenAfterInner, "the inner call's finalizer must not end the outer bracket");
        Assert.IsFalse(Clan_PreAfterLoad_Patch.InClanLoad);
    }

    [TestMethod]
    public void Finalizer_ItsPrefixStripped_LeavesTheOuterBracketOpen()
    {
        FakeClan.Inner = () => new FakeFinalizerOnly().PreAfterLoad();

        new FakeClan().PreAfterLoad();

        Assert.IsTrue(FakeClan.SeenAfterInner, "a finalizer without its prefix owns nothing to clear");
        Assert.IsFalse(Clan_PreAfterLoad_Patch.InClanLoad);
    }
}
