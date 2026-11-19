using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// The [Check] line a crew seat writes when its archer leaves it (#1482). tools/ingame_checks/1482.toml reads it,
/// so the id and the detail's shape are a contract with that manifest.
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
    public void TheDetail_IsTheShapeTheManifestReads()
    {
        // No whitespace inside a value: the verdict reader splits the detail on spaces.
        Assert.AreEqual("seat=[Mumakil#2]_seat3 rescues=0", CrewSeatChecks.Detail("[Mumakil#2] seat3", 0));
        Assert.AreEqual("seat=[Howdah#1]_seat0 rescues=4", CrewSeatChecks.Detail("[Howdah#1] seat0", 4));
        Assert.AreEqual("seat=unknown rescues=0", CrewSeatChecks.Detail(null, 0));
    }

    [TestMethod]
    public void EachBeast_HasItsOwnId_ThatTheOutcomeLogAccepts()
    {
        // One id for both would let a battle with only a mumakil pass the howdah's check.
        var accepted = new System.Text.RegularExpressions.Regex(@"^[a-z0-9]+(\.[a-z0-9-]+)+$");
        StringAssert.Matches(CrewSeatChecks.MumakilHeldId, accepted);
        StringAssert.Matches(CrewSeatChecks.HowdahHeldId, accepted);
        Assert.AreNotEqual(CrewSeatChecks.MumakilHeldId, CrewSeatChecks.HowdahHeldId);
    }
}
