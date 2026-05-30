using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Mcm;

namespace TAOM.Tests.Features.Mcm;

/// <summary>
/// Tests for <see cref="McmLayoutRewriter"/> — the pure XML transform that flips MCM's reversed
/// options-screen ListPanels from VerticalBottomToTop to VerticalTopToBottom (v1.4.0 layout regression).
/// </summary>
[TestClass]
public class McmLayoutRewriterTests
{
    private static XmlDocument Doc(string xml)
    {
        var d = new XmlDocument();
        d.LoadXml(xml);
        return d;
    }

    // Mirrors MCM's embedded SettingsPropertyGroupView.xml: an outer container, a members list,
    // and a subgroups list — all VerticalBottomToTop — plus an inner HorizontalLeftToRight that
    // must be left alone.
    private const string GroupViewXml =
        "<Prefab><Window>" +
        "<ListPanel LayoutImp.LayoutMethod=\"VerticalBottomToTop\" IsVisible=\"@IsGroupVisible\">" +
        "<Children>" +
        "<ListPanel LayoutImp.LayoutMethod=\"HorizontalLeftToRight\" />" +
        "<ListPanel DataSource=\"{SettingProperties}\" LayoutImp.LayoutMethod=\"VerticalBottomToTop\" />" +
        "<ListPanel DataSource=\"{SettingPropertyGroups}\" LayoutImp.LayoutMethod=\"VerticalBottomToTop\" />" +
        "</Children>" +
        "</ListPanel>" +
        "</Window></Prefab>";

    [TestMethod]
    public void FlipMcmLayout_GroupView_FlipsAllThreeReversedListPanels()
    {
        var doc = Doc(GroupViewXml);

        int flipped = McmLayoutRewriter.FlipMcmLayout("SettingsPropertyGroupView", doc);

        Assert.AreEqual(3, flipped, "the 3 VerticalBottomToTop ListPanels should flip");
        var nodes = doc.SelectNodes("//ListPanel");
        foreach (XmlNode n in nodes)
        {
            var v = ((XmlElement)n).GetAttribute("LayoutImp.LayoutMethod");
            Assert.AreNotEqual("VerticalBottomToTop", v, "no VerticalBottomToTop should remain");
        }
    }

    [TestMethod]
    public void FlipMcmLayout_LeavesHorizontalAndAlreadyCorrectUntouched()
    {
        var doc = Doc(
            "<Prefab><Window>" +
            "<ListPanel Id=\"SettingPropertiesList\" LayoutImp.LayoutMethod=\"VerticalBottomToTop\">" +
            "<Children>" +
            "<ListPanel LayoutImp.LayoutMethod=\"HorizontalLeftToRight\" />" +
            "<ListPanel Id=\"ModList\" LayoutImp.LayoutMethod=\"VerticalTopToBottom\" />" +
            "</Children></ListPanel>" +
            "</Window></Prefab>");

        int flipped = McmLayoutRewriter.FlipMcmLayout("SettingsView", doc);

        Assert.AreEqual(1, flipped, "only the single VerticalBottomToTop node flips");
        Assert.AreEqual("HorizontalLeftToRight",
            ((XmlElement)doc.SelectSingleNode("//ListPanel[@LayoutImp.LayoutMethod='HorizontalLeftToRight']")).GetAttribute("LayoutImp.LayoutMethod"));
        Assert.AreEqual("VerticalTopToBottom",
            ((XmlElement)doc.SelectSingleNode("//ListPanel[@Id='ModList']")).GetAttribute("LayoutImp.LayoutMethod"));
    }

    [TestMethod]
    public void FlipMcmLayout_HandlesStackLayoutSpelling()
    {
        var doc = Doc("<Prefab><Window><ListPanel StackLayout.LayoutMethod=\"VerticalBottomToTop\" /></Window></Prefab>");

        int flipped = McmLayoutRewriter.FlipMcmLayout("SettingsView", doc);

        Assert.AreEqual(1, flipped);
        Assert.AreEqual("VerticalTopToBottom",
            ((XmlElement)doc.SelectSingleNode("//ListPanel")).GetAttribute("StackLayout.LayoutMethod"));
    }

    [TestMethod]
    public void FlipMcmLayout_NonMcmName_DoesNothing()
    {
        var doc = Doc("<Prefab><Window><ListPanel LayoutImp.LayoutMethod=\"VerticalBottomToTop\" /></Window></Prefab>");

        int flipped = McmLayoutRewriter.FlipMcmLayout("SomeOtherModPrefab", doc);

        Assert.AreEqual(0, flipped, "a non-MCM prefab must be untouched");
        Assert.AreEqual("VerticalBottomToTop",
            ((XmlElement)doc.SelectSingleNode("//ListPanel")).GetAttribute("LayoutImp.LayoutMethod"));
    }

    [TestMethod]
    public void FlipMcmLayout_NameWithXmlExtension_IsRecognized()
    {
        var doc = Doc("<Prefab><Window><ListPanel LayoutImp.LayoutMethod=\"VerticalBottomToTop\" /></Window></Prefab>");

        int flipped = McmLayoutRewriter.FlipMcmLayout("SettingsView.xml", doc);

        Assert.AreEqual(1, flipped, "MCM's lazy Create passes name+\".xml\" — must still match");
    }

    [TestMethod]
    public void FlipMcmLayout_NullDocument_ReturnsZeroAndDoesNotThrow()
    {
        Assert.AreEqual(0, McmLayoutRewriter.FlipMcmLayout("SettingsView", null));
    }

    [TestMethod]
    public void FlipMcmLayout_NullOrEmptyName_ReturnsZero()
    {
        var doc = Doc("<Prefab><Window><ListPanel LayoutImp.LayoutMethod=\"VerticalBottomToTop\" /></Window></Prefab>");

        Assert.AreEqual(0, McmLayoutRewriter.FlipMcmLayout(null, doc));
        Assert.AreEqual(0, McmLayoutRewriter.FlipMcmLayout("", doc));
    }

    [TestMethod]
    public void FlipMcmLayout_IsIdempotent()
    {
        var doc = Doc(GroupViewXml);

        int first = McmLayoutRewriter.FlipMcmLayout("SettingsPropertyGroupView", doc);
        int second = McmLayoutRewriter.FlipMcmLayout("SettingsPropertyGroupView", doc);

        Assert.AreEqual(3, first);
        Assert.AreEqual(0, second, "re-running flips nothing — already corrected");
    }

    [DataTestMethod]
    [DataRow("SettingsView", true)]
    [DataRow("SettingsPropertyGroupView", true)]
    [DataRow("ModOptionsView_MCM", true)]
    [DataRow("ModOptionsPageView", true)]
    [DataRow("settingsview", true)] // case-insensitive
    [DataRow("SettingsPropertyView", false)] // MCM prefab, but no reversed list — not in scope set
    [DataRow("SomeOtherPrefab", false)]
    [DataRow(null, false)]
    [DataRow("", false)]
    public void IsMcmLayoutPrefab_MatchesOnlyTheScopedSet(string name, bool expected)
    {
        Assert.AreEqual(expected, McmLayoutRewriter.IsMcmLayoutPrefab(name));
    }
}
