using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Domain;
using TAOM.Features.SignatureStrikes.Hooks;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The roster's null and lifecycle paths. <c>Agent</c> is sealed with no public constructor, so
/// the registration path proper (identity from <c>HeroObject.StringId</c> / <c>Character.Race</c>)
/// is exercised in game; these pin what the engine callbacks can hand the roster on the way in:
/// a null handle (<c>GetNearbyAgentsAux</c> and a reclaimed id both produce one) and the
/// mission-boundary clears.
/// </summary>
[TestClass]
public class SignatureAgentRosterTests
{
    private ISignatureStrikeRegistry _registry = null!;
    private SignatureAgentRoster _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _registry = Substitute.For<ISignatureStrikeRegistry>();
        _sut = new SignatureAgentRoster(_registry, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void TryRegister_NullAgent_ReturnsFalseAndNeverConsultsTheRegistry()
    {
        Assert.IsFalse(_sut.TryRegister(null));

        _registry.DidNotReceive().ResolveSignatureIndex(Arg.Any<string?>(), Arg.Any<int?>());
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void TryGet_NullAgent_ReturnsFalse()
    {
        Assert.IsFalse(_sut.TryGet(null, out _));
    }

    [TestMethod]
    public void Remove_NullAgent_DoesNotThrow()
    {
        _sut.Remove(null);

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Clear_OnAnEmptyRoster_IsANoOp()
    {
        _sut.Clear();

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void NewEntry_KeepsItsSignatureAndStartsWithNeverStruckStamps()
    {
        // NaN is the service's "never struck" sentinel; a zero would read as a strike at t=0 and
        // gate the first real one behind the whole cooldown.
        var entry = new SignatureAgentEntry(signatureIndex: 1);

        Assert.AreEqual(1, entry.SignatureIndex);
        foreach (StrikeKind kind in System.Enum.GetValues(typeof(StrikeKind)))
            Assert.IsTrue(float.IsNaN(entry.Times.Get(kind)), kind.ToString());
    }
}
