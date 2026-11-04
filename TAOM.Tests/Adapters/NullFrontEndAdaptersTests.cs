using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.CustomBattles;

namespace TAOM.Tests.Adapters;

/// <summary>ADR-013, B4a. The null adapters core keeps registered where TAOM.Client is absent (a dedicated server).</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class NullFrontEndAdaptersTests
{
    [TestMethod]
    public void NullFactionMapWidgetAdapter_ReportsNothingAndResetsNothing()
    {
        var adapter = new NullFactionMapWidgetAdapter();

        Assert.AreEqual("", adapter.HoveredFactionName);
        Assert.AreEqual("", adapter.LastClickedRegionName);
        adapter.ResetSession();
    }

    [TestMethod]
    public void NullFrontEndSpriteTableAdapter_RegistersNothing()
    {
        new NullFrontEndSpriteTableAdapter().RegisterSprite("x", new FrontEndTexture(new object(), 0), null);
    }

    [TestMethod]
    public void NullThumbnailCacheAdapter_ClearsNothing()
        => Assert.IsFalse(new NullThumbnailCacheAdapter().TryClearAll());

    [TestMethod]
    public void NullCustomBattleFactionSelectionFactory_KeepsTheVanillaSelector()
    {
        // Through reflection: the test project does not reference the CustomBattle assembly the return type lives in.
        var create = typeof(NullCustomBattleFactionSelectionFactory).GetMethod("Create")!;

        var created = create.Invoke(new NullCustomBattleFactionSelectionFactory(), new object[] { new Action<TaleWorlds.Core.BasicCultureObject>(_ => { }) });

        Assert.IsNull(created);
    }
}
