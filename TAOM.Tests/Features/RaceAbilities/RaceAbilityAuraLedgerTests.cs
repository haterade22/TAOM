using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities;

// Which fear aura frightens a soldier when several reach him: the strongest, once per pulse. Plain objects
// stand in for agents.

namespace TAOM.Tests.Features.RaceAbilities;

[TestClass]
public class RaceAbilityAuraLedgerTests
{
    private RaceAbilityAuraLedger<object> _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new RaceAbilityAuraLedger<object>();

    [TestMethod]
    public void Offer_SeveralAurasOnOneVictim_KeepsTheStrongest()
    {
        var victim = new object();

        _sut.Offer(victim, 1f, "weak");
        _sut.Offer(victim, 3f, "strong");
        _sut.Offer(victim, 2f, "middling");

        Assert.AreEqual(1, _sut.Count);
        Assert.AreEqual(3f, _sut.Entries.Single().Value.drain, 0.0001f);
        Assert.AreEqual("strong", _sut.Entries.Single().Value.abilityId);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-1f)]
    [DataRow(float.NaN)]
    public void Offer_NoDrain_IsIgnored(float drain)
    {
        _sut.Offer(new object(), drain, "necromancer_shadow");

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Clear_EmptiesTheLedger()
    {
        _sut.Offer(new object(), 2f, "necromancer_shadow");

        _sut.Clear();

        Assert.AreEqual(0, _sut.Count);
    }
}
