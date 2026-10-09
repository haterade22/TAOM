using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets;
using TAOM.Features.CompanionTactics.FormationPresets.Models;
using TAOM.Features.CompanionTactics.FormationPresets.UI;
using SaveResult = TAOM.Features.CompanionTactics.FormationPresets.Models.SaveResult;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Pins that the overlay's commands delegate to the boundary classes (ADR-002) and show the right text.
/// The OOB VM is a bare uninitialized object: its real constructor needs Game.Current, and
/// the commands only pass the reference through.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class OOBButtonsVMTests
{
    private IFormationPresetService _presets = null!;
    private IOrderOfBattleVMTracker _tracker = null!;
    private IOOBCaptainAutoAssigner _assigner = null!;
    private IOOBPresetApplier _applier = null!;
    private OOBButtonsVM _sut = null!;
    private OrderOfBattleVM _oob = null!;
    private readonly List<string> _messages = new();
    private readonly List<MultiSelectionInquiryData> _menus = new();
    private readonly List<TextInquiryData> _prompts = new();

    [TestInitialize]
    public void Setup()
    {
        _presets = Substitute.For<IFormationPresetService>();
        _presets.Presets.Returns(new List<HoNFormationPreset>());
        _tracker = Substitute.For<IOrderOfBattleVMTracker>();
        _assigner = Substitute.For<IOOBCaptainAutoAssigner>();
        _applier = Substitute.For<IOOBPresetApplier>();
        _oob = (OrderOfBattleVM)FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM));
        _oob.IsPlayerGeneral = true;
        _sut = new OOBButtonsVM(_presets, _tracker, _assigner, _applier, Substitute.For<IModLogger>());
        _messages.Clear();
        _menus.Clear();
        _prompts.Clear();
        InformationManager.DisplayMessageInternal += Capture;
        MBInformationManager.OnShowMultiSelectionInquiry += CaptureMenu;
        InformationManager.OnShowTextInquiry += CapturePrompt;
    }

    [TestCleanup]
    public void Cleanup()
    {
        InformationManager.DisplayMessageInternal -= Capture;
        MBInformationManager.OnShowMultiSelectionInquiry -= CaptureMenu;
        InformationManager.OnShowTextInquiry -= CapturePrompt;
    }

    private void Capture(InformationMessage message) => _messages.Add(message.Information);

    private void CaptureMenu(MultiSelectionInquiryData data, bool pause, bool prioritize) => _menus.Add(data);

    private void CapturePrompt(TextInquiryData data, bool pause, bool prioritize) => _prompts.Add(data);

    // Opens the preset menu and picks the entry with the given identifier, as the player would.
    private void PickMenuEntry(string identifier)
    {
        _tracker.Current.Returns(_oob);
        _sut.ExecuteManagePresets();
        var menu = _menus.Single();
        var element = menu.InquiryElements.Single(e => (string)e.Identifier == identifier);
        menu.AffirmativeAction(new List<InquiryElement> { element });
    }

    private HoNFormationPreset StoredPreset(string name = "Plan A")
    {
        var preset = new HoNFormationPreset(name);
        _presets.Presets.Returns(new List<HoNFormationPreset> { preset });
        _presets.GetPresetById(preset.Id).Returns(preset);
        return preset;
    }

    private void SaveWithName(string name)
    {
        PickMenuEntry("save");
        _prompts.Single().AffirmativeAction(name);
    }

    [TestMethod]
    public void ExecuteAssignCharacters_ScreenOpen_DelegatesToCaptainAutoAssigner()
    {
        _tracker.Current.Returns(_oob);
        _assigner.AssignCaptains(_oob).Returns(new AutoAssignResult(AutoAssignStatus.Assigned, 2));

        _sut.ExecuteAssignCharacters();

        _assigner.Received(1).AssignCaptains(_oob);
    }

    [TestMethod]
    public void ExecuteAssignCharacters_NoScreen_DoesNotCallCaptainAutoAssigner()
    {
        _tracker.Current.Returns((OrderOfBattleVM)null!);

        _sut.ExecuteAssignCharacters();

        _assigner.DidNotReceiveWithAnyArgs().AssignCaptains(default!);
    }

    [TestMethod]
    public void ExecuteAssignCharacters_NoScreen_ShowsTheNoScreenMessage()
    {
        _tracker.Current.Returns((OrderOfBattleVM)null!);

        _sut.ExecuteAssignCharacters();

        CollectionAssert.AreEqual(new[] { "No Order of Battle screen detected." }, _messages.ToArray());
    }

    // TextObject.ToString swallows a localization failure into an "Error at id" string, so a
    // delegation-only test stays green while the player reads garbage: assert the text itself.
    [TestMethod]
    [DataRow(AutoAssignStatus.Assigned, 2, "Captains assigned: 2.")]
    [DataRow(AutoAssignStatus.NotGeneral, 0, "Only the general of this battle can assign heroes.")]
    [DataRow(AutoAssignStatus.NoneAssigned, 0, "No hero suits an open captain slot.")]
    public void ExecuteAssignCharacters_EachResult_ShowsItsMessage(AutoAssignStatus status, int count, string expected)
    {
        _tracker.Current.Returns(_oob);
        _assigner.AssignCaptains(_oob).Returns(new AutoAssignResult(status, count));

        _sut.ExecuteAssignCharacters();

        CollectionAssert.AreEqual(new[] { expected }, _messages.ToArray(),
            "Shown: " + string.Join(" | ", _messages.Select(m => m ?? "<null>")));
    }

    // ---- Button texts ----

    [TestMethod]
    public void ButtonTexts_NoPresets_AreTheDefaults()
    {
        Assert.AreEqual("Assign Heroes", _sut.AssignHeroesText);
        Assert.AreEqual("Presets", _sut.PresetsButtonText);
    }

    [TestMethod]
    public void PresetsButtonText_WithPresets_ShowsTheCount()
    {
        _presets.Presets.Returns(new List<HoNFormationPreset> { new("a"), new("b") });
        var sut = new OOBButtonsVM(_presets, _tracker, _assigner, _applier, Substitute.For<IModLogger>());

        Assert.AreEqual("Presets (2)", sut.PresetsButtonText);
    }

    // ---- Menu ----

    [TestMethod]
    public void ExecuteManagePresets_PlayerIsNotTheGeneral_ShowsTheMessageAndNoMenu()
    {
        _oob.IsPlayerGeneral = false;
        _tracker.Current.Returns(_oob);

        _sut.ExecuteManagePresets();

        CollectionAssert.AreEqual(new[] { "Only the general of this battle can use presets." }, _messages.ToArray());
        Assert.AreEqual(0, _menus.Count);
    }

    [TestMethod]
    public void ExecuteManagePresets_General_OffersVanillaOkAndCancel()
    {
        _tracker.Current.Returns(_oob);

        _sut.ExecuteManagePresets();

        var menu = _menus.Single();
        Assert.AreEqual("Ok", menu.AffirmativeText);
        Assert.AreEqual("Cancel", menu.NegativeText);
    }

    [TestMethod]
    public void ExecuteManagePresets_NoScreen_ShowsTheNoScreenMessageAndNoMenu()
    {
        _tracker.Current.Returns((OrderOfBattleVM)null!);

        _sut.ExecuteManagePresets();

        CollectionAssert.AreEqual(new[] { "No Order of Battle screen detected." }, _messages.ToArray());
        Assert.AreEqual(0, _menus.Count);
    }

    [TestMethod]
    public void ExecuteManagePresets_WithAPreset_OffersSaveLoadAndDeleteWithTheName()
    {
        StoredPreset("Plan A");
        _tracker.Current.Returns(_oob);

        _sut.ExecuteManagePresets();

        var menu = _menus.Single();
        CollectionAssert.AreEqual(
            new[] { "Save Current Layout as Preset", "Load: Plan A", "Delete: Plan A" },
            menu.InquiryElements.Select(e => e.Title).ToArray());
        Assert.AreEqual("Formation Presets", menu.TitleText);
        Assert.AreEqual("Save, load or delete a layout of formation types, captains and hero troops.", menu.DescriptionText);
    }

    // A name saved by an older build never passed through SaveCurrent's clean-up; the text processor would read its
    // braces as markup and show "Load: Guard " (Codex, 2026-10-09).
    [TestMethod]
    public void ExecuteManagePresets_StoredNameWithBraces_ShowsTheNameWithoutThem()
    {
        StoredPreset("Guard {OLD}");
        _tracker.Current.Returns(_oob);

        _sut.ExecuteManagePresets();

        CollectionAssert.AreEqual(
            new[] { "Save Current Layout as Preset", "Load: Guard OLD", "Delete: Guard OLD" },
            _menus.Single().InquiryElements.Select(e => e.Title).ToArray());
    }

    // ---- Load ----

    [TestMethod]
    public void Load_Picked_AppliesThePresetToTheScreenAndShowsTheCounts()
    {
        var preset = StoredPreset("Plan A");
        _applier.Apply(_oob, preset).Returns(new PresetApplyResult(3, 2, 4, 0));

        PickMenuEntry("load:" + preset.Id);

        _applier.Received(1).Apply(_oob, preset);
        CollectionAssert.AreEqual(
            new[] { "Preset \"Plan A\" loaded. Formation types: 3, captains: 2, hero troops: 4." },
            _messages.ToArray(), "Shown: " + string.Join(" | ", _messages.Select(m => m ?? "<null>")));
    }

    [TestMethod]
    public void Load_SomeAssignmentsDoNotFit_AlsoShowsTheSkippedLine()
    {
        var preset = StoredPreset("Plan A");
        _applier.Apply(_oob, preset).Returns(new PresetApplyResult(1, 0, 0, 5));

        PickMenuEntry("load:" + preset.Id);

        CollectionAssert.AreEqual(
            new[]
            {
                "Preset \"Plan A\" loaded. Formation types: 1, captains: 0, hero troops: 0.",
                "Saved assignments that do not fit this battle: 5.",
            },
            _messages.ToArray(), "Shown: " + string.Join(" | ", _messages.Select(m => m ?? "<null>")));
    }

    // ---- Delete ----

    [TestMethod]
    public void Delete_Picked_DeletesThePresetAndSaysSo()
    {
        var preset = StoredPreset("Plan A");
        _presets.DeletePreset(preset.Id).Returns(true);

        PickMenuEntry("delete:" + preset.Id);

        CollectionAssert.AreEqual(new[] { "Preset \"Plan A\" deleted." }, _messages.ToArray());
    }

    // ---- Save ----

    [TestMethod]
    public void Save_Named_CapturesTheScreenThenSavesAndShowsTheCounts()
    {
        var captured = FormationPresetLayout.Capture("Plan A", new[]
        {
            new PresetFormationSnapshot(0, 1, "cap", new[] { "t1", "t2" }),
            new PresetFormationSnapshot(1, 2, null!, new string[0]),
        });
        _applier.Capture(_oob, "Plan A").Returns(captured);
        _presets.SavePreset(captured).Returns(SaveResult.Saved);

        SaveWithName("  Plan A  ");

        var prompt = _prompts.Single();
        Assert.AreEqual("Save Formation Preset", prompt.TitleText);
        Assert.AreEqual("Enter a name for this preset:", prompt.Text);
        Assert.AreEqual("Save", prompt.AffirmativeText);
        Received.InOrder(() =>
        {
            _applier.Capture(_oob, "Plan A");
            _presets.SavePreset(captured);
        });
        CollectionAssert.AreEqual(
            new[] { "Preset \"Plan A\" saved. Formation types: 2, captains: 1, hero troops: 2." },
            _messages.ToArray(), "Shown: " + string.Join(" | ", _messages.Select(m => m ?? "<null>")));
    }

    [TestMethod]
    public void Save_NameWithBraces_ReachesCaptureWithoutThem()
    {
        var captured = new HoNFormationPreset("Plan A");
        _applier.Capture(_oob, "Plan A").Returns(captured);
        _presets.SavePreset(captured).Returns(SaveResult.Saved);

        SaveWithName("Plan {A}");

        _applier.Received(1).Capture(_oob, "Plan A");
    }

    [TestMethod]
    [DataRow("   ")]
    [DataRow("{}")]
    public void Save_EmptyName_ShowsItsMessageAndCapturesNothing(string typed)
    {
        SaveWithName(typed);

        CollectionAssert.AreEqual(new[] { "Preset name cannot be empty." }, _messages.ToArray());
        _applier.DidNotReceiveWithAnyArgs().Capture(default!, default!);
        _presets.DidNotReceiveWithAnyArgs().SavePreset(default!);
    }

    [TestMethod]
    [DataRow(SaveResult.LimitReached, "Preset limit reached. Delete one before saving.")]
    [DataRow(SaveResult.NameInUse, "A preset named \"Plan A\" already exists.")]
    [DataRow(SaveResult.Invalid, "Could not save the preset.")]
    public void Save_Refused_ShowsTheReason(SaveResult result, string expected)
    {
        var captured = new HoNFormationPreset("Plan A");
        _applier.Capture(_oob, "Plan A").Returns(captured);
        _presets.SavePreset(captured).Returns(result);

        SaveWithName("Plan A");

        CollectionAssert.AreEqual(new[] { expected }, _messages.ToArray(),
            "Shown: " + string.Join(" | ", _messages.Select(m => m ?? "<null>")));
    }
}
