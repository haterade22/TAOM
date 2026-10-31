using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CompanionTactics;
using TAOM.Features.CompanionTactics.BattleActionBar;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Tests.Features.CompanionTactics.BattleActionBar;

/// <summary>
/// Issue #970 review fixes. A layer with default input restrictions shows the cursor and stops
/// MissionMainAgentController's attack and block (it runs only when !MissionScreen.MouseVisible), so the layer asks
/// for mouse buttons only and every non-button widget passes the click through. The view derives from a type this
/// project cannot reference, so its source is read.
/// </summary>
[TestClass]
public class BattleActionBarInputTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ViewSource => File.ReadAllText(Path.Combine(RepoRoot,
        @"Client\Features\CompanionTactics\BattleActionBar\Hooks\BattleActionBarMissionView.cs"));

    private static XElement Prefab => XDocument.Load(Path.Combine(RepoRoot,
        @"Main\_Module\GUI\PreFabs\BattleActionBar.xml")).Root!;

    [TestMethod]
    public void View_RestrictsOnlyMouseButtons_NotTheDefaultCursorAndAllInput()
    {
        var src = ViewSource;
        StringAssert.Contains(src, "SetInputRestrictions(false, InputUsageMask.MouseButtons)");
        Assert.IsFalse(src.Contains("SetInputRestrictions()"), "the default restrictions show the cursor and eat attack and block");
    }

    [TestMethod]
    public void View_HasNoNumberKeyHotkeyPolling()
    {
        var src = ViewSource;
        Assert.IsFalse(src.Contains("InputKey.D1"), "hotkeys were removed");
        Assert.IsFalse(src.Contains("HandleHotkeyInput"), "hotkeys were removed");
        Assert.IsFalse(src.Contains("_lastFormation"), "unused field");
    }

    [TestMethod]
    public void View_ClearsStancesWhenAttaching_AndDetachesWhenTheSettingTurnsOff()
    {
        var src = ViewSource;
        var attach = src.Substring(src.IndexOf("private void TryAttachLayer", StringComparison.Ordinal));
        attach = attach.Substring(0, attach.IndexOf("OnMissionScreenTick", StringComparison.Ordinal));
        StringAssert.Contains(attach, "ClearAllStances");
        StringAssert.Contains(src, "DetachLayer(");
        StringAssert.Contains(src, "!_settings.ShowBattleActionBar");
    }

    [TestMethod]
    public void Prefab_OnlyTheButtonsHitTest()
    {
        // Every element outside a ButtonWidget subtree (and outside ItemTemplate/Children grammar nodes) must pass
        // the click through: EventManager.AnyWidgetsAt hits any visible widget that accepts events, and the flag
        // does not propagate to children.
        var grammar = new[] { "Window", "Children", "ItemTemplate" };
        var offenders = Prefab.Descendants()
            .Where(e => !grammar.Contains(e.Name.LocalName))
            .Where(e => e.Name.LocalName != "ButtonWidget" && !e.Ancestors("ButtonWidget").Any())
            .Where(e => (string?)e.Attribute("DoNotAcceptEvents") != "true")
            .Select(e => e.Name.LocalName)
            .ToList();
        CollectionAssert.AreEqual(new string[0], offenders, "non-button widgets that still take clicks");
    }

    [TestMethod]
    public void Prefab_HasNoHotkeyBadge()
    {
        Assert.IsFalse(File.ReadAllText(Path.Combine(RepoRoot, @"Main\_Module\GUI\PreFabs\BattleActionBar.xml")).Contains("Hotkey"));
    }

    [TestMethod]
    public void Setting_IsRenamedAndOffByDefault()
    {
        var type = typeof(TaomSettings);
        Assert.IsNull(type.GetProperty("EnableBattleActionBar"), "the old name keeps a persisted true in TAOM.json");
        var prop = type.GetProperty("ShowBattleActionBar");
        Assert.IsNotNull(prop);
        Assert.IsFalse((bool)prop!.GetValue(new TaomSettings())!);
        Assert.IsFalse(new CompanionTacticsSettingsProvider().ShowBattleActionBar);
    }

    [TestMethod]
    public void Actions_CarryNoHotkeyLabel()
    {
        Assert.IsNull(typeof(BattleAction).GetProperty("Hotkey"));
    }

    [TestMethod]
    public void Click_AfterTheSettingTurnsOff_DoesNotRecordAStance()
    {
        var settings = Substitute.For<ICompanionTacticsSettingsProvider>();
        settings.ShowBattleActionBar.Returns(true);
        var analyzer = Substitute.For<IFormationCompositionAnalyzer>();
        analyzer.Analyze(Arg.Any<IFormationAdapter>()).Returns(new FormationComposition(false, true, false, false, false, false));
        var stances = Substitute.For<ITroopStanceManager>();
        var outcomes = Substitute.For<IOutcomeLog>();
        var sut = new BattleActionBarService(settings, analyzer, stances, Substitute.For<IModLogger>(), outcomes);
        var formation = Substitute.For<IFormationAdapter>();
        formation.FormationIndex.Returns(2);

        var brace = sut.GetActionsForFormation(formation).First(a => a.Id == "action_brace");
        settings.ShowBattleActionBar.Returns(false);
        brace.Execute();

        stances.DidNotReceiveWithAnyArgs().SetStance(default, default);
        outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    private static string Between(string src, string from, string to)
    {
        var start = src.IndexOf(from, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, from);
        var end = src.IndexOf(to, start, StringComparison.Ordinal);
        Assert.IsTrue(end > start, to);
        return src.Substring(start, end - start);
    }

    [TestMethod]
    public void View_AttachCheck_PassesOnlyForALoadedMovie()
    {
        // GauntletLayer.LoadMovie does not throw for a missing prefab, so "layer attached" alone proves nothing.
        var attach = Between(ViewSource, "private void TryAttachLayer", "public override void OnMissionScreenTick");
        StringAssert.Contains(attach, "Movie?.IsLoaded == true");
        StringAssert.Contains(attach, "BattleActionBarChecks.AttachId");
        StringAssert.Contains(attach, "\"reason=movie-not-loaded mission=\"");
        StringAssert.Contains(attach, "\"reason=exception mission=\"");
    }

    [TestMethod]
    public void View_EndOfMission_ClassifiesBeforeDetaching()
    {
        var finalize = Between(ViewSource, "public override void OnMissionScreenFinalize", "private void DetachLayer");
        var classify = finalize.IndexOf("ClassifyAtEnd(", StringComparison.Ordinal);
        Assert.IsTrue(classify >= 0, "the end-of-mission verdict is missing");
        StringAssert.Contains(finalize, "\"reason=never-attached mission=\"");
        StringAssert.Contains(finalize, "\"reason=setting-off mission=\"");
    }

    [TestMethod]
    public void View_Tick_WritesNoOutcomeLine()
    {
        // Per frame: a bool store at most, never a line.
        var tick = Between(ViewSource, "public override void OnMissionScreenTick", "public override void OnMissionScreenFinalize");
        Assert.IsFalse(tick.Contains("_outcomes"), "the tick writes an outcome");
    }

    [TestMethod]
    public void View_ResolvesTheOutcomeLog() =>
        StringAssert.Contains(ViewSource, "_outcomes = IoC.Resolve<IOutcomeLog>()");

    [TestMethod]
    public void BarVM_HandsEachButtonItsResync_SoAClickHighlightsImmediately()
    {
        var ui = Path.Combine(RepoRoot, "Main", "Features", "CompanionTactics", "BattleActionBar", "UI");
        StringAssert.Contains(File.ReadAllText(Path.Combine(ui, "BattleActionBarVM.cs")), "new ActionButtonVM(action, SyncActiveFromStance)");
        StringAssert.Contains(File.ReadAllText(Path.Combine(ui, "ActionButtonVM.cs")), "_onExecuted?.Invoke()");
    }
}
