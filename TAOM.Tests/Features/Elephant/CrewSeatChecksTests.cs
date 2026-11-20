using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The [Check] lines a crew seat writes when its archer leaves it (#1482). tools/ingame_checks/1482.toml reads
/// them, so the ids and the shape of each detail are a contract with that manifest.
/// </summary>
[TestClass]
public class CrewSeatChecksTests
{
    [TestMethod]
    public void ASeatThatNeededNoRescue_HeldItsArcher()
    {
        Assert.IsTrue(CrewSeatChecks.Held(rescues: 0));
    }

    [TestMethod]
    public void ASeatThatTeleportedItsArcherBack_DidNotHoldIt()
    {
        Assert.IsFalse(CrewSeatChecks.Held(rescues: 1));
        Assert.IsFalse(CrewSeatChecks.Held(rescues: 40));
    }

    [TestMethod]
    public void OnlyALoosedArrow_CountsAsAShot()
    {
        // The first fix held every archer and none of them shot: a held seat proves nothing about the bow.
        Assert.IsTrue(CrewSeatChecks.Shot(shots: 1));
        Assert.IsFalse(CrewSeatChecks.Shot(shots: 0));
        Assert.IsFalse(CrewSeatChecks.Shot(shots: -1), "no origin to read a count from");
    }

    [TestMethod]
    public void TheDetails_AreTheShapeTheManifestReads()
    {
        // No whitespace inside a value: the verdict reader splits the detail on spaces.
        Assert.AreEqual("seat=[Mumakil#2]_seat3 rescues=0", CrewSeatChecks.HeldDetail("[Mumakil#2] seat3", 0));
        Assert.AreEqual("seat=[Howdah#1]_seat0 rescues=4", CrewSeatChecks.HeldDetail("[Howdah#1] seat0", 4));
        Assert.AreEqual("seat=unknown rescues=0", CrewSeatChecks.HeldDetail(null, 0));
        Assert.AreEqual("seat=[Mumakil#2]_seat3 shots=44", CrewSeatChecks.ShotDetail("[Mumakil#2] seat3", 44));
    }

    [TestMethod]
    public void EachBeastAndEachFact_HasItsOwnId_ThatTheOutcomeLogAccepts()
    {
        // One id for both beasts would let a battle with only a mumakil pass the howdah's check.
        var accepted = new System.Text.RegularExpressions.Regex(@"^[a-z0-9]+(\.[a-z0-9-]+)+$");
        string[] ids =
        {
            CrewSeatChecks.MumakilHeldId, CrewSeatChecks.HowdahHeldId,
            CrewSeatChecks.MumakilShotId, CrewSeatChecks.HowdahShotId,
        };
        foreach (string id in ids) StringAssert.Matches(id, accepted);
        Assert.AreEqual(ids.Length, ids.Distinct().Count());
    }
}
