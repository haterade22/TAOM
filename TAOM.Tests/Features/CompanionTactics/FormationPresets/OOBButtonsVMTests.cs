using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets;
using TAOM.Features.CompanionTactics.FormationPresets.Models;
using TAOM.Features.CompanionTactics.FormationPresets.UI;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Pins that the overlay's Assign Heroes command delegates to the boundary assigner (ADR-002).
/// The OOB VM is a bare uninitialized object: its real constructor needs Game.Current, and
/// the command only passes the reference through.
/// </summary>
[TestClass]
public class OOBButtonsVMTests
{
    private IFormationPresetService _presets = null!;
    private IOrderOfBattleVMTracker _tracker = null!;
    private IOOBCaptainAutoAssigner _assigner = null!;
    private OOBButtonsVM _sut = null!;
    private readonly List<string> _messages = new();

    [TestInitialize]
    public void Setup()
    {
        _presets = Substitute.For<IFormationPresetService>();
        _presets.Presets.Returns(new List<HoNFormationPreset>());
        _tracker = Substitute.For<IOrderOfBattleVMTracker>();
        _assigner = Substitute.For<IOOBCaptainAutoAssigner>();
        _sut = new OOBButtonsVM(_presets, _tracker, _assigner, Substitute.For<IModLogger>());
        _messages.Clear();
        InformationManager.DisplayMessageInternal += Capture;
    }

    [TestCleanup]
    public void Cleanup() => InformationManager.DisplayMessageInternal -= Capture;

    private void Capture(InformationMessage message) => _messages.Add(message.Information);

    [TestMethod]
    public void ExecuteAssignCharacters_ScreenOpen_DelegatesToCaptainAutoAssigner()
    {
        var oob = (OrderOfBattleVM)FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM));
        _tracker.Current.Returns(oob);
        _assigner.AssignCaptains(oob).Returns(new AutoAssignResult(AutoAssignStatus.Assigned, 2));

        _sut.ExecuteAssignCharacters();

        _assigner.Received(1).AssignCaptains(oob);
    }

    [TestMethod]
    public void ExecuteAssignCharacters_NoScreen_DoesNotCallCaptainAutoAssigner()
    {
        _tracker.Current.Returns((OrderOfBattleVM)null!);

        _sut.ExecuteAssignCharacters();

        _assigner.DidNotReceiveWithAnyArgs().AssignCaptains(default!);
    }

    // TextObject.ToString swallows a localization failure into an "Error at id" string, so a
    // delegation-only test stays green while the player reads garbage: assert the text itself.
    [TestMethod]
    [DataRow(AutoAssignStatus.Assigned, 2, "Captains assigned: 2.")]
    [DataRow(AutoAssignStatus.NotGeneral, 0, "Only the general of this battle can assign heroes.")]
    [DataRow(AutoAssignStatus.NoneAssigned, 0, "No hero suits an open captain slot.")]
    public void ExecuteAssignCharacters_EachResult_ShowsItsMessage(AutoAssignStatus status, int count, string expected)
    {
        var oob = (OrderOfBattleVM)FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM));
        _tracker.Current.Returns(oob);
        _assigner.AssignCaptains(oob).Returns(new AutoAssignResult(status, count));

        _sut.ExecuteAssignCharacters();

        CollectionAssert.AreEqual(new[] { expected }, _messages.ToArray(),
            "Shown: " + string.Join(" | ", _messages.Select(m => m ?? "<null>")));
    }
}
