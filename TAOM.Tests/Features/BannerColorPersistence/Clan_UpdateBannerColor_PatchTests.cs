using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.BannerColorPersistence;
using TAOM.Features.BannerColorPersistence.Hooks;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.BannerColorPersistence;

[TestClass]
public class Clan_UpdateBannerColor_PatchTests
{
    private IBannerColorService _service;
    private IBannerHeroAdapter _heroAdapter;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IBannerColorService>();
        _heroAdapter = Substitute.For<IBannerHeroAdapter>();
        Clan_UpdateBannerColor_Patch.Initialize(_service, _heroAdapter);
    }

    [TestMethod]
    public void Postfix_ServiceIsNull_DoesNotCallHeroAdapter()
    {
        // Arrange
        Clan_UpdateBannerColor_Patch.Initialize(null, _heroAdapter);

        // Act — null instance is passed; guard fires before adapter call
        Clan_UpdateBannerColor_Patch.Postfix(null);

        // Assert
        _heroAdapter.DidNotReceive().SyncKingdomColors(Arg.Any<Clan>());
    }

    [TestMethod]
    public void Postfix_DriftGuardDisabled_DoesNotCallHeroAdapter()
    {
        // Arrange
        _service.IsDriftGuardEnabled().Returns(false);

        // Act — null instance is fine; guard exits before accessing it
        Clan_UpdateBannerColor_Patch.Postfix(null);

        // Assert
        _heroAdapter.DidNotReceive().SyncKingdomColors(Arg.Any<Clan>());
    }

    [TestMethod]
    public void Postfix_DriftGuardEnabled_NullInstance_DoesNotCallHeroAdapter()
    {
        // Arrange
        _service.IsDriftGuardEnabled().Returns(true);

        // Act — null instance triggers Kingdom null-check, short-circuits
        Clan_UpdateBannerColor_Patch.Postfix(null);

        // Assert
        _heroAdapter.DidNotReceive().SyncKingdomColors(Arg.Any<Clan>());
    }
}
