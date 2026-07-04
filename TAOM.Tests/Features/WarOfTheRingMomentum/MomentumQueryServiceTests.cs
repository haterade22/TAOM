using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;
using TAOM.Features.WarOfTheRingMomentum.Models;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

[TestClass]
public class MomentumQueryServiceTests
{
    private MomentumStateStore _stateStore = null!;
    private IMomentumSettingsProvider _settings = null!;
    private MomentumQueryService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _stateStore = new MomentumStateStore(Substitute.For<IModLogger>());
        _settings = Substitute.For<IMomentumSettingsProvider>();
        _settings.VictoryThreshold.Returns(500);

        _sut = new MomentumQueryService(_stateStore, _settings);
    }

    [TestMethod]
    public void SliderValue_FreeAhead_IsNegative()
    {
        // +250 internal at threshold 500 → 50% victory progress → slider −50 (Free = left).
        _stateStore.State.Free.EditMomentum(250 * MomentumWarState.MomentumScale);

        Assert.AreEqual(-50, _sut.SliderValue);
    }

    [TestMethod]
    public void SliderValue_EvilAhead_IsPositive()
    {
        _stateStore.State.Evil.EditMomentum(250 * MomentumWarState.MomentumScale);

        Assert.AreEqual(50, _sut.SliderValue);
    }

    [TestMethod]
    public void SliderValue_BeyondThreshold_ClampsAt100()
    {
        // Player gate can hold the war open past the threshold.
        _stateStore.State.Evil.EditMomentum(800 * MomentumWarState.MomentumScale);

        Assert.AreEqual(100, _sut.SliderValue);
    }

    [TestMethod]
    public void MomentumChanged_ForwardsStoreEvent()
    {
        int fired = 0;
        _sut.MomentumChanged += () => fired++;

        _stateStore.NotifyMomentumChanged();

        Assert.AreEqual(1, fired);
    }

    [TestMethod]
    public void GetEvents_ReturnsSideEvents()
    {
        _stateStore.State.Evil.AddEvent(new MomentumEvent(200, "raid", MomentumActionType.VillageRaided, 100.0));

        var events = _sut.GetEvents(MomentumSide.Evil, MomentumActionType.VillageRaided);

        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(200, events[0].Value);
    }
}
