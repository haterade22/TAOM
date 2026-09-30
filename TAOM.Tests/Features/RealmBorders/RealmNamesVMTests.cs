using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RealmBorders.Domain;
using TAOM.Features.RealmBorders.UI;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// The realm names layer's per-frame placement: names wait for the borders to be visible enough,
/// centre on their projected point, and hide behind the camera. ViewModel is an engine type.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class RealmNamesVMTests
{
    private static RealmNamesVM WithGondor()
    {
        var names = new RealmNamesVM();
        names.SetLabels(new[] { new RealmLabel("empire_w", new MapPoint(10f, 20f), 900) }, _ => "Gondor");
        return names;
    }

    [TestMethod]
    public void Place_BordersBelowTheThreshold_HidesTheNames()
    {
        var names = WithGondor();

        names.Place(RealmNamesVM.MinimumAlpha - 0.01f, 1920f, 1080f, _ => (true, 960f, 540f));

        Assert.IsFalse(names.Items.Single().IsShown);
    }

    [TestMethod]
    public void Place_OnScreen_CentresTheNameOnItsPoint()
    {
        var names = WithGondor();

        names.Place(1f, 1920f, 1080f, _ => (true, 1060f, 490f));

        var item = names.Items.Single();
        Assert.IsTrue(item.IsShown);
        Assert.AreEqual(100f, item.OffsetX);
        Assert.AreEqual(-50f, item.OffsetY);
        Assert.AreEqual(1f, item.Alpha);
        Assert.IsTrue(item.ShowTolkien, "Gondor is all aniron glyphs");
    }

    [TestMethod]
    public void Place_BehindTheCamera_HidesTheName()
    {
        var names = WithGondor();

        names.Place(1f, 1920f, 1080f, _ => (false, 0f, 0f));

        Assert.IsFalse(names.Items.Single().IsShown);
    }

    [TestMethod]
    public void SetLabels_SameList_KeepsTheItems()
    {
        var labels = new[] { new RealmLabel("empire_w", new MapPoint(10f, 20f), 900) };
        var names = new RealmNamesVM();
        names.SetLabels(labels, _ => "Gondor");
        var items = names.Items;

        names.SetLabels(labels, _ => "Gondor");

        Assert.AreSame(items, names.Items);
    }
}
