using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection;
using TAOM.Features.FactionUI.Campaign;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The heroes and troops list pages show the selected entry as a live character tableau in the right
/// preview frame. Both share one raised viewport inside the unchanged 330 by 590 portrait frame; the
/// hero tableau keeps its dead-hero desaturation, the troop tableau binds only what the base
/// CharacterViewModel offers, and the caption and Open entry button keep their place.
/// </summary>
[TestClass]
public class EncyclopediaItemListCharacterPreviewTests
{
    private const int FrameWidth = 330, FrameHeight = 590;

    private static XElement LoadPrefab()
    {
        var root = XDocument.Parse(ReadSource("Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopediaItemList.xml")).Root;
        Assert.IsNotNull(root, "the prefab has no root element");
        return root!;
    }

    private static XElement ById(XElement root, string id)
    {
        var element = root.Descendants().FirstOrDefault(x => (string?)x.Attribute("Id") == id);
        Assert.IsNotNull(element, $"no widget with Id '{id}'");
        return element!;
    }

    private static int Int(XElement element, string attribute, int fallback = 0)
    {
        var value = (string?)element.Attribute(attribute);
        return value == null ? fallback : int.Parse(value);
    }

    [DataTestMethod]
    [DataRow("heroes", true, true)]
    [DataRow("troops", true, true)]
    [DataRow("heroes", false, false)]
    [DataRow("troops", false, false)]
    [DataRow("kingdoms", true, false)]
    [DataRow("clans", true, false)]
    [DataRow("settlements", true, false)]
    [DataRow("concepts", true, false)]
    public void UsesCharacterPreview_CategoryAndSelection_OnlyHeroesAndTroopsWithASelection(string category, bool hasSelection, bool expected)
    {
        Assert.AreEqual(expected, EncyclopediaListScreenVM.UsesCharacterPreview(category, hasSelection));
        Assert.IsFalse(expected && EncyclopediaListScreenVM.UsesBannerPreview(category, hasSelection), "one preview per category");
    }

    [TestMethod]
    public void CharacterPreview_FillsTheIllustrationSlot_WithThePortraitFrame()
    {
        var root = LoadPrefab();
        var preview = ById(root, "CharacterPreview");
        var picture = ById(root, "EntryPicture");
        Assert.AreEqual(FrameWidth, Int(preview, "SuggestedWidth"));
        Assert.AreEqual(FrameHeight, Int(preview, "SuggestedHeight"));
        Assert.AreEqual(Int(picture, "SuggestedWidth"), Int(preview, "SuggestedWidth"));
        Assert.AreEqual(Int(picture, "SuggestedHeight"), Int(preview, "SuggestedHeight"));
        Assert.AreSame(picture.Parent, preview.Parent, "the character preview shares the illustration's slot");
        Assert.AreEqual("@ShowCharacterPreview", (string?)preview.Attribute("IsVisible"));
        Assert.AreEqual("@ShowIllustration", (string?)picture.Attribute("IsVisible"));
        Assert.AreEqual("ec_portrait_panel", (string?)preview.Element("Children")!.Elements().First().Attribute("Sprite"),
            "the portrait panel draws first, behind the live render");
    }

    [TestMethod]
    public void CharacterRenderClip_RaisedViewport_KeepsItsBottomInsideTheFrame()
    {
        var clip = ById(LoadPrefab(), "CharacterRenderClip");
        int width = Int(clip, "SuggestedWidth"), height = Int(clip, "SuggestedHeight"), top = Int(clip, "PositionYOffset");
        Assert.AreEqual(290, width);
        Assert.AreEqual(35, top);
        Assert.AreEqual(540, height);
        Assert.AreEqual(575, top + height, "the viewport bottom stays where the old 470-high viewport ended");
        Assert.IsTrue(width <= FrameWidth && top >= 0 && top + height <= FrameHeight, "the viewport stays inside the 330 by 590 frame");
        Assert.AreEqual("Center", (string?)clip.Attribute("HorizontalAlignment"));
        Assert.AreEqual("true", (string?)clip.Attribute("ClipContents"));
        Assert.AreEqual("CharacterPreview", (string?)clip.Parent!.Parent!.Attribute("Id"));
    }

