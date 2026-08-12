using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The service around <see cref="ServiceWarPolicy"/> (field report 5). The policy tests pin the set
/// arithmetic; these pin the two things only the service can get wrong — the ORDER in which it
/// snapshots, and what it records afterwards.
/// </summary>
[TestClass]
public class ServiceDiplomacyServiceTests
{
    private IEnlistmentStore _store = null!;
    private IServiceDiplomacyAdapter _diplomacy = null!;
    private ServiceDiplomacyService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(logger);
        _diplomacy = Substitute.For<IServiceDiplomacyAdapter>();
        _diplomacy.DeclareWarOn(Arg.Any<string>()).Returns(true);
        _diplomacy.MakePeaceWith(Arg.Any<string>()).Returns(true);
        _diplomacy.GetPlayerFactionId().Returns("player_clan");
        _sut = new ServiceDiplomacyService(_store, _diplomacy, logger);

        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
    }

    private static IReadOnlyList<string> R(params string[] ids) => new List<string>(ids);

    [TestMethod]
    public void ApplyServiceWars_SnapshotsTheOathEnemiesBEFOREDeclaringAnything()
    {
        // THE ordering test, and the reason this service exists rather than a bare policy call.
        // If the snapshot were taken after the declarations, every war the mirror just created would
        // read as pre-existing, FactionsToPeaceOnDischarge would subtract all of them, and the
        // discharge would unwind nothing — the player keeps his commander's wars forever.
        //
        // Simulated by having the adapter report the POST-declaration world on any later call: the
        // service must not be reading it.
        _diplomacy.GetPlayerEnemies().Returns(R("empire"), R("empire", "sturgia", "vlandia"));
        _diplomacy.GetEnemiesOf("lord_1_1").Returns(R("sturgia", "vlandia"));

        _sut.ApplyServiceWars("lord_1_1");

        CollectionAssert.AreEqual(new List<string> { "empire" }, _store.Record.EnemiesAtOath);
        CollectionAssert.AreEqual(new List<string> { "sturgia", "vlandia" }, _store.Record.MirroredWars);
    }

    [TestMethod]
    public void ApplyServiceWars_PinsTheFactionThatDidTheDeclaring()
    {
        _diplomacy.GetPlayerEnemies().Returns(R());
        _diplomacy.GetEnemiesOf("lord_1_1").Returns(R("sturgia"));

        _sut.ApplyServiceWars("lord_1_1");

        Assert.AreEqual("player_clan", _store.Record.OathFactionId);
    }

    /// <summary>
    /// Data-flow review, 2026-08-12. `Hero.MapFaction` is `Clan.Kingdom ?? Clan` (verified 1.4.8)
    /// and the enlist gate admits a player whose clan is already a vassal — so the identity that
    /// declares is not necessarily the identity that discharges.
    ///
    /// A player independent at oath declares as his own clan. If that clan joins a kingdom before
    /// discharge, the unwind resolves `MapFaction` live and would call `MakePeaceAction.Apply` on
    /// the KINGDOM — ending a war for every vassal in it because one soldier left service, with
    /// nothing on screen connecting the two.
    /// </summary>
    [TestMethod]
    public void UnwindServiceWars_PlayerJoinedAKingdomMidService_MakesNoPeaceAtAll()
    {
        _store.Record.MirroredWars = new List<string> { "sturgia", "vlandia" };
        _store.Record.EnemiesAtOath = new List<string>();
        _store.Record.OathFactionId = "player_clan";

        _diplomacy.GetPlayerFactionId().Returns("kingdom_of_vlandia");

        _sut.UnwindServiceWars();

        _diplomacy.DidNotReceive().MakePeaceWith(Arg.Any<string>());
    }

    [TestMethod]
    public void UnwindServiceWars_IdentityChanged_StillClearsTheMirror()
    {
        // The mirror names wars declared by an identity we no longer speak for: not ours to unwind,
        // and not ours to keep. Leaving it would re-fire at the NEXT discharge, when the player's
        // faction might have changed back.
        _store.Record.MirroredWars = new List<string> { "sturgia" };
        _store.Record.EnemiesAtOath = new List<string>();
        _store.Record.OathFactionId = "player_clan";
        _diplomacy.GetPlayerFactionId().Returns("kingdom_of_vlandia");

        _sut.UnwindServiceWars();

        Assert.AreEqual(0, _store.Record.MirroredWars.Count);
        Assert.AreEqual(0, _store.Record.EnemiesAtOath.Count);
    }

    [TestMethod]
    public void UnwindServiceWars_SameIdentity_UnwindsNormally()
    {
        _store.Record.MirroredWars = new List<string> { "sturgia" };
        _store.Record.EnemiesAtOath = new List<string>();
        _store.Record.OathFactionId = "player_clan";
        _diplomacy.GetPlayerFactionId().Returns("player_clan");

        _sut.UnwindServiceWars();

        _diplomacy.Received(1).MakePeaceWith("sturgia");
    }

    [TestMethod]
    public void UnwindServiceWars_NoPinOnAnOlderSave_UnwindsAsPreviousBuildsDid()
    {
        // Refusing here would strand a player who is mid-service across the upgrade in wars he can
        // never undo. The pre-pin behaviour is the status quo, not a new hazard.
        _store.Record.MirroredWars = new List<string> { "sturgia" };
        _store.Record.EnemiesAtOath = new List<string>();
        _store.Record.OathFactionId = null;
        _diplomacy.GetPlayerFactionId().Returns("kingdom_of_vlandia");

        _sut.UnwindServiceWars();

        _diplomacy.Received(1).MakePeaceWith("sturgia");
    }

    [TestMethod]
    public void ApplyServiceWars_DoesNotRedeclareAWarAlreadyHeld()
    {
        _diplomacy.GetPlayerEnemies().Returns(R("empire"));
        _diplomacy.GetEnemiesOf("lord_1_1").Returns(R("empire", "sturgia"));

        _sut.ApplyServiceWars("lord_1_1");

        _diplomacy.DidNotReceive().DeclareWarOn("empire");
        _diplomacy.Received(1).DeclareWarOn("sturgia");
    }

    [TestMethod]
    public void ApplyServiceWars_ADeclarationTheEngineRefusedIsNotRecorded()
    {
        // Only what actually landed goes in the mirror. Recording a refused declaration would leave
        // a promise to make peace with a faction we never fought — and that peace is a real,
        // unearned diplomatic gift at discharge.
        _diplomacy.GetPlayerEnemies().Returns(R());
        _diplomacy.GetEnemiesOf("lord_1_1").Returns(R("sturgia", "eliminated_faction"));
        _diplomacy.DeclareWarOn("eliminated_faction").Returns(false);

        _sut.ApplyServiceWars("lord_1_1");

        CollectionAssert.AreEqual(new List<string> { "sturgia" }, _store.Record.MirroredWars);
    }

    [TestMethod]
    public void UnwindServiceWars_EndsOnlyTheMirroredWars()
    {
        _store.Record.EnemiesAtOath = new List<string> { "empire" };
        _store.Record.MirroredWars = new List<string> { "empire", "sturgia" };

        _sut.UnwindServiceWars();

        _diplomacy.Received(1).MakePeaceWith("sturgia");
        _diplomacy.DidNotReceive().MakePeaceWith("empire");
    }

    [TestMethod]
    public void UnwindServiceWars_ClearsBothListsSoTheNextTermStartsClean()
    {
        // A mirror surviving discharge would be unwound AGAIN at the next discharge, handing the
        // player peace with factions he had since chosen to fight.
        _store.Record.EnemiesAtOath = new List<string> { "empire" };
        _store.Record.MirroredWars = new List<string> { "sturgia" };

        _sut.UnwindServiceWars();

        Assert.AreEqual(0, _store.Record.MirroredWars.Count);
        Assert.AreEqual(0, _store.Record.EnemiesAtOath.Count);
    }

    [TestMethod]
    public void UnwindServiceWars_WithNothingMirrored_MakesNoPeace()
    {
        _sut.UnwindServiceWars();

        _diplomacy.DidNotReceive().MakePeaceWith(Arg.Any<string>());
    }

    [TestMethod]
    public void SecondEnlistment_AfterADischarge_SnapshotsTheNewOathState()
    {
        // Serve, discharge, serve again. The second oath must snapshot the world as it is THEN —
        // including any war the first term left standing because the player started it himself.
        _diplomacy.GetPlayerEnemies().Returns(R());
        _diplomacy.GetEnemiesOf("lord_1_1").Returns(R("sturgia"));
        _sut.ApplyServiceWars("lord_1_1");
        _sut.UnwindServiceWars();

        _diplomacy.GetPlayerEnemies().Returns(R("vlandia"));
        _diplomacy.GetEnemiesOf("lord_2_1").Returns(R("vlandia", "battania"));
        _sut.ApplyServiceWars("lord_2_1");

        CollectionAssert.AreEqual(new List<string> { "vlandia" }, _store.Record.EnemiesAtOath);
        CollectionAssert.AreEqual(new List<string> { "battania" }, _store.Record.MirroredWars);
    }
}
