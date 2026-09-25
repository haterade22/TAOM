using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics;
using TAOM.Features.CompanionTactics.FormationPresets;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

/// <summary>
/// The two early returns of the boundary assigner. Everything past them drives vanilla's
/// selection handlers, which need an initialized VM (in-game check). An uninitialized VM is safe
/// here: <c>IsPlayerGeneral</c> returns the plain <c>_isPlayerGeneral</c> field, false by default.
/// </summary>
[TestClass]
public class OOBCaptainAutoAssignerTests
{
    private IHeroAutoAssigner _planner = null!;
    private OOBCaptainAutoAssigner _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _planner = Substitute.For<IHeroAutoAssigner>();
        _sut = new OOBCaptainAutoAssigner(_planner,
            Substitute.For<ICompanionTacticsSettingsProvider>(), Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void AssignCaptains_NullVm_ReturnsNoneAssigned()
    {
        var result = _sut.AssignCaptains(null!);

        Assert.AreEqual(AutoAssignStatus.NoneAssigned, result.Status);
        Assert.AreEqual(0, result.AssignedCount);
        _planner.DidNotReceiveWithAnyArgs().PlanCaptains(default!, default!);
    }

    [TestMethod]
    public void AssignCaptains_PlayerNotGeneral_ReturnsNotGeneralWithoutPlanning()
    {
        var oob = (OrderOfBattleVM)FormatterServices.GetUninitializedObject(typeof(OrderOfBattleVM));

        var result = _sut.AssignCaptains(oob);

        Assert.AreEqual(AutoAssignStatus.NotGeneral, result.Status);
        Assert.AreEqual(0, result.AssignedCount);
        _planner.DidNotReceiveWithAnyArgs().PlanCaptains(
            default(IReadOnlyList<IHeroCombatAdapter>)!, default(IReadOnlyList<int>)!);
    }
}
