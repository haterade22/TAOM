using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SignatureStrikes.Hooks;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The mission allowlist. A live <c>Mission</c> cannot be built in a unit test, so only the null
/// arm is pinned here; the <c>CombatType</c> and multiplayer arms are covered by
/// SignatureStrikesBindingTests (the members resolve) and the in-game negative smoke (a
/// tournament as Sauron shows no effect lines).
/// </summary>
[TestClass]
public class SignatureMissionGateTests
{
    [TestMethod]
    public void IsEligible_NullMission_ReturnsFalse()
    {
        Assert.IsFalse(SignatureMissionGate.IsEligible(null));
    }
}
