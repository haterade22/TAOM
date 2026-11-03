using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

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

    private static FordsConfigCatalog Catalog()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(Path.Combine(ModuleRoot, "ModuleData"));
        return new FordsConfigCatalog(paths, Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void EveryFaction_HasFeatsAndSaysWhatItIsGoodAndBadAt()
    {
        foreach (var faction in Catalog().Factions)
        {
            Assert.IsNotNull(faction.Feats, faction.FactionId);
            Assert.IsTrue(faction.Traits!.Any(t => t.Strength), faction.FactionId + " strength");
            Assert.IsTrue(faction.Traits!.Any(t => !t.Strength), faction.FactionId + " weakness");
        }
    }

    [TestMethod]
    public void Rohan_IsMostlyRiders_AndItsRidersBarelyHurtTowers()
    {
        Assert.IsTrue(Catalog().TryGetFaction("rohan", out var rohan));

        var mounted = rohan.Units.Count(u => u.Role == UnitRole.Cavalry || u.Role == UnitRole.HorseArcher);

        Assert.IsTrue(mounted > rohan.Units.Count / 2, $"{mounted} of {rohan.Units.Count}");
        Assert.IsTrue(rohan.Feats!.MountedDamageToTowers < 1f);
    }

    [TestMethod]
    public void Gondor_RotatesRegionsOfFourOffers_EverySlotFixed_OnAThirtySecondTimer()
    {
        Assert.IsTrue(Catalog().TryGetFaction("gondor", out var gondor));

        var rotation = gondor.Rotation!;

        Assert.AreEqual(30f, rotation.Seconds);
        Assert.IsTrue(rotation.CallFee > 0);
        Assert.IsTrue(rotation.Regions.Count >= 8, rotation.Regions.Count + " regions");
        foreach (var region in rotation.Regions)
            Assert.AreEqual(4, region.Units.Count, region.Id);
    }

    [TestMethod]
    public void Mordor_CallsCreaturesOnAThirtySecondTimer_AndAtMostOneOfThemPerKey()
    {
        Assert.IsTrue(Catalog().TryGetFaction("mordor", out var mordor));

        Assert.AreEqual(30f, mordor.Creatures!.CooldownSeconds);
        Assert.IsTrue(mordor.Creatures.Offers.Count > 0 && mordor.Creatures.Offers.Count <= 4);
        Assert.IsTrue(mordor.Feats!.UnitCapBonus > 0);
    }

    [TestMethod]
    public void OfferIds_AreUniqueInAFaction_SoABuyAlwaysFindsItsOffer()
    {
        foreach (var faction in Catalog().Factions)
        {
            var repeats = faction.AllOffers.GroupBy(o => o.OfferId).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            CollectionAssert.AreEqual(Array.Empty<string>(), repeats, faction.FactionId);
        }
    }

    [TestMethod]
    public void EveryAiBuyCycleEntry_IsAnOfferOrARoleTheFactionCanShow()
    {
        foreach (var faction in Catalog().Factions)
        {
            Assert.IsTrue(faction.AiBuyCycle is { Count: > 0 }, faction.FactionId + " has no buy cycle");
            var known = new HashSet<string>(faction.Units.Select(o => o.OfferId));
            known.UnionWith(faction.AllOffers.Where(o => faction.Creatures?.Offers.Contains(o) != true).Select(o => o.Role.ToString()));
            var unknown = faction.AiBuyCycle!.Where(e => !known.Contains(e)).ToArray();
            CollectionAssert.AreEqual(Array.Empty<string>(), unknown, faction.FactionId);
        }
    }

    [TestMethod]
    public void TheAiBuyCycles_ShowEachFactionsIdentity()
    {
        var catalog = Catalog();
        Assert.IsTrue(catalog.TryGetFaction("mordor", out var mordor));
        Assert.IsTrue(catalog.TryGetFaction("rohan", out var rohan));

        Assert.IsTrue(mordor.AiBuyCycle!.Count(e => e == "orc_rabble") * 3 >= mordor.AiBuyCycle.Count, "a horde buys cheap packs most often");
        Assert.IsTrue(rohan.AiBuyCycle!.Count(e => e == "militia" || e == "yeomen") >= 2, "Rohan keeps some foot to take towers");
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
