using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
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

    [TestInitialize]
    public void Setup()
    {
        _presets = Substitute.For<IFormationPresetService>();
        _presets.Presets.Returns(new List<HoNFormationPreset>());
        _tracker = Substitute.For<IOrderOfBattleVMTracker>();
        _assigner = Substitute.For<IOOBCaptainAutoAssigner>();
        _sut = new OOBButtonsVM(_presets, _tracker, _assigner, Substitute.For<IModLogger>());
    }

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
}
