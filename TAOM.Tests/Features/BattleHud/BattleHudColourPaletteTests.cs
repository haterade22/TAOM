using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleHud.UI;
using TAOM.Features.BattleHud.Widgets;
using TAOM.Features.CareerSystem.UI;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BattleHud;

// The battle HUD colour option (Accessibility group): Default keeps the stone rings exactly as they were;
// the three colour vision choices draw the health, horse and shield rings flat in the agreed palettes
// on a dark solid track. What no test shows is the look in game: that is the preview and the in-game check.
[TestClass]
public class BattleHudColourPaletteTests
{
    private const string Widget = "Client/Features/BattleHud/Widgets/TaomBattleHudRingWidget.cs";
    private const string Mixin = "Client/Features/BattleHud/UI/MissionAgentStatusBattleHudMixin.cs";

    // ── The mode: stable indices, anything unknown is Default ────────────────────────────────────

    [TestMethod]
    public void Choices_AreTheFourModesInTheirPersistedOrder()
    {
        CollectionAssert.AreEqual(
            new[] { "Default", "Deuteranopia (red-green)", "Protanopia (red-green)", "Tritanopia (blue-yellow)" },
            BattleHudColourPalette.Choices, "MCM persists the index: never reorder or remove a choice");
        Assert.AreEqual(0, (int)BattleHudColourMode.Default);
        Assert.AreEqual(1, (int)BattleHudColourMode.Deuteranopia);
        Assert.AreEqual(2, (int)BattleHudColourMode.Protanopia);
        Assert.AreEqual(3, (int)BattleHudColourMode.Tritanopia);
    }

    [DataTestMethod]
    [DataRow(0, BattleHudColourMode.Default)]
    [DataRow(1, BattleHudColourMode.Deuteranopia)]
    [DataRow(2, BattleHudColourMode.Protanopia)]
    [DataRow(3, BattleHudColourMode.Tritanopia)]
    public void Resolve_KnownIndex_ReturnsItsMode(int index, BattleHudColourMode expected)
        => Assert.AreEqual(expected, BattleHudColourPalette.Resolve(index));

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(4)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void Resolve_UnknownIndex_ReturnsDefault(int index)
        => Assert.AreEqual(BattleHudColourMode.Default, BattleHudColourPalette.Resolve(index));

    [TestMethod]
    public void Resolve_NoIndex_ReturnsDefault()
        => Assert.AreEqual(BattleHudColourMode.Default, BattleHudColourPalette.Resolve(null));

    // ── The palettes ─────────────────────────────────────────────────────────────────────────────

    [TestMethod]
    public void Accessible_Default_IsNull_SoTheStoneRingsDrawUnchanged()
        => Assert.IsNull(BattleHudColourPalette.Accessible(BattleHudColourMode.Default));

    [TestMethod]
    public void Accessible_UnknownMode_IsNull()
        => Assert.IsNull(BattleHudColourPalette.Accessible((BattleHudColourMode)99));

    [DataTestMethod]
    [DataRow(BattleHudColourMode.Deuteranopia, 0xF0E442u, 0x56B4E9u, 0xF5F5F5u)]
    [DataRow(BattleHudColourMode.Protanopia, 0xE6B450u, 0x66C2FFu, 0xF5F5F5u)]
    [DataRow(BattleHudColourMode.Tritanopia, 0xF28EABu, 0x55D6C2u, 0xF5F5F5u)]
    public void Accessible_ColourVisionMode_HasTheAgreedHealthHorseShieldColours(BattleHudColourMode mode, uint health, uint horse, uint shield)
    {
        var palette = BattleHudColourPalette.Accessible(mode);

        Assert.IsTrue(palette.HasValue, mode.ToString());
        Assert.AreEqual(health, palette!.Value.Health, "health");
        Assert.AreEqual(horse, palette.Value.Horse, "horse");
        Assert.AreEqual(shield, palette.Value.Shield, "shield");
    }

