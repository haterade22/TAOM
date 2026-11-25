using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SignatureStrikes.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The mission allowlist. A live <c>Mission</c> cannot be built in a unit test, so three things are pinned here: the null
/// arm, the pure rule over each marker on its own, and the source lines that carry the engine's answers into that rule.
/// That the engine members resolve is SignatureStrikesBindingTests' part.
/// </summary>
[TestClass]
public class SignatureMissionGateTests
{
    private const string GateFile = "Main/Features/SignatureStrikes/Hooks/SignatureMissionGate.cs";

    [TestMethod]
    public void IsEligible_NullMission_ReturnsFalse()
    {
        Assert.IsFalse(SignatureMissionGate.IsEligible(null));
    }

    [TestMethod]
    [DataRow(false, false, false, DisplayName = "a battle, a town scene")]
    [DataRow(true, false, true, DisplayName = "the training yard: only the practice controller")]
    [DataRow(false, true, true, DisplayName = "a tournament match: only the tournament behaviour")]
    [DataRow(true, true, true, DisplayName = "both")]
    public void IsArena_EitherMarkerAlone_IsAnArena(bool practiceYard, bool tournament, bool expected)
    {
        Assert.AreEqual(expected, SignatureMissionGate.IsArena(practiceYard, tournament));
    }

    [TestMethod]
    [DataRow(true, false, false, true, DisplayName = "a battle")]
    [DataRow(true, true, false, false, DisplayName = "the training yard")]
    [DataRow(true, false, true, false, DisplayName = "a tournament match")]
    [DataRow(true, true, true, false, DisplayName = "both markers")]
    [DataRow(false, false, false, false, DisplayName = "no combat")]
    [DataRow(false, true, false, false, DisplayName = "no combat, in the yard")]
    public void AllowsKind_OnlyCombatOutsideTheArena(bool combat, bool practiceYard, bool tournament, bool expected)
    {
        Assert.AreEqual(expected, SignatureMissionGate.AllowsKind(combat, practiceYard, tournament));
    }

    /// <summary>
    /// #1537: <c>Mission.CombatType</c> reads <c>Combat</c> in every mission, because nothing in the installed game sets it. A
    /// gate that keeps a feature out of the yard and the tournament must therefore hand the arena's own mission behaviours to
    /// the rule above. Each row is one link of that chain, read with comments blanked: the two engine reads, the line that
    /// passes both to the rule, and the call of each feature that must stay out. A link that goes back to the combat type
    /// alone still compiles, and lets the feature into the yard.
    /// </summary>
    [TestMethod]
    [DataRow(GateFile, "HasPracticeController(Mission mission) => mission.GetMissionBehavior<ArenaPracticeFightMissionController>() != null;")]
    [DataRow(GateFile, "HasTournament(Mission mission) => mission.GetMissionBehavior<TournamentBehavior>() != null;")]
    [DataRow(GateFile, "AllowsKind(mission.CombatType == Mission.MissionCombatType.Combat, HasPracticeController(mission), HasTournament(mission));")]
    [DataRow(GateFile, "mission != null && !GameNetwork.IsSessionActive && IsCombatOutsideArena(mission);")]
    [DataRow("Main/Features/SignatureStrikes/Hooks/SignatureStrikesMissionLogic.cs", "_eligible ??= SignatureMissionGate.IsEligible(Mission, _logger);")]
    [DataRow("Main/Features/RaceAbilities/Hooks/RaceAbilitiesMissionLogic.cs", "var eligible = mcm && json && SignatureMissionGate.IsEligible(Mission);")]
    [DataRow("Main/Features/DwarfAttacks/DwarfAttacksMissionBehavior.cs", "_eligible = mission != null && SignatureMissionGate.IsCombatOutsideArena(mission);")]
    public void EveryArenaGate_HandsTheArenaBehavioursToTheRule(string file, string link)
    {
        var source = RepoPaths.ReadSource(file, stripComments: true);
        StringAssert.Contains(source, link, $"{file} no longer carries the arena's mission behaviours to the gate rule");
    }
}
