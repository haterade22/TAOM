using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.TimeAcceleration.UI;

namespace TAOM.Tests.Features.TimeAcceleration;

/// <summary>
/// The Extra Fast Forward button's visibility on the themed map bar. A peer that defers to the co-op server writes no
/// multiplier, so the button is hidden there; the first bind must already be right, before any Tick reaches OnRefresh.
/// The view model is a bare engine object: the mixin only holds it.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TimeAccelerationMixinTests
{
    private MapTimeControlVM _vm = null!;
    private ITimeAccelerationService _service = null!;

    // The mixin holds its view model only through a WeakReference, so the test keeps the strong reference.
    private TimeAccelerationMixin Mixin(bool available)
    {
        _vm = (MapTimeControlVM)FormatterServices.GetUninitializedObject(typeof(MapTimeControlVM));
        _service = Substitute.For<ITimeAccelerationService>();
        _service.IsExtraFastForwardAvailable.Returns(available);
        return new TimeAccelerationMixin(_vm, _service);
    }

    [TestMethod]
    public void Constructor_Solo_ExtraFastForwardAvailable()
    {
        Assert.IsTrue(Mixin(available: true).IsExtraFastForwardAvailable);
    }

    [TestMethod]
    public void Constructor_CoopDefers_ExtraFastForwardHiddenOnTheFirstBind()
    {
        Assert.IsFalse(Mixin(available: false).IsExtraFastForwardAvailable);
    }

    [TestMethod]
    public void OnRefresh_SessionStartsDeferring_HidesExtraFastForward()
    {
        var mixin = Mixin(available: true);
        _service.IsExtraFastForwardAvailable.Returns(false);

        mixin.OnRefresh();

        Assert.IsFalse(mixin.IsExtraFastForwardAvailable);
    }

    [TestMethod]
    public void OnRefresh_SessionEnds_ShowsExtraFastForwardAgain()
    {
        var mixin = Mixin(available: false);
        _service.IsExtraFastForwardAvailable.Returns(true);

        mixin.OnRefresh();

        Assert.IsTrue(mixin.IsExtraFastForwardAvailable);
    }
}
