using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerArchetypeServiceTests
{
    private static readonly Dictionary<string, CareerArchetype> SampleMap = new()
    {
        ["ranger_of_ithilien"] = CareerArchetype.Ranged,
        ["knight_of_belfalas"] = CareerArchetype.Cavalry,
        ["captain_of_osgiliath"] = CareerArchetype.Infantry,
    };

    [TestMethod]
    public void TryGetArchetype_KnownRangedCareer_ReturnsTrueAndRanged()
    {
        var sut = new CareerArchetypeService(SampleMap);

        var found = sut.TryGetArchetype("ranger_of_ithilien", out var archetype);

        Assert.IsTrue(found);
        Assert.AreEqual(CareerArchetype.Ranged, archetype);
    }

    [TestMethod]
    public void TryGetArchetype_KnownCavalryCareer_ReturnsTrueAndCavalry()
    {
        var sut = new CareerArchetypeService(SampleMap);

        var found = sut.TryGetArchetype("knight_of_belfalas", out var archetype);

        Assert.IsTrue(found);
        Assert.AreEqual(CareerArchetype.Cavalry, archetype);
    }

    [TestMethod]
    public void TryGetArchetype_KnownInfantryCareer_ReturnsTrueAndInfantry()
    {
        var sut = new CareerArchetypeService(SampleMap);

        var found = sut.TryGetArchetype("captain_of_osgiliath", out var archetype);

        Assert.IsTrue(found);
        Assert.AreEqual(CareerArchetype.Infantry, archetype);
    }

    [TestMethod]
    public void TryGetArchetype_UnknownCareer_ReturnsFalse()
    {
        var sut = new CareerArchetypeService(SampleMap);

        var found = sut.TryGetArchetype("nonexistent_career", out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryGetArchetype_NullCareerId_ReturnsFalse()
    {
        var sut = new CareerArchetypeService(SampleMap);

        var found = sut.TryGetArchetype(null, out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryGetArchetype_EmptyCareerId_ReturnsFalse()
    {
        var sut = new CareerArchetypeService(SampleMap);

        var found = sut.TryGetArchetype(string.Empty, out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryGetArchetype_NullMap_HandledGracefully()
    {
        var sut = new CareerArchetypeService(null);

        var found = sut.TryGetArchetype("ranger_of_ithilien", out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void IoCMap_AllRegisteredCareers_HaveArchetypeAssigned()
    {
        var map = CareerSystemIoC.GetCareerArchetypeMap();

        // The full registered set is the source of truth. Sanity-check a representative
        // career from each archetype to catch accidental deletions.
        Assert.IsTrue(map.ContainsKey("captain_of_osgiliath"));
        Assert.IsTrue(map.ContainsKey("ranger_of_ithilien"));
        Assert.IsTrue(map.ContainsKey("knight_of_belfalas"));
        Assert.AreEqual(CareerArchetype.Infantry, map["captain_of_osgiliath"]);
        Assert.AreEqual(CareerArchetype.Ranged, map["ranger_of_ithilien"]);
        Assert.AreEqual(CareerArchetype.Cavalry, map["knight_of_belfalas"]);
    }
}
