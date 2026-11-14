using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Client.Features.EventWindow;

namespace TAOM.Tests.Features.EventWindow;

/// <summary>
/// The story event window (TaomEventWindow.xml), shared by the War Chronicle and the Realms coronation: one window at a
/// time, the oldest first, and never opened outside a game; the prefab's bindings against <see cref="EventWindowVM"/>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class EventWindowTests
{
    private static readonly string Prefab = Path.GetFullPath(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module", "GUI", "PreFabs", "EventWindow", "TaomEventWindow.xml"));

    [TestInitialize]
    [TestCleanup]
    public void Reset() => EventWindows.Clear();

    private static TAOM.Adapters.EventWindow Window(string heading = "The Bridge of Khazad-dum") =>
        new TAOM.Adapters.EventWindow(heading, "The Grey Pilgrim falls.", null, "Continue");

    [TestMethod]
    public void TheAdapter_QueuesTheWindow_AndCountsAsOpenUntilItIsGone()
    {
        IEventWindowAdapter adapter = new EventWindowAdapter();
        Assert.IsFalse(adapter.IsOpen);

        Assert.IsTrue(adapter.Show(Window()), "a client always has a window to open");

        Assert.IsTrue(adapter.IsOpen, "a waiting window holds the chronicle's next announcement back");
        EventWindows.Clear();
        Assert.IsFalse(adapter.IsOpen);
    }

    [TestMethod]
    public void OutsideAGame_AWaitingWindowNeverOpens()
    {
        EventWindows.Enqueue(Window());

        EventWindows.Tick();

        Assert.IsTrue(EventWindows.IsOpen, "still waiting: there is no game to show it in");
    }

    [TestMethod]
    public void TheCoreDefault_HasNoWindow_SoTheChronicleFallsBackToItsInquiry()
    {
        IEventWindowAdapter adapter = new NullEventWindowAdapter();

        Assert.IsFalse(adapter.Show(Window()));
        Assert.IsFalse(adapter.IsOpen);
    }

    [TestMethod]
    public void TheWindow_TreatsBlankImageAndBannerAsNone_AndHasNoLinesByDefault()
    {
        var window = new TAOM.Adapters.EventWindow("Heading", "Text", " ", "Continue", "");

        Assert.IsNull(window.Image);
        Assert.IsNull(window.BannerCode);
        Assert.AreEqual(0, window.Lines.Count);
    }

    private static bool IsBound(Type type, string property) =>
        type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
            ?.GetCustomAttribute<DataSourceProperty>() != null;

    /// <summary>The binding scope of an element: the nearest DataSource or item template above it (its own included).</summary>
    private static Type ScopeOf(XElement element)
    {
        for (var e = element; e != null; e = e.Parent)
        {
            if ((string?)e.Attribute("DataSource") == "{Banner}")
                return typeof(BannerImageIdentifierVM);
            if (e.Parent?.Name.LocalName == "ItemTemplate")
                return (string?)e.Parent.Parent?.Attribute("DataSource") == "{Effects}" ? typeof(EventWindowEffectRowVM) : typeof(EventWindowLineVM);
        }

        return typeof(EventWindowVM);
    }

    [TestMethod]
    public void ThePrefab_BindsOnlyPropertiesOfItsScope_AndItsCommandsExist()
    {
        var prefab = XDocument.Load(Prefab);
        var missing = (
            from element in prefab.Descendants()
            from attribute in element.Attributes()
            let value = attribute.Value
            where value.StartsWith("@", StringComparison.Ordinal)
            let scope = ScopeOf(element)
            where !IsBound(scope, value.Substring(1))
            select $"{element.Name.LocalName} {attribute.Name}={value} ({scope.Name})").ToList();
        var commands = (
            from attribute in prefab.Descendants().Attributes()
            where attribute.Name.LocalName.StartsWith("Command.", StringComparison.Ordinal)
            where typeof(EventWindowVM).GetMethod(attribute.Value, BindingFlags.Public | BindingFlags.Instance) == null
            select attribute.Value).ToList();

        Assert.AreEqual(0, missing.Count, string.Join("\n", missing));
        Assert.AreEqual(0, commands.Count, string.Join("\n", commands));
        Assert.IsTrue(prefab.Descendants().Any(e => (string?)e.Attribute("Text") == "@Heading"), "the heading is the event's");
        Assert.IsTrue(prefab.Descendants().Any(e => e.Name.LocalName == "EncyclopediaPictureWidget"
                && (string?)e.Attribute("PictureFile") == "@ImageFile" && (string?)e.Attribute("FrameMask") == "BlankWhiteSquare"),
            "the illustration is the event's loose picture, covered by its mask so it never stretches");
    }

    [TestMethod]
    public void ThePrefab_IsTheApprovedStonePanel_WithCategoryAndEffects_AndNoVanillaFrame()
    {
        var prefab = XDocument.Load(Prefab);
        var names = prefab.Descendants().Select(e => e.Name.LocalName).ToList();

        Assert.IsTrue(prefab.Descendants().Any(e => e.Name.LocalName == "EncyclopediaStonePanelWidget"
            && (string?)e.Attribute("Sprite") == "ec_panel_9" && (string?)e.Attribute("SuggestedWidth") == "690"
            && (string?)e.Attribute("SuggestedHeight") == "432"), "the 690x432 stone panel with the gold rim");
        Assert.IsTrue(prefab.Descendants().Any(e => (string?)e.Attribute("SuggestedWidth") == "270" && (string?)e.Attribute("SuggestedHeight") == "360"
                && (string?)e.Attribute("VerticalAlignment") == "Top"),
            "the 3:4 portrait, level with the category at the top of the text column");
        Assert.IsFalse(names.Any(n => n == "ScrollablePanel" || n == "ScrollbarWidget"), "the window does not scroll");
        Assert.IsFalse(prefab.Descendants().Any(e => (string?)e.Attribute("Text") == "@EffectsNote"), "the effects note is not shown");
        var close = prefab.Descendants().Single(e => (string?)e.Attribute("Id") == "ClosePopupWidget");
        Assert.AreEqual("TAOM.Encyclopedia.Close", (string?)close.Attribute("Brush"),
            "the close button is the shared TAOM close (ec_close), as on the other TAOM screens");
        Assert.IsFalse(close.Descendants().Any(e => e.Name.LocalName == "TextWidget"), "only the shared close sprite draws, no text X");
        Assert.IsTrue(prefab.Descendants().Any(e => (string?)e.Attribute("Text") == "@Category"));
        Assert.IsTrue(prefab.Descendants().Any(e => (string?)e.Attribute("DataSource") == "{Effects}"));
        Assert.IsTrue(prefab.Descendants().Any(e => (string?)e.Attribute("DataSource") == "{Lines}"), "the coronation's lines stay");
        CollectionAssert.DoesNotContain(names, "Standard.Background");
        Assert.IsFalse(prefab.Descendants().Any(e => ((string?)e.Attribute("Sprite"))?.Contains("tabbar") == true), "no tab bar");
        Assert.IsFalse(prefab.Descendants().Any(e => ((string?)e.Attribute("Brush"))?.StartsWith("Frame1", StringComparison.Ordinal) == true), "no vanilla frame");
    }

    [TestMethod]
    public void TheWindow_DropsAnEffectsListWithoutRows_AndABlankCategory()
    {
        var window = new TAOM.Adapters.EventWindow("Heading", "Text", null, "Continue", category: " ",
            effects: new EventWindowEffects(new EventWindowEffectRow[0], "For 15 days", "note"));

        Assert.IsNull(window.Category);
        Assert.IsNull(window.Effects);
    }

    [TestMethod]
    public void TheVM_ShowsTheCategoryAndTheEffectRows_WithTheSharedDurationAndNote()
    {
        var window = new TAOM.Adapters.EventWindow("The Black Gate", "The Captains of the West march.", null, "Continue",
            category: "War Chronicle",
            effects: new EventWindowEffects(new[]
            {
                new EventWindowEffectRow("Free Peoples", "Volunteer recruitment rate", "+10%"),
                new EventWindowEffectRow("Umbar", "Additional escape roll", "+20%"),
            }, "For 15 days", "Your own clan is never affected."));

        var vm = new EventWindowVM(window, () => { }, Path.GetTempPath());

        Assert.AreEqual("War Chronicle", vm.Category);
        Assert.IsTrue(vm.HasCategory);
        Assert.IsTrue(vm.HasEffects);
        Assert.AreEqual(2, vm.Effects.Count);
        Assert.AreEqual("Umbar", vm.Effects[1].Target);
        Assert.AreEqual("+20%", vm.Effects[1].Value);
        Assert.IsFalse(vm.Effects[0].HasDuration);
        Assert.AreEqual("For 15 days", vm.EffectsDuration);
        Assert.IsTrue(vm.HasEffectsNote);
        Assert.IsFalse(string.IsNullOrEmpty(vm.EffectsHeading));
    }

    [TestMethod]
    public void TheVM_PerRowDurations_ShowOnTheirRows()
    {
        var window = new TAOM.Adapters.EventWindow("H", "T", null, "Continue", effects: new EventWindowEffects(new[]
        {
            new EventWindowEffectRow("Rohan", "Volunteer recruitment rate", "+10%", "For 15 days"),
            new EventWindowEffectRow("Isengard", "Volunteer recruitment rate", "-15%", "For 30 days"),
        }));

        var vm = new EventWindowVM(window, () => { }, Path.GetTempPath());

        Assert.IsFalse(vm.HasEffectsDuration);
        Assert.IsTrue(vm.Effects[1].HasDuration);
        Assert.AreEqual("For 30 days", vm.Effects[1].Duration);
    }

    [TestMethod]
    public void TheVM_CoronationWithoutEffects_KeepsItsLinesAndHidesTheEffects()
    {
        var window = new TAOM.Adapters.EventWindow("Long live the King", "He is crowned.", null, "Long live the King", null,
            new[] { "Minas Tirith passes to him", "" }, "Coronation");

        var vm = new EventWindowVM(window, () => { }, Path.GetTempPath());

        Assert.AreEqual("Coronation", vm.Category);
        Assert.IsTrue(vm.HasLines);
        Assert.AreEqual("Minas Tirith passes to him", vm.Lines.Single().Text);
        Assert.IsFalse(vm.HasEffects);
        Assert.AreEqual(string.Empty, vm.EffectsHeading);
        Assert.IsFalse(vm.HasEffectsDuration);
        Assert.IsFalse(vm.HasEffectsNote);
    }
}
