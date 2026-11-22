using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.FactionUI.Menus;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// When the movie hook keeps vanilla's map bar instead of the themed one. Only a co-op mod whose roles TAOM cannot probe
/// (BannerlordTogether: it defers on every peer with no session TAOM can see) keeps the native bar; BannerlordCoop's
/// clients, its server process and solo play all get the themed bar.
/// </summary>
[TestClass]
public class ThemedMapBarCoopPolicyTests
{
    private static ICoopSessionProvider Coop(bool deferToHost, bool sessionActive)
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.ShouldDeferToHost.Returns(deferToHost);
        coop.IsSessionActive.Returns(sessionActive);
        return coop;
    }

    [TestMethod]
    public void KeepsNativeMapBar_Solo_False()
    {
        Assert.IsFalse(ThemedMapBarCoopPolicy.KeepsNativeMapBar(Coop(deferToHost: false, sessionActive: false)));
    }

    [TestMethod]
    public void KeepsNativeMapBar_CoopClientInASession_False()
    {
        Assert.IsFalse(ThemedMapBarCoopPolicy.KeepsNativeMapBar(Coop(deferToHost: true, sessionActive: true)));
    }

    [TestMethod]
    public void KeepsNativeMapBar_CoopServerProcess_False()
    {
        Assert.IsFalse(ThemedMapBarCoopPolicy.KeepsNativeMapBar(Coop(deferToHost: false, sessionActive: true)));
    }

    [TestMethod]
    public void KeepsNativeMapBar_UnprobeableCoopMod_True()
    {
        Assert.IsTrue(ThemedMapBarCoopPolicy.KeepsNativeMapBar(Coop(deferToHost: true, sessionActive: false)));
    }

    [DataTestMethod]
    [DataRow("MapBar")]
    [DataRow("TaomFieldCampOverlay")]
    public void IsMapBarMovie_TheBarAndTheCampIconBesideItsClock_True(string movie)
    {
        Assert.IsTrue(ThemedMapBarCoopPolicy.IsMapBarMovie(movie));
    }

    [DataTestMethod]
    [DataRow("EscapeMenu")]
    [DataRow("EncyclopediaBar")]
    [DataRow(null)]
    public void IsMapBarMovie_OtherMovies_False(string? movie)
    {
        Assert.IsFalse(ThemedMapBarCoopPolicy.IsMapBarMovie(movie));
    }
}
