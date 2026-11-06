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
    private IOutcomeLog _outcomes = null!;
    private BattleActionBarService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ICompanionTacticsSettingsProvider>();
        _settings.ShowBattleActionBar.Returns(true);
        _settings.EnableVolleyFire.Returns(true);

        _analyzer = Substitute.For<IFormationCompositionAnalyzer>();
        _stances = Substitute.For<ITroopStanceManager>();
        _logger = Substitute.For<IModLogger>();
        _outcomes = Substitute.For<IOutcomeLog>();

        _sut = new BattleActionBarService(_settings, _analyzer, _stances, _logger, _outcomes);
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
        _settings.ShowBattleActionBar.Returns(false);
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

    [DataTestMethod]
    [DataRow("action_brace", TroopStance.BracedForCavalry)]
    [DataRow("action_pike_wall", TroopStance.PikeWall)]
    [DataRow("action_testudo", TroopStance.Testudo)]
    [DataRow("action_charge", TroopStance.LineCharge)]
    [DataRow("action_skirmish", TroopStance.Skirmish)]
    public void EachStanceAction_PassesTheStanceCheck_AfterSetStance(string actionId, TroopStance stance)
    {
        SetComposition(polearm: true, shield: true, cavalry: true);

        _sut.GetActionsForFormation(MakeFormation(formationIndex: 3)).First(a => a.Id == actionId).Execute();

        Received.InOrder(() =>
        {
            _stances.SetStance(3, stance);
            _outcomes.Pass("taom.battle-action-bar.stance", stance.ToString(), $"stance={stance} formation=3");
        });
    }

    [TestMethod]
    public void DisplayOnlyActions_PassNothing()
    {
        SetComposition(ranged: true, shield: true);
        var actions = _sut.GetActionsForFormation(MakeFormation());

        foreach (var id in new[] { "action_hold_fire", "action_free_fire", "action_volley", "action_shield_wall" })
            actions.First(a => a.Id == id).Execute();

        _outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    [TestMethod]
    public void StanceClick_RendersTheLineTheManifestReads()
    {
        var outcomes = new OutcomeLog(_logger, new TAOM.Tests.Features.LoadTimeStamps.FakeStampClock(), () => 0);
        var sut = new BattleActionBarService(_settings, _analyzer, _stances, _logger, outcomes);
        SetComposition(polearm: true);
        var pikeWall = sut.GetActionsForFormation(MakeFormation(formationIndex: 2)).First(a => a.Id == "action_pike_wall");

        pikeWall.Execute();
        pikeWall.Execute();

        _logger.Received(1).LogInfo("[Check] id=taom.battle-action-bar.stance result=pass stance=PikeWall formation=2");
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[Check]")));
    }
}