    [TestMethod]
    public void CharacterRenderClip_HostsBothTableaux_StretchedUniformlyToTheViewport()
    {
        var clip = ById(LoadPrefab(), "CharacterRenderClip");
        var scale = ById(clip, "CharacterRenderScale");
        var hero = ById(clip, "HeroPortrait");
        var troop = ById(clip, "TroopPortrait");

        // One shared host, exactly 1.2 times the clip on both axes, centred and 15 px lower; the clip
        // trims what overflows, so the portrait bounds stay those of the clip.
        Assert.AreSame(clip, scale.Parent!.Parent, "the scale host sits directly in the clip");
        Assert.AreEqual(Int(clip, "SuggestedWidth") * 6, Int(scale, "SuggestedWidth") * 5, "width is 1.2 times the clip");
        Assert.AreEqual(Int(clip, "SuggestedHeight") * 6, Int(scale, "SuggestedHeight") * 5, "height is 1.2 times the clip");
        Assert.AreEqual(348, Int(scale, "SuggestedWidth"));
        Assert.AreEqual(648, Int(scale, "SuggestedHeight"));
        Assert.AreEqual("Center", (string?)scale.Attribute("HorizontalAlignment"));
        Assert.AreEqual("Center", (string?)scale.Attribute("VerticalAlignment"));
        Assert.AreEqual(15, Int(scale, "PositionYOffset"));
        Assert.IsNull(scale.Attribute("DataSource"), "visibility and tableau scopes stay at the root data source");

        foreach (var tableau in new[] { hero, troop })
        {
            // Every widget from the tableau up to the shared host fills its parent, so the render target
            // is the host's own 348 by 648, the clip's aspect; no axis is stretched alone.
            for (var widget = tableau; widget != scale; widget = widget.Parent!.Parent!)
            {
                Assert.AreEqual("StretchToParent", (string?)widget.Attribute("WidthSizePolicy"), $"{widget.Attribute("Id")} width");
                Assert.AreEqual("StretchToParent", (string?)widget.Attribute("HeightSizePolicy"), $"{widget.Attribute("Id")} height");
                Assert.IsNull(widget.Attribute("PositionYOffset"));
            }
            Assert.AreEqual("true", (string?)tableau.Attribute("DoNotUseCustomScale"));
            Assert.AreEqual("false", (string?)tableau.Attribute("IsBannerShownInBackground"));
            Assert.AreEqual("@IsTableauEnabled", (string?)tableau.Attribute("IsTableauEnabled"));
        }
        Assert.AreEqual("@ShowHeroRender", (string?)hero.Parent!.Parent!.Attribute("IsVisible"));
        Assert.AreEqual("@ShowTroopRender", (string?)troop.Parent!.Parent!.Attribute("IsVisible"));
    }

    [TestMethod]
    public void TroopPortrait_IsAPlainTableau_WithoutTheHeroOnlyDeathBinding()
    {
        var root = LoadPrefab();
        var hero = ById(root, "HeroPortrait");
        var troop = ById(root, "TroopPortrait");
        Assert.AreEqual("EncyclopediaCharacterTableauWidget", hero.Name.LocalName);
        Assert.AreEqual("{HeroCharacter}", (string?)hero.Attribute("DataSource"));
        Assert.AreEqual("@IsDead", (string?)hero.Attribute("IsDead"), "fallen heroes stay desaturated");
        Assert.AreEqual("CharacterTableauWidget", troop.Name.LocalName);
        Assert.AreEqual("{TroopCharacter}", (string?)troop.Attribute("DataSource"));
        Assert.IsNull(troop.Attribute("IsDead"));
        Assert.AreEqual("@EquipmentCode", (string?)troop.Attribute("EquipmentCode"));
        Assert.AreEqual("@Race", (string?)troop.Attribute("Race"));
    }

