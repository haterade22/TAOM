using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The closed id set the doctrine JSON may name. Vanilla ids spell the engine's type names
/// without the <c>Tactic</c> prefix; TAOM ids are the four Phase B tactics. Growing the set is a
/// one-line enum edit plus a factory case, which is the extension path the design promises.
/// </summary>
[TestClass]
public class DoctrineTacticIdsTests
{
    [TestMethod]
    public void TryParse_VanillaName_ResolvesCaseInsensitively()
    {
        Assert.IsTrue(DoctrineTacticIds.TryParse("frontalCavalryCharge", out var tactic));
        Assert.AreEqual(DoctrineTactic.FrontalCavalryCharge, tactic);
    }

    [TestMethod]
    public void TryParse_TaomName_Resolves()
    {
        Assert.IsTrue(DoctrineTacticIds.TryParse("ArcherRing", out var tactic));
        Assert.AreEqual(DoctrineTactic.ArcherRing, tactic);
    }

    [TestMethod]
    public void TryParse_UnknownOrBlank_ReturnsFalse()
    {
        Assert.IsFalse(DoctrineTacticIds.TryParse("TacticCharge", out _), "the engine's Tactic prefix is not part of the id");
        Assert.IsFalse(DoctrineTacticIds.TryParse("", out _));
        Assert.IsFalse(DoctrineTacticIds.TryParse(null, out _));
        Assert.IsFalse(DoctrineTacticIds.TryParse("7", out _), "a bare integer must not parse to an enum member");
    }

    [TestMethod]
    public void IsVanilla_SplitsTheNineEngineTacticsFromTheElevenTaomOnes()
    {
        var vanilla = DoctrineTacticIds.All.Where(DoctrineTacticIds.IsVanilla).ToList();
        var taom = DoctrineTacticIds.All.Where(t => !DoctrineTacticIds.IsVanilla(t)).ToList();

        CollectionAssert.AreEquivalent(new[]
        {
            DoctrineTactic.Charge, DoctrineTactic.FullScaleAttack, DoctrineTactic.DefensiveEngagement,
            DoctrineTactic.DefensiveLine, DoctrineTactic.DefensiveRing, DoctrineTactic.FrontalCavalryCharge,
            DoctrineTactic.RangedHarrassmentOffensive, DoctrineTactic.HoldChokePoint, DoctrineTactic.CoordinatedRetreat,
        }, vanilla);
        CollectionAssert.AreEquivalent(new[]
        {
            DoctrineTactic.ShieldWall, DoctrineTactic.InfantryMass, DoctrineTactic.CavalryDominance, DoctrineTactic.ArcherRing,
            DoctrineTactic.TwoLineWall, DoctrineTactic.Envelop, DoctrineTactic.DisciplinedLine, DoctrineTactic.ArcherAdvance,
            DoctrineTactic.EoredScreen, DoctrineTactic.HitAndRun, DoctrineTactic.MumakVanguard,
        }, taom);
    }

    [TestMethod]
    public void EngineTypeName_VanillaTactic_IsTheTacticPrefixedName()
    {
        Assert.AreEqual("TacticRangedHarrassmentOffensive", DoctrineTacticIds.EngineTypeName(DoctrineTactic.RangedHarrassmentOffensive));
    }

    [TestMethod]
    public void EngineTypeName_TaomTactic_IsTheTaomPrefixedName()
    {
        Assert.AreEqual("TaomTacticShieldWall", DoctrineTacticIds.EngineTypeName(DoctrineTactic.ShieldWall));
    }
}
