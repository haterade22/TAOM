using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionUI.Campaign;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Character notification A: open perk tiers counted from the native PerkVM state (1 EarnedButNotSelected is
/// selectable; 4 EarnedPreviousPerkNotSelected is blocked), one count per tier, recounted only when the native
/// per-skill signal, the hero or an own command says so; badges, gold pools and the open-tier diamond in the prefab.
/// </summary>
[TestClass]
public class CharacterOpenTiersTests
{
    private const string PrefabPath = "Main/_Module/GUI/PreFabs/FactionUI/TAOMCharacterDeveloper.xml";
    private const string BrushPath = "Main/_Module/GUI/Brushes/TAOMCharacterDeveloper.xml";

    private static int Open(params (int Level, int State)[] perks) => CharacterOpenTiers.OpenTiers(perks);

    [TestMethod]
    public void OpenTiers_SelectablePair_CountsTheTierOnce()
    {
        Assert.AreEqual(1, Open((25, 1), (25, 1)));
    }

    [TestMethod]
    public void OpenTiers_SingleSelectablePerk_CountsOne()
    {
        Assert.AreEqual(1, Open((75, 1)));
    }

    [TestMethod]
    public void OpenTiers_BlockedBehindAnUnchosenTier_IsExcluded()
    {
        // Tier 50 waits on tier 25: the native marks it 4, and only tier 25 can be chosen now.
        Assert.AreEqual(1, Open((25, 1), (25, 1), (50, 4), (50, 4)));
    }

    [TestMethod]
    public void OpenTiers_ChosenRejectedAndFutureTiers_AreExcluded()
    {
        Assert.AreEqual(0, Open((25, 2), (25, 3), (50, 0), (50, 0), (75, 0)));
    }

    [TestMethod]
    public void OpenTiers_SeveralSelectableTiers_CountsEachTier()
    {
        Assert.AreEqual(2, Open((25, 2), (25, 3), (50, 1), (50, 1), (75, 1), (100, 0)));
    }

    [TestMethod]
    public void OpenTiers_NoPerks_IsZero()
    {
        Assert.AreEqual(0, Open());
    }

    [TestMethod]
    public void ForAttribute_SumsOnlyItsOwnSkills()
    {
        var tiers = new CharacterOpenTiers();
        tiers.Store(new Dictionary<string, int> { ["OneHanded"] = 2, ["TwoHanded"] = 1, ["Polearm"] = 0, ["Bow"] = 3 });
        Assert.AreEqual(3, tiers.ForAttribute(new[] { "OneHanded", "TwoHanded", "Polearm" }));
        Assert.AreEqual(3, tiers.ForAttribute(new[] { "Bow", "Crossbow", "Throwing" }), "an unknown skill counts zero");
        Assert.AreEqual(0, tiers.ForAttribute(Array.Empty<string>()));
    }

    [TestMethod]
    public void ForSkill_UnknownSkill_IsZeroSoTheBadgeHides()
    {
        Assert.AreEqual(0, new CharacterOpenTiers().ForSkill("Riding"));
    }

    [TestMethod]
    public void Store_NewHeroCounts_DropThePreviousHerosCounts()
    {
        var tiers = new CharacterOpenTiers();
        tiers.Store(new Dictionary<string, int> { ["Riding"] = 2 });
        tiers.Store(new Dictionary<string, int> { ["Athletics"] = 1 });
        Assert.AreEqual(0, tiers.ForSkill("Riding"), "no badge carried over from another hero");
        Assert.AreEqual(1, tiers.ForSkill("Athletics"));
    }

    [TestMethod]
    public void NeedsRecount_FirstHero_IsTrue()
    {
        Assert.IsTrue(new CharacterOpenTiers().NeedsRecount(new object(), new[] { 0, 1 }, false));
    }

    [TestMethod]
    public void NeedsRecount_SameHeroAndSignal_IsFalse()
    {
        var tiers = new CharacterOpenTiers();
        var hero = new object();
        tiers.NeedsRecount(hero, new[] { 2, 0, 1 }, false);
        Assert.IsFalse(tiers.NeedsRecount(hero, new[] { 2, 0, 1 }, false), "no recount on a quiet frame");
    }

    [TestMethod]
    public void NeedsRecount_SignalChanged_IsTrueOnce()
    {
        // A perk choice or a reset changes the native SkillVM.NumOfUnopenedPerks of that skill.
        var tiers = new CharacterOpenTiers();
        var hero = new object();
        tiers.NeedsRecount(hero, new[] { 2, 0, 1 }, false);
        Assert.IsTrue(tiers.NeedsRecount(hero, new[] { 1, 0, 1 }, false));
        Assert.IsFalse(tiers.NeedsRecount(hero, new[] { 1, 0, 1 }, false));
    }

    [TestMethod]
    public void NeedsRecount_ReusedBuffer_StillSeesTheChange()
    {
        // The VM refills one array every frame; the stored signal must be a copy, not the same array.
        var tiers = new CharacterOpenTiers();
        var hero = new object();
        var buffer = new[] { 2, 0 };
        tiers.NeedsRecount(hero, buffer, false);
        buffer[0] = 1;
        Assert.IsTrue(tiers.NeedsRecount(hero, buffer, false));
    }

    [TestMethod]
    public void NeedsRecount_OtherHeroWithTheSameSignal_IsTrue()
    {
        var tiers = new CharacterOpenTiers();
        tiers.NeedsRecount(new object(), new[] { 1, 1 }, false);
        Assert.IsTrue(tiers.NeedsRecount(new object(), new[] { 1, 1 }, false), "a hero switch always recounts");
    }

    [TestMethod]
    public void NeedsRecount_Forced_IsTrueWithAnUnchangedSignal()
    {
        var tiers = new CharacterOpenTiers();
        var hero = new object();
        tiers.NeedsRecount(hero, new[] { 1 }, false);
        Assert.IsTrue(tiers.NeedsRecount(hero, new[] { 1 }, true), "Reset and a perk choice recount at once");
    }

    [TestMethod]
    public void PerkLook_Selectable_HasABrighterIvoryRimAndNoTick()
    {
        var open = CharacterPerkButtonWidget.Look(1);
        Assert.IsFalse(open.Chosen, "no check mark until one is chosen");
        Assert.IsTrue(open.Available);
        Assert.IsTrue(open.RimBrightness > CharacterPerkButtonWidget.Look(2).RimBrightness, "the open pair's rims read brighter than a chosen rim");
        foreach (int state in new[] { 0, 2, 3, 4 })
            Assert.AreEqual(1f, CharacterPerkButtonWidget.Look(state).RimBrightness, $"state {state} keeps its rim");
    }

    [TestMethod]
    public void PerkItem_IsTierOpen_OnlyOnTheLabelledPerk()
    {
        Assert.IsTrue(CharacterPerkItemVM.TierOpen(showLevel: true, state: 1));
        Assert.IsFalse(CharacterPerkItemVM.TierOpen(showLevel: false, state: 1), "the second of a pair carries no label");
        foreach (int state in new[] { 0, 2, 3, 4 })
            Assert.IsFalse(CharacterPerkItemVM.TierOpen(true, state), $"state {state}");
    }

    private static XElement Prefab() => XDocument.Parse(ReadSource(PrefabPath)).Root!;
    private static XElement Brush(string name) =>
        XDocument.Parse(ReadSource(BrushPath)).Root!.Elements("Brush").Single(b => (string?)b.Attribute("Name") == name);
    private static XElement ById(XElement root, string id) => root.Descendants().Single(x => (string?)x.Attribute("Id") == id);
    private static float Num(XElement element, string attribute) =>
        float.Parse((string?)element.Attribute(attribute) ?? "0", System.Globalization.CultureInfo.InvariantCulture);

    [DataTestMethod]
    // The attribute badge rides 6 px higher than the skill badge so it clears the tab's value text (Intelligence).
    // The attribute tab shows a + only while the native VM allows a point (no points or capped: hidden); the
    // skill card shows a + while a perk tier is open. No count: OpenTierCount leaves out earned tiers not yet selectable.
    [DataRow("AttributeTabs", "AttributeSpendIndicator", -2f, "{Native}", "@CanAddPoint")]
    [DataRow("SkillCards", "SkillOpenTierBadge", 4f, null, "@HasOpenTiers")]
    public void Badge_IsADarkGoldRimmedDiscAtTheUpperRight_ShownOnlyWhenActionable(string list, string id, float centreY,
        string? dataSource, string visible)
    {
        var item = ById(Prefab(), list).Element("ItemTemplate")!.Elements().Single();
        Assert.IsFalse(item.Descendants().Any(x => (string?)x.Attribute("Id") == "AttributeOpenTierBadge"), "no attribute count badge");
        var badge = ById(item, id);
        Assert.AreSame(item, badge.Parent!.Parent, "the badge sits on the tab or card itself");
        Assert.AreEqual(dataSource, (string?)badge.Attribute("DataSource"));
        Assert.AreEqual(visible, (string?)badge.Attribute("IsVisible"), "hidden unless actionable");
        Assert.IsFalse(badge.DescendantsAndSelf().Any(x => x.Attribute("Command.Click") != null), "display only, no command");
        foreach (var element in badge.DescendantsAndSelf().Where(x => x.Name.LocalName != "Children"))
            Assert.AreEqual("true", (string?)element.Attribute("DoNotAcceptEvents"), "the badge never takes the tab's click");
        var parts = badge.Element("Children")!.Elements().ToList();
        Assert.AreEqual("cs_circle", (string?)parts[0].Attribute("Sprite"), "a dark disc first");
        StringAssert.StartsWith((string?)parts[0].Attribute("Color"), "#0D0B09");
        Assert.AreEqual("TAOM.Character.BadgeRim", (string?)parts[1].Attribute("Brush"), "then the fine gold rim");
        Assert.AreEqual(5, parts.Count, "disc, rim and the three strokes of the +");
        AssertCentredPlus(parts.Skip(2), 26f, 10f);
        Assert.IsFalse(badge.Descendants().Any(x => x.Name.LocalName == "TextWidget"), "no font glyph and no count");
        // Upper right: the centre inside the item's right 20 px and on its top edge.
        float size = Num(badge, "SuggestedWidth"), cx = Num(badge, "PositionXOffset") + size / 2f, cy = Num(badge, "PositionYOffset") + size / 2f;
        Assert.AreEqual(26f, size);
        Assert.IsTrue(cx > Num(item, "SuggestedWidth") - 20f && cx < Num(item, "SuggestedWidth"), "at the right edge");
        Assert.AreEqual(centreY, cy, 0.5f, "on the top edge");
        Assert.IsTrue(item.Element("Children")!.Elements().Last() == badge, "drawn over the tab");
    }

    [TestMethod]
    public void SkillBadge_StaysClearOfTheLevelNameAndPips()
    {
        var card = ById(Prefab(), "SkillCards").Element("ItemTemplate")!.Elements().Single();
        var badge = ById(card, "SkillOpenTierBadge");
        // The card's {Native} layer stretches over the card at no offset, so its children share the badge's frame.
        bool Overlap(XElement a, XElement b) =>
            Num(a, "PositionXOffset") < Num(b, "PositionXOffset") + Num(b, "SuggestedWidth") && Num(b, "PositionXOffset") < Num(a, "PositionXOffset") + Num(a, "SuggestedWidth")
            && Num(a, "PositionYOffset") < Num(b, "PositionYOffset") + Num(b, "SuggestedHeight") && Num(b, "PositionYOffset") < Num(a, "PositionYOffset") + Num(a, "SuggestedHeight");
        foreach (var id in new[] { "SkillLevel", "SkillName", "SkillFocus" })
            Assert.IsFalse(Overlap(badge, ById(card, id)), $"the badge covers {id}");
    }

    /// <summary>
    /// A + of <paramref name="length"/> by 2 px drawn as a vertical bar and two arms, centred in the circle. Font
    /// glyphs sat low and right. The strokes never overlap: the disabled brush fades each one, so an overlap would
    /// draw a darker centre (0.45 over 0.45 is about 0.70).
    /// </summary>
    private static void AssertCentredPlus(IEnumerable<XElement> strokes, float container, float length)
    {
        var rects = new List<(float X, float Y, float W, float H)>();
        foreach (var stroke in strokes)
        {
            Assert.AreEqual("BrushWidget", stroke.Name.LocalName);
            Assert.AreEqual("TAOM.Character.PlusStroke", (string?)stroke.Attribute("Brush"));
            Assert.IsNull(stroke.Attribute("MarginLeft") ?? stroke.Attribute("MarginTop") ?? stroke.Attribute("MarginRight") ?? stroke.Attribute("MarginBottom"));
            Assert.IsNull(stroke.Attribute("PositionYOffset"));
            float w = Num(stroke, "SuggestedWidth"), h = Num(stroke, "SuggestedHeight");
            // Left alignment is the default; a centred stroke takes no X offset, so the two forms never stack.
            float x = (string?)stroke.Attribute("HorizontalAlignment") == "Center" ? (container - w) / 2f : Num(stroke, "PositionXOffset");
            if ((string?)stroke.Attribute("HorizontalAlignment") == "Center")
                Assert.IsNull(stroke.Attribute("PositionXOffset"));
            Assert.AreEqual("Center", (string?)stroke.Attribute("VerticalAlignment"));
            float y = (container - h) / 2f;
            foreach (var edge in new[] { x, y, x + w, y + h })
                Assert.AreEqual(Math.Floor(edge), edge, $"stroke edge {edge} lands on a whole pixel in {container}");
            rects.Add((x, y, w, h));
        }
        Assert.AreEqual(3, rects.Count, "a vertical bar and two arms");
        for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
            {
                var (a, b) = (rects[i], rects[j]);
                bool overlap = a.X < b.X + b.W && b.X < a.X + a.W && a.Y < b.Y + b.H && b.Y < a.Y + a.H;
                Assert.IsFalse(overlap, $"strokes {i} and {j} overlap");
            }
        float left = rects.Min(r => r.X), right = rects.Max(r => r.X + r.W), top = rects.Min(r => r.Y), bottom = rects.Max(r => r.Y + r.H);
        Assert.AreEqual(container / 2f, (left + right) / 2f, "the + centres across the circle");
        Assert.AreEqual(container / 2f, (top + bottom) / 2f, "the + centres down the circle");
        Assert.AreEqual(length, right - left, "the full width");
        Assert.AreEqual(length, bottom - top, "the full height");
        // Disjoint strokes whose areas add up to a full cross leave no gap in it.
        Assert.AreEqual(2f * length * 2f - 4f, rects.Sum(r => r.W * r.H), "a whole 2 px cross, no gap");
        Assert.IsTrue(rects.All(r => r.W == 2f || r.H == 2f), "every stroke is 2 px thick");
    }

    [TestMethod]
    public void BadgeBrushes_ReuseTheGoldRimAndAnIvoryPlus()
    {
        var rim = Brush("TAOM.Character.BadgeRim");
        CollectionAssert.AreEqual(new[] { "cs_skill_icon_rim" }, rim.Descendants("BrushLayer").Select(l => (string?)l.Attribute("Sprite")).Where(s => s != null).ToList());
        var plus = Brush("TAOM.Character.PlusStroke");
        var layers = plus.Element("Layers")!.Elements().ToList();
        CollectionAssert.AreEqual(new[] { "BlankWhiteSquare", "BlankWhiteSquare" }, layers.Select(l => (string?)l.Attribute("Sprite")).ToList());
        CollectionAssert.AreEqual(new[] { "#E8DCC0FF", "#CFAE6BFF" }, layers.Select(l => (string?)l.Attribute("Color")).ToList(), "ivory, and gold under the cursor");
        string? Hidden(string style, string layer) => (string?)plus.Descendants("Style").Single(s => (string?)s.Attribute("Name") == style)
            .Elements("BrushLayer").SingleOrDefault(l => (string?)l.Attribute("Name") == layer)?.Attribute("IsHidden");
        Assert.AreEqual("true", Hidden("Default", "Gold"), "ivory at rest");
        Assert.AreEqual("true", Hidden("Hovered", "Ivory"), "gold on hover, as the old + text");
        Assert.AreEqual("true", Hidden("Disabled", "Gold"), "a disabled + shows no gold");
    }

    [TestMethod]
    public void PlusButtons_DrawACentredPlus_ThatFollowsTheButtonsState()
    {
        var root = Prefab();
        foreach (var command in new[] { "ExecuteAddAttributePoint", "ExecuteAddFocus" })
        {
            var button = root.Descendants().Single(x => (string?)x.Attribute("Command.Click") == command);
            Assert.AreEqual("true", (string?)button.Attribute("UpdateChildrenStates"), $"{command}: hover and disabled reach the strokes");
            Assert.IsFalse(button.Descendants().Any(x => x.Name.LocalName == "TextWidget"), $"{command}: no font glyph");
            AssertCentredPlus(button.Element("Children")!.Elements(), Num(button, "SuggestedWidth"), 12f);
            Assert.AreEqual(Num(button, "SuggestedWidth"), Num(button, "SuggestedHeight"), $"{command}: a square button");
        }
    }

    [DataTestMethod]
    [DataRow("@HasAttributePoints", "@UnspentAttributePoints")]
    [DataRow("@HasFocusPoints", "@UnspentCharacterPoints")]
    public void PoolValue_TurnsBrightGoldWhenPositive(string flag, string value)
    {
        var row = ById(Prefab(), "AllocationRow");
        var gold = row.Descendants().Single(x => (string?)x.Attribute("IsVisible") == flag);
        var plain = row.Descendants().Single(x => (string?)x.Attribute("IsHidden") == flag);
        Assert.AreSame(gold.Parent, plain.Parent, "twins in one place");
        Assert.IsNull(gold.Ancestors().FirstOrDefault(a => a.Attribute("DataSource") != null && a != row), "the flag binds on the screen VM");
        foreach (var (wrapper, brush) in new[] { (gold, "TAOM.Character.AllocValueReady"), (plain, "TAOM.Character.AllocValue") })
        {
            var text = wrapper.Descendants().Single(x => (string?)x.Attribute("IntText") == value);
            Assert.AreEqual("{CurrentCharacter}", (string?)text.Attribute("DataSource"));
            Assert.AreEqual(brush, (string?)text.Attribute("Brush"));
            Assert.AreEqual("true", (string?)wrapper.Attribute("DoNotAcceptEvents"), "the pool's hint stays hoverable");
        }
        var ready = Brush("TAOM.Character.AllocValueReady");
        Assert.AreEqual("#CFAE6BFF", (string?)ready.Descendants("Style").Single().Attribute("FontColor"));
        Assert.IsTrue(Num(ready, "FontSize") > Num(Brush("TAOM.Character.AllocValue"), "FontSize"), "a live pool reads larger");
    }

    [TestMethod]
    public void PlusBrush_ShowsTheGoldEdgeOnlyWhileTheNativeControlIsEnabled()
    {
        var brush = Brush("TAOM.Character.Plus");
        CollectionAssert.AreEqual(new[] { "cc_card_9", "cc_card_sel_9" }, brush.Element("Layers")!.Elements().Select(l => (string?)l.Attribute("Sprite")).ToList());
        XElement Layer(string style, string layer) => brush.Descendants("Style").Single(s => (string?)s.Attribute("Name") == style)
            .Elements("BrushLayer").Single(l => (string?)l.Attribute("Name") == layer);
        foreach (var style in new[] { "Default", "Hovered", "Pressed" })
        {
            Assert.AreEqual("true", (string?)Layer(style, "Default").Attribute("IsHidden"), style);
            Assert.IsNull(Layer(style, "Gold").Attribute("IsHidden"), style);
        }
        Assert.AreEqual("true", (string?)Layer("Disabled", "Gold").Attribute("IsHidden"), "a disabled + shows no gold");
        Assert.IsNull(Layer("Disabled", "Default").Attribute("IsHidden"));
        // The gold follows the native IsEnabled binding; the commands stay native.
        var root = Prefab();
        foreach (var (command, enabled) in new[] { ("ExecuteAddAttributePoint", "@CanAddPoint"), ("ExecuteAddFocus", "@CanAddFocus") })
            Assert.AreEqual(enabled, (string?)root.Descendants().Single(x => (string?)x.Attribute("Command.Click") == command).Attribute("IsEnabled"));
    }

    [TestMethod]
    public void OpenTier_ShowsASmallDiamondBesideTheCentredTierLabel()
    {
        var reached = ById(Prefab(), "PerkTierReached");
        var row = reached.Descendants("ListPanel").Single();
        Assert.AreEqual("Center", (string?)row.Attribute("HorizontalAlignment"));
        var slots = row.Element("Children")!.Elements().ToList();
        Assert.AreEqual(3, slots.Count, "spacer, label, diamond slot");
        Assert.AreEqual("@LevelText", (string?)slots[1].Attribute("Text"));
        Assert.AreEqual(Num(slots[0], "SuggestedWidth"), Num(slots[2], "SuggestedWidth"), "equal sides keep the label centred");
        var diamond = slots[2].Descendants().Single(x => (string?)x.Attribute("Sprite") == "fs_diamond");
        Assert.AreEqual("@IsTierOpen", (string?)diamond.Attribute("IsVisible"));
        Assert.IsTrue(Num(diamond, "SuggestedWidth") <= 12f, "a small diamond");
        Assert.IsFalse(Prefab().Descendants().Attributes().Any(a => a.Value.IndexOf("CHOOSE", StringComparison.OrdinalIgnoreCase) >= 0), "no CHOOSE label");
    }

    [TestMethod]
    public void ScreenVM_ExposesTheNotificationBindings()
    {
        foreach (var (type, property) in new[] { (typeof(CharacterAttributeTabVM), "OpenTierCount"), (typeof(CharacterAttributeTabVM), "HasOpenTiers"),
            (typeof(CharacterSkillItemVM), "OpenTierCount"), (typeof(CharacterSkillItemVM), "HasOpenTiers"), (typeof(CharacterPerkItemVM), "IsTierOpen"),
            (typeof(CharacterScreenVM), "HasAttributePoints"), (typeof(CharacterScreenVM), "HasFocusPoints") })
            Assert.IsNotNull(type.GetProperty(property), $"{type.Name}.{property}");
    }
}
