using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Refuge;
using TAOM.Features.Refuge.Hooks;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// #1086: a co-op guest's book is the save's whole book, so every refuge in it is another player's. The
/// encounter redirect must stand down for a guest before it reads anything, or a guest clicking a refuge
/// gets its manage, upgrade and dismantle menu.
/// </summary>
[TestClass]
public class RefugeEncounterPatchTests
{
    private IEnlistmentStateQuery _enlistment = null!;
    private IGameMenuAdapter _menus = null!;
    private ICoopSessionProvider _coop = null!;

    [TestInitialize]
    public void Setup()
    {
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _menus = Substitute.For<IGameMenuAdapter>();
        _coop = Substitute.For<ICoopSessionProvider>();
        RefugeEncounterPatch.Initialize(Substitute.For<IRefugeService>(), _enlistment, _menus, _coop);
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var name in new[] { "_refuges", "_enlistment", "_menus", "_coop" })
            typeof(RefugeEncounterPatch).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
    }

    [TestMethod]
    public void Prefix_ForACoopGuest_RunsVanillaBeforeReadingAnything()
    {
        _coop.ShouldDeferToHost.Returns(true);

        Assert.IsTrue(RefugeEncounterPatch.Prefix());

        _ = _enlistment.DidNotReceive().IsEnlisted;
        _menus.DidNotReceiveWithAnyArgs().EnsureMenuOpen(default!);
    }

    [TestMethod]
    public void Prefix_WithoutAHost_GoesOnToTheRefugeChecks()
    {
        // The twin above passes only because of the gate: here the same call reaches the next check.
        _coop.ShouldDeferToHost.Returns(false);
        _enlistment.IsEnlisted.Returns(true);

        Assert.IsTrue(RefugeEncounterPatch.Prefix());

        _ = _enlistment.Received(1).IsEnlisted;
    }
}
