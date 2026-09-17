using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CultureDoctrine.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The engine-free half of <see cref="TeamCombatantSelector"/>: which combatants a team's
/// doctrine is folded from, mirroring the engine's per-troop rule (`Mission.GetAgentTeam`,
/// `Mission.cs:5233-5240`: on the player's side a troop lands on the ally team iff its origin is
/// neither under the player's command nor in the player's army), and the side-wide Tactics
/// skill vanilla registers every team with. <c>IBattleCombatant</c> is an engine interface, so
/// the fakes need the game assemblies on this machine.
/// </summary>
[TestClass]
public class TeamCombatantSelectorTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static IBattleCombatant Combatant(BattleSideEnum side, string culture, int troops, int skill, bool underPlayer)
    {
        var c = Substitute.For<IBattleCombatant>();
        c.Side.Returns(side);
        c.BasicCulture.Returns(new BasicCultureObject { StringId = culture });
        c.GetNumberOfMissionReadyTroops().Returns(troops);
        c.GetTacticsSkillAmount().Returns(skill);
        c.IsUnderPlayersCommand(side).Returns(underPlayer);
        return c;
    }

    [TestMethod]
    public void Select_EnemySide_TakesEveryCombatantOnThatSide()
    {
        RequireGame();
        var all = new[]
        {
            Combatant(BattleSideEnum.Attacker, "mordor", 100, 10, false),
            Combatant(BattleSideEnum.Attacker, "isengard", 50, 40, false),
            Combatant(BattleSideEnum.Defender, "gondor", 200, 90, true),
        };

        var selection = TeamCombatantSelector.Select(all, BattleSideEnum.Attacker, splitPlayerSide: false, teamIsAllyTeam: false, _ => false);

        CollectionAssert.AreEqual(new[] { "mordor", "isengard" }, selection.Combatants.Select(c => c.CultureId).ToArray());
        Assert.AreEqual(40, selection.SideTacticsSkill);
    }

    [TestMethod]
    public void Select_PlayerSideSplit_AllyTeamGetsTheUncommandedPartiesOutsideThePlayersArmy()
    {
        RequireGame();
        var own = Combatant(BattleSideEnum.Defender, "gondor", 100, 30, underPlayer: true);
        var armyMate = Combatant(BattleSideEnum.Defender, "gondor", 80, 20, underPlayer: false);
        var ally1 = Combatant(BattleSideEnum.Defender, "vlandia", 120, 70, underPlayer: false);
        var ally2 = Combatant(BattleSideEnum.Defender, "vlandia", 60, 10, underPlayer: false);
        var all = new[] { own, armyMate, ally1, ally2 };

        var ally = TeamCombatantSelector.Select(all, BattleSideEnum.Defender, splitPlayerSide: true, teamIsAllyTeam: true, c => ReferenceEquals(c, armyMate));
        var player = TeamCombatantSelector.Select(all, BattleSideEnum.Defender, splitPlayerSide: true, teamIsAllyTeam: false, c => ReferenceEquals(c, armyMate));

        CollectionAssert.AreEqual(new[] { 120, 60 }, ally.Combatants.Select(c => c.TroopCount).ToArray(), "both allied parties, not only the first");
        CollectionAssert.AreEqual(new[] { 100, 80 }, player.Combatants.Select(c => c.TroopCount).ToArray(), "the player's party and the party in the player's army");
    }

    [TestMethod]
    public void Select_PlayerSideSplit_BothTeamsRegisterWithTheSidesBestSkill()
    {
        RequireGame();
        var all = new[]
        {
            Combatant(BattleSideEnum.Attacker, "gondor", 100, 30, underPlayer: true),
            Combatant(BattleSideEnum.Attacker, "vlandia", 120, 70, underPlayer: false),
        };

        var ally = TeamCombatantSelector.Select(all, BattleSideEnum.Attacker, true, true, _ => false);
        var player = TeamCombatantSelector.Select(all, BattleSideEnum.Attacker, true, false, _ => false);

        Assert.AreEqual(70, ally.SideTacticsSkill);
        Assert.AreEqual(70, player.SideTacticsSkill, "MissionCombatantsLogic.cs:169 gates every team on the side's maximum");
    }

    [TestMethod]
    public void Select_NoAllyTeam_PlayerTeamGetsTheWholeSide()
    {
        RequireGame();
        var all = new[]
        {
            Combatant(BattleSideEnum.Attacker, "gondor", 100, 30, underPlayer: true),
            Combatant(BattleSideEnum.Attacker, "vlandia", 120, 70, underPlayer: false),
        };

        var player = TeamCombatantSelector.Select(all, BattleSideEnum.Attacker, splitPlayerSide: false, teamIsAllyTeam: false, _ => false);

        Assert.AreEqual(2, player.Combatants.Count);
    }

    [TestMethod]
    public void Select_NullCombatant_IsSkipped()
    {
        RequireGame();
        var all = new[] { null, Combatant(BattleSideEnum.Attacker, "mordor", 10, 0, false) };

        var selection = TeamCombatantSelector.Select(all!, BattleSideEnum.Attacker, false, false, _ => false);

        Assert.AreEqual(1, selection.Combatants.Count);
    }
}