    [DataTestMethod]
    [DataRow(BattleHudColourMode.Deuteranopia)]
    [DataRow(BattleHudColourMode.Protanopia)]
    [DataRow(BattleHudColourMode.Tritanopia)]
    public void Accessible_EveryFill_StandsAtLeastThreeToOneAgainstItsTrack(BattleHudColourMode mode)
    {
        // WCAG 2.x contrast ratio, the 3:1 floor for graphical objects. A check of the sRGB values only:
        // it says nothing about any one player's sight.
        var palette = BattleHudColourPalette.Accessible(mode)!.Value;

        Assert.IsTrue(Luminance(palette.Track) < 0.05, "the track is dark");
        foreach (var fill in new[] { palette.Health, palette.Horse, palette.Shield })
            Assert.IsTrue(Contrast(fill, palette.Track) >= 3.0, $"{mode} {fill:X6} on {palette.Track:X6}: {Contrast(fill, palette.Track):F2}");
    }

    [TestMethod]
    public void Accessible_AllModes_ShareOneDarkTrack()
    {
        var tracks = new[] { BattleHudColourMode.Deuteranopia, BattleHudColourMode.Protanopia, BattleHudColourMode.Tritanopia }
            .Select(m => BattleHudColourPalette.Accessible(m)!.Value.Track).Distinct().ToList();

        Assert.AreEqual(1, tracks.Count);
    }

    // ── The setting ──────────────────────────────────────────────────────────────────────────────