    [TestMethod]
    public void HeroSilhouette_KeepsItsAspect_InsideTheRaisedViewport()
    {
        var root = LoadPrefab();
        var clip = ById(root, "CharacterRenderClip");
        var silhouette = ById(root, "HeroSilhouette");
        int width = Int(silhouette, "SuggestedWidth"), height = Int(silhouette, "SuggestedHeight"), top = Int(silhouette, "PositionYOffset");
        int clipTop = Int(clip, "PositionYOffset");
        Assert.AreEqual(245.0 / 410.0, (double)width / height, 0.002, "scaled uniformly from the 245 by 410 outline");
        Assert.IsTrue(width <= Int(clip, "SuggestedWidth"), "no wider than the viewport");
        Assert.IsTrue(top >= clipTop && top + height <= clipTop + Int(clip, "SuggestedHeight"), "inside the viewport");
        Assert.IsTrue(height > 410, "the outline grows with the taller viewport");
        Assert.AreEqual("Center", (string?)silhouette.Attribute("HorizontalAlignment"));
        Assert.AreEqual("@ShowHeroSilhouette", (string?)silhouette.Attribute("IsVisible"));
        Assert.AreSame(clip.Parent, silhouette.Parent, "the outline replaces the render in the same frame, not inside its clip");
    }

    [TestMethod]
    public void CharacterPreview_EndsAboveTheCaption()
    {
        var root = LoadPrefab();
        var clip = ById(root, "CharacterRenderClip");
        var name = root.Descendants().First(x => (string?)x.Attribute("Text") == "@SelectedName");
        Assert.IsTrue(Int(clip, "PositionYOffset") + Int(clip, "SuggestedHeight") < Int(name, "PositionYOffset"));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CharacterPreview_EveryBinding_ResolvesOnTheViewModelOfItsScope()
    {
        var scopes = new Dictionary<string, Type>
        {
            ["HeroCharacter"] = typeof(HeroViewModel),
            ["TroopCharacter"] = typeof(CharacterViewModel),
        };
        var failures = new List<string>();
        Walk(ById(LoadPrefab(), "CharacterPreview"), typeof(EncyclopediaListScreenVM), scopes, failures);
        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TroopCharacter_IsTypedAsTheBaseCharacterViewModel()
    {
        // The troop scope is checked against CharacterViewModel above; the property must not claim more.
        var property = typeof(EncyclopediaListScreenVM).GetProperty("TroopCharacter", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(property);
        Assert.AreEqual(typeof(CharacterViewModel), property!.PropertyType);
        Assert.IsNull(typeof(CharacterViewModel).GetProperty("IsDead"), "the base view model has no IsDead to bind");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ListScreenVM_EveryDataSourceProperty_IsBoundInThePrefab()
    {
        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attribute in LoadPrefab().DescendantsAndSelf().Attributes())
        {
            string value = attribute.Value;
            if (value.StartsWith("@", StringComparison.Ordinal)) bound.Add(value.Substring(1));
            else if (value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal)) bound.Add(value.Trim('{', '}'));
        }
        var unbound = typeof(EncyclopediaListScreenVM)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(IsDataSourceProperty)
            .Select(p => p.Name)
            .Where(name => !bound.Contains(name))
            .ToArray();
        Assert.AreEqual(0, unbound.Length, "[DataSourceProperty] with no binding: " + string.Join(", ", unbound));
    }

    private static bool IsDataSourceProperty(PropertyInfo property) =>
        property.GetCustomAttributes(true).Any(a => a.GetType().Name.StartsWith("DataSourceProperty", StringComparison.Ordinal));

    private static void Walk(XElement element, Type scope, Dictionary<string, Type> scopes, List<string> failures)
    {
        var dataSource = (string?)element.Attribute("DataSource");
        if (dataSource != null)
        {
            string name = dataSource.Trim('{', '}');
            var property = scope.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !IsDataSourceProperty(property)) failures.Add($"{scope.Name} has no [DataSourceProperty] {name}");
            if (!scopes.TryGetValue(name, out var inner)) { failures.Add($"unexpected DataSource {dataSource}; teach this test its type"); return; }
            scope = inner;
        }
        foreach (var attribute in element.Attributes())
        {
            if (!attribute.Value.StartsWith("@", StringComparison.Ordinal)) continue;
            string name = attribute.Value.Substring(1);
            var property = scope.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null || !IsDataSourceProperty(property))
                failures.Add($"<{element.Name.LocalName} Id='{element.Attribute("Id")?.Value}'> {attribute.Name}='{attribute.Value}' names nothing on {scope.Name}");
        }
        foreach (var child in element.Elements()) Walk(child, scope, scopes, failures);
    }
}
