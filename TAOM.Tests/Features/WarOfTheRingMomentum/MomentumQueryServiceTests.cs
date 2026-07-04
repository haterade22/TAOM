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
    public void SliderValue_FreeAhead_IsPositive()
    {
        // Ratio balance, positive = Free. Free 25000 vs Evil 0 → fully Free → +100.
        _stateStore.State.Free.EditMomentum(250 * MomentumWarState.MomentumScale);

        Assert.AreEqual(100, _sut.SliderValue);
    }

    [TestMethod]
    public void SliderValue_EvilAhead_IsNegative()
    {
        _stateStore.State.Evil.EditMomentum(250 * MomentumWarState.MomentumScale);

        Assert.AreEqual(-100, _sut.SliderValue);
    }

    [TestMethod]
    public void SliderValue_FreeTwiceEvil_IsPositiveThird()
    {
        // Free 20000 vs Evil 10000 → (2−1)/(2+1) = +33 (Free moderately ahead).
        _stateStore.State.Free.EditMomentum(200 * MomentumWarState.MomentumScale);
        _stateStore.State.Evil.EditMomentum(100 * MomentumWarState.MomentumScale);

        Assert.AreEqual(33, _sut.SliderValue);
    }

    [TestMethod]
    public void SliderValue_HugeRunawayLead_StillMovesNotPinned()
    {
        // The bug: a long-war runaway lead used to clamp the bar to one end forever.
        // With the ratio, a close-but-large contest reads near center, never hard-pinned.
        _stateStore.State.Free.EditMomentum(1_000_000);
        _stateStore.State.Evil.EditMomentum(1_100_000);

        int v = _sut.SliderValue;
        Assert.IsTrue(v < 0 && v > -20, $"expected a small negative (Evil slightly ahead), got {v}");
    }

    [TestMethod]
    public void SliderValue_NoMomentum_IsZero()
    {
        Assert.AreEqual(0, _sut.SliderValue);
    }

    [TestMethod]
    public void SliderValue_BothSidesEqual_IsZero()
    {
        _stateStore.State.Free.EditMomentum(5000);
        _stateStore.State.Evil.EditMomentum(5000);

        Assert.AreEqual(0, _sut.SliderValue);
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
