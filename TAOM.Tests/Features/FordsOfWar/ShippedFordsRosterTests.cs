using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>
/// The mode's own roster: <c>fords_of_war/fords_troops.xml</c> holds a copy of every troop the mode fields, so a balance edit there
/// can never reach a campaign. Reads the shipped files.
/// </summary>
[TestClass]
public class ShippedFordsRosterTests
{
    private static readonly string ModuleRoot = Locate();

    private static string Fords(string name) => Path.Combine(ModuleRoot, "ModuleData", "fords_of_war", name);

    private static string Locate()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Main", "_Module");
            if (File.Exists(Path.Combine(candidate, "SubModule.xml")))
                return candidate;
        }

        throw new InvalidOperationException("Main/_Module not found above " + AppContext.BaseDirectory);
    }

    private static List<XElement> Roster() => XDocument.Load(Fords("fords_troops.xml")).Root!.Elements("NPCCharacter").ToList();

    /// <summary>Every troop the two config files name.</summary>
    private static HashSet<string> Named()
    {
        var named = new HashSet<string>();
        foreach (Match m in Regex.Matches(File.ReadAllText(Fords("factions.json")), "\"troop\"\\s*:\\s*\"([^\"]+)\""))
            named.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(File.ReadAllText(Fords("maps.json")), "\"troopId\"\\s*:\\s*\"([^\"]+)\""))
            named.Add(m.Groups[1].Value);
        return named;
    }

    [TestMethod]
    public void EveryTroopTheModeFields_IsAFordsCopyInTheRoster()
    {
        var roster = new HashSet<string>(Roster().Select(t => (string)t.Attribute("id")!));
        var named = Named();

        Assert.IsTrue(named.Count > 0);
        CollectionAssert.AreEqual(Array.Empty<string>(), named.Where(id => !id.StartsWith("fow_", StringComparison.Ordinal)).ToArray(), "names a campaign troop");
        CollectionAssert.AreEqual(Array.Empty<string>(), named.Except(roster).ToArray(), "named but not in the roster");
    }

    [TestMethod]
    public void TheRoster_HoldsOnlyTroopsTheModeFields()
    {
        var named = Named();

        var unused = Roster().Select(t => (string)t.Attribute("id")!).Where(id => !named.Contains(id)).ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), unused);
    }

    [TestMethod]
    public void EveryCopy_StaysOutOfTheCustomBattlePicker_AndHasNoUpgrades()
    {
        foreach (var troop in Roster())
        {
            var id = (string)troop.Attribute("id")!;
            // ArmyCompositionGroupVM lists c.IsSoldier && !c.IsObsolete, and Custom Battle loads CustomGame data too.
            Assert.AreEqual("true", (string?)troop.Attribute("is_obsolete"), id);
            Assert.IsNull(troop.Element("upgrade_targets"), id);
        }
    }

    [TestMethod]
    public void EveryCopy_HasAFaceAndEquipment()
    {
        foreach (var troop in Roster())
        {
            var id = (string)troop.Attribute("id")!;
            Assert.IsNotNull(troop.Element("face"), id);
            Assert.IsTrue(troop.Descendants("EquipmentRoster").Any(), id);
        }
    }

    [TestMethod]
    public void SubModule_LoadsTheRosterForCustomGameOnly()
    {
        var node = XDocument.Load(Path.Combine(ModuleRoot, "SubModule.xml")).Descendants("XmlNode")
            .Single(n => (string?)n.Element("XmlName")?.Attribute("path") == "fords_of_war/fords_troops");

        Assert.AreEqual("NPCCharacters", (string)node.Element("XmlName")!.Attribute("id")!);
        CollectionAssert.AreEqual(new[] { "CustomGame" },
            node.Descendants("GameType").Select(g => (string)g.Attribute("value")!).ToArray());
    }
}
