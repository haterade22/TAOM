using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Client.Features.Realms.Popups;

namespace TAOM.Tests.Features.Realms;

/// <summary>
/// RealmsTitleTreePopup.xml against its view models and the canvas layout (#1255). Every <c>@</c> binding must name a
/// property of the view model its scope resolves to: a nested <c>DataSource="{...}"</c> rescopes all bindings on its
/// element, which once made the pennant's <c>Brush.GlobalAlphaFactor="@ContentAlpha"</c> resolve against the banner
/// identifier and draw every banner empty. DataSourceProperty is an engine attribute.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TitleTreePrefabTests
{
    private static readonly string Gui = Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module", "GUI"));

    private static readonly string[] Lists = { "Cards", "Lines", "Liege", "Subordinates" };

    private static XDocument Popup() =>
        XDocument.Load(Path.Combine(Gui, "PreFabs", "Realms", "RealmsTitleTreePopup.xml"));

    private static bool IsBound(Type type, string property) =>
        type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
            ?.GetCustomAttribute<DataSourceProperty>() != null;

    // Lines is bound twice: the unlit bases, then the lit overlays above every base.
    private static XElement[] ScopesOf(XDocument popup, string list) =>
        popup.Descendants().Where(e => (string?)e.Attribute("DataSource") == "{" + list + "}").ToArray();

    private static XElement ListOf(XDocument popup, string list) => ScopesOf(popup, list).Single();

    private static XElement[] SelectedScopes(XDocument popup) =>
        popup.Descendants().Where(e => (string?)e.Attribute("DataSource") == "{Selected}").ToArray();

    private static XElement Template(XDocument popup, string list) => ListOf(popup, list).Element("ItemTemplate")!;

    private static XElement CardGroup(string kindProperty) =>
        Template(Popup(), "Cards").Descendants().Single(e => (string?)e.Attribute("IsVisible") == "@" + kindProperty);

    private static float Num(XElement e, string attribute) =>
        float.Parse((string)e.Attribute(attribute)!, CultureInfo.InvariantCulture);

    private static string[] BindingsOutside(XElement root, params XElement[] skipped) =>
        root.DescendantsAndSelf()
            .Where(e => !skipped.Any(s => s.DescendantsAndSelf().Contains(e)))
            .SelectMany(e => e.Attributes())
            .Where(a => a.Name.LocalName != "DataSource" && a.Value.StartsWith("@", StringComparison.Ordinal))
            .Select(a => a.Value.Substring(1))
            .ToArray();

    private static string?[] Commands(XElement root, params XElement[] skipped) =>
        root.DescendantsAndSelf()
            .Where(e => !skipped.Any(s => s.DescendantsAndSelf().Contains(e)))
            .SelectMany(e => e.Attributes().Where(a => a.Name.LocalName.StartsWith("Command.", StringComparison.Ordinal)))
            .Select(a => (string?)a.Value)
            .ToArray();

    [DataTestMethod]
    [DataRow("Cards", typeof(TitleCardVM))]
    [DataRow("Lines", typeof(TitleTreeLineVM))]
    [DataRow("Liege", typeof(TitleCardVM))]
    [DataRow("Subordinates", typeof(TitleCardVM))]
    public void Template_BindsOnlyItsViewModel_WithoutRescoping(string list, Type item)
    {
        var scopes = ScopesOf(Popup(), list);
        Assert.IsTrue(scopes.Length > 0, list);
        foreach (var template in scopes.Select(s => s.Element("ItemTemplate")!))
        {
            var elements = template.Descendants().ToList();

            var scoped = elements.Where(e => e.Attribute("DataSource") != null).Select(e => e.Name.LocalName).ToList();
            Assert.AreEqual(0, scoped.Count, $"{list}: nested DataSource rescopes the bindings on {string.Join(", ", scoped)}");

            var missing = BindingsOutside(template).Where(p => !IsBound(item, p)).ToList();
            Assert.AreEqual(0, missing.Count, $"{list}: not on {item.Name}: {string.Join(", ", missing)}");
            foreach (var command in Commands(template))
                Assert.IsNotNull(item.GetMethod(command!), $"{list}: {command} is not on {item.Name}");
        }
        Assert.IsTrue(IsBound(typeof(TitleTreePopupVM), list), list + " is a popup list");
    }

    [TestMethod]
    public void SelectedScopes_BindTheSelectedCard_AndItsLinkOpensThePage()
    {
        var scopes = SelectedScopes(Popup());

        Assert.IsTrue(scopes.Length > 0);
        Assert.IsTrue(IsBound(typeof(TitleTreePopupVM), nameof(TitleTreePopupVM.Selected)));
        foreach (var scope in scopes)
        {
            var missing = BindingsOutside(scope).Where(p => !IsBound(typeof(TitleCardVM), p)).ToList();
            Assert.AreEqual(0, missing.Count, "not on TitleCardVM: " + string.Join(", ", missing));
        }
        var commands = scopes.SelectMany(s => Commands(s)).ToList();
        CollectionAssert.AreEqual(new[] { nameof(TitleCardVM.ExecuteLink) }, commands,
            "the selected title's name, and only it, opens the encyclopedia page");
    }

    [TestMethod]
    public void PopupBindings_OutsideTheListsAndSelection_AreTitleTreePopupProperties()
    {
        var popup = Popup();
        var skipped = Lists.SelectMany(l => ScopesOf(popup, l)).Concat(SelectedScopes(popup)).ToArray();

        var missing = BindingsOutside(popup.Root!, skipped).Where(p => !IsBound(typeof(TitleTreePopupVM), p)).ToList();

        Assert.AreEqual(0, missing.Count, "not on TitleTreePopupVM: " + string.Join(", ", missing));
        foreach (var command in Commands(popup.Root!, skipped))
            Assert.IsNotNull(typeof(TitleTreePopupVM).GetMethod(command!), command);
    }

    [TestMethod]
    public void CardTemplate_PennantBindsTheCardsBannerAndFades()
    {
        var template = Template(Popup(), "Cards");

        var mask = template.Descendants("EncyclopediaMaskedImageWidget").Single();
        Assert.AreEqual("@" + nameof(TitleCardVM.BannerId), (string?)mask.Attribute("ImageId"));
        Assert.AreEqual("@" + nameof(TitleCardVM.BannerArgs), (string?)mask.Attribute("AdditionalArgs"));
        Assert.AreEqual("@" + nameof(TitleCardVM.BannerProvider), (string?)mask.Attribute("TextureProviderName"));
        var pennant = template.Descendants().Where(e => e.Name.LocalName == "EncyclopediaMaskedImageWidget"
            || ((string?)e.Attribute("Sprite"))?.StartsWith("ec_hero_clan_", StringComparison.Ordinal) == true).ToList();
        foreach (var layer in pennant)
            Assert.AreEqual("@ContentAlpha", (string?)layer.Attribute("Brush.GlobalAlphaFactor"), layer.ToString());
    }

    [TestMethod]
    public void CardTemplate_FadesTextThroughTheBrush()
    {
        var texts = Template(Popup(), "Cards").Descendants("TextWidget").Where(t => !IsTwin(t)).ToList();

        Assert.AreEqual(9, texts.Count, "title, holder and folded count for each of the three kinds");
        foreach (var text in texts)
        {
            Assert.IsNull(text.Attribute("AlphaFactor"), "AlphaFactor never reaches a TextWidget's text");
            Assert.AreEqual("@ContentAlpha", (string?)text.Attribute("Brush.GlobalAlphaFactor"), (string?)text.Attribute("Text"));
        }
    }

    // A measuring twin: a transparent copy of a wrapping text nested in it, 2 pixels narrower.
    private static bool IsTwin(XElement text) => text.Parent?.Parent?.Name.LocalName == "TextWidget";

    // The engine measures a text at its float width but draws it wrapped at the width truncated to whole pixels, so a
    // line within a pixel of the edge measures as one line short and the text below it in the stack covers that line.
    [DataTestMethod]
    [DataRow(nameof(TitleCardVM.IsRoot))]
    [DataRow(nameof(TitleCardVM.IsNode))]
    [DataRow(nameof(TitleCardVM.IsRow))]
    public void CardGroup_WrappingTextsMeasureEveryLineTheyDraw(string kind)
    {
        var stack = CardGroup(kind).DescendantsAndSelf("ListPanel").First().Element("Children")!.Elements("TextWidget").ToList();

        foreach (var text in stack.Where(t => (string?)t.Attribute("Text") != "@FoldedText"))
        {
            var twin = text.Element("Children")?.Elements("TextWidget").SingleOrDefault();
            string? label = (string?)text.Attribute("Text");
            Assert.IsNotNull(twin, kind + " " + label);
            Assert.AreEqual("Top", (string?)text.Attribute("Brush.TextVerticalAlignment"), label + ": a reserved line stays below the text");
            foreach (var attribute in new[] { "Text", "Brush", "Brush.FontSize" })
                Assert.AreEqual((string?)text.Attribute(attribute), (string?)twin!.Attribute(attribute), label + " " + attribute);
            Assert.AreEqual("StretchToParent", (string?)twin!.Attribute("WidthSizePolicy"), label);
            Assert.AreEqual("CoverChildren", (string?)twin.Attribute("HeightSizePolicy"), label);
            Assert.IsTrue(Num(twin, "MarginLeft") + Num(twin, "MarginRight") >= 2f, label + ": wraps no later than the drawn text");
            Assert.AreEqual("0", (string?)twin.Attribute("Brush.GlobalAlphaFactor"), label + ": never drawn");
            Assert.IsNull(twin.Attribute("IsVisible"), label + ": a hidden widget is not measured");
        }
    }

    [DataTestMethod]
    [DataRow(nameof(TitleCardVM.IsRoot), TitleTreeTextFit.RootTextTop, TitleTreeTextFit.RootTitleFont, TitleTreeTextFit.RootHolderFont, TitleTreeTextFit.RootSmallFont)]
    [DataRow(nameof(TitleCardVM.IsNode), TitleTreeTextFit.NodeTextTop, TitleTreeTextFit.NodeTitleFont, TitleTreeTextFit.NodeSmallFont, TitleTreeTextFit.NodeSmallFont)]
    [DataRow(nameof(TitleCardVM.IsRow), -1f, TitleTreeTextFit.RowTitleFont, TitleTreeTextFit.RowSmallFont, TitleTreeTextFit.RowSmallFont)]
    public void CardGroup_TextMatchesTheHeightEstimate(string kind, float top, float title, float holder, float folded)
    {
        var stack = CardGroup(kind).DescendantsAndSelf("ListPanel").First();
        var texts = stack.Element("Children")!.Elements("TextWidget").ToList();

        if (top >= 0f)
            Assert.AreEqual(top, Num(stack, "MarginTop"), kind);
        else
            Assert.AreEqual(TitleTreeTextFit.RowTextWidth, Num(stack, "SuggestedWidth"), kind);
        CollectionAssert.AreEqual(new[] { title, holder, folded }, texts.Select(t => Num(t, "Brush.FontSize")).ToArray(), kind);
    }

    [DataTestMethod]
    [DataRow(nameof(TitleCardVM.IsRoot))]
    [DataRow(nameof(TitleCardVM.IsNode))]
    [DataRow(nameof(TitleCardVM.IsRow))]
    public void CardGroup_StacksTitleHolderThenFoldedCount(string kind)
    {
        var stack = CardGroup(kind).DescendantsAndSelf("ListPanel").First();

        Assert.AreEqual("VerticalTopToBottom", (string?)stack.Attribute("StackLayout.LayoutMethod"), kind);
        var texts = stack.Element("Children")!.Elements("TextWidget").Select(t => (string?)t.Attribute("Text")).ToArray();
        CollectionAssert.AreEqual(new[] { "@TitleText", "@HolderText", "@FoldedText" }, texts, kind);
    }

    [TestMethod]
    public void CardTemplate_ClickSelectsTheTitleAndHints()
    {
        var card = Template(Popup(), "Cards").Elements().Single();

        Assert.AreEqual("ButtonWidget", card.Name.LocalName);
        Assert.AreEqual(nameof(TitleCardVM.ExecuteSelect), (string?)card.Attribute("Command.Click"));
        Assert.AreEqual("@" + nameof(TitleCardVM.IsSelected), (string?)card.Attribute("IsSelected"));
        Assert.AreEqual("true", (string?)card.Attribute("DoNotPassEventsToChildren"));
        var hint = card.Element("Children")!.Elements("HintWidget").Single();
        Assert.AreEqual(nameof(TitleCardVM.ExecuteBeginHint), (string?)hint.Attribute("Command.HoverBegin"));
        Assert.AreEqual(nameof(TitleCardVM.ExecuteEndHint), (string?)hint.Attribute("Command.HoverEnd"));
    }

    [TestMethod]
    public void ScrollPanel_ClipsToTheLayoutViewport_WithBothScrollbars_AndFollowsTheSelection()
    {
        var popup = Popup();
        var panel = popup.Descendants(nameof(RealmsTitleTreeScrollablePanel)).Single();

        Assert.AreEqual(TitleTreeCanvasLayout.ViewportWidth, Num(panel, "SuggestedWidth"), 0.01f);
        Assert.AreEqual(TitleTreeCanvasLayout.ViewportHeight, Num(panel, "SuggestedHeight"), 0.01f);
        Assert.AreEqual("@" + nameof(TitleTreePopupVM.SelectionVersion), (string?)panel.Attribute(nameof(RealmsTitleTreeScrollablePanel.SelectionVersion)));
        foreach (var bar in new[] { "HorizontalScrollbar", "VerticalScrollbar" })
        {
            var id = ((string)panel.Attribute(bar)!).Replace("..\\", string.Empty);
            Assert.AreEqual(1, popup.Descendants("ScrollbarWidget").Count(s => (string?)s.Attribute("Id") == id), bar);
        }
        var inner = popup.Descendants().Single(e => (string?)e.Attribute("Id") == "InnerPanel");
        Assert.AreEqual("@" + nameof(TitleTreePopupVM.CanvasWidth), (string?)inner.Attribute("SuggestedWidth"));
        Assert.AreEqual("@" + nameof(TitleTreePopupVM.CanvasHeight), (string?)inner.Attribute("SuggestedHeight"));
    }

    [TestMethod]
    public void Popup_IsACentredStoneModal_WithoutTheNativeFrame()
    {
        var popup = Popup();
        var text = popup.ToString();

        foreach (var native in new[] { "Frame1", "Standard.Background", "scroll_hide", "tabbar", "Popup.CloseButton" })
            Assert.IsFalse(text.Contains(native), native);
        var modal = popup.Descendants().Single(e => (string?)e.Attribute("Id") == "TitleTreeModal");
        Assert.AreEqual("Fixed", (string?)modal.Attribute("WidthSizePolicy"));
        Assert.AreEqual("Center", (string?)modal.Attribute("HorizontalAlignment"));
        Assert.AreEqual("Center", (string?)modal.Attribute("VerticalAlignment"));
        Assert.IsTrue(modal.Descendants("EncyclopediaStonePanelWidget").Any(s => (string?)s.Attribute("Sprite") == "ec_panel_9"));
    }
}
