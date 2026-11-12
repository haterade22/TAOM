using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.CompanionTactics.BattleActionBar;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;
using TAOM.Features.CompanionTactics.BattleActionBar.UI;

namespace TAOM.Tests.Features.CompanionTactics.BattleActionBar;

/// <summary>
/// The bar shows only when the selected formation has an action: an empty bar is a dark box holding nothing but
/// the formation name (#1119). ViewModel and MBBindingList run engine code, so the class needs the game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BattleActionBarVMTests
{
    private IBattleActionBarService _service = null!;
    private BattleActionBarVM _sut = null!;

    // RepresentativeClass is an auto-property, so an uninitialized Formation reads it safely.
    private static Formation BareFormation() => (Formation)FormatterServices.GetUninitializedObject(typeof(Formation));

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IBattleActionBarService>();
        _sut = new BattleActionBarVM(_service, Substitute.For<ITroopStanceManager>());
    }

    [TestMethod]
    public void UpdateForFormation_FormationWithNoActions_HidesTheBar()
    {
        var adapter = Substitute.For<IFormationAdapter>();
        _service.GetActionsForFormation(adapter).Returns(new List<BattleAction>());

        _sut.UpdateForFormation(BareFormation(), adapter);

        Assert.IsFalse(_sut.IsVisible);
        Assert.AreEqual(0, _sut.ActionButtons.Count);
        Assert.AreEqual(string.Empty, _sut.FormationName);
    }

    [TestMethod]
    public void UpdateForFormation_FormationWithAnAction_ShowsTheBarWithItsButton()
    {
        var adapter = Substitute.For<IFormationAdapter>();
        _service.GetActionsForFormation(adapter).Returns(new List<BattleAction>
        {
            new("shield_wall", "Shield Wall", default, () => { }),
        });

        _sut.UpdateForFormation(BareFormation(), adapter);

        Assert.IsTrue(_sut.IsVisible);
        Assert.AreEqual(1, _sut.ActionButtons.Count);
    }

    [TestMethod]
    public void UpdateForFormation_NoFormation_HidesTheBarWithoutAskingForActions()
    {
        _sut.UpdateForFormation(null, null);

        Assert.IsFalse(_sut.IsVisible);
        _service.DidNotReceiveWithAnyArgs().GetActionsForFormation(default!);
    }
}
