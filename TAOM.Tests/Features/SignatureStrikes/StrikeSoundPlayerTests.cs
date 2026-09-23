using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SignatureStrikes.Hooks;

namespace TAOM.Tests.Features.SignatureStrikes;

/// <summary>
/// The engine-free paths of the strike sound player (#645). <c>Mission</c> and <c>Agent</c> have no
/// test constructor, so the playing path is pinned by <see cref="SignatureStrikesBindingTests"/>
/// (the four engine members it calls) and the in-game smoke; these pin what happens before any
/// engine member is touched, the SignatureAgentRosterTests null-handle precedent.
/// </summary>
[TestClass]
public class StrikeSoundPlayerTests
{
    [TestMethod]
    public void Play_NoSoundConfigured_ReturnsNoneWithoutTouchingTheEngine()
    {
        var sut = new StrikeSoundPlayer(Substitute.For<IModLogger>());

        Assert.AreEqual("none", sut.Play(null!, null!, null));
        Assert.AreEqual("none", sut.Play(null!, null!, ""));
    }

    [TestMethod]
    public void Clear_OnAFreshPlayer_IsANoOpAndLogsNothing()
    {
        var logger = Substitute.For<IModLogger>();
        var sut = new StrikeSoundPlayer(logger);

        sut.Clear();

        Assert.AreEqual("none", sut.Play(null!, null!, null));
        Assert.AreEqual(0, logger.ReceivedCalls().Count());
    }
}
