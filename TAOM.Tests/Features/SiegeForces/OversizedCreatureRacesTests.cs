using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Features.SiegeForces.Domain;
using TAOM.Features.TrollBruteForce;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// The one definition of an oversized creature race, shared by the siege troop picker and the creature siege
/// role. Both consumers key on a race int the engine hands out, and derive the oversized ids once from the names, so
/// the rule is the validate-before-lookup shape (csharp-architecture.md "Lookup Functions With Fallbacks"):
/// <c>RaceManager.GetRaceIdFromName</c> answers 0 for a name it does not know, and a fallback is for logging, never for
/// acceptance. The fake models that fallback, so an absent race is tested against the engine's real trap.
/// </summary>
[TestClass]
public class OversizedCreatureRacesTests
{
    [TestMethod]
    public void RaceNames_AreTheTwoTrollRaces()
    {
        // The literals are the contract. The next test pins the same names to their constants separately, so
        // renaming a constant and the list together cannot go unnoticed.
        CollectionAssert.AreEqual(new[] { "cave_troll", "hill_troll" }, OversizedCreatureRaces.RaceNames.ToArray());
    }

    [TestMethod]
    public void RaceNames_AreTheTrollBruteForceMonsterIds()
    {
        CollectionAssert.AreEqual(
            new[] { TrollBruteForceConfig.CaveTrollMonsterId, TrollBruteForceConfig.HillTrollMonsterId },
            OversizedCreatureRaces.RaceNames.ToArray());
    }

    [TestMethod]
    public void RaceNames_CannotBeChangedByACaller()
    {
        // A shared list that a consumer can edit is a race definition two features can drift apart on.
        Assert.IsNull(OversizedCreatureRaces.RaceNames as string[], "the list is backed by a plain array a caller could cast to");
        var asList = (IList<string>)OversizedCreatureRaces.RaceNames;
        Assert.ThrowsException<NotSupportedException>(() => asList[0] = "human");
        Assert.ThrowsException<NotSupportedException>(() => asList.Add("human"));
        CollectionAssert.AreEqual(new[] { "cave_troll", "hill_troll" }, OversizedCreatureRaces.RaceNames.ToArray());
    }

    [TestMethod]
    public void ResolveRaceIds_BothRacesRegistered_ReturnsBothIdsInNameOrder()
    {
        var ids = OversizedCreatureRaces.ResolveRaceIds(FakeRaceManager.WithTrolls());

        CollectionAssert.AreEqual(new[] { FakeRaceManager.CaveTroll, FakeRaceManager.HillTroll }, ids);
    }

    [TestMethod]
    public void ResolveRaceIds_OneRaceMissing_ReturnsOnlyTheOther_NotTheHumanFallback()
    {
        // GetRaceIdFromName answers 0 (human) for a name it does not know. Resolving a missing race through it
        // would put the human race id into the mask and make every human an oversized creature.
        var races = new FakeRaceManager("human", "hill_troll");

        var ids = OversizedCreatureRaces.ResolveRaceIds(races);

        CollectionAssert.AreEqual(new[] { 1 }, ids);
        CollectionAssert.DoesNotContain(ids, FakeRaceManager.Human);
        CollectionAssert.DoesNotContain(races.NamesAskedForAnId, "cave_troll", "the missing race must be validated, never looked up");
    }

    [TestMethod]
    public void ResolveRaceIds_NoTrollRaceRegistered_ReturnsAnEmptyArray_WithoutALookup()
    {
        var races = FakeRaceManager.HumansOnly();

        var ids = OversizedCreatureRaces.ResolveRaceIds(races);

        Assert.IsNotNull(ids);
        Assert.AreEqual(0, ids.Length);
        Assert.AreEqual(0, races.IdLookups, "no name resolved, so no id lookup may be made");
    }

    [TestMethod]
    public void ResolveRaceIds_TwoNamesOnOneId_ReturnsTheIdOnce()
    {
        var races = Substitute.For<IRaceManager>();
        races.IsValidRaceName(Arg.Any<string>()).Returns(true);
        races.GetRaceIdFromName(Arg.Any<string>()).Returns(5);

        CollectionAssert.AreEqual(new[] { 5 }, OversizedCreatureRaces.ResolveRaceIds(races));
    }

    [TestMethod]
    public void ResolveRaceIds_WithoutARaceManager_ReturnsAnEmptyArray()
    {
        var ids = OversizedCreatureRaces.ResolveRaceIds(null!);

        Assert.IsNotNull(ids);
        Assert.AreEqual(0, ids.Length);
    }

    [TestMethod]
    public void ResolveRaceIds_ReturnsAFreshArrayEachCall()
    {
        var races = FakeRaceManager.WithTrolls();
        var first = OversizedCreatureRaces.ResolveRaceIds(races);
        first[0] = 99;

        CollectionAssert.AreEqual(new[] { FakeRaceManager.CaveTroll, FakeRaceManager.HillTroll },
            OversizedCreatureRaces.ResolveRaceIds(races));
    }
}
