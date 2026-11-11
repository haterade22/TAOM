using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The layered banner editor's layer operations and history (TaomBannerEditorVM). Layer 0 is the background and
/// never moves or goes; every operation must leave the banner's cached code matching its layers, as the engine's
/// insert and remove calls do not clear it themselves.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BannerLayerEditingTests
{
    private static BannerData Layer(int mesh) =>
        new BannerData(mesh, 1, 2, new Vec2(483f, 483f), new Vec2(764f, 764f), false, false, 0f);

    private static Banner BannerWith(params int[] sigils)
    {
        var banner = new Banner();
        banner.AddIconData(new BannerData(11, 3, 4, new Vec2(1528f, 1528f), new Vec2(764f, 764f), false, false, 0f));
        foreach (var mesh in sigils)
            banner.AddIconData(Layer(mesh));
        return banner;
    }

    private static int[] Meshes(Banner banner) => banner.BannerDataList.Select(d => d.MeshId).ToArray();

    private static void AssertCodeIsCurrent(Banner banner) =>
        Assert.AreEqual(banner.Serialize(), banner.BannerCode, "the cached banner code is stale: the tableau would draw the old banner");

    [TestMethod]
    public void Insert_AfterTheSelectedLayer_AndRefreshesTheCode()
    {
        var banner = BannerWith(100, 101);
        _ = banner.BannerCode;

        Assert.AreEqual(2, BannerLayerEditing.Insert(banner, Layer(200), 1));
        CollectionAssert.AreEqual(new[] { 11, 100, 200, 101 }, Meshes(banner));
        AssertCodeIsCurrent(banner);
    }

    [TestMethod]
    public void Insert_AfterTheTopLayer_Appends_AndNeverGoesUnderTheBackground()
    {
        var banner = BannerWith(100);

        Assert.AreEqual(2, BannerLayerEditing.Insert(banner, Layer(200), 1));
        Assert.AreEqual(1, BannerLayerEditing.Insert(banner, Layer(300), -5));
        CollectionAssert.AreEqual(new[] { 11, 300, 100, 200 }, Meshes(banner));
    }

    [TestMethod]
    public void Insert_IntoAFullBanner_Refuses()
    {
        var banner = BannerWith(Enumerable.Range(100, Banner.MaxIconCount).ToArray());

        Assert.IsFalse(BannerLayerEditing.CanAdd(banner));
        Assert.AreEqual(-1, BannerLayerEditing.Insert(banner, Layer(999), 1));
        Assert.AreEqual(BannerLayerEditing.MaxLayers, banner.GetBannerDataListCount());
    }

    [TestMethod]
    public void Duplicate_CopiesTheLayerAboveItself_AsAnIndependentCopy()
    {
        var banner = BannerWith(100, 101);

        Assert.AreEqual(2, BannerLayerEditing.Duplicate(banner, 1));
        CollectionAssert.AreEqual(new[] { 11, 100, 100, 101 }, Meshes(banner));
        banner.BannerDataList[2].MeshId = 500;
        Assert.AreEqual(100, banner.BannerDataList[1].MeshId, "the duplicate shares its BannerData with the original");
        Assert.AreEqual(-1, BannerLayerEditing.Duplicate(banner, 0), "the background was duplicated");
    }

    [TestMethod]
    public void Remove_TakesASigil_ButNeverTheBackground()
    {
        var banner = BannerWith(100, 101);
        _ = banner.BannerCode;

        Assert.IsFalse(BannerLayerEditing.Remove(banner, 0));
        Assert.IsTrue(BannerLayerEditing.Remove(banner, 1));
        CollectionAssert.AreEqual(new[] { 11, 101 }, Meshes(banner));
        AssertCodeIsCurrent(banner);
    }

    [TestMethod]
    public void Move_SwapsNeighbours_AndStopsAtTheBackgroundAndTheTop()
    {
        var banner = BannerWith(100, 101, 102);
        _ = banner.BannerCode;

        Assert.AreEqual(2, BannerLayerEditing.Move(banner, 1, +1));
        CollectionAssert.AreEqual(new[] { 11, 101, 100, 102 }, Meshes(banner));
        AssertCodeIsCurrent(banner);

        Assert.AreEqual(1, BannerLayerEditing.Move(banner, 1, -1), "a sigil moved under the background");
        Assert.AreEqual(3, BannerLayerEditing.Move(banner, 3, +1), "the top sigil moved past the top");
        Assert.AreEqual(0, BannerLayerEditing.Move(banner, 0, +1), "the background moved");
        CollectionAssert.AreEqual(new[] { 11, 101, 100, 102 }, Meshes(banner));
    }

    [TestMethod]
    public void SwapCentreAndReset_EditTheLayer()
    {
        var data = new BannerData(100, 1, 2, new Vec2(90f, 40f), new Vec2(10f, 20f), false, false, 0.25f);

        BannerLayerEditing.SwapColors(data);
        BannerLayerEditing.CentreLayer(data);
        BannerLayerEditing.ResetSize(data, isPattern: false);

        Assert.AreEqual(2, data.ColorId);
        Assert.AreEqual(1, data.ColorId2);
        Assert.AreEqual(new Vec2(764f, 764f), data.Position);
        Assert.AreEqual(new Vec2(483f, 483f), data.Size);
        BannerLayerEditing.ResetSize(data, isPattern: true);
        Assert.AreEqual(new Vec2(1528f, 1528f), data.Size);
    }

    [TestMethod]
    public void ClampsAndDegrees_StayInTheSliderRanges()
    {
        Assert.AreEqual(0, BannerLayerEditing.ClampPosition(-4));
        Assert.AreEqual(1528, BannerLayerEditing.ClampPosition(2000));
        Assert.AreEqual(BannerLayerEditing.MinSize, BannerLayerEditing.ClampSize(0));
        Assert.AreEqual(1528, BannerLayerEditing.ClampSize(9999));

        Assert.AreEqual(90, BannerLayerEditing.ToDegrees(0.25f));
        Assert.AreEqual(270, BannerLayerEditing.ToDegrees(-0.25f));
        Assert.AreEqual(0, BannerLayerEditing.ToDegrees(1f));
        Assert.AreEqual(0.5f, BannerLayerEditing.ToTurns(180), 1e-6f);
        Assert.AreEqual(0.75f, BannerLayerEditing.ToTurns(-90), 1e-6f);
    }

    [TestMethod]
    public void History_UndoesRedoes_AndDropsTheRedoBranchOnANewEdit()
    {
        var history = new BannerHistory("a");

        Assert.IsFalse(history.Record("a"), "recording the current state added a step");
        Assert.IsTrue(history.Record("b"));
        Assert.IsTrue(history.Record("c"));
        Assert.AreEqual("b", history.Undo());
        Assert.AreEqual("a", history.Undo());
        Assert.IsNull(history.Undo());
        Assert.AreEqual("b", history.Redo());

        Assert.IsTrue(history.Record("d"));
        Assert.IsFalse(history.CanRedo, "an edit after undo kept the old redo branch");
        Assert.AreEqual("b", history.Undo());
    }

    [TestMethod]
    public void History_KeepsTheNewestStatesUpToItsLimit()
    {
        var history = new BannerHistory("0");
        for (int i = 1; i <= BannerHistory.Limit + 20; i++)
            history.Record(i.ToString());

        int undos = 0;
        while (history.Undo() != null)
            undos++;

        Assert.AreEqual(BannerHistory.Limit - 1, undos);
        Assert.AreEqual("21", history.Current);
    }
}
