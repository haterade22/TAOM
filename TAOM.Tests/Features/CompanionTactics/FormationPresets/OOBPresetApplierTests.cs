using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics;
using TAOM.Features.CompanionTactics.FormationPresets;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

/// <summary>
/// The early returns of the boundary applier. Everything past them drives vanilla's selection handlers, which need
/// an initialized VM (in-game check). An uninitialized VM is safe here: <c>IsPlayerGeneral</c> returns the plain
/// <c>_isPlayerGeneral</c> field, false by default.
/// </summary>
[TestClass]
public class OOBPresetApplierTests
{
    private OOBPresetApplier _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _sut = new OOBPresetApplier(Substitute.For<ICompanionTacticsSettingsProvider>(), Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void Apply_NullVm_AppliesNothing()
    {
        var result = _sut.Apply(null!, new HoNFormationPreset("p"));

        Assert.AreEqual(new PresetApplyResult(0, 0, 0, 0), result);
    }

    [TestMethod]
    public void Apply_NullPreset_AppliesNothing()
    {
        var oob = (OrderOfBattleVM)FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM));

        var result = _sut.Apply(oob, null!);

        Assert.AreEqual(new PresetApplyResult(0, 0, 0, 0), result);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Apply_PlayerNotGeneral_AppliesNothing()
    {
        var oob = (OrderOfBattleVM)FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM));
        var preset = new HoNFormationPreset("p");
        preset.FormationClasses[0] = 1;
        preset.HeroFormationAssignments["h"] = 0;

        var result = _sut.Apply(oob, preset);

        Assert.AreEqual(new PresetApplyResult(0, 0, 0, 0), result);
    }
}
