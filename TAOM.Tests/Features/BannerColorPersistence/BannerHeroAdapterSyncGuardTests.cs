using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BannerColorPersistence;

/// <summary>
/// Kingdom colour sync skips a ruling house only while Realms' house banners are on (#1297). Each XML kingdom builds
/// its own banner from its banner_key, so ClanOriginalBanner and Kingdom.Banner are never the same object; a guard on
/// that comparison alone stopped the sync for every XML kingdom, even with house banners off. The adapter takes sealed
/// campaign types, so this reads its source.
/// </summary>
[TestClass]
public class BannerHeroAdapterSyncGuardTests
{
    [TestMethod]
    public void SyncKingdomColors_SkipsTheHouseOnlyWhileHouseBannersAreOn()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/BannerHeroAdapter.cs", stripComments: true);

        StringAssert.Matches(src, new Regex(
            @"if \(HouseBanners\.IsActive && !ReferenceEquals\(clan\.ClanOriginalBanner, kingdom\.Banner\)\) return;"));
    }
}
