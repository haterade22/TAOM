using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;
using TAOM.Features.FactionUI.UI.Widgets;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The layered banner editor's prefab (TAOMBannerEditor.xml) against TaomBannerEditorVM and its item view models, both
/// directions, by the FactionScreenPrefabBindingTests walk: a <c>DataSource</c> rescopes the element's own attributes
/// and its children, and an <c>ItemTemplate</c> binds to the list's item type. The <c>{Native}</c> scopes walk into the
/// vanilla BannerEditorVM, so a renamed vanilla member fails here as well. Also pins the CampaignPresentation case that
/// wraps the vanilla view model, without which the prefab binds the vanilla one and every TAOM binding is dead.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BannerEditorPrefabBindingTests
{
    private const string PrefabPath = "Main/_Module/GUI/PreFabs/FactionUI/TAOMBannerEditor.xml";

    private static readonly Type[] TaomVmTypes =
    {
        typeof(TaomBannerEditorVM), typeof(TaomBannerLayerVM), typeof(TaomBannerIconVM), typeof(TaomBannerColorVM),
    };

    private static Dictionary<Type, HashSet<string>> WalkPrefab(List<string> failures)
    {
        var root = XDocument.Parse(ReadSource(PrefabPath)).Root;
        Assert.IsNotNull(root, "the prefab has no root element");
        var bound = new Dictionary<Type, HashSet<string>>();
        Walk(root!, typeof(TaomBannerEditorVM), failures, bound);
        return bound;
    }

    [TestMethod]
    public void Prefab_EveryBinding_ExistsOnTheVmItBindsAgainst()
    {
        var failures = new List<string>();
        WalkPrefab(failures);

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void VmTypes_EveryDataSourceProperty_IsBoundOnItsOwnType()
    {
        var bound = WalkPrefab(new List<string>());
        var failures = new List<string>();

        foreach (var vmType in TaomVmTypes)
        {
            if (!bound.TryGetValue(vmType, out var names))
            {
                failures.Add($"{vmType.Name} is never bound by the prefab");
                continue;
            }
            foreach (var property in vmType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (IsDataSourceProperty(property) && !names.Contains(property.Name))
                    failures.Add($"{vmType.Name}.{property.Name} is [DataSourceProperty] but the prefab never binds it there");
            }
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void Prefab_KeepsTheVanillaCanvasAndNativeActions()
    {
        // The canvas is vanilla's BannerBuilderEditableAreaWidget: it finds its handles and the tableau by these ids and
        // this path, and does nothing (no drag, no rotate) when one is missing.
        var source = ReadSource(PrefabPath);
        foreach (var needed in new[]
                 {
                     "<MaskedBannerTableauWidget Id=\"BannerTableauWidget\"","BannerTableauWidget=\"..\\..\\BannerTableauWidget\"",
                     "Id=\"EditableAreaVisualWidget\"", "Id=\"DragWidgetRight\"", "Id=\"DragWidgetTop\"",
                     "Id=\"DragWidgetTopRight\"", "Id=\"RotateWidget\"", "Command.RefreshBanner=\"ExecuteUpdateBanner\"",
                     "Command.Click=\"ExecuteCancel\"", "Command.Click=\"ExecuteDone\"",
                     "ScopeID=\"BannerEditorCharacterNavigationScope\"",
                 })
            StringAssert.Contains(source, needed);
    }

    [TestMethod]
    public void Canvas_ClipsTheWholeCanvasToTheChosenRender()
    {
        // The tableau draws the whole 1528 canvas; a clipping viewport shows only the part one render shows, the way
        // bannerlord.party's inner box does. Both sizes follow the Shield / Pennant toggle.
        var root = XDocument.Parse(ReadSource(PrefabPath)).Root!;
        var tableau = root.Descendants("MaskedBannerTableauWidget").Single(e => (string?)e.Attribute("Id") == "BannerTableauWidget");
        var viewport = tableau.Parent!.Parent!;
        var frame = viewport.Parent!.Parent!;

        Assert.AreEqual("CanvasViewport", (string?)viewport.Attribute("Id"), "the tableau's parent is not the viewport");
        Assert.AreEqual("true", (string?)viewport.Attribute("ClipContents"), "the viewport does not clip the canvas");
        Assert.AreEqual("true", (string?)tableau.Attribute("IsNineGrid"), "the editable area widget maps the full canvas");
        Assert.AreEqual("@PreviewWidth", (string?)viewport.Attribute("SuggestedWidth"));
        Assert.AreEqual(BannerPreviewFrame.ViewHeight, (float)viewport.Attribute("SuggestedHeight")!);
        Assert.AreEqual("@PreviewCanvasSize", (string?)tableau.Attribute("SuggestedWidth"));
        Assert.AreEqual("@PreviewCanvasSize", (string?)tableau.Attribute("SuggestedHeight"));
        Assert.IsTrue((float)frame.Attribute("SuggestedWidth")! >= BannerPreviewFrame.ViewHeight, "the frame is narrower than the shield view");

        // The pennant frame is drawn the encyclopedia's way: the canvas through the hero clan mask, then its drape and rim
        // over exactly the viewport. They are the tableau's first children, so the editable area's handles draw on top,
        // and they take no events.
        Assert.AreEqual("TAOM.Encyclopedia.HeroClanMask", (string?)tableau.Attribute("Brush"));
        Assert.AreEqual("@IsPennantPreview", (string?)tableau.Attribute("IsMasked"));
        Assert.IsTrue(typeof(TaleWorlds.MountAndBlade.GauntletUI.Widgets.BannerTableauWidget).IsAssignableFrom(typeof(MaskedBannerTableauWidget)),
            "the editable area widget only drives a BannerTableauWidget");
        var children = tableau.Element("Children")!.Elements().ToList();
        CollectionAssert.AreEqual(new[] { "PennantDrape", "PennantRim", null },
            children.Select(e => (string?)e.Attribute("Id")).ToArray(), "drape, rim, then the editable area");
        Assert.AreEqual("BannerBuilderEditableAreaWidget", children[2].Name.LocalName);
        foreach (var (overlay, sprite) in new[] { (children[0], "ec_hero_clan_drape"), (children[1], "ec_hero_clan_rim") })
        {
            Assert.AreEqual(sprite, (string?)overlay.Attribute("Sprite"));
            Assert.AreEqual("@IsPennantPreview", (string?)overlay.Attribute("IsVisible"), sprite);
            Assert.AreEqual("true", (string?)overlay.Attribute("DoNotAcceptEvents"), sprite);
            Assert.AreEqual("@PreviewWidth", (string?)overlay.Attribute("SuggestedWidth"), sprite);
            Assert.AreEqual(BannerPreviewFrame.ViewHeight, (float)overlay.Attribute("SuggestedHeight")!, sprite);
        }

        // Hit tests only reach a widget inside its parent's area, so the toggle cannot sit inside the frame's margin.
        foreach (var command in new[] { "ExecuteShowShieldPreview", "ExecuteShowPennantPreview" })
        {
            var button = root.Descendants().Single(e => (string?)e.Attribute("Command.Click") == command);
            Assert.IsFalse(button.Ancestors().Contains(frame), $"{command} sits inside the canvas frame");
        }
    }

    [TestMethod]
    public void PreviewFrames_MatchTheRendersTheyShow()
    {
        // Shield: the default banner camera's middle 512 fills the square view.
        var shield = BannerPreviewFrame.Shield;
        Assert.AreEqual(BannerPreviewFrame.ViewHeight, shield.ViewWidth);
        Assert.AreEqual(512f, shield.VisibleCanvas.Width, 0.01f);
        Assert.AreEqual(512f, shield.VisibleCanvas.Height, 0.01f);

        // Pennant: what EncyclopediaMaskedImageWidget crops from the square nine grid texture at the pennant's zoom.
        var pennant = BannerPreviewFrame.Pennant;
        var cover = EncyclopediaMaskedImageWidget.CalculateOverlaySize(1f, 1f, pennant.ViewWidth, BannerPreviewFrame.ViewHeight, BannerPreviewFrame.PennantZoom);
        Assert.AreEqual(cover.Width, pennant.CanvasSize, 0.01f, "the tableau is not the masked widget's cover size");
        Assert.AreEqual(408f / 664f, pennant.ViewWidth / BannerPreviewFrame.ViewHeight, 0.001f, "the view is not the pennant's aspect");
        Assert.AreEqual(1528f * 408f / 664f / 1.35f, pennant.VisibleCanvas.Width, 0.5f);
        Assert.AreEqual(1528f / 1.35f, pennant.VisibleCanvas.Height, 0.5f);
    }

    [TestMethod]
    public void EncyclopediaPennants_UseThePreviewsZoom()
    {
        // The pennant preview copies the encyclopedia's zoom; a pennant drawn at another zoom would no longer match it.
        foreach (var page in new[] { "TAOMEncyclopediaHeroPage.xml", "TAOMEncyclopediaClanPage.xml", "TAOMEncyclopediaFactionPage.xml" })
        {
            var source = ReadSource("Main/_Module/GUI/PreFabs/FactionUI/" + page);
            foreach (Match tableau in Regex.Matches(source, "<EncyclopediaMaskedImageWidget[^>]*Brush=\"TAOM.Encyclopedia.HeroClanMask\"[^>]*>"))
                StringAssert.Contains(tableau.Value, "Zoom=\"1.35\"", page);
        }
    }

    [TestMethod]
    public void ToolStrip_RandomizesEveryLayer()
    {
        var source = ReadSource(PrefabPath);
        StringAssert.Contains(source, "Command.Click=\"ExecuteRandomizeAll\"");
        Assert.IsNotNull(typeof(TaomBannerEditorVM).GetMethod("ExecuteRandomizeAll", Type.EmptyTypes), "the VM has no ExecuteRandomizeAll");
    }

    [TestMethod]
    public void TransformSliders_KeepTheirHandleSize()
    {
        // A discrete SliderWidget otherwise widens its handle to at least 50 (UpdateHandleSize), stretching the diamond.
        var root = XDocument.Parse(ReadSource(PrefabPath)).Root!;
        var sliders = root.Descendants("SliderWidget").ToList();
        Assert.AreEqual(5, sliders.Count, "position x/y, width, height, rotation");
        foreach (var slider in sliders)
        {
            var value = (string?)slider.Attribute("ValueInt");
            Assert.AreEqual("true", (string?)slider.Attribute("DoNotUpdateHandleSize"), value);
            var handle = slider.Descendants().Single(e => (string?)e.Attribute("Id") == (string?)slider.Attribute("Handle"));
            Assert.AreEqual("CC.SliderHandle", (string?)handle.Attribute("Brush"), value);
            Assert.AreEqual(30f / 44f, (float)handle.Attribute("SuggestedWidth")! / (float)handle.Attribute("SuggestedHeight")!, 0.02f,
                $"{value}: the handle is not cc_slider_handle's 30x44 proportions");
        }
    }

    [TestMethod]
    public void CampaignPresentation_WrapsTheVanillaBannerEditor()
    {
        var source = ReadSource("Client/Features/FactionUI/Campaign/CampaignPresentation.cs", stripComments: true);
        StringAssert.Contains(source, "case \"TAOMBannerEditor\" when source is BannerEditorVM editor:");
        StringAssert.Contains(source, "return new TaomBannerEditorVM(editor);");
    }

    private static void Walk(XElement element, Type? scope, List<string> failures, Dictionary<Type, HashSet<string>> bound)
    {
        Type? itemType = null;
        var dataSource = element.Attribute("DataSource");
        if (dataSource != null && scope != null)
        {
            var match = Regex.Match(dataSource.Value, @"^\{(\w+)\}$");
            var property = match.Success ? Bind(scope, match.Groups[1].Value, failures, bound) : null;
            if (!match.Success)
                failures.Add($"unparseable DataSource '{dataSource.Value}' on <{element.Name}>");
            itemType = property == null ? null : ListItemType(property.PropertyType);
            scope = property == null || itemType != null ? null : property.PropertyType;
        }

        foreach (var attribute in element.Attributes())
        {
            var name = attribute.Name.LocalName;
            if (name == "DataSource" || scope == null)
                continue;
            if (name.StartsWith("Command.", StringComparison.Ordinal))
            {
                if (scope.GetMethod(attribute.Value, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) == null)
                    failures.Add($"{scope.Name} has no public parameterless '{attribute.Value}' for {name}");
            }
            else if (attribute.Value.StartsWith("@", StringComparison.Ordinal))
            {
                Bind(scope, attribute.Value.Substring(1), failures, bound);
            }
        }

        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName != "ItemTemplate")
            {
                Walk(child, scope, failures, bound);
                continue;
            }
            if (itemType == null)
                failures.Add($"<ItemTemplate> under <{element.Name}>, which binds no list");
            foreach (var template in child.Elements())
                Walk(template, itemType, failures, bound);
        }
    }

    private static PropertyInfo? Bind(Type scope, string propertyName, List<string> failures, Dictionary<Type, HashSet<string>> bound)
    {
        if (!bound.TryGetValue(scope, out var names))
            bound[scope] = names = new HashSet<string>(StringComparer.Ordinal);
        names.Add(propertyName);

        var property = scope.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null)
            failures.Add($"{scope.Name} has no public property '{propertyName}'");
        else if (!IsDataSourceProperty(property))
            failures.Add($"{scope.Name}.{propertyName} is bound but lacks [DataSourceProperty], so it never refreshes");
        return property;
    }

    private static Type? ListItemType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition().Name.StartsWith("MBBindingList", StringComparison.Ordinal)
            ? type.GetGenericArguments()[0]
            : null;

    private static bool IsDataSourceProperty(PropertyInfo property) =>
        property.GetCustomAttributes(true).Any(a => a.GetType().Name.StartsWith("DataSourceProperty", StringComparison.Ordinal));
}
