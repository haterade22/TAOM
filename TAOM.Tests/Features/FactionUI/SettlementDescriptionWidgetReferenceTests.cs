using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.GauntletUI.BaseTypes;
using TAOM.Tests.Migration;
using BindingPath = TaleWorlds.Library.BindingPath;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The settlement page's full-description panel closes through a native
/// <c>EncyclopediaDividerButtonWidget</c> whose <c>ItemListWidget</c> must name the overlay that contains it.
/// Gauntlet resolves that attribute with <c>Widget.FindChild(new BindingPath(value))</c>
/// (<c>WidgetExtensions.SetWidgetAttributeFromStringAux</c> IL_0264 to IL_026f) and swallows a failure, so a
/// path that does not resolve leaves <c>ItemListWidget</c> null, and the button's <c>OnLateUpdate</c> then
/// dereferences it every frame: the native crash in taom_crash_20261106_172615_8800dc35. A bare <c>..</c>
/// fails because <c>FindChild(BindingPath)</c> passes its null <c>SubPath</c> to the parent (IL_000f to
/// IL_0027). These tests run the installed engine's own resolution on bare widgets, so no renderer or UI
/// context is needed: the <c>..</c> and <c>.</c> branches read only the parent link.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SettlementDescriptionWidgetReferenceTests
{
    private const string PagePath = "Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopediaSettlementPage.xml";

    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void CloseDescriptionButton_ItemListWidgetPath_ResolvesToContainingOverlay()
    {
        var page = XDocument.Parse(ReadSource(PagePath));
        var close = page.Descendants().SingleOrDefault(e => (string?)e.Attribute("Id") == "CloseDescriptionButton");
        Assert.IsNotNull(close, "the settlement page has no CloseDescriptionButton");
        var overlay = close!.Parent?.Parent;
        Assert.AreEqual("SettlementDescriptionOverlay", (string?)overlay?.Attribute("Id"),
            "CloseDescriptionButton must sit directly under SettlementDescriptionOverlay's Children");
        string path = (string?)close.Attribute("ItemListWidget") ?? "";

        var overlayWidget = BareWidget(parent: null);
        var closeWidget = BareWidget(parent: overlayWidget);

        Widget? target = closeWidget.FindChild(new BindingPath(path));

        Assert.AreSame(overlayWidget, target,
            $"ItemListWidget=\"{path}\" must resolve to the overlay, or the native button dereferences a null target every frame");
    }

    [TestMethod]
    public void FindChild_BareParentPath_ThrowsInInstalledEngine()
    {
        var parent = BareWidget(parent: null);
        var child = BareWidget(parent: parent);

        Assert.ThrowsException<NullReferenceException>(() => child.FindChild(new BindingPath("..")));
    }

    [TestMethod]
    public void FindChild_ParentThenSelfPath_ReturnsParent()
    {
        var parent = BareWidget(parent: null);
        var child = BareWidget(parent: parent);

        Assert.AreSame(parent, child.FindChild(new BindingPath("..\\.")));
    }

    // A Widget with only its parent link set: FindChild's ".." and "." branches read nothing else.
    private static Widget BareWidget(Widget? parent)
    {
        var widget = (Widget)FormatterServices.GetUninitializedObject(typeof(Widget));
        var parentField = typeof(Widget).GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(parentField, "the installed Widget has no _parent field");
        parentField!.SetValue(widget, parent);
        return widget;
    }
}
