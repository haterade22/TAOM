using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Spike;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>A troop id the shipped roster lacks gives a null at the first spawn of a wave (#1079), so the table is pinned to the shipped file.</summary>
[TestClass]
public class LongDefeatSpikeFortressTests
{
    private static string ModuleData(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var root = Path.Combine(dir.FullName, "Main", "_Module", "ModuleData");
            if (Directory.Exists(root))
                return Path.Combine(new[] { root }.Concat(parts).ToArray());
        }

        throw new InvalidOperationException("Main/_Module/ModuleData not found above " + AppContext.BaseDirectory);
    }

    [DataTestMethod]
    [DataRow("helms")]
    [DataRow("minas")]
    public void Troops_ForEachFortress_AllExistInTheShippedRoster(string name)
    {
        var shipped = XDocument.Load(ModuleData("fords_of_war", "fords_troops.xml")).Root!.Elements("NPCCharacter").Select(e => (string?)e.Attribute("id")).ToHashSet();

        var missing = LongDefeatSpikeFortress.For(name)!.Troops.Where(id => !shipped.Contains(id)).ToList();

        CollectionAssert.AreEqual(Array.Empty<string>(), missing, string.Join(", ", missing));
    }

    [DataTestMethod]
    [DataRow("helms")]
    [DataRow("minas")]
    public void PlayerAndScene_ForEachFortress_ExistInTheShippedData(string name)
    {
        var fortress = LongDefeatSpikeFortress.For(name)!;

        StringAssert.Contains(File.ReadAllText(ModuleData("characters", "custom_battle_lords.xml")), $"id=\"{fortress.Player}\"");
        StringAssert.Contains(File.ReadAllText(ModuleData("custom_battle_scenes.xml")), fortress.Scene);
    }

    [TestMethod]
    public void Troops_CoverEveryIdTheWavesAndTheGarrisonUse()
    {
        var fortress = LongDefeatSpikeFortress.For("helms")!;

        CollectionAssert.IsSubsetOf(fortress.AttackerInfantry, fortress.Troops.ToList());
        CollectionAssert.Contains(fortress.Troops.ToList(), fortress.AttackerRanged);
        CollectionAssert.Contains(fortress.Troops.ToList(), fortress.DefenderInfantry);
        CollectionAssert.Contains(fortress.Troops.ToList(), fortress.DefenderRanged);
    }

    [TestMethod]
    public void For_UnknownName_ReturnsNull()
    {
        Assert.IsNull(LongDefeatSpikeFortress.For("rivendell"));
    }
}
