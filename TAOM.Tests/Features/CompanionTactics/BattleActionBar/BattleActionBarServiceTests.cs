using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics;
using TAOM.Features.CompanionTactics.BattleActionBar;
using TAOM.Features.CompanionTactics.BattleActionBar.Models;

namespace TAOM.Tests.Features.CompanionTactics.BattleActionBar;

[TestClass]
public class BattleActionBarServiceTests
{
    private ICompanionTacticsSettingsProvider _settings = null!;
    private IFormationCompositionAnalyzer _analyzer = null!;
    private ITroopStanceManager _stances = null!;
    private IModLogger _logger = null!;
    private BattleActionBarService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICompanionTacticsSettingsProvider>();
        _settings.EnableBattleActionBar.Returns(true);
        _settings.EnableVolleyFire.Returns(true);

        _analyzer = Substitute.For<IFormationCompositionAnalyzer>();
        _stances = Substitute.For<ITroopStanceManager>();
        _logger = Substitute.For<IModLogger>();

        _sut = new BattleActionBarService(_settings, _analyzer, _stances, _logger);
    }

    private static IFormationAdapter MakeFormation(int formationIndex = 0)
    {
        var f = Substitute.For<IFormationAdapter>();
        f.FormationIndex.Returns(formationIndex);
        f.CountOfUnits.Returns(20);
        return f;
    }

    private void SetComposition(bool ranged = false, bool polearm = false, bool shield = false, bool cavalry = false)
    {
        _analyzer.Analyze(Arg.Any<IFormationAdapter>())
            .Returns(new FormationComposition(ranged, polearm, shield, cavalry, false, false));
    }

    [TestMethod]
    public void GetActionsForFormation_NullFormation_ReturnsEmpty()
    {
        var actions = _sut.GetActionsForFormation(null);

        Assert.AreEqual(0, actions.Count);
    }

    [TestMethod]
    public void GetActionsForFormation_FeatureDisabled_ReturnsEmpty()
    {
        _settings.EnableBattleActionBar.Returns(false);
        SetComposition(ranged: true);
        var f = MakeFormation();

        var actions = _sut.GetActionsForFormation(f);

        Assert.AreEqual(0, actions.Count);
    }

    [TestMethod]
    public void GetActionsForFormation_RangedComposition_HasRangedActions()
    {
        SetComposition(ranged: true);
        var f = MakeFormation();

        var actions = _sut.GetActionsForFormation(f);

        Assert.IsTrue(actions.Any(a => a.Category == ActionCategory.Ranged));
    }

    [TestMethod]
    public void GetActionsForFormation_RangedAndVolleyEnabled_VolleyButtonPresent()
    {
        SetComposition(ranged: true);
        var f = MakeFormation();

        var actions = _sut.GetActionsForFormation(f);

        Assert.IsTrue(actions.Any(a => a.Id == "action_volley"));
    }

    [TestMethod]
    public void GetActionsForFormation_RangedAndVolleyDisabled_NoVolleyButton()
    {
        _settings.EnableVolleyFire.Returns(false);
        SetComposition(ranged: true);
        var f = MakeFormation();

        var actions = _sut.GetActionsForFormation(f);

        Assert.IsFalse(actions.Any(a => a.Id == "action_volley"),
            "Volley should be omitted when EnableVolleyFire is false");
        Assert.IsTrue(actions.Any(a => a.Id == "action_hold_fire"),
            "Other ranged actions should remain");
    }

    [TestMethod]
    public void GetActionsForFormation_NoComposition_NoButtons()
    {
        SetComposition();
        var f = MakeFormation();

        var actions = _sut.GetActionsForFormation(f);

        Assert.AreEqual(0, actions.Count);
    }

    [TestMethod]
    public void GetActionsForFormation_PolearmComposition_HasPolearmCategoryActions()
    {
        SetComposition(polearm: true);
        var f = MakeFormation();

        var actions = _sut.GetActionsForFormation(f);

        Assert.IsTrue(actions.Any(a => a.Category == ActionCategory.Polearm));
    }

    [TestMethod]
    public void GetActionsForFormation_ShieldComposition_HasShieldCategoryActions()
    {
        SetComposition(shield: true);
        var f = MakeFormation();

        var actions = _sut.GetActionsForFormation(f);

        Assert.IsTrue(actions.Any(a => a.Category == ActionCategory.Shield));
    }

    [TestMethod]
    public void GetActionsForFormation_CavalryComposition_HasCavalryCategoryActions()
    {
        SetComposition(cavalry: true);
        var f = MakeFormation();

        var actions = _sut.GetActionsForFormation(f);

        Assert.IsTrue(actions.Any(a => a.Category == ActionCategory.Cavalry));
    }

    [TestMethod]
    public void GetActionsForFormation_PolearmExecute_SetsBracedStance()
    {
        SetComposition(polearm: true);
        var f = MakeFormation(formationIndex: 7);

        var actions = _sut.GetActionsForFormation(f);
        var braceAction = actions.First(a => a.Id == "action_brace");
        braceAction.Execute();

        _stances.Received(1).SetStance(7, TroopStance.BracedForCavalry);
    }
}
