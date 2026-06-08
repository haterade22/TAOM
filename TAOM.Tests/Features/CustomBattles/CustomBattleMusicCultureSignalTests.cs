using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles.Hooks;
using TAOM.Features.Music;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.CustomBattles;

[TestClass]
public class CustomBattleMusicCultureSignalTests
{
    private ICustomBattleMusicContextService _musicContext;

    [TestInitialize]
    public void Setup()
    {
        _musicContext = Substitute.For<ICustomBattleMusicContextService>();
        CustomBattleSideVM_OnCultureSelection_Patch.Initialize(
            Substitute.For<ISideCommanderFilter>(),
            Substitute.For<IModLogger>(),
            _musicContext);
    }

    [TestMethod]
    public void SignalMusicCulture_PlayerSideDelegatesCultureStringId()
    {
        var culture = new BasicCultureObject { StringId = "gondor" };

        CustomBattleSideVM_OnCultureSelection_Patch.SignalMusicCulture(true, culture);

        _musicContext.Received(1).SelectPlayerCulture("gondor");
    }

    [TestMethod]
    public void SignalMusicCulture_EnemySideDoesNotDelegateCulture()
    {
        var culture = new BasicCultureObject { StringId = "mordor" };

        CustomBattleSideVM_OnCultureSelection_Patch.SignalMusicCulture(false, culture);

        _musicContext.DidNotReceive().SelectPlayerCulture(Arg.Any<string>());
    }

    [TestMethod]
    public void SignalMusicCulture_NullCultureDoesNotDelegateCulture()
    {
        CustomBattleSideVM_OnCultureSelection_Patch.SignalMusicCulture(true, null);

        _musicContext.DidNotReceive().SelectPlayerCulture(Arg.Any<string>());
    }
}
