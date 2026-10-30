using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

/// <summary>The name-sort helper the rewrite calls: vanilla's result with no service, the toggle off or on.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ShadowfaxInventoryCallsTests
{
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private InventoryOpenFastPathService _service = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.InventoryFastOpen.Returns(true);
        _service = new InventoryOpenFastPathService(_settings, new FakeStampClock(), new RecordingLogger());
        ShadowfaxInventoryCalls.Initialize(_service);
    }

    [TestCleanup]
    public void Cleanup() => ShadowfaxInventoryCalls.Initialize(null);

    private static readonly Func<ItemRosterElement, string> NeverCalled = _ => throw new InvalidOperationException("key");

    [TestMethod]
    public void SortByNameOrPassThrough_On_ReturnsTheSourceUntouched()
    {
        var source = new List<ItemRosterElement> { default, default };

        var result = ShadowfaxInventoryCalls.SortByNameOrPassThrough(source, NeverCalled);

        Assert.AreSame(source, result);
    }

    private static List<ItemRosterElement> Rows(params int[] amounts) =>
        amounts.Select(a => new ItemRosterElement { Amount = a }).ToList();

    [TestMethod]
    public void SortByNameOrPassThrough_Off_SortsByTheKeyLikeVanilla()
    {
        _settings.InventoryFastOpen.Returns(false);
        var source = Rows(2, 3, 1);

        var result = ShadowfaxInventoryCalls.SortByNameOrPassThrough(source, e => "k" + e.Amount).ToArray();

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, result.Select(e => e.Amount).ToArray());
    }

    [TestMethod]
    public void SortByNameOrPassThrough_NoService_SortsLikeVanilla()
    {
        ShadowfaxInventoryCalls.Initialize(null);
        var source = Rows(2, 1);

        var result = ShadowfaxInventoryCalls.SortByNameOrPassThrough(source, e => "k" + e.Amount).ToArray();

        CollectionAssert.AreEqual(new[] { 1, 2 }, result.Select(e => e.Amount).ToArray());
    }
}
