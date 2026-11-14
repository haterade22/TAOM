using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Features.FactionUI.Campaign;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// The character page (layout D): an 1800 by 850 record on a flat near-black surround under the
/// encyclopedia header, the encyclopedia stone only inside the identity and content panels, identity on
/// the left, the outlined attribute strip, low skill tabs, the allocation row and round perk medallions on
/// one tier rail whose effects show in the native perk tooltip. Every investment, perk choice and close
/// action stays on the native VM.
/// </summary>
[TestClass]
public class CharacterScreenPrefabTests
{
    private const string PrefabPath = "Main/_Module/GUI/PreFabs/FactionUI/TAOMCharacterDeveloper.xml";
    private const string BrushPath = "Main/_Module/GUI/Brushes/TAOMCharacterDeveloper.xml";

    private static XElement LoadPrefab()
    {
        var root = XDocument.Parse(ReadSource(PrefabPath)).Root;
        Assert.IsNotNull(root, "the prefab has no root element");
        return root!;
    }

    private static XElement ById(XElement root, string id)
    {
        var element = root.Descendants().FirstOrDefault(x => (string?)x.Attribute("Id") == id);
        Assert.IsNotNull(element, $"no widget with Id '{id}'");
        return element!;
    }

    private static float Num(XElement element, string attribute, float fallback = 0f)
    {
        var value = (string?)element.Attribute(attribute);
        return value == null ? fallback : float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool Has(XElement root, string attribute, string value) =>
        root.DescendantsAndSelf().Any(x => (string?)x.Attribute(attribute) == value);

    [TestMethod]
    public void Root_IsTheNamedCharacterScreenWidget()
    {
        // CampaignPresentation finds the widget through FindChildrenWithType, which skips unnamed widgets.
        var widget = LoadPrefab().Element("Window")!.Elements().Single();
        Assert.AreEqual("CharacterScreenWidget", widget.Name.LocalName);
        Assert.AreEqual("CharacterScreen", (string?)widget.Attribute("Id"));
    }

    [TestMethod]
    public void Surround_IsFlatNearBlack_AndTheStoneDrawsOnlyInsideTheTwoPanels()
    {
        var root = LoadPrefab();
        Assert.IsFalse(root.DescendantsAndSelf().Attributes().Any(a => a.Value.Contains("cs_charcoal")), "no charcoal backdrop");
        Assert.IsFalse(root.Descendants().Any(x => x.Name.LocalName == "CharacterStoneBackdropWidget"), "no full-window stone");
        Assert.IsFalse(root.Descendants().Any(x => ((string?)x.Attribute("Sprite") ?? "").StartsWith("ec_stone", StringComparison.Ordinal)),
            "no unmasked stone image: stone draws only through a panel mask");

        var surround = ById(root, "Surround");
        Assert.AreSame(surround, ById(root, "CharacterScreen").Element("Children")!.Elements().First(), "the surround draws first");
        Assert.AreEqual("BlankWhiteSquare_9", (string?)surround.Attribute("Sprite"));
        Assert.AreEqual("StretchToParent", (string?)surround.Attribute("WidthSizePolicy"));
        Assert.AreEqual("StretchToParent", (string?)surround.Attribute("HeightSizePolicy"));
        string color = (string)surround.Attribute("Color")!;
        Assert.AreEqual(9, color.Length, "#RRGGBBAA");
        for (int i = 1; i < 7; i += 2)
            Assert.IsTrue(Convert.ToInt32(color.Substring(i, 2), 16) <= 0x18, $"{color} is near black");
        Assert.AreEqual("FF", color.Substring(7, 2), "opaque");

        var panels = root.Descendants().Where(x => x.Name.LocalName == "EncyclopediaStonePanelWidget").ToList();
        CollectionAssert.AreEquivalent(new[] { "TitleStone", "IdentityFrame", "ContentStone" }, panels.Select(p => (string?)p.Attribute("Id")).ToList());
        foreach (var panel in panels)
        {
            // The identity takes the encyclopedia hero page's arch rim (EncyclopediaStonePanelWidget pairs it
            // with ec_portrait_mask); the content keeps the shared panel rim and its mask; the title is the
            // encyclopedia's own header stone (ec_header through ec_header_mask).
            string id = (string)panel.Attribute("Id")!;
            string expected = id == "IdentityFrame" ? "ec_portrait_panel" : id == "TitleStone" ? "ec_header" : "ec_panel_9";
            Assert.AreEqual(expected, (string?)panel.Attribute("Sprite"), "the rim and its matching mask");
            Assert.AreEqual("true", (string?)panel.Attribute("DoNotAcceptEvents"));
            foreach (var filter in new[] { "Color", "ColorFactor", "AlphaFactor", "SaturationFactor", "Brush" })
                Assert.IsNull(panel.Attribute(filter), $"the stone keeps its original colour: no {filter}");
        }
        var content = ById(root, "ContentPanel").Element("Children")!.Elements().First();
        Assert.AreEqual("ContentStone", (string?)content.Attribute("Id"), "the content stone draws first, under the cards");
        Assert.AreEqual("StretchToParent", (string?)content.Attribute("WidthSizePolicy"));
        Assert.AreEqual("StretchToParent", (string?)content.Attribute("HeightSizePolicy"));
    }

    [DataTestMethod]
    [DataRow(1920f, 1080f, DisplayName = "1920 by 1080")]
    [DataRow(2560f, 1440f, DisplayName = "2560 by 1440")]
    [DataRow(5120f, 1440f, DisplayName = "5120 by 1440")]
    public void PanelStone_IsNeverEnlargedOnScreen(float screenWidth, float screenHeight)
    {
        // The record keeps its 1800 by 850 layout size, and Gauntlet scales layout units to the screen by
        // the smaller of width / 1920 and height / 1080. A panel stone pixel therefore covers
        // cover x scale screen pixels; above 1 the grain is enlarged.
        float screenScale = Math.Min(screenWidth / 1920f, screenHeight / 1080f);
        var root = LoadPrefab();
        var identity = ById(root, "IdentityFrame");
        var content = ById(root, "ContentPanel");
        // Layout D takes the content panel up to y 68 (696 high), which the approval accepted at a 1.046
        // enlargement of the grain; the identity arch stays at or below 1.
        foreach (var (id, width, height, limit) in new[]
        {
            ("identity", Num(identity, "SuggestedWidth"), Num(identity, "SuggestedHeight"), 1f),
            ("content", Num(content, "SuggestedWidth"), Num(content, "SuggestedHeight"), 1.05f),
        })
        {
            bool wide = EncyclopediaStonePanelWidget.UseWideStone(width, height, 640f, 1800f, 1774f, 887f);
            float cover = EncyclopediaStonePanelWidget.CoverScale(width, height, wide ? 1774f : 640f, wide ? 887f : 1800f);
            Assert.IsTrue(cover * screenScale <= limit, $"{id}: {cover * screenScale:0.000} screen pixels per stone pixel");
        }
    }

    [TestMethod]
    public void Record_IsTheApprovedSize_AndEveryFixedChildFitsInside()
    {
        var record = ById(LoadPrefab(), "CharacterRecord");
        Assert.AreEqual(1800f, Num(record, "SuggestedWidth"));
        Assert.AreEqual(850f, Num(record, "SuggestedHeight"));
        foreach (var child in record.Element("Children")!.Elements())
        {
            if ((string?)child.Attribute("WidthSizePolicy") == "StretchToParent") continue;
            float x = Num(child, "PositionXOffset"), y = Num(child, "PositionYOffset");
            Assert.IsTrue(x >= 0 && y >= 0 && x + Num(child, "SuggestedWidth") <= 1800f && y + Num(child, "SuggestedHeight") <= 850f,
                $"{child.Attribute("Id")?.Value ?? child.Name.LocalName} leaves the record");
        }
    }

    [TestMethod]
    public void Portrait_IsTheEncyclopediaArchTreatment_WithTheLiveTableauAndNoSquareFrame()
    {
        var root = LoadPrefab();
        var tableau = ById(root, "CharacterPortrait");
        Assert.AreEqual("CharacterTableauWidget", tableau.Name.LocalName);
        Assert.AreEqual("{HeroCharacter}", (string?)tableau.Attribute("DataSource"));
        foreach (var binding in new[] { "BannerCodeText", "BodyProperties", "CharStringId", "EquipmentCode", "IsFemale", "MountCreationKey", "StanceIndex", "ArmorColor1", "ArmorColor2", "Race" })
            Assert.AreEqual("@" + binding, (string?)tableau.Attribute(binding), binding);
        Assert.AreEqual("true", (string?)tableau.Attribute("DoNotUseCustomScale"));
        Assert.AreEqual("false", (string?)tableau.Attribute("IsEnabled"), "the portrait takes no input, as in the encyclopedia");

        var clip = ById(root, "PortraitClip");
        Assert.AreSame(clip, tableau.Parent!.Parent);
        Assert.AreEqual("true", (string?)clip.Attribute("ClipContents"));
        Assert.AreEqual("{CurrentCharacter}", (string?)clip.Attribute("DataSource"));
        // The encyclopedia hero page: a 460 by 421 tableau, bottom-centred in a 354 by 324 viewport.
        Assert.AreEqual(460f / 354f, Num(tableau, "SuggestedWidth") / Num(clip, "SuggestedWidth"), 0.01f, "the encyclopedia's horizontal enlargement");
        Assert.AreEqual(421f / 324f, Num(tableau, "SuggestedHeight") / Num(clip, "SuggestedHeight"), 0.01f, "the encyclopedia's vertical enlargement");
        Assert.AreEqual(354f / 324f, Num(clip, "SuggestedWidth") / Num(clip, "SuggestedHeight"), 0.01f, "the encyclopedia viewport's aspect");
        Assert.AreEqual("Center", (string?)tableau.Attribute("HorizontalAlignment"));
        Assert.AreEqual("Bottom", (string?)tableau.Attribute("VerticalAlignment"), "bottom-centred, as in the encyclopedia");
        Assert.IsNull(tableau.Attribute("PositionYOffset"));

        // The arch rim is the only frame: no square frame or panel rim over the portrait.
        Assert.IsFalse(root.Descendants().Any(x => (string?)x.Attribute("Id") == "PortraitFrame"), "no square inner frame");
        Assert.IsFalse(ById(root, "IdentityPanel").Descendants().Any(x => (string?)x.Attribute("Sprite") == "ec_panel_9"), "no panel rim inside the arch");
        var arch = ById(root, "IdentityFrame");
        Assert.AreEqual(1560f / 700f, Num(arch, "SuggestedHeight") / Num(arch, "SuggestedWidth"), 0.01f, "the arch at its source aspect");
        float archLeft = Num(arch, "PositionXOffset") - Num(ById(root, "IdentityPanel"), "PositionXOffset");
        Assert.IsTrue(Num(clip, "PositionXOffset") >= archLeft + 10f
            && Num(clip, "PositionXOffset") + Num(clip, "SuggestedWidth") <= archLeft + Num(arch, "SuggestedWidth") - 10f, "the viewport stays inside the rim");
    }

    private static (float X, float Y, float W, float H) Rect(XElement element) =>
        (Num(element, "PositionXOffset"), Num(element, "PositionYOffset"), Num(element, "SuggestedWidth"), Num(element, "SuggestedHeight"));

    private static bool Overlap((float X, float Y, float W, float H) a, (float X, float Y, float W, float H) b) =>
        a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;

    private static XElement BrushNamed(string name) =>
        XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").Single(b => (string?)b.Attribute("Name") == name);

    private static float BrushFontSize(string name) => Num(BrushNamed(name), "FontSize");

    private static float StyleAlpha(XElement brush, string style) =>
        Num(brush.Descendants("Style").Single(s => (string?)s.Attribute("Name") == style).Element("BrushLayer")!, "AlphaFactor", 1f);

    [TestMethod]
    public void SkillCards_DropTheIconRim_AndKeepNameLevelAndPipsApart()
    {
        var root = LoadPrefab();
        var card = ById(root, "SkillCards").Descendants("ItemTemplate").Single().Elements().Single();
        Assert.IsFalse(Has(card, "Sprite", "cs_skill_icon_rim"), "the native skill icon needs no second circular rim");
        var native = card.Descendants().Single(x => (string?)x.Attribute("DataSource") == "{Native}");
        Assert.AreEqual("true", (string?)native.Attribute("UpdateChildrenStates"), "the card's hover and selection reach the name");
        var icon = Rect(ById(native, "SkillIcon"));
        var nameElement = native.Elements("Children").Elements().Single(x => (string?)x.Attribute("Text") == "@NameText");
        var levelElement = native.Elements("Children").Elements().Single(x => (string?)x.Attribute("IntText") == "@Level");
        var name = Rect(nameElement);
        var level = Rect(levelElement);
        // D: icon on the left, the name above the focus pips, the level on the right, all vertically
        // inside the tab.
        Assert.IsTrue(icon.X + icon.W < name.X && name.X + name.W <= level.X, "icon, name, level from left to right");
        Assert.AreEqual(Num(card, "SuggestedHeight") / 2f, icon.Y + icon.H / 2f, 1f, "the icon is centred in the tab");
        var pipsElement = native.Elements("Children").Elements().Single(x => x.Name.LocalName == "CharacterFocusWidget");
        var pips = Rect(pipsElement);
        Assert.IsFalse(Overlap(icon, name) || Overlap(icon, pips) || Overlap(name, level) || Overlap(pips, level) || Overlap(name, pips));
        Assert.AreEqual(name.X, pips.X, "the pips sit beneath the name");
        Assert.IsTrue(pips.Y >= name.Y + name.H);
        foreach (var r in new[] { icon, name, level, pips })
            Assert.IsTrue(r.X >= 0 && r.Y >= 0 && r.X + r.W <= Num(card, "SuggestedWidth") && r.Y + r.H <= Num(card, "SuggestedHeight"), "inside the card");
        foreach (var pip in pipsElement.Element("Children")!.Elements())
        {
            Assert.AreEqual(16f / 28f, Num(pip, "SuggestedWidth") / Num(pip, "SuggestedHeight"), 0.01f, "a pip keeps the sprite's aspect");
            Assert.IsTrue(Num(pip, "PositionXOffset") + Num(pip, "SuggestedWidth") <= pips.W && Num(pip, "SuggestedHeight") <= pips.H);
        }
    }

    [TestMethod]
    public void Allocation_IsFourAlignedGroups_WithoutCoinsOrDuplicatePips()
    {
        var root = LoadPrefab();
        Assert.IsFalse(Has(root, "Sprite", "CharacterDeveloper\\attribute_point_icon") || Has(root, "Sprite", "CharacterDeveloper\\cp_icon"), "no point coins");
        Assert.AreEqual(1, root.Descendants().Count(x => x.Name.LocalName == "CharacterFocusWidget"), "focus pips only on the skill card");
        var row = ById(root, "AllocationRow");
        var children = row.Element("Children")!.Elements().ToList();
        // D: three short separators draw first, between the four groups and under nothing that hovers.
        var separators = children.Where(x => Num(x, "SuggestedWidth") == 1f).ToList();
        Assert.AreEqual(3, separators.Count);
        foreach (var separator in separators)
        {
            Assert.AreEqual("true", (string?)separator.Attribute("DoNotAcceptEvents"), "a separator steals no hover");
            Assert.IsTrue(children.IndexOf(separator) < 3, "the separators draw under the groups");
        }
        CollectionAssert.AreEqual(new[] { 315f, 630f, 945f }, separators.Select(s => Num(s, "PositionXOffset")).ToList(), "one between each pair of groups");
        var groups = children.Except(separators).ToList();
        Assert.AreEqual(4, groups.Count);
        for (int i = 0; i < 4; i++)
        {
            Assert.AreEqual(Num(groups[0], "SuggestedWidth"), Num(groups[i], "SuggestedWidth"), "equal columns");
            Assert.AreEqual(Num(row, "SuggestedHeight"), Num(groups[i], "SuggestedHeight"), "one baseline");
            Assert.IsNull(groups[i].Attribute("PositionYOffset"));
            Assert.AreEqual(i * Num(groups[0], "SuggestedWidth"), Num(groups[i], "PositionXOffset"), "contiguous, no overlap");
            Assert.IsNull(groups[i].Attribute("DoNotAcceptEvents"), "the hints inside stay hoverable");
        }
        Assert.IsTrue(4 * Num(groups[0], "SuggestedWidth") <= Num(row, "SuggestedWidth"));
        Assert.IsTrue(Has(groups[0], "Command.Click", "ExecuteAddAttributePoint") && Has(groups[0], "IsEnabled", "@CanAddPoint"));
        Assert.IsTrue(Has(groups[1], "LocalizedText", "{=taom_char_attribute_points}Attribute Points") && Has(groups[1], "IntText", "@UnspentAttributePoints"));
        Assert.IsTrue(Has(groups[1], "DataSource", "{UnspentAttributePointsHint}"));
        Assert.IsTrue(Has(groups[2], "Text", "@FocusPointsText") && Has(groups[2], "IntText", "@UnspentCharacterPoints") && Has(groups[2], "DataSource", "{UnspentCharacterPointsHint}"));
        Assert.IsTrue(Has(groups[2], "Command.Click", "ExecuteAddFocus") && Has(groups[2], "IsEnabled", "@CanAddFocus"));
        var rate = groups[3].Descendants().Single(x => (string?)x.Attribute("Text") == "@CurrentLearningRateText");
        Assert.IsTrue(Has(groups[3], "DataSource", "{LearningRateTooltip}"));
        // "Learning Rate: x 1.25" at 17 px Cinzel is about 230 px: the whole column keeps it on one line.
        Assert.AreEqual("StretchToParent", (string?)rate.Attribute("WidthSizePolicy"));
        Assert.IsNull(rate.Attribute("Brush.FontSize"), "the label size, not a shrunk 15 px");
    }

    [TestMethod]
    public void IdentityStack_ProgressThenTraitsThenPartyRoleThenCareer_InsideTheArch()
    {
        // Mockup A: the traits grid under the portrait and the level progress, the party role beneath it,
        // Career below that, all inside the arch rim and never overlapping.
        var root = LoadPrefab();
        var panel = ById(root, "IdentityPanel");
        var arch = ById(root, "IdentityFrame");
        float archLeft = Num(arch, "PositionXOffset") - Num(panel, "PositionXOffset");
        float archRight = archLeft + Num(arch, "SuggestedWidth");
        var progress = Rect(panel.Descendants().Single(x => x.Name.LocalName == "CharacterProgressWidget"));
        var traits = Rect(ById(root, "TraitsArea"));
        var role = Rect(ById(root, "PartyRoleArea"));
        var career = Rect(ById(root, "CareerButton"));
        Assert.AreSame(panel, ById(root, "TraitsArea").Parent!.Parent);
        Assert.AreSame(panel, ById(root, "PartyRoleArea").Parent!.Parent);
        Assert.IsTrue(traits.Y >= progress.Y + progress.H + 4f, "the traits start below the level progress");
        Assert.IsTrue(role.Y >= traits.Y + traits.H + 4f, "the party role sits beneath the traits");
        Assert.IsTrue(career.Y >= role.Y + role.H + 4f, "Career sits clear beneath the party role");
        foreach (var (name, r) in new[] { ("traits", traits), ("party role", role) })
            Assert.IsTrue(r.X >= archLeft + 20f && r.X + r.W <= archRight - 20f, $"the {name} stay inside the rim");
        // Career sizes to its medallion group and is centred in the identity panel, whose centre is the arch's.
        var careerButton = ById(root, "CareerButton");
        Assert.AreSame(panel, careerButton.Parent!.Parent);
        Assert.AreEqual("CoverChildren", (string?)careerButton.Attribute("WidthSizePolicy"));
        Assert.AreEqual("Center", (string?)careerButton.Attribute("HorizontalAlignment"));
        Assert.AreEqual(0f, career.X, "no horizontal offset from the centre");
        Assert.AreEqual(archLeft + Num(arch, "SuggestedWidth") / 2f, Num(panel, "SuggestedWidth") / 2f, 0.5f, "Career centred in the arch");
        Assert.IsTrue(career.Y + career.H <= Num(arch, "SuggestedHeight") - 16f, "above the arch's bottom rim");
        foreach (var area in new[] { ById(root, "TraitsArea"), ById(root, "PartyRoleArea") })
            foreach (var child in area.Element("Children")!.Elements())
            {
                var c = Rect(child);
                Assert.IsTrue(c.X >= 0 && c.Y >= 0 && c.X + c.W <= Num(area, "SuggestedWidth") && c.Y + c.H <= Num(area, "SuggestedHeight"),
                    $"{child.Attribute("Id")?.Value ?? child.Name.LocalName} stays inside {area.Attribute("Id")!.Value}");
            }
    }

    [TestMethod]
    public void TraitsPopup_TheButtonAndTheToggle_AreGone()
    {
        string prefab = ReadSource(PrefabPath);
        foreach (var gone in new[] { "TraitsPopup", "TraitsButton", "TraitsCloseButton", "ShowTraits", "ExecuteToggleTraits" })
            Assert.IsFalse(prefab.Contains(gone), $"the prefab still names {gone}");
        string vm = ReadSource("Client/Features/FactionUI/Campaign/CharacterScreenVM.cs");
        foreach (var gone in new[] { "ShowTraits", "_showTraits", "ExecuteToggleTraits" })
            Assert.IsFalse(vm.Contains(gone), $"the VM still holds {gone}");
        Assert.AreEqual(1, LoadPrefab().Descendants().Count(x => x.Name.LocalName == "EncyclopediaHeroTraitVisualWidget"), "one trait icon template, inline");
    }

    [TestMethod]
    public void InlineTraits_AreATwoColumnGrid_OfNativeIconsNamesAndValues()
    {
        var root = LoadPrefab();
        var grid = ById(root, "TraitGrid");
        Assert.AreEqual("GridWidget", grid.Name.LocalName);
        Assert.AreEqual("{Traits}", (string?)grid.Attribute("DataSource"), "the screen's trait items, rebuilt per member");
        Assert.AreEqual(2f, Num(grid, "ColumnCount"), "two columns, as in mockup A");
        float cellWidth = Num(grid, "DefaultCellWidth"), cellHeight = Num(grid, "DefaultCellHeight");
        Assert.IsTrue(2f * cellWidth <= Num(grid, "SuggestedWidth"), "both columns fit the grid");
        var cell = grid.Element("ItemTemplate")!.Elements().Single();
        Assert.AreEqual(cellWidth, Num(cell, "SuggestedWidth"));
        Assert.AreEqual(cellHeight, Num(cell, "SuggestedHeight"));

        var native = cell.Element("Children")!.Elements().Single(x => (string?)x.Attribute("DataSource") == "{Native}");
        var icon = native.Descendants().Single(x => x.Name.LocalName == "EncyclopediaHeroTraitVisualWidget");
        Assert.AreEqual("@TraitId", (string?)icon.Attribute("TraitId"), "the native icon picks its sprite from the native trait id");
        Assert.AreEqual("@Value", (string?)icon.Attribute("TraitValue"));
        var name = cell.Descendants().Single(x => (string?)x.Attribute("Text") == "@NameText");
        // A long translated name wraps; its own clip box keeps the wrap out of the next row's cell.
        var nameBox = name.Parent!.Parent!;
        Assert.AreEqual("TraitNameClip", (string?)nameBox.Attribute("Id"));
        Assert.AreEqual("true", (string?)nameBox.Attribute("ClipContents"));
        Assert.AreEqual("StretchToParent", (string?)name.Attribute("WidthSizePolicy"), "the name fills its column");
        Assert.AreEqual("StretchToParent", (string?)name.Attribute("HeightSizePolicy"));
        name = nameBox;
        var positive = ById(cell, "TraitValuePositive");
        var negative = ById(cell, "TraitValueNegative");
        Assert.AreEqual("@ValueText", (string?)positive.Attribute("Text"));
        Assert.AreEqual("@ValueText", (string?)negative.Attribute("Text"));
        Assert.AreEqual("@IsNegative", (string?)positive.Attribute("IsHidden"), "one value shows at a time");
        Assert.AreEqual("@IsNegative", (string?)negative.Attribute("IsVisible"));
        Assert.AreNotEqual((string?)positive.Attribute("Brush"), (string?)negative.Attribute("Brush"), "a negative trait reads differently from a positive one");
        var iconRect = Rect(icon);
        var cellChildren = new[] { Rect(name), Rect(positive) };
        Assert.IsFalse(Overlap(iconRect, Rect(name)) || Overlap(Rect(name), Rect(positive)), "icon, name and value never overlap");
        foreach (var r in cellChildren.Append(iconRect))
            Assert.IsTrue(r.X >= 0 && r.Y >= 0 && r.X + r.W <= cellWidth && r.Y + r.H <= cellHeight, "inside the cell");
        Assert.AreEqual(Rect(positive), Rect(negative), "both values in one place");
    }

    [TestMethod]
    public void InlineTraits_ManyOrLongTraitsScroll_InsideTheirBoundedArea()
    {
        var root = LoadPrefab();
        var grid = ById(root, "TraitGrid");
        var scroll = grid.Ancestors().First(x => x.Name.LocalName == "ScrollablePanel");
        Assert.AreEqual("TraitScroll", (string?)scroll.Attribute("Id"));
        Assert.AreEqual("CoverChildren", (string?)grid.Attribute("HeightSizePolicy"), "the grid grows; the panel scrolls");
        Assert.IsNotNull((string?)scroll.Attribute("VerticalScrollbar"));
        Assert.AreEqual("true", (string?)scroll.Attribute("AutoHideScrollBars"), "the four personality traits need no bar");
        var clip = ById(scroll, (string)scroll.Attribute("ClipRect")!);
        Assert.AreEqual("true", (string?)clip.Attribute("ClipContents"), "a long name or a fifth trait never draws past the area");
        Assert.AreEqual(2f * Num(grid, "DefaultCellHeight"), Num(scroll, "SuggestedHeight"), "two rows show: the four native personality traits");
        var bar = ById(root, "TraitScrollbar");
        Assert.IsFalse(Overlap(Rect(scroll), Rect(bar)), "the bar sits beside the grid");
    }

    [TestMethod]
    public void InlineTraits_HoverShowsTheNativeHint()
    {
        var cell = ById(LoadPrefab(), "TraitGrid").Element("ItemTemplate")!.Elements().Single();
        var hint = cell.Descendants().Single(x => x.Name.LocalName == "HintWidget");
        Assert.AreEqual("{Hint}", (string?)hint.Attribute("DataSource"), "the native trait effect tooltip");
        Assert.AreEqual("ExecuteBeginHint", (string?)hint.Attribute("Command.HoverBegin"));
        Assert.AreEqual("ExecuteEndHint", (string?)hint.Attribute("Command.HoverEnd"));
        var parent = hint.Parent!.Parent!;
        Assert.AreEqual("{Native}", (string?)parent.Attribute("DataSource"));
        Assert.AreEqual("StretchToParent", (string?)parent.Attribute("WidthSizePolicy"), "the whole cell hovers, inside the cell");
        Assert.AreSame(parent, cell.Element("Children")!.Elements().Last(), "the hover area draws over the texts");
        foreach (var x in hint.AncestorsAndSelf().TakeWhile(x => x != cell.Parent))
        {
            Assert.AreNotEqual("true", (string?)x.Attribute("DoNotAcceptEvents"), $"{x.Name.LocalName} must take the hover");
            Assert.AreNotEqual("false", (string?)x.Attribute("IsEnabled"));
        }
        foreach (var text in cell.Descendants().Where(x => x.Name.LocalName == "TextWidget"))
            Assert.AreEqual("true", (string?)text.Attribute("DoNotAcceptEvents"), "the texts never steal the hover");
    }

    [TestMethod]
    public void InlineTraits_NoTraits_ShowsTheLocalizedEmptyLine()
    {
        var root = LoadPrefab();
        var empty = ById(root, "NoTraitsText");
        Assert.AreEqual("FrontEndTextWidget", empty.Name.LocalName);
        Assert.AreEqual("{=taom_char_no_traits}No notable traits", (string?)empty.Attribute("LocalizedText"));
        Assert.AreEqual("@HasNoTraits", (string?)empty.Attribute("IsVisible"));
        Assert.AreSame(ById(root, "TraitsArea"), empty.Parent!.Parent);
        var heading = ById(root, "TraitsHeading");
        Assert.AreEqual("{Native}", (string?)heading.Attribute("DataSource"));
        Assert.AreEqual("@TraitsText", (string?)heading.Attribute("Text"), "the native Traits label");
    }

    [TestMethod]
    public void PartyRole_ShowsTheNativeLabelAndRoles_OnlyWhenTheMemberHasOne()
    {
        var root = LoadPrefab();
        var area = ById(root, "PartyRoleArea");
        Assert.AreEqual("@HasPartyRole", (string?)area.Attribute("IsVisible"));
        var heading = area.Descendants().Single(x => (string?)x.Attribute("Text") == "@PartyRoleText");
        Assert.AreEqual("{Native}", (string?)heading.Attribute("DataSource"));
        var stats = area.Descendants().Single(x => (string?)x.Attribute("DataSource") == "{CharacterStats}");
        Assert.AreEqual("{CurrentCharacter}", (string?)stats.Ancestors().First(x => x.Attribute("DataSource") != null).Attribute("DataSource"),
            "the current member's native role list");
        Assert.IsNotNull(stats.Descendants().FirstOrDefault(x => (string?)x.Attribute("Text") == "@Definition"));
    }

    [TestMethod]
    public void TraitValue_IsSignedAndFlagsNegative()
    {
        Assert.AreEqual("+2", CharacterTraitItemVM.SignedValue(2));
        Assert.AreEqual("+1", CharacterTraitItemVM.SignedValue(1));
        Assert.AreEqual("-1", CharacterTraitItemVM.SignedValue(-1));
        Assert.AreEqual("-2", CharacterTraitItemVM.SignedValue(-2));
        var negative = new CharacterTraitItemVM(new FakeTrait("Mercy", -1), "Cruel");
        Assert.IsTrue(negative.IsNegative);
        Assert.AreEqual("-1", negative.ValueText);
        Assert.AreEqual("Cruel", negative.NameText, "the native level name, as given");
        var positive = new CharacterTraitItemVM(new FakeTrait("Honor", 2), "Honorable");
        Assert.IsFalse(positive.IsNegative);
        Assert.AreEqual("+2", positive.ValueText);
    }

    [TestMethod]
    public void RebuildTraits_MemberChange_ReplacesEveryItemWithTheNewMembers()
    {
        var list = new MBBindingList<CharacterTraitItemVM>();
        var first = new ViewModel[] { new FakeTrait("Honor", 1), new FakeTrait("Valor", 2) };
        CharacterScreenVM.RebuildTraits(list, first, t => "name of " + CharacterScreenVM.Read<string>(t, "TraitId"));
        CollectionAssert.AreEqual(first, list.Select(i => i.Native).ToList());
        Assert.AreEqual("name of Valor", list[1].NameText);

        var second = new ViewModel[] { new FakeTrait("Mercy", -2) };
        CharacterScreenVM.RebuildTraits(list, second, t => "x");
        CollectionAssert.AreEqual(second, list.Select(i => i.Native).ToList(), "no trait of the previous member stays");

        CharacterScreenVM.RebuildTraits(list, new ViewModel[0], t => "x");
        Assert.AreEqual(0, list.Count, "a member with no traits shows none");

        var many = Enumerable.Range(0, 9).Select(i => (ViewModel)new FakeTrait("T" + i, i % 2 == 0 ? 1 : -1)).ToArray();
        CharacterScreenVM.RebuildTraits(list, many, t => "x");
        CollectionAssert.AreEqual(many, list.Select(i => i.Native).ToList(), "every trait, in the native order");
    }

    [TestMethod]
    public void Synchronize_RebuildsTraitsAndRoleFromTheCurrentMember_WithTheNativeLevelName()
    {
        string vm = ReadSource("Client/Features/FactionUI/Campaign/CharacterScreenVM.cs");
        int block = vm.IndexOf("if (!ReferenceEquals(character, CurrentCharacter))", StringComparison.Ordinal);
        int end = vm.IndexOf("var skill = Read<ViewModel>(character, \"CurrentSkill\");", block, StringComparison.Ordinal);
        Assert.IsTrue(block > 0 && end > block);
        string memberChange = vm.Substring(block, end - block);
        StringAssert.Contains(memberChange, "RebuildTraits(Traits, Items(character, \"Traits\")");
        StringAssert.Contains(memberChange, "Items(character, \"CharacterStats\")");
        StringAssert.Contains(vm, "CampaignUIHelper.GetTraitNameText(");
        StringAssert.Contains(vm, "CampaignUIHelper.GetHeroTraits()");
        Assert.IsFalse(vm.Contains("Hero.MainHero"), "no main-hero shortcut");
    }

    private sealed class FakeTrait : ViewModel
    {
        public FakeTrait(string id, int value) { TraitId = id; Value = value; }
        [DataSourceProperty] public string TraitId { get; }
        [DataSourceProperty] public int Value { get; }
    }

    [DataTestMethod]
    [DataRow("CancelButton")]
    [DataRow("ResetButton")]
    [DataRow("DoneButton")]
    public void Buttons_AreTheThinOrnamentButton_AtItsSourceAspect(string id)
    {
        var button = ById(LoadPrefab(), id);
        Assert.AreEqual("TAOM.Character.Action", (string?)button.Attribute("Brush"));
        Assert.AreEqual(176f / 1280f, Num(button, "SuggestedHeight") / Num(button, "SuggestedWidth"), 0.005f, "fs_confirm is never stretched");
    }

    [TestMethod]
    public void CareerButton_IsTheMedallionLink_NotTheOrnamentButton()
    {
        // The approved Career link: a 48 rim around the 28 leadership icon, the gold label and a 12 by 16
        // chevron, 14 px apart in one row, 48 high from y 632. The button draws no box of its own.
        var root = LoadPrefab();
        var career = ById(root, "CareerButton");
        Assert.IsNull(career.Attribute("Brush"), "no fs_confirm ornament behind the link");
        Assert.AreEqual("632", (string?)career.Attribute("PositionYOffset"));
        Assert.AreEqual("48", (string?)career.Attribute("SuggestedHeight"));
        Assert.AreEqual("true", (string?)career.Attribute("UpdateChildrenStates"), "hover reaches the row");
        var row = career.Element("Children")!.Elements().Single();
        Assert.AreEqual("ListPanel", row.Name.LocalName);
        Assert.AreEqual("true", (string?)row.Attribute("UpdateChildrenStates"), "hover reaches the rim, label and chevron");
        var parts = row.Element("Children")!.Elements().ToList();
        CollectionAssert.AreEqual(new[] { "CareerRim", "CareerLabel", "CareerChevron" }, parts.Select(p => (string?)p.Attribute("Id")).ToList());
        var rim = parts[0];
        Assert.AreEqual("TAOM.Character.CareerRim", (string?)rim.Attribute("Brush"));
        Assert.AreEqual((48f, 48f), (Num(rim, "SuggestedWidth"), Num(rim, "SuggestedHeight")));
        var icon = rim.Element("Children")!.Elements().Single();
        Assert.AreEqual("CareerIcon", (string?)icon.Attribute("Id"));
        Assert.AreEqual(@"SPGeneral\Skills\gui_skills_icon_leadership", (string?)icon.Attribute("Sprite"));
        Assert.AreEqual((28f, 28f), (Num(icon, "SuggestedWidth"), Num(icon, "SuggestedHeight")));
        var label = parts[1];
        Assert.AreEqual("TAOM.Character.CareerLink", (string?)label.Attribute("Brush"));
        Assert.AreEqual("{=taom_career}Career", (string?)label.Attribute("LocalizedText"));
        Assert.AreEqual(14f, Num(label, "MarginLeft"));
        var chevron = parts[2];
        Assert.AreEqual("TAOM.Character.Next", (string?)chevron.Attribute("Brush"));
        Assert.AreEqual((12f, 16f), (Num(chevron, "SuggestedWidth"), Num(chevron, "SuggestedHeight")));
        Assert.AreEqual(14f, Num(chevron, "MarginLeft"));
    }

    [TestMethod]
    public void ActionBrush_DrawsFsConfirm_AndTheOldButtonBrushesAreGone()
    {
        var brushes = XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").ToList();
        var action = brushes.Single(b => (string?)b.Attribute("Name") == "TAOM.Character.Action");
        CollectionAssert.AreEqual(new[] { "fs_confirm" }, action.Descendants("BrushLayer").Select(l => (string?)l.Attribute("Sprite")).Where(s => s != null).ToList());
        foreach (var state in new[] { "Hovered", "Pressed", "Disabled" })
            Assert.IsTrue(action.Descendants("Style").Any(s => (string?)s.Attribute("Name") == state), $"a {state} look");
        foreach (var old in new[] { "TAOM.Character.Back", "TAOM.Character.Confirm" })
            Assert.IsFalse(brushes.Any(b => (string?)b.Attribute("Name") == old), $"{old} is gone");
        string text = ReadSource(BrushPath) + ReadSource(PrefabPath);
        Assert.IsFalse(text.Contains("cc_btn_back") || text.Contains("cc_btn_next"), "the character page draws no character-creation button");
    }

    [TestMethod]
    public void Footer_ButtonsShareOneSizeAndBaseline()
    {
        var root = LoadPrefab();
        var buttons = new[] { "CancelButton", "ResetButton", "DoneButton" }.Select(id => Rect(ById(root, id))).ToList();
        foreach (var b in buttons)
        {
            Assert.AreEqual(buttons[0].Y, b.Y);
            Assert.AreEqual(buttons[0].W, b.W);
            Assert.IsTrue(b.Y >= 764f + 16f && b.Y + b.H <= 850f, "below both panels, inside the record");
        }
        // D: the footer shares the header's centre, the visible union 79 to 1780, with Cancel and Done under
        // its two ends.
        Assert.AreEqual(929.5f, buttons[1].X + buttons[1].W / 2f, 0.5f, "reset under the title");
        Assert.AreEqual(79f, buttons[0].X, "cancel under the identity arch's left edge");
        Assert.AreEqual(1780f, buttons[2].X + buttons[2].W, 0.5f, "done under the content panel's right edge");
        Assert.AreEqual(929.5f, ((buttons[0].X + buttons[0].W / 2f) + (buttons[2].X + buttons[2].W / 2f)) / 2f, 0.5f, "cancel and done mirror each other about 929.5");
    }

    [DataTestMethod]
    [DataRow("CancelButton")]
    [DataRow("ResetButton")]
    [DataRow("DoneButton")]
    public void ButtonLabels_AreCentredOnTheFsConfirmFace_NotTheTexture(string id)
    {
        // fs_confirm.png is 1280 by 176; its opaque face (alpha 200 and up) spans x 299 to 982 and y 31 to
        // 121, so the face centre sits 12 source pixels above the texture centre (the pendant hangs below).
        var button = ById(LoadPrefab(), id);
        var label = button.Element("Children")!.Elements().Single();
        Assert.AreEqual("TAOM.Character.ButtonText", (string?)label.Attribute("Brush"));
        float w = Num(button, "SuggestedWidth"), h = Num(button, "SuggestedHeight"), scale = w / 1280f;
        float left = Num(label, "MarginLeft"), right = w - Num(label, "MarginRight");
        float top = Num(label, "MarginTop"), bottom = h - Num(label, "MarginBottom");
        Assert.IsTrue(left >= 299f * scale && right <= 982f * scale, $"{id}: the label stays on the face");
        Assert.AreEqual(Num(label, "MarginLeft"), Num(label, "MarginRight"), 1f, $"{id}: centred across the face");
        Assert.AreEqual((31f + 121f) / 2f * scale, (top + bottom) / 2f, 1f, $"{id}: centred on the face, not the texture");
        float fontSize = Num(label, "Brush.FontSize", BrushFontSize("TAOM.Character.ButtonText"));
        Assert.IsTrue(bottom - top >= 86f * fontSize / 64f - 0.5f, $"{id}: the box holds one Cinzel line");
        Assert.AreEqual("FS_CinzelWide", (string?)BrushNamed("TAOM.Character.ButtonText").Attribute("Font"));
    }

    [TestMethod]
    public void SkillCards_UseTheNativeSkillIconWidget()
    {
        var root = LoadPrefab();
        var cards = ById(root, "SkillCards");
        Assert.AreEqual("{Skills}", (string?)cards.Attribute("DataSource"));
        var icon = cards.Descendants().Single(x => x.Name.LocalName == "SkillIconVisualWidget");
        Assert.AreEqual("@SkillId", (string?)icon.Attribute("SkillId"));
        Assert.AreEqual("true", (string?)icon.Attribute("UseSmallVariation"));
        Assert.AreEqual("{Native}", (string?)icon.Parent!.Parent!.Attribute("DataSource"), "SkillId is the native SkillVM's");
        // Skill art comes only from the native widget; the one named skill sprite is the Career link's leadership icon.
        var named = root.DescendantsAndSelf().Attributes().Where(a => a.Value.Contains("gui_skills_icon_") || a.Value == "@IconSprite").ToList();
        Assert.IsTrue(named.All(a => a.Name.LocalName == "Sprite" && a.Value == @"SPGeneral\Skills\gui_skills_icon_leadership"
            && (string?)a.Parent!.Attribute("Id") == "CareerIcon"), "skill art outside CareerIcon: " + string.Join(", ", named.Select(a => a.Value)));
        Assert.IsNotNull(cards.Descendants().FirstOrDefault(x => (string?)x.Attribute("IntText") == "@Level"));
        Assert.IsNotNull(cards.Descendants().FirstOrDefault(x => x.Name.LocalName == "CharacterFocusWidget" && (string?)x.Attribute("Focus") == "@CurrentFocusLevel"));
    }

    [TestMethod]
    public void AttributeTabs_ShowNameAndNativeValueInlineOnOneCentredRow()
    {
        var tabs = ById(LoadPrefab(), "AttributeTabs");
        Assert.AreEqual("{Tabs}", (string?)tabs.Attribute("DataSource"));
        Assert.IsTrue(Has(tabs, "Command.Click", "ExecuteSelect"));
        Assert.IsTrue(Has(tabs, "IsSelected", "@IsSelected"));
        var name = tabs.Descendants().Single(x => (string?)x.Attribute("Text") == "@NameText");
        var value = tabs.Descendants().Single(x => (string?)x.Attribute("IntText") == "@AttributeValue");
        Assert.AreEqual("{Native}", (string?)value.Attribute("DataSource"));
        // B: the name and its value read as one line, centred in the tab, not stacked.
        var row = name.Parent!.Parent!;
        Assert.AreSame(row, value.Parent!.Parent, "the name and the value share one row");
        Assert.AreEqual("ListPanel", row.Name.LocalName);
        Assert.AreEqual("HorizontalLeftToRight", (string?)row.Attribute("StackLayout.LayoutMethod"));
        Assert.AreEqual("Center", (string?)row.Attribute("HorizontalAlignment"));
        Assert.AreEqual("Center", (string?)row.Attribute("VerticalAlignment"));
        Assert.AreEqual("true", (string?)row.Attribute("UpdateChildrenStates"), "the selected tab colours its label");
        foreach (var text in new[] { name, value }) Assert.IsNull(text.Attribute("PositionYOffset"), "no stacked offset");
    }

    [TestMethod]
    public void AttributeStrip_IsOneUnderstatedOutline_WithSeparatorsAndADiamondOnTheSelection()
    {
        var root = LoadPrefab();
        Assert.IsFalse(root.Descendants().Any(x => (string?)x.Attribute("Id") == "TabFrame"), "no stretched header behind the tabs");
        var tabs = ById(root, "AttributeTabs");
        var rim = ById(root, "AttributeStripRim");
        Assert.AreEqual("BrushWidget", rim.Name.LocalName);
        Assert.AreEqual("TAOM.Character.StripRim", (string?)rim.Attribute("Brush"));
        Assert.AreEqual("true", (string?)rim.Attribute("DoNotAcceptEvents"));
        Assert.AreEqual(Rect(tabs), Rect(rim), "the outline frames the whole strip");
        Assert.IsTrue(rim.ElementsBeforeSelf().Contains(ById(root, "ContentStone")) && tabs.ElementsBeforeSelf().Contains(rim), "the outline draws over the stone and under the tabs");
        var strip = BrushNamed("TAOM.Character.StripRim");
        CollectionAssert.AreEqual(new[] { "ec_panel_9" }, strip.Descendants("BrushLayer").Select(l => (string?)l.Attribute("Sprite")).Where(s => s != null).Distinct().ToList());
        Assert.IsTrue(StyleAlpha(strip, "Default") < 1f, "an understated outline, not the full panel rim");

        var item = tabs.Element("ItemTemplate")!.Elements().Single();
        Assert.IsNull(item.Attribute("Brush"), "no rectangular hover or selected tint");
        Assert.IsNull(item.Attribute("Sprite"));
        Assert.IsFalse(Has(tabs, "Sprite", "ec_header"));
        Assert.AreEqual(Num(tabs, "SuggestedWidth"), Num(item, "SuggestedWidth") * 6f, 1f, "six equal slots: evenly spaced labels");
        var mark = ById(item, "AttributeSelectedMark");
        Assert.AreEqual("fs_diamond", (string?)mark.Attribute("Sprite"));
        Assert.AreEqual("@IsSelected", (string?)mark.Attribute("IsVisible"), "the small diamond marks the selected attribute");
        Assert.IsTrue(Num(mark, "SuggestedWidth") <= 14f, "a small diamond");
        Assert.AreEqual("true", (string?)item.Attribute("UpdateChildrenStates"), "the selection reaches the label");

        var content = ById(root, "ContentPanel").Element("Children")!.Elements().ToList();
        var separators = content.Where(x => Num(x, "SuggestedWidth") == 1f && Num(x, "PositionYOffset") > Num(tabs, "PositionYOffset")
            && Num(x, "PositionYOffset") + Num(x, "SuggestedHeight") < Num(tabs, "PositionYOffset") + Num(tabs, "SuggestedHeight")).ToList();
        CollectionAssert.AreEqual(Enumerable.Range(1, 5).Select(i => Num(tabs, "PositionXOffset") + i * Num(item, "SuggestedWidth")).ToList(),
            separators.Select(s => Num(s, "PositionXOffset")).ToList(), "five short separators between the six cells");
        foreach (var gone in new[] { "TAOM.Character.Tab", "TAOM.Character.HoverLine" })
            Assert.IsFalse(XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").Any(b => (string?)b.Attribute("Name") == gone), $"{gone} is gone");
    }

    [TestMethod]
    public void Composition_KeepsTheApprovedDAnchors()
    {
        var root = LoadPrefab();
        foreach (var id in new[] { "IdentityFrame", "IdentityPanel", "ContentPanel" })
            Assert.AreEqual(68f, Num(ById(root, id), "PositionYOffset"), $"{id} starts at y 68");
        Assert.AreEqual(696f, Num(ById(root, "IdentityFrame"), "SuggestedHeight"), "identity ends at 764");
        var content = ById(root, "ContentPanel");
        Assert.AreEqual(696f, Num(content, "SuggestedHeight"), "content ends at 764");
        var tabs = ById(root, "AttributeTabs");
        Assert.AreSame(content, tabs.Parent!.Parent, "D: the attribute strip sits on the stone");
        Assert.AreEqual(18f, Num(tabs, "PositionYOffset"));
        Assert.IsTrue(Num(tabs, "PositionYOffset") + Num(tabs, "SuggestedHeight") + 12f <= Num(ById(root, "SkillCards"), "PositionYOffset"), "a clear gap between the strip and the skill tabs");
        foreach (var id in new[] { "CancelButton", "ResetButton", "DoneButton" })
            Assert.AreEqual(784f, Num(ById(root, id), "PositionYOffset"), $"{id} sits on the footer");
        // Mockup A: the name and level under the arch's crown, then the portrait.
        var level = Rect(ById(root, "MemberLevel"));
        Assert.IsTrue(Rect(ById(root, "MemberName")).Y + Rect(ById(root, "MemberName")).H <= level.Y + 1f);
        Assert.IsTrue(level.Y + level.H <= Num(ById(root, "PortraitClip"), "PositionYOffset"), "the portrait starts below the name and level");
    }

    [TestMethod]
    public void ContentChildren_FitInsideThe696Panel()
    {
        var root = LoadPrefab();
        var content = ById(root, "ContentPanel");
        foreach (var child in content.Element("Children")!.Elements())
        {
            if ((string?)child.Attribute("WidthSizePolicy") == "StretchToParent") continue;
            float x = Num(child, "PositionXOffset"), y = Num(child, "PositionYOffset");
            Assert.IsTrue(x >= 0 && y >= 0 && x + Num(child, "SuggestedWidth") <= 1316f && y + Num(child, "SuggestedHeight") <= 696f,
                $"{child.Attribute("Id")?.Value ?? child.Name.LocalName} leaves the content panel");
        }
        var scroll = ById(root, "PerkScroll");
        var perkContent = ById(root, "PerkContent");
        Assert.IsTrue(Num(perkContent, "SuggestedHeight") <= Num(scroll, "SuggestedHeight"), "perks scroll sideways only");
        var button = perkContent.Descendants().Single(x => x.Name.LocalName == "CharacterPerkButtonWidget");
        Assert.IsTrue(CharacterPerkItemVM.RowY(2) + Num(button, "PositionYOffset") + Num(button, "SuggestedHeight") <= Num(perkContent, "SuggestedHeight"),
            "the second choice's medallion and name are not clipped");
    }

    [TestMethod]
    public void Hairlines_FrameTheAllocationRow_AtItsFullWidth()
    {
        var root = LoadPrefab();
        Assert.IsFalse(Has(root, "Sprite", "fs_divider_wide"), "D draws hairlines, not the wide ornament");
        var row = ById(root, "AllocationRow");
        var lines = ById(root, "ContentPanel").Element("Children")!.Elements().Where(x => (string?)x.Attribute("Sprite") == "fs_divider_thin").ToList();
        CollectionAssert.AreEquivalent(new[] { Num(row, "PositionYOffset"), Num(row, "PositionYOffset") + Num(row, "SuggestedHeight") },
            lines.Select(l => Num(l, "PositionYOffset")).ToList(), "one hairline above the row and one below");
        foreach (var line in lines)
        {
            Assert.AreEqual(Num(row, "PositionXOffset"), Num(line, "PositionXOffset"));
            Assert.AreEqual(Num(row, "SuggestedWidth"), Num(line, "SuggestedWidth"));
            Assert.AreEqual(1f, Num(line, "SuggestedHeight"));
            Assert.AreEqual("true", (string?)line.Attribute("DoNotAcceptEvents"));
        }
    }

    [TestMethod]
    public void TitleHeader_IsTheEncyclopediaHeaderComposition_WithTheLocalizedCharacterLabel()
    {
        // The encyclopedia header (ec_header on an EncyclopediaStonePanelWidget, 60 high, the centred 378 by 48
        // TAOM.Encyclopedia.Title). D spans the visible union of the two panels, x 79 to 1780, so the title
        // sits on 929.5; a 220 by 2 fs_divider_thin and a 12 fs_diamond on each side, mirrored about it.
        var root = LoadPrefab();
        Assert.IsFalse(root.Descendants().Any(x => (string?)x.Attribute("Id") == "TitleBar"), "no sliced title bar");
        var header = ById(root, "TitleHeader");
        var identity = ById(root, "IdentityFrame");
        var content = ById(root, "ContentPanel");
        float unionLeft = Num(identity, "PositionXOffset"), unionRight = Num(content, "PositionXOffset") + Num(content, "SuggestedWidth");
        Assert.AreEqual(unionLeft, Num(header, "PositionXOffset"), "starts over the identity arch");
        Assert.AreEqual(unionRight - unionLeft, Num(header, "SuggestedWidth"), "ends over the content panel");
        Assert.AreEqual(929.5f, Num(header, "PositionXOffset") + Num(header, "SuggestedWidth") / 2f, 0.01f, "centred over the union 79 to 1780");
        Assert.AreEqual(60f, Num(header, "SuggestedHeight"));
        Assert.IsTrue(Num(header, "PositionYOffset") + 60f <= 68f, "above the arch and the content panel");
        Assert.AreEqual("true", (string?)header.Attribute("DoNotAcceptEvents"));
        var stone = ById(header, "TitleStone");
        Assert.AreEqual("EncyclopediaStonePanelWidget", stone.Name.LocalName);
        Assert.AreSame(header.Element("Children")!.Elements().First(), stone, "the stone draws first");
        Assert.AreEqual("StretchToParent", (string?)stone.Attribute("WidthSizePolicy"));
        Assert.AreEqual("StretchToParent", (string?)stone.Attribute("HeightSizePolicy"));
        Assert.AreEqual(1, root.Descendants().Count(x => (string?)x.Attribute("Sprite") == "ec_header"), "one whole header, no slices and no header frames on cards");

        var label = ById(header, "TitleLabel");
        Assert.AreEqual("FrontEndTextWidget", label.Name.LocalName);
        Assert.AreEqual("{=taom_char_title}Character", (string?)label.Attribute("LocalizedText"));
        Assert.AreEqual("TAOM.Encyclopedia.Title", (string?)label.Attribute("Brush"), "the encyclopedia's own title brush");
        Assert.AreEqual(378f, Num(label, "SuggestedWidth"));
        Assert.AreEqual(48f, Num(label, "SuggestedHeight"));
        Assert.AreEqual("Center", (string?)label.Attribute("HorizontalAlignment"));
        Assert.AreEqual(6f, Num(label, "PositionYOffset"), "the encyclopedia title sits 6 below the header top");

        var dividers = header.Descendants().Where(x => (string?)x.Attribute("Sprite") == "fs_divider_thin").ToList();
        var diamonds = header.Descendants().Where(x => (string?)x.Attribute("Sprite") == "fs_diamond").ToList();
        Assert.IsFalse(Has(header, "Sprite", "cs_scroll_thumb"), "D: the diamond replaces the scroll thumb");
        Assert.AreEqual(2, dividers.Count, "the ornament, mirrored on both sides of the title");
        Assert.AreEqual(2, diamonds.Count);
        foreach (var d in dividers)
        {
            Assert.AreEqual("ImageWidget", d.Name.LocalName);
            Assert.AreEqual(220f, Num(d, "SuggestedWidth"));
            Assert.AreEqual(2f, Num(d, "SuggestedHeight"));
            Assert.AreEqual(29f, Num(d, "PositionYOffset"));
        }
        foreach (var t in diamonds)
        {
            Assert.AreEqual("ImageWidget", t.Name.LocalName);
            Assert.AreEqual(12f, Num(t, "SuggestedWidth"));
            Assert.AreEqual(12f, Num(t, "SuggestedHeight"));
            Assert.AreEqual(Num(dividers[0], "PositionYOffset") + 1f, Num(t, "PositionYOffset") + 6f, 0.01f, "the diamond centred on the line");
        }
        float centre = Num(header, "SuggestedWidth") / 2f;
        foreach (var group in new[] { dividers, diamonds })
        {
            var mid = group.Select(x => Num(x, "PositionXOffset") + Num(x, "SuggestedWidth") / 2f).OrderBy(v => v).ToList();
            Assert.AreEqual(centre, (mid[0] + mid[1]) / 2f, 0.01f, "mirrored about the title centre");
        }
        // Left to right: line, diamond, title, diamond, line.
        var order = dividers.Concat(diamonds).OrderBy(x => Num(x, "PositionXOffset")).ToList();
        CollectionAssert.AreEqual(new[] { "fs_divider_thin", "fs_diamond", "fs_diamond", "fs_divider_thin" }, order.Select(x => (string?)x.Attribute("Sprite")).ToList());
        for (int i = 0; i + 1 < order.Count; i++)
            Assert.IsTrue(Num(order[i], "PositionXOffset") + Num(order[i], "SuggestedWidth") <= Num(order[i + 1], "PositionXOffset"), "no ornament overlaps the next");
    }

    [TestMethod]
    public void Cards_NeverUseTheOpaqueCard()
    {
        var root = LoadPrefab();
        var brushes = XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").ToDictionary(b => (string)b.Attribute("Name")!);
        foreach (var area in new[] { ById(root, "SkillCards"), ById(root, "PerkScroll") })
            foreach (var element in area.DescendantsAndSelf())
            {
                Assert.IsFalse(((string?)element.Attribute("Sprite") ?? "").StartsWith("cc_card", StringComparison.Ordinal), "cc_card has an opaque centre");
                if ((string?)element.Attribute("Brush") is not { } name) continue;
                Assert.AreNotEqual("TAOM.Character.Card", name);
                var sprites = brushes[name].Descendants("BrushLayer").Select(l => (string?)l.Attribute("Sprite")).Where(s => s != null);
                Assert.IsFalse(sprites.Any(s => s!.StartsWith("cc_card", StringComparison.Ordinal)), $"{name} draws cc_card on a card");
            }
    }

    [DataTestMethod]
    [DataRow("TAOM.Character.TabRim", new[] { "Default", "Hovered", "Selected", "Disabled" })]
    public void CardRims_AreTheTransparentPanel_WithDistinctStates(string name, string[] states)
    {
        var brush = XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").Single(b => (string?)b.Attribute("Name") == name);
        var sprites = brush.Descendants("BrushLayer").Select(l => (string?)l.Attribute("Sprite")).Where(s => s != null).Distinct().ToList();
        CollectionAssert.AreEqual(new[] { "ec_panel_9" }, sprites, "a rim with a transparent centre, no fill");
        Assert.IsFalse(brush.Descendants().Attributes("Color").Any(), "no colour fill");
        var alphas = states.Select(s =>
        {
            var style = brush.Descendants("Style").SingleOrDefault(x => (string?)x.Attribute("Name") == s);
            Assert.IsNotNull(style, $"{name} lacks the {s} style");
            return Num(style!.Element("BrushLayer")!, "AlphaFactor", 1f);
        }).ToList();
        Assert.AreEqual(alphas.Count, alphas.Distinct().Count(), $"{name}: every state looks different");
    }

    [TestMethod]
    public void SkillCards_AreLowRimmedTabs_WithADiamondOnTheSelectedTab()
    {
        var root = LoadPrefab();
        var card = ById(root, "SkillCards").Descendants("ItemTemplate").Single().Elements().Single();
        Assert.IsNull(card.Attribute("Brush"), "the button itself draws nothing; the rim widget carries the states");
        Assert.AreEqual("true", (string?)card.Attribute("UpdateChildrenStates"), "hover and selection reach the rim");
        var rim = ById(card, "SkillTabRim");
        Assert.AreEqual("BrushWidget", rim.Name.LocalName);
        Assert.AreEqual("TAOM.Character.TabRim", (string?)rim.Attribute("Brush"));
        Assert.AreEqual("StretchToParent", (string?)rim.Attribute("WidthSizePolicy"));
        Assert.AreEqual("StretchToParent", (string?)rim.Attribute("HeightSizePolicy"));
        Assert.AreEqual("true", (string?)rim.Attribute("DoNotAcceptEvents"));
        foreach (var element in card.Descendants())
        {
            string sprite = (string?)element.Attribute("Sprite") ?? "";
            Assert.IsFalse(sprite.StartsWith("ec_", StringComparison.Ordinal), $"{sprite}: the panel rim comes only through the TabRim brush");
            Assert.IsFalse((string?)element.Attribute("WidthSizePolicy") == "StretchToParent" && element.Attribute("Sprite") != null, "no block background");
        }
        foreach (var gone in new[] { "TAOM.Character.SkillRim", "TAOM.Character.HoverLine" })
            Assert.IsFalse(XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").Any(b => (string?)b.Attribute("Name") == gone), $"{gone} is gone");
        var mark = ById(card, "SkillSelectedMark");
        Assert.AreEqual("cs_tab_diamond", (string?)mark.Attribute("Sprite"));
        Assert.AreEqual("@IsSelected", (string?)mark.Attribute("IsVisible"));
        Assert.AreEqual("true", (string?)mark.Attribute("DoNotAcceptEvents"));
        Assert.AreEqual(Num(card, "SuggestedWidth") / 2f, Num(mark, "PositionXOffset") + Num(mark, "SuggestedWidth") / 2f, 0.5f, "centred on the tab");
        Assert.AreEqual(Num(card, "SuggestedHeight"), Num(mark, "PositionYOffset") + Num(mark, "SuggestedHeight") / 2f, 0.5f, "on the tab's bottom edge");
        Assert.IsTrue(card.Element("Children")!.Elements().ToList().IndexOf(mark) > card.Element("Children")!.Elements().ToList().IndexOf(rim), "the diamond draws over the rim");
    }

    [TestMethod]
    public void PerkMedallions_AreTheRoundRimAroundTheNativeIcon_WithTheStateMarks()
    {
        var root = LoadPrefab();
        var card = root.Descendants().Single(x => x.Name.LocalName == "CharacterPerkButtonWidget");
        Assert.IsNull(card.Attribute("Brush"), "no rectangular card");
        Assert.IsFalse(Has(card, "Sprite", "ec_panel_9"), "no rectangular rim");
        Assert.IsFalse(Has(card, "Sprite", "ec_header"));
        Assert.IsFalse(Has(card, "Id", "PerkHeader"), "no pointed header on hover");
        Assert.IsFalse(XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").Any(b => (string?)b.Attribute("Name") == "TAOM.Character.PerkRim"), "the card rim brush is gone");

        var rim = ById(card, (string)card.Attribute("RimWidget")!);
        Assert.AreEqual("cs_skill_icon_rim", (string?)rim.Attribute("Sprite"));
        Assert.AreEqual(64f, Num(rim, "SuggestedWidth"));
        Assert.AreEqual(64f, Num(rim, "SuggestedHeight"));
        Assert.AreEqual(Num(card, "SuggestedWidth") / 2f, Num(rim, "PositionXOffset") + 32f, 0.5f, "the medallion centred in its column");

        var icon = ById(card, (string)card.Attribute("IconWidget")!);
        Assert.AreEqual("{Native}", (string?)icon.Attribute("DataSource"));
        Assert.AreEqual("@PerkId", (string?)icon.Attribute("Sprite"), "the native SPPerks icon");
        Assert.AreEqual(36f, Num(icon, "SuggestedWidth"));
        Assert.AreEqual(36f, Num(icon, "SuggestedHeight"));
        Assert.AreEqual(Num(rim, "PositionXOffset") + 32f, Num(icon, "PositionXOffset") + 18f, 0.5f, "the icon centred in the rim");
        Assert.AreEqual(Num(rim, "PositionYOffset") + 32f, Num(icon, "PositionYOffset") + 18f, 0.5f);

        var name = ById(card, (string)card.Attribute("NameWidget")!);
        Assert.AreEqual("@NameText", (string?)name.Attribute("Text"));
        Assert.AreEqual("TAOM.Character.PerkName", (string?)name.Attribute("Brush"));
        Assert.AreEqual("Center", (string?)BrushNamed("TAOM.Character.PerkName").Attribute("TextHorizontalAlignment"), "the name centred under the medallion");
        Assert.AreEqual(Num(card, "SuggestedWidth") / 2f, Num(name, "PositionXOffset") + Num(name, "SuggestedWidth") / 2f, 0.5f);
        Assert.IsTrue(Num(name, "PositionYOffset") >= Num(rim, "PositionYOffset") + 64f, "the name below the medallion");

        var chosen = ById(card, (string)card.Attribute("SelectedWidget")!);
        Assert.AreEqual("PerkChosenMark", (string?)chosen.Attribute("Id"));
        Assert.AreEqual("ec_list_tick", (string?)chosen.Attribute("Sprite"), "a tick for the chosen perk");
        var available = ById(card, (string)card.Attribute("AvailableWidget")!);
        Assert.AreEqual("PerkAvailableMark", (string?)available.Attribute("Id"));
        Assert.AreEqual("cs_tab_diamond", (string?)available.Attribute("Sprite"), "a hollow diamond for an available perk");
        foreach (var mark in new[] { chosen, available })
        {
            Assert.AreEqual("false", (string?)mark.Attribute("IsVisible"), "the widget shows the mark for its state only");
            Assert.IsTrue(Num(mark, "PositionXOffset") >= Num(rim, "PositionXOffset") + 32f && Num(mark, "PositionYOffset") + Num(mark, "SuggestedHeight") <= Num(rim, "PositionYOffset") + 32f,
                "the mark sits on the rim's upper right");
        }
        foreach (var child in card.Element("Children")!.Elements())
        {
            var c = Rect(child);
            Assert.AreEqual("true", (string?)child.Attribute("DoNotAcceptEvents"), $"{child.Attribute("Id")?.Value} never takes the card's input");
            Assert.IsTrue(c.X >= 0 && c.Y >= 0 && c.X + c.W <= Num(card, "SuggestedWidth") && c.Y + c.H <= Num(card, "SuggestedHeight"), $"{child.Attribute("Id")?.Value} stays inside the hover area");
        }
    }

    [DataTestMethod]
    [DataRow(2, true, false, 1f, 1f, DisplayName = "chosen: gold, the tick")]
    [DataRow(1, false, true, 1f, 1f, DisplayName = "available: ivory, the diamond")]
    [DataRow(3, false, false, 0.55f, 0.67f, DisplayName = "the pair's rejected choice: dimmed")]
    [DataRow(4, false, false, 0.55f, 0.67f, DisplayName = "behind an unchosen tier: dimmed")]
    [DataRow(0, false, false, 0.45f, 0.47f, DisplayName = "not earned: dimmed most")]
    public void PerkLook_TheNativeStateAlone_PicksMarkAndDimming(int state, bool chosen, bool available, float rimAlpha, float nameAlpha)
    {
        var look = CharacterPerkButtonWidget.Look(state);
        Assert.AreEqual(chosen, look.Chosen, "PerkStates.EarnedAndActive is 2 in the installed 1.5.7 DLL");
        Assert.AreEqual(available, look.Available, "PerkStates.EarnedButNotSelected is 1");
        Assert.AreEqual(rimAlpha, look.RimAlpha, 0.001f);
        Assert.AreEqual(nameAlpha, look.NameAlpha, 0.001f);
        if (state == 2) Assert.AreEqual(CharacterPerkButtonWidget.Gold, look.IconColor, "a gold icon");
        if (state == 1) Assert.AreEqual(CharacterPerkButtonWidget.Ivory, look.IconColor, "an ivory icon");
        if (state != 1 && state != 2) Assert.IsTrue(look.IconAlpha < 1f, "a dimmed icon");
    }

    [TestMethod]
    public void PerkLook_LockedIsFainterThanRejected()
    {
        var locked = CharacterPerkButtonWidget.Look(0);
        var rejected = CharacterPerkButtonWidget.Look(3);
        Assert.IsTrue(locked.RimAlpha < rejected.RimAlpha && locked.IconAlpha < rejected.IconAlpha && locked.NameAlpha < rejected.NameAlpha);
    }

    [TestMethod]
    public void DetailStripAndNextTierSentence_AreGone_WithTheirInspectionState()
    {
        var root = LoadPrefab();
        foreach (var id in new[] { "DetailPanel", "DetailScroll", "DetailScrollbar", "NextTierText" })
            Assert.IsFalse(root.Descendants().Any(x => (string?)x.Attribute("Id") == id), $"{id} is gone");
        foreach (var binding in new[] { "@DetailTitle", "@DetailText", "@NextTierText", "@IsInspected" })
            Assert.IsFalse(root.DescendantsAndSelf().Attributes().Any(a => a.Value == binding), $"{binding} is bound nowhere");
        Assert.IsNull(typeof(CharacterScreenVM).Assembly.GetType("TAOM.Features.FactionUI.Campaign.CharacterPerkInspection"), "no inspection state is left");
        foreach (var property in new[] { "DetailTitle", "DetailText", "NextTierText" })
            Assert.IsNull(typeof(CharacterScreenVM).GetProperty(property), $"CharacterScreenVM.{property} has no consumer");
        Assert.IsNull(typeof(CharacterPerkItemVM).GetProperty("IsInspected"));
        Assert.IsNull(typeof(CharacterPerkButtonWidget).GetProperty("IsInspected"));
        // The numeric marker, the reached and locked tier labels and the rail stay.
        foreach (var id in new[] { "PerkProgressMarker", "PerkTierReached", "PerkTierLocked", "PerkTrack", "PerkProgressFill" })
            Assert.IsNotNull(ById(root, id));
    }

    [TestMethod]
    public void PerkCard_HoverShowsTheNativePerkEffectTooltip()
    {
        // The installed PerkVM builds Hint as a BasicTooltipViewModel whose lambda returns
        // CampaignUIHelper.GetPerkEffectText(perk, ...): the native effect text, on hover, in place of the strip.
        var card = LoadPrefab().Descendants().Single(x => x.Name.LocalName == "CharacterPerkButtonWidget");
        Assert.IsNull(card.Attribute("IsInspected"));
        Assert.AreEqual("@State", (string?)card.Attribute("PerkState"), "the native state stays the only source of chosen and dimmed");
        Assert.AreEqual("ExecuteSelect", (string?)card.Attribute("Command.Click"));
        Assert.AreEqual("ExecuteBeginHint", (string?)card.Attribute("Command.HoverBegin"));
        Assert.AreEqual("ExecuteEndHint", (string?)card.Attribute("Command.HoverEnd"));
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        foreach (var command in new[] { "ExecuteBeginHint", "ExecuteEndHint", "ExecuteSelect" })
            Assert.IsNotNull(typeof(CharacterPerkItemVM).GetMethod(command, flags, null, Type.EmptyTypes, null), $"CharacterPerkItemVM.{command}() answers the binding");
        foreach (var gone in new[] { "ExecuteInspect", "ExecuteEndInspect" })
            Assert.IsNull(typeof(CharacterPerkItemVM).GetMethod(gone, flags));
        string vm = ReadSource("Client/Features/FactionUI/Campaign/CharacterScreenVM.cs");
        StringAssert.Contains(vm, "((PerkVM)Native).Hint.ExecuteBeginHint()");
        StringAssert.Contains(vm, "((PerkVM)Native).Hint.ExecuteEndHint()");
    }

    [TestMethod]
    public void PerkItem_PassesInputThrough_ToTheOverlappedCard()
    {
        // A second choice's item starts RowY(2) down, inside the first item's height, so its empty top lies
        // over the first card's bottom. Only the card button may take input; EventManager skips a
        // DoNotAcceptEvents widget.
        var item = ById(LoadPrefab(), "PerkContent").Descendants("ItemTemplate").Single().Elements().Single();
        Assert.IsTrue(CharacterPerkItemVM.RowY(2) < Num(item, "SuggestedHeight"), "the overlap this test guards exists");
        Assert.AreEqual("true", (string?)item.Attribute("DoNotAcceptEvents"), "the item container passes input through");
        var card = item.Descendants().Single(x => x.Name.LocalName == "CharacterPerkButtonWidget");
        Assert.IsNull(card.Attribute("DoNotAcceptEvents"), "the card stays clickable");
        Assert.IsNull(card.Attribute("IsEnabled"), "the card stays enabled");
        Assert.AreEqual("true", (string?)card.Attribute("DoNotPassEventsToChildren"), "the card's own frames and name never take its input");
        var insideCard = new HashSet<XElement>(card.Descendants());
        foreach (var decoration in item.Descendants().Where(x => x.Name.LocalName != "Children" && x != card && !insideCard.Contains(x)))
            Assert.AreEqual("true", (string?)decoration.Attribute("DoNotAcceptEvents"), $"{decoration.Attribute("Id")?.Value ?? decoration.Name.LocalName} steals no input");
        foreach (var child in item.Element("Children")!.Elements())
        {
            float x = Num(child, "PositionXOffset"), y = Num(child, "PositionYOffset");
            Assert.IsTrue(x >= 0 && y >= 0 && x + Num(child, "SuggestedWidth") <= Num(item, "SuggestedWidth") && y + Num(child, "SuggestedHeight") <= Num(item, "SuggestedHeight"),
                $"{child.Attribute("Id")?.Value ?? child.Name.LocalName} stays inside the item");
        }
    }

    [TestMethod]
    public void PlusButtons_UseTheirOwnBrush()
    {
        var root = LoadPrefab();
        foreach (var command in new[] { "ExecuteAddAttributePoint", "ExecuteAddFocus" })
        {
            var button = root.Descendants().Single(x => (string?)x.Attribute("Command.Click") == command);
            Assert.AreEqual("TAOM.Character.Plus", (string?)button.Attribute("Brush"), command);
        }
        var brush = XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").Single(b => (string?)b.Attribute("Name") == "TAOM.Character.Plus");
        Assert.IsTrue(brush.Descendants("Style").Any(s => (string?)s.Attribute("Name") == "Disabled"), "a spent point shows");
        Assert.IsFalse(XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").Any(b => (string?)b.Attribute("Name") == "TAOM.Character.Card"), "the opaque card brush is gone");
    }

    [TestMethod]
    public void PairedPerks_ShowTheLocalizedOrBetweenTheChoices()
    {
        var root = LoadPrefab();
        var or = ById(root, "PerkOr");
        Assert.AreEqual("FrontEndTextWidget", or.Name.LocalName);
        Assert.AreEqual("{=taom_char_perk_or}or", (string?)or.Attribute("LocalizedText"));
        Assert.AreEqual("@ConnectorAlpha", (string?)or.Attribute("AlphaFactor"), "quieter in a locked tier");
        var connector = ById(root, "PerkConnector");
        Assert.AreSame(connector, or.Parent!.Parent);
        Assert.AreEqual("@HasAlternativeBelow", (string?)connector.Attribute("IsVisible"), "only the first of a pair draws the connector down to the second");
        Assert.AreEqual("true", (string?)connector.Attribute("DoNotAcceptEvents"));
        foreach (var line in connector.Element("Children")!.Elements().Where(x => x != or))
        {
            Assert.AreEqual("@ConnectorAlpha", (string?)line.Attribute("AlphaFactor"));
            Assert.AreEqual(1f, Num(line, "SuggestedWidth"), "a hairline");
            Assert.AreEqual(Num(connector, "SuggestedWidth") / 2f, Num(line, "PositionXOffset") + 0.5f, 0.01f, "on the medallions' centre line");
            Assert.IsFalse(Overlap(Rect(line), Rect(or)), "the line breaks around the 'or'");
        }
        var card = root.Descendants().Single(x => x.Name.LocalName == "CharacterPerkButtonWidget");
        Assert.AreSame(card.Parent!.Parent, connector.Parent!.Parent, "the connector rides in the first choice's item");
        // Item-local: the first medallion ends at its button bottom; the second starts RowY(2) - RowY(1) lower.
        float firstBottom = Num(card, "PositionYOffset") + Num(card, "SuggestedHeight");
        float secondTop = CharacterPerkItemVM.RowY(2) - CharacterPerkItemVM.RowY(1) + Num(card, "PositionYOffset");
        float top = Num(connector, "PositionYOffset"), bottom = top + Num(connector, "SuggestedHeight");
        Assert.IsTrue(top >= firstBottom && bottom <= secondTop, "the connector stays in the gap between the two medallions");
    }

    [TestMethod]
    public void PerkConnector_OnlyTheFirstOfAPairDrawsIt_AndALockedTierDimsIt()
    {
        Assert.IsTrue(CharacterPerkItemVM.HasConnectorBelow(1), "the first of a pair");
        Assert.IsFalse(CharacterPerkItemVM.HasConnectorBelow(0), "a single perk has no pair");
        Assert.IsFalse(CharacterPerkItemVM.HasConnectorBelow(2), "the second of a pair has nothing below");
        Assert.AreEqual(1f, CharacterPerkItemVM.ConnectorAlphaFor(true));
        Assert.IsTrue(CharacterPerkItemVM.ConnectorAlphaFor(false) < 1f);
    }

    [TestMethod]
    public void PerkPageArrows_ShowOnlyWhenThePerksOverflowTheView()
    {
        var root = LoadPrefab();
        foreach (var id in new[] { "PreviousPerkPage", "NextPerkPage" })
            Assert.AreEqual("@CanScrollPerks", (string?)ById(root, id).Attribute("IsVisible"), id);
        Assert.AreEqual(CharacterScreenVM.PerkViewWidth, Num(ById(root, "PerkScroll"), "SuggestedWidth"), "the view the overflow test measures against");
        Assert.IsFalse(CharacterScreenVM.PerkContentWidth(5) > CharacterScreenVM.PerkViewWidth, "five tiers fit: no arrows");
        Assert.IsTrue(CharacterScreenVM.PerkContentWidth(6) > CharacterScreenVM.PerkViewWidth, "a sixth tier scrolls");
    }

    [DataTestMethod]
    [DataRow("taom_char_title")]
    [DataRow("taom_char_perk_or")]
    [DataRow("taom_char_attribute_points")]
    [DataRow("taom_char_perk_tier_locked")]
    [DataRow("taom_char_no_traits")]
    public void NewLabels_AreRegisteredInEnglish(string key)
    {
        Assert.IsTrue(EnglishModuleStringIds().Contains(key), $"{key} is not registered");
    }

    // Only the English source is ours to write: the language files are filled by the project lead's
    // translation pass (the localize skill), so these tests never require a language row.
    private static HashSet<string> EnglishModuleStringIds() => new(
        XDocument.Parse(ReadSource("Main/_Module/ModuleData/taom_module_strings.xml")).Descendants("string")
            .Select(s => (string)s.Attribute("id")!), StringComparer.Ordinal);

    [TestMethod]
    public void PerkMarker_DrawsTheDiamondAndOneNumberFromTheClampedVisual()
    {
        var marker = ById(LoadPrefab(), "PerkProgressMarker");
        Assert.AreEqual("StretchToParent", (string?)marker.Attribute("WidthSizePolicy"), "the marker parts place themselves on the content");
        // D: a short drop line and the diamond below the rail, placed together by the drop's left edge.
        var drop = ById(marker, "PerkMarkerDrop");
        Assert.AreEqual("@MarkerDiamondLeft", (string?)drop.Attribute("PositionXOffset"));
        Assert.AreEqual(CharacterSkillProgress.MarkerDiamondSize, Num(drop, "SuggestedWidth"));
        var diamond = ById(drop, "PerkMarkerDiamond");
        Assert.IsNull(diamond.Attribute("PositionXOffset"), "the drop places the diamond");
        Assert.AreEqual(CharacterSkillProgress.MarkerDiamondSize, Num(diamond, "SuggestedWidth"));
        var track = ById(LoadPrefab(), "PerkTrack");
        Assert.IsTrue(Num(marker, "PositionYOffset") >= Num(track, "PositionYOffset") + Num(track, "SuggestedHeight"), "the marker sits below the rail");
        Assert.IsTrue(Num(marker, "PositionYOffset") + Num(diamond, "PositionYOffset") + Num(diamond, "SuggestedHeight") <= Num(marker, "PositionYOffset") + Num(marker, "SuggestedHeight"));
        var right = ById(marker, "PerkMarkerNumber");
        var left = ById(marker, "PerkMarkerNumberFlipped");
        foreach (var label in new[] { right, left })
        {
            Assert.AreEqual("@MarkerLabelLeft", (string?)label.Attribute("PositionXOffset"));
            Assert.AreEqual("@SkillLevelText", (string?)label.Attribute("Text"));
            Assert.AreEqual(CharacterSkillProgress.MarkerLabelWidth, Num(label, "SuggestedWidth"));
        }
        Assert.AreEqual("@IsMarkerLabelFlipped", (string?)right.Attribute("IsHidden"), "one number shows at a time");
        Assert.AreEqual("@IsMarkerLabelFlipped", (string?)left.Attribute("IsVisible"));
        Assert.AreEqual("Left", (string?)right.Attribute("Brush.TextHorizontalAlignment"), "beside the diamond on its right");
        Assert.AreEqual("Right", (string?)left.Attribute("Brush.TextHorizontalAlignment"), "beside the diamond on its left");
    }

    [DataTestMethod]
    [DataRow("Command.Click", "ExecuteAddAttributePoint")]
    [DataRow("IsEnabled", "@CanAddPoint")]
    [DataRow("Command.Click", "ExecuteAddFocus")]
    [DataRow("IsEnabled", "@CanAddFocus")]
    [DataRow("Command.Click", "ExecuteCancel")]
    [DataRow("Command.Click", "ExecuteDone")]
    [DataRow("Command.Click", "ExecuteReset")]
    [DataRow("Text", "@CancelLbl")]
    [DataRow("Text", "@DoneLbl")]
    [DataRow("Text", "@ResetLbl")]
    [DataRow("Command.Click", "ExecutePreviousMember")]
    [DataRow("Command.Click", "ExecuteNextMember")]
    [DataRow("IsEnabled", "@CanSwitchMember")]
    [DataRow("Text", "@CurrentLearningRateText")]
    [DataRow("IntText", "@UnspentAttributePoints")]
    [DataRow("IntText", "@UnspentCharacterPoints")]
    [DataRow("Text", "@FocusPointsText")]
    [DataRow("Text", "@LevelProgressText")]
    [DataRow("Text", "@NameText")]
    [DataRow("Text", "@LevelText")]
    [DataRow("DataSource", "{UnspentAttributePointsHint}")]
    [DataRow("DataSource", "{UnspentCharacterPointsHint}")]
    [DataRow("DataSource", "{LearningRateTooltip}")]
    [DataRow("DataSource", "{Traits}")]
    [DataRow("DataSource", "{CharacterStats}")]
    [DataRow("Text", "@TraitsText")]
    [DataRow("Text", "@PartyRoleText")]
    [DataRow("TraitId", "@TraitId")]
    [DataRow("TraitValue", "@Value")]
    [DataRow("DataSource", "{Hint}")]
    [DataRow("Text", "@Definition")]
    public void Prefab_KeepsTheNativeBinding(string attribute, string value)
    {
        Assert.IsTrue(Has(LoadPrefab(), attribute, value), $"{attribute}=\"{value}\" is missing");
    }

    [TestMethod]
    public void CareerButton_KeepsTheNativeCommandAndVisibility()
    {
        var career = LoadPrefab().Descendants().Single(x => (string?)x.Attribute("Command.Click") == "ExecuteOpenCareerScreen");
        Assert.AreEqual("{Native}", (string?)career.Attribute("DataSource"));
        Assert.AreEqual("@HasCareer", (string?)career.Attribute("IsVisible"), "a companion without a career sees no button");
    }

    [TestMethod]
    public void PerkCards_ShowTheNameAndKeepNativeStateAndCommands()
    {
        var root = LoadPrefab();
        var scroll = ById(root, "PerkScroll");
        Assert.AreEqual("ScrollablePanel", scroll.Name.LocalName);
        Assert.IsNotNull((string?)scroll.Attribute("HorizontalScrollbar"));
        Assert.AreEqual("@PerkWidth", (string?)ById(root, "PerkContent").Attribute("SuggestedWidth"));
        Assert.AreEqual("CharacterPerkScrollbarWidget", ById(root, "PerkScrollbar").Name.LocalName);

        var card = root.Descendants().Single(x => x.Name.LocalName == "CharacterPerkButtonWidget");
        Assert.AreEqual("@State", (string?)card.Attribute("PerkState"));
        Assert.AreEqual("ExecuteSelect", (string?)card.Attribute("Command.Click"));
        Assert.AreEqual("ExecuteBeginHint", (string?)card.Attribute("Command.HoverBegin"));
        Assert.AreEqual("ExecuteEndHint", (string?)card.Attribute("Command.HoverEnd"));
        Assert.AreEqual("true", (string?)card.Attribute("UpdateChildrenStates"), "the card text follows the card state");
        foreach (var reference in new[] { "RimWidget", "IconWidget", "NameWidget", "SelectedWidget", "AvailableWidget" })
        {
            string id = (string)card.Attribute(reference)!;
            Assert.AreEqual(1, card.Descendants().Count(x => (string?)x.Attribute("Id") == id), $"{reference} names one widget inside the medallion");
        }
        var named = card.Descendants().Single(x => (string?)x.Attribute("Id") == (string)card.Attribute("NameWidget")!);
        Assert.AreEqual("@NameText", (string?)named.Attribute("Text"), "the dimmed name is the perk name");
        Assert.IsTrue(Num(card, "SuggestedWidth") >= 200f && Num(card, "SuggestedHeight") >= 54f, "the medallion and its name hover as one");
    }

    [TestMethod]
    public void PartyRoles_ScrollInsideTheirBoundedArea()
    {
        var root = LoadPrefab();
        var stats = root.Descendants().Single(x => (string?)x.Attribute("DataSource") == "{CharacterStats}");
        var scroll = stats.Ancestors().FirstOrDefault(x => x.Name.LocalName == "ScrollablePanel");
        Assert.IsNotNull(scroll, "a governor who also holds a party role scrolls, never pushes Career down");
        Assert.IsNotNull((string?)scroll!.Attribute("VerticalScrollbar"));
        Assert.AreEqual("true", (string?)ById(scroll, (string)scroll.Attribute("ClipRect")!).Attribute("ClipContents"));
        Assert.IsTrue(scroll.Ancestors().Contains(ById(root, "PartyRoleArea")));
    }

    [TestMethod]
    public void PartyRoleRow_GrowsWithWrappedText_InsideTheFixedViewport()
    {
        var root = LoadPrefab();
        var stats = root.Descendants().Single(x => (string?)x.Attribute("DataSource") == "{CharacterStats}");
        var row = stats.Element("ItemTemplate")!.Elements().Single();
        Assert.AreEqual("@Definition", (string?)row.Attribute("Text"));
        Assert.AreEqual("CoverChildren", (string?)row.Attribute("HeightSizePolicy"),
            "a fixed row height cuts a wrapped governor title so its last line never scrolls into view");
        Assert.IsNull(row.Attribute("SuggestedHeight"));
        Assert.AreEqual("CoverChildren", (string?)stats.Attribute("HeightSizePolicy"));
        var scroll = stats.Ancestors().First(x => x.Name.LocalName == "ScrollablePanel");
        Assert.AreEqual("26", (string?)scroll.Attribute("SuggestedHeight"), "the viewport stays 26 px so Career keeps its place");
        Assert.AreEqual("632", (string?)ById(root, "CareerButton").Attribute("PositionYOffset"));
    }

    [TestMethod]
    public void Labels_AreNativeOrLocalized_AndNoMockupDataShips()
    {
        var root = LoadPrefab();
        var literals = root.Descendants()
            .Where(x => x.Attribute("Text") is { } t && !t.Value.StartsWith("@", StringComparison.Ordinal) && t.Value != "+")
            .Select(x => $"<{x.Name.LocalName} Text=\"{x.Attribute("Text")!.Value}\">")
            .ToList();
        Assert.AreEqual(0, literals.Count, "literal labels stay English: " + string.Join(", ", literals));
        string text = ReadSource(PrefabPath);
        foreach (var mockup in new[] { "Ruling Steward", "Rank 3", "DENETHOR", "Minas Tirith", "Basher", "Wrapped Handles", "41,250", "Quartermaster", "Calculating" })
            Assert.IsFalse(text.Contains(mockup), $"mockup data '{mockup}' ships");
    }

    [TestMethod]
    public void LocalizedLabels_AreRegisteredInEnglish()
    {
        var keys = LoadPrefab().Descendants().Select(x => (string?)x.Attribute("LocalizedText")).Where(v => v != null)
            .Select(v => v!.Substring(2, v.IndexOf('}') - 2)).Distinct().ToList();
        Assert.IsTrue(keys.Count > 0);
        var ids = EnglishModuleStringIds();
        foreach (var key in keys) Assert.IsTrue(ids.Contains(key), $"taom_module_strings.xml lacks {key}");
    }

    [TestMethod]
    public void PerkLayout_EveryTierHasAColumn_AndPairsStack()
    {
        Assert.AreEqual(1260f, CharacterScreenVM.PerkContentWidth(5), "five tiers fill the view");
        Assert.AreEqual(1260f, CharacterScreenVM.PerkContentWidth(1), "a single tier still spans the view");
        float width = CharacterScreenVM.PerkContentWidth(12);
        Assert.IsTrue(CharacterPerkItemVM.ColumnX(11) + CharacterPerkItemVM.ColumnWidth <= width, "the last tier is reachable by scrolling");
        Assert.AreEqual(CharacterPerkItemVM.ColumnX(1) - CharacterPerkItemVM.ColumnX(0), CharacterPerkItemVM.ColumnX(5) - CharacterPerkItemVM.ColumnX(4), 0.01f);
        Assert.AreEqual(CharacterPerkItemVM.RowY(0), CharacterPerkItemVM.RowY(1), "a single perk and the first of a pair share the top row");
        Assert.AreEqual(182f, CharacterPerkItemVM.RowY(2) - CharacterPerkItemVM.RowY(1), "the second medallion 182 below the first, the connector and 'or' between");
        Assert.AreEqual(1260f, CharacterPerkItemVM.ColumnWidth * 5f, 0.01f, "D: five 252 columns across the 1260 view");
        var item = ById(LoadPrefab(), "PerkContent").Descendants("ItemTemplate").Single().Elements().Single();
        Assert.AreEqual(CharacterPerkItemVM.ColumnWidth, Num(item, "SuggestedWidth"), "one item per column");
    }

    [TestMethod]
    public void Brushes_UseOnlyTheApprovedFonts()
    {
        var fonts = XDocument.Parse(ReadSource(BrushPath)).Descendants().Select(x => (string?)x.Attribute("Font")).Where(f => f != null).Distinct().ToList();
        Assert.IsTrue(fonts.Count > 0);
        CollectionAssert.IsSubsetOf(fonts, new[] { "FS_CinzelWide", "FS_Garamond" });
        Assert.IsTrue(fonts.Contains("FS_CinzelWide"), "headings use Cinzel");
    }
}