    private static PropertyInfo SettingProperty()
    {
        var property = typeof(TaomSettings).GetProperty("BattleHudColourMode", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(property, "TaomSettings.BattleHudColourMode");
        return property!;
    }

    [TestMethod]
    public void Setting_IsAFourChoiceDropdownDefaultingToDefault()
    {
        var dropdown = new TaomSettings().BattleHudColourMode;

        CollectionAssert.AreEqual(BattleHudColourPalette.Choices, dropdown.ToArray());
        Assert.AreEqual(0, dropdown.SelectedIndex);
        Assert.AreEqual(typeof(Dropdown<string>), SettingProperty().PropertyType);
    }

    [TestMethod]
    public void Setting_SitsInTheAccessibilityGroup_WithNoRestart()
    {
        var property = SettingProperty();
        var group = property.GetCustomAttribute<SettingPropertyGroupAttribute>();
        var dropdown = property.GetCustomAttribute<SettingPropertyDropdownAttribute>();

        Assert.AreEqual("Accessibility", group?.GroupName);
        Assert.IsNotNull(dropdown);
        Assert.IsFalse(dropdown!.RequireRestart);
    }

    [TestMethod]
    public void Setting_HintLimitsTheScopeToHealthMountAndShield()
    {
        var hint = SettingProperty().GetCustomAttribute<SettingPropertyDropdownAttribute>()!.HintText;

        StringAssert.Contains(hint, "player health");
        StringAssert.Contains(hint, "mount health");
        StringAssert.Contains(hint, "shield");
        StringAssert.Matches(hint, new Regex("^\\{=taom_[a-z0-9_]+\\}"), "localized");
    }

    // ── The mixin reads the setting fresh and binds the mode ─────────────────────────────────────

    [TestMethod]
    public void ColourModeIndex_NoSettings_IsDefault()
        => Assert.AreEqual(0, MissionAgentStatusBattleHudMixin.ColourModeIndex(null));

    [TestMethod]
    public void ColourModeIndex_NoDropdown_IsDefault()
        => Assert.AreEqual(0, MissionAgentStatusBattleHudMixin.ColourModeIndex(new TaomSettings { BattleHudColourMode = null! }));

    [TestMethod]
    public void ColourModeIndex_FreshSettings_IsDefault()
        => Assert.AreEqual(0, MissionAgentStatusBattleHudMixin.ColourModeIndex(new TaomSettings()));

    [TestMethod]
    public void ColourModeIndex_SelectionChanged_FollowsTheNewSelection()
    {
        var settings = new TaomSettings();

        settings.BattleHudColourMode.SelectedIndex = 3;
        Assert.AreEqual(3, MissionAgentStatusBattleHudMixin.ColourModeIndex(settings));

        settings.BattleHudColourMode.SelectedIndex = 1;
        Assert.AreEqual(1, MissionAgentStatusBattleHudMixin.ColourModeIndex(settings));

        settings.BattleHudColourMode = new Dropdown<string>(BattleHudColourPalette.Choices, 2);
        Assert.AreEqual(2, MissionAgentStatusBattleHudMixin.ColourModeIndex(settings), "a replaced dropdown is read, not a held one");
    }

    [TestMethod]
    public void Mixin_ExposesTheModeAsADataSourceProperty()
    {
        var property = typeof(MissionAgentStatusBattleHudMixin).GetProperty("HudColourMode", BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(property);
        Assert.AreEqual(typeof(int), property!.PropertyType);
        Assert.IsTrue(CustomAttributeData.GetCustomAttributes(property).Any(a => a.AttributeType == typeof(TaleWorlds.Library.DataSourceProperty)));
    }

    [TestMethod]
    public void Mixin_HoldsNoSettingsInstance()
    {
        // MCM can hand out a new instance (a preset, a reset); a held one would pin the old choice.
        var fields = typeof(MissionAgentStatusBattleHudMixin)
            .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        foreach (var field in fields)
        {
            Assert.IsFalse(typeof(TaomSettings).IsAssignableFrom(field.FieldType), $"{field.Name} holds the settings");
            Assert.IsFalse(field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Dropdown<>), $"{field.Name} holds a dropdown");
        }
    }

    [TestMethod]
    public void Mixin_RefreshReadsTheLiveSettingsIntoTheMode()
    {
        var src = RepoPaths.ReadSource(Mixin, stripComments: true);
        var refresh = src.Substring(src.IndexOf("public override void OnRefresh()", StringComparison.Ordinal));
        refresh = refresh.Substring(0, refresh.IndexOf("private static WeaponClass?", StringComparison.Ordinal));

        StringAssert.Contains(refresh, "HudColourMode = ColourModeIndex(TaomSettings.Instance);");
    }

    // ── The markup binds it, and the widget draws from it ────────────────────────────────────────

    [TestMethod]
    public void RingMarkup_BindsTheModeFromTheMixin()
    {
        var rings = new CareerEnergyBarPrefab().GetDocument().DocumentElement!.SelectSingleNode("Children/TaomBattleHudRingWidget") as System.Xml.XmlElement;

        Assert.IsNotNull(rings);
        Assert.AreEqual("@HudColourMode", rings!.GetAttribute("ColourMode"));
    }

    [TestMethod]
    public void Widget_HasAnIntColourModeProperty()
    {
        var property = typeof(TaomBattleHudRingWidget).GetProperty("ColourMode", BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(property);
        Assert.AreEqual(typeof(int), property!.PropertyType);
        Assert.IsTrue(property.CanWrite);
    }

    [TestMethod]
    public void Widget_NamesNoSettingsOrContainer_SoThePreviewCompilesIt()
    {
        var src = RepoPaths.ReadSource(Widget, stripComments: true);

        foreach (var banned in new[] { "TaomSettings", "IoC", "MCM" })
            Assert.IsFalse(src.Contains(banned), $"the widget names {banned}");
    }

    [TestMethod]
    public void Widget_Render_DrawsAccessibleBandsFlat_AndKeepsTheCentreOnTheStone()
    {
        var src = Regex.Replace(RepoPaths.ReadSource(Widget, stripComments: true), @"\s+", " ");

        StringAssert.Contains(src, "BattleHudColourPalette.Accessible(BattleHudColourPalette.Resolve(ColourMode))");
        StringAssert.Contains(src, "var bandStone = palette.HasValue ? null : stone;", "accessible bands skip the stone and its gain");
        Assert.AreEqual(3, Regex.Matches(src, @"Band\(drawContext, band, bandStone,").Count, "all three bands use the chosen paint");
        StringAssert.Contains(src, "CentreFill(drawContext, Textured(drawContext, circle, stone, AbilityColour(), alpha, 0f, 1f), stone,",
            "the ability centre keeps its stone");
    }

    private static double Contrast(uint a, uint b)
    {
        var la = Luminance(a);
        var lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(uint rgb)
        => 0.2126 * Linear((rgb >> 16) & 0xFF) + 0.7152 * Linear((rgb >> 8) & 0xFF) + 0.0722 * Linear(rgb & 0xFF);

    private static double Linear(uint channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
