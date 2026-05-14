using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;

namespace TAOM.Tests.Features.BannerColorPersistence;

[TestClass]
public class Clan_UpdateBannerColorsAccordingToKingdom_PatchTests
{
    private IBannerColorService _service;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IBannerColorService>();
        Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize(_service);
    }

    // Phase 9b #172 F2 — Prefix takes `Clan __instance` so DriftGuard can scope to the player
    // clan only. `Clan` is sealed and not constructible in unit tests; `Clan.PlayerClan` throws
    // NRE without Campaign.Current. The deterministic test surface is the "DriftGuard disabled"
    // branch (returns true regardless of __instance) and the "service is null" branch — both
    // short-circuit BEFORE the Clan.PlayerClan comparison so they're test-safe.

    [TestMethod]
    public void Prefix_DriftGuardDisabled_ReturnsTrueAllowingOriginal()
    {
        _service.IsDriftGuardEnabled().Returns(false);

        // Pass null __instance — gate short-circuits BEFORE Clan.PlayerClan dereference because
        // `!_service.IsDriftGuardEnabled()` is true → return true immediately.
        bool result = Clan_UpdateBannerColorsAccordingToKingdom_Patch.Prefix(null);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Prefix_ServiceIsNull_ReturnsTrueAllowingVanilla()
    {
        // Null service simulates uninitialized state; null-conditional `?? false` means allow vanilla.
        Clan_UpdateBannerColorsAccordingToKingdom_Patch.Initialize((IBannerColorService?)null);

        bool result = Clan_UpdateBannerColorsAccordingToKingdom_Patch.Prefix(null);

        Assert.IsTrue(result);
    }

    // Phase 9b #172 F2 — the "DriftGuard enabled + player clan vs non-player clan" branches
    // are not reachable from unit tests because Clan.PlayerClan requires Campaign.Current.
    // These paths are covered by manual in-game testing post-merge.
}
