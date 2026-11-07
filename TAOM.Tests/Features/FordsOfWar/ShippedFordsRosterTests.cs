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
    public void Rohan_FieldsCheapRidersOften_InEveryFreeWave_AndMostOfItsAiBuys()
    {
        // Mike, 2026-11-05: "Rohan needs cheap cavalry because they seem weak. They should get a lot of cavalry opportunities."
        Assert.IsTrue(Catalog().TryGetFaction("rohan", out var rohan));
        var riders = rohan.Units.Where(u => u.Role == UnitRole.Cavalry || u.Role == UnitRole.HorseArcher).Select(u => u.OfferId).ToHashSet();

        foreach (var wave in rohan.FreeWaves)
            Assert.AreEqual(4, wave.Units.Where(u => u.Role == UnitRole.Cavalry).Sum(u => u.Count), $"the wave from minute {wave.FromMinute} brings 4 riders (5 won 10 of 10, Mike, 2026-11-06)");
        var cycle = rohan.AiBuyCycle!.Where(e => !e.StartsWith("up:", StringComparison.Ordinal)).ToList();
        Assert.IsTrue(cycle.Count(riders.Contains) * 3 >= cycle.Count * 2, "two of three AI pack buys are riders");
    }

    [TestMethod]
    public void RoundTwoResults_GiveTheLosingFactionsLargerFreeWaves()
    {
        // Round robin 2 on 329e841d2 (balance-log.md): Dol Guldur won 0 of 12 and Isengard 3 of 12, each for the second round.
        // Gundabad's lanes were already full (76.8 of 90 on average), so a larger wave would only wait: it gets cheaper upgrades instead.
        foreach (var wave in Shipped("dolguldur").FreeWaves)
            Assert.AreEqual(12, wave.Units.Sum(u => u.Count), $"Dol Guldur's wave from minute {wave.FromMinute}: two more uruks");
        foreach (var wave in Shipped("isengard").FreeWaves)
            Assert.AreEqual(12, wave.Units.Sum(u => u.Count), $"Isengard's wave from minute {wave.FromMinute}: a lower price did not move it, so one more uruk and one more spearman");
    }

    [TestMethod]
    public void EveryFaction_GuardsItsHeroesWithThreeToSixOfItsOwnEliteTroops()
    {
        // Mike, 2026-11-05: "When summoning a hero unit for every culture they should be summoned with specific elite troops that guard them."
        var groups = Roster().ToDictionary(t => (string)t.Attribute("id")!, t => (string?)t.Attribute("default_group") ?? "Infantry");
        foreach (var faction in Catalog().Factions)
        {
            var guard = faction.HeroGuard;
            Assert.IsNotNull(guard, faction.FactionId);
            Assert.IsTrue(guard!.Count >= 3 && guard.Count <= 6, $"{faction.FactionId}: {guard.Count}");
            var offer = faction.AllOffers.FirstOrDefault(o => o.TroopId == guard.TroopId);
            Assert.IsNotNull(offer, $"{faction.FactionId}: the guard is one of the faction's own offers");
            Assert.AreEqual(offer!.Role, guard.Role, faction.FactionId);
            var group = guard.Role switch { UnitRole.Ranged => "Ranged", UnitRole.Cavalry => "Cavalry", UnitRole.HorseArcher => "HorseArcher", _ => "Infantry" };
            Assert.AreEqual(groups[guard.TroopId], group, faction.FactionId);
        }
    }

    [TestMethod]
    public void EveryFreeWaveTroop_SpawnsInTheRoleItsTroopDeclares()
    {
        // WaveScheduler takes a wave unit's role from the entry, else from an offer of the same troop, else Infantry; an archer with neither
        // counted as melee near a tower and could fail check 09 (Codex and Lens 5, 2026-11-05).
        var groups = Roster().ToDictionary(t => (string)t.Attribute("id")!, t => (string?)t.Attribute("default_group") ?? "Infantry");
        foreach (var faction in Catalog().Factions)
        {
            foreach (var unit in faction.FreeWaves.SelectMany(w => w.Units))
            {
                var role = unit.Role ?? faction.AllOffers.FirstOrDefault(o => o.TroopId == unit.TroopId)?.Role ?? UnitRole.Infantry;
                // Spear and Siege are TAOM's kinds of foot soldier, so only the engine's four classes are compared.
                var group = role switch { UnitRole.Ranged => "Ranged", UnitRole.Cavalry => "Cavalry", UnitRole.HorseArcher => "HorseArcher", _ => "Infantry" };
                Assert.AreEqual(groups[unit.TroopId], group, $"{faction.FactionId}/{unit.TroopId} spawns as {role}");
            }
        }
    }

    [TestMethod]
    public void DolGuldur_FieldsMostlyUruks_WithKhamulCavalry_SpiderRidersToBuy_AndAGoblinSwarm()
    {
        // Mike, 2026-11-05: "a lot more Dol Guldur Uruks as units instead of Goblins, maybe one or two goblins for swarm", three rows,
        // Khamul cavalry and spider riders. Spiders are bought, so the creature call keeps only the fell wargs.
        Assert.IsTrue(Catalog().TryGetFaction("dolguldur", out var dg));
        var troops = dg.Units.Select(u => u.TroopId).ToList();

        Assert.AreEqual(12, dg.Units.Count, "three rows of four");
        Assert.IsTrue(troops.Count(t => t.StartsWith("fow_dg_uruk_")) >= 7, string.Join(",", troops));
        Assert.IsTrue(troops.Count(t => t.StartsWith("fow_dg_goblin_")) <= 1, "one goblin swarm pack");
        Assert.IsTrue(dg.Units.Any(u => u.Role == UnitRole.Cavalry && u.TroopId.StartsWith("fow_dg_khamul_")), "Khamul cavalry");
        Assert.IsTrue(troops.Contains("fow_goblin_spider_rider"), "spider riders in the buy grid");
        Assert.IsFalse(dg.Creatures!.Offers.Any(c => c.TroopId == "fow_goblin_spider_rider"), "spiders are not also a creature call");
        foreach (var wave in dg.FreeWaves)
        {
            var uruks = wave.Units.Where(u => u.TroopId.StartsWith("fow_dg_uruk_")).Sum(u => u.Count);
            var goblins = wave.Units.Where(u => u.TroopId.StartsWith("fow_dg_goblin_")).Sum(u => u.Count);
            Assert.IsTrue(uruks > 2 * goblins && goblins >= 1, $"minute {wave.FromMinute}: {uruks} uruks, {goblins} goblins");
        }
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
            known.UnionWith(FordsMatchConfig.Default.UpgradesOrDefault.Select(t => "up:" + t.Id));
            known.UnionWith((faction.Creatures?.Offers ?? new List<UnitOffer>()).Select(o => "beast:" + o.OfferId));
            var unknown = faction.AiBuyCycle!.Where(e => !known.Contains(e)).ToArray();
            CollectionAssert.AreEqual(Array.Empty<string>(), unknown, faction.FactionId);
        }
    }

    [TestMethod]
    public void Sentries_GiveRohanTheFastestTowers_AndStoneGivesGondorTheToughest()
    {
        var factions = Catalog().Factions.ToList();
        var rohan = factions.Single(f => f.FactionId == "rohan").FeatsOrNone;
        var gondor = factions.Single(f => f.FactionId == "gondor").FeatsOrNone;

        Assert.IsTrue(factions.Where(f => f.FactionId != "rohan").All(f => f.FeatsOrNone.TowerFireRate < rohan.TowerFireRate), "Rohan's towers fire fastest");
        Assert.IsTrue(factions.Where(f => f.FactionId != "gondor").All(f => f.FeatsOrNone.TowerHitPoints < gondor.TowerHitPoints), "Gondor's towers are toughest");
    }

    private static FactionCatalog Shipped(string id)
    {
        Assert.IsTrue(Catalog().TryGetFaction(id, out var faction), id);
        return faction;
    }

    [TestMethod]
    public void FarSight_GivesTheElvesTheLongestTowers_AndTheFewestTroopsKindsAndSmallestPacks()
    {
        var elves = new[] { "rivendell", "mirkwood" };
        var others = Catalog().Factions.Where(f => !elves.Contains(f.FactionId)).ToList();
        foreach (var elf in elves.Select(Shipped))
        {
            Assert.IsTrue(others.All(f => f.FeatsOrNone.TowerRange < elf.FeatsOrNone.TowerRange), elf.FactionId + " longest towers");
            Assert.IsTrue(others.All(f => f.FeatsOrNone.UnitCapBonus > elf.FeatsOrNone.UnitCapBonus), elf.FactionId + " fewest troops");
            Assert.IsTrue(others.All(f => f.Units.Count + (f.Rotation == null ? 0 : 4) > elf.Units.Count), elf.FactionId + " fewest kinds on show");
            Assert.IsTrue(elf.Units.All(o => o.PackSize <= 5), elf.FactionId + " a low count per troop");
            Assert.IsTrue(elf.FreeWaves.All(w => w.Units.Sum(u => u.Count) <= 5), elf.FactionId + " a small free wave");
        }

        var rivendell = Shipped("rivendell");
        Assert.AreEqual(rivendell.Heroes.Count, FordsMatchConfig.Default.Heroes.MaxAlivePerSide + rivendell.FeatsOrNone.HeroSlotBonus, "all its heroes may fight at once");
    }

    [TestMethod]
    public void Dale_AloneHurtsTowersFromRange()
    {
        var dale = Shipped("dale").FeatsOrNone;

        Assert.IsTrue(Catalog().Factions.Where(f => f.FactionId != "dale").All(f => f.FeatsOrNone.RangedDamageToTowers < dale.RangedDamageToTowers));
    }

    [TestMethod]
    public void DolGuldur_AloneSlowsTheEnemysTowers_AndCallsFellWargs()
    {
        var dolguldur = Shipped("dolguldur");

        Assert.IsTrue(Catalog().Factions.Where(f => f.FactionId != "dolguldur").All(f => f.FeatsOrNone.EnemyTowerFireRate > dolguldur.FeatsOrNone.EnemyTowerFireRate));
        CollectionAssert.Contains(dolguldur.Creatures!.Offers.Select(o => o.TroopId).ToList(), "fow_dg_fell_warg_rider");
    }

    [TestMethod]
    public void Easterlings_AloneAnswerOrdersFaster()
    {
        var easterlings = Shipped("easterlings").FeatsOrNone;

        Assert.IsTrue(Catalog().Factions.Where(f => f.FactionId != "easterlings").All(f => f.FeatsOrNone.OrderCooldown > easterlings.OrderCooldown));
    }

    [TestMethod]
    public void Umbar_AloneGetsRichFromTheMap()
    {
        var others = Catalog().Factions.Where(f => f.FactionId != "umbar").ToList();
        var umbar = Shipped("umbar").FeatsOrNone;

        Assert.IsTrue(others.All(f => f.FeatsOrNone.CampReward < umbar.CampReward));
        Assert.IsTrue(others.All(f => f.FeatsOrNone.CaptureIncome < umbar.CaptureIncome));
    }

    [TestMethod]
    public void MountainGold_GivesEreborTheMostIncome_AndTheFastestMending_ButTheSlowestWaves()
    {
        var others = Catalog().Factions.Where(f => f.FactionId != "erebor").ToList();
        var erebor = Shipped("erebor").FeatsOrNone;

        Assert.IsTrue(others.All(f => f.FeatsOrNone.Income < erebor.Income));
        Assert.IsTrue(others.All(f => f.FeatsOrNone.TowerRegen < erebor.TowerRegen));
        Assert.IsTrue(others.Where(f => f.FeatsOrNone.TowerRange <= 1f).All(f => f.FeatsOrNone.WaveInterval < erebor.WaveInterval), "the slowest waves but for the elves' few");
    }

    [TestMethod]
    public void Harad_CallsOneGreatBeastAtATime_OnItsOwnLongTimer()
    {
        var harad = Shipped("harad");

        Assert.AreEqual(120f, harad.Creatures!.CooldownSeconds);
        CollectionAssert.AreEquivalent(new[] { "fow_harad_elephant_rider", "fow_harad_mumakil_rider" }, harad.Creatures.Offers.Select(o => o.TroopId).ToArray());
        Assert.IsTrue(harad.Creatures.Offers.All(o => o.PackSize == 1), "one beast at a time");
        Assert.AreEqual(1000, harad.Creatures.Offers.Single(o => o.TroopId == "fow_harad_mumakil_rider").PackPrice, "a player stacks gold to 1000 for a mumak");
        CollectionAssert.Contains(harad.AiBuyCycle!.ToList(), "beast:mumak");
    }

    [TestMethod]
    public void Gundabad_RushesEarly_AndFades()
    {
        var others = Catalog().Factions.Where(f => f.FactionId != "gundabad").ToList();
        var gundabad = Shipped("gundabad");

        Assert.IsTrue(others.All(f => f.FeatsOrNone.WaveInterval > gundabad.FeatsOrNone.WaveInterval), "the fastest waves");
        Assert.IsTrue(others.All(f => f.FeatsOrNone.TowerHitPoints > gundabad.FeatsOrNone.TowerHitPoints), "the weakest towers");
        Assert.AreEqual(1, gundabad.FreeWaves.Count, "its free waves never improve");
    }

    [TestMethod]
    public void EveryShippedFaction_HasItsOwnAccentColour()
    {
        var accents = Catalog().Factions.Select(f => TAOM.Client.Features.FordsOfWar.FordsFactionStyle.Accent(f.FactionId)).ToList();

        Assert.AreEqual(accents.Count, accents.Distinct().Count());
        Assert.IsFalse(accents.Contains(TAOM.Client.Features.FordsOfWar.FordsFactionStyle.Accent("not_a_faction")));
    }

    [TestMethod]
    public void EveryCreatureFaction_NamesItsBeastsInItsBuyOrder_OrTheAiNeverCallsThem()
    {
        foreach (var faction in Catalog().Factions)
        {
            var offers = faction.Creatures?.Offers.Select(o => o.OfferId).ToList() ?? new List<string>();
            var beasts = faction.AiBuyCycle!.Where(e => e.StartsWith("beast:", StringComparison.Ordinal)).Select(e => e.Substring("beast:".Length)).ToList();
            CollectionAssert.AreEqual(Array.Empty<string>(), beasts.Except(offers).ToArray(), faction.FactionId + " names a beast it cannot call");
            Assert.AreEqual(offers.Count > 0, beasts.Count > 0, faction.FactionId + " has creatures its AI never calls");
        }
    }

    [TestMethod]
    public void EveryFaction_BuysUpgrades_AndPricesOnlyRealOnes()
    {
        var tracks = FordsMatchConfig.Default.UpgradesOrDefault.Select(t => t.Id).ToList();
        foreach (var faction in Catalog().Factions)
        {
            Assert.IsTrue(faction.AiBuyCycle!.Any(e => e.StartsWith("up:", StringComparison.Ordinal)), faction.FactionId + " never buys an upgrade");
            Assert.IsTrue(faction.UpgradeCost is { Count: > 0 }, faction.FactionId + " has no upgrade prices of its own");
            CollectionAssert.AreEqual(Array.Empty<string>(), faction.UpgradeCost!.Keys.Except(tracks).ToArray(), faction.FactionId);
        }
    }

    [TestMethod]
    public void TheShippedHeroArrival_IsEveryFiveMinutes_AndIsTheDefault()
    {
        Assert.AreEqual(300f, Catalog().Match.Heroes.FreeArrivalSeconds);
        Assert.AreEqual(300f, FordsMatchConfig.Default.Heroes.FreeArrivalSeconds);
    }

    [TestMethod]
    public void TheShippedTowerLimit_MakesAnOuterTowerLastOverTwoMinutes_AndIsTheDefault()
    {
        var towers = Catalog().Match.Towers;

        Assert.AreEqual(FordsMatchConfig.Default.Towers.AssaultDamagePerSecond, towers.AssaultDamagePerSecond);
        Assert.IsTrue(towers.TowerHitPoints / towers.AssaultDamagePerSecond > 120f, "a horde must not take a tower in seconds");
    }

    [TestMethod]
    public void TheShippedWear_StartsAfterTheBalanceWindow_AndIsTheDefault()
    {
        var towers = Catalog().Match.Towers;

        Assert.AreEqual(FordsMatchConfig.Default.Towers.WearStartMinutes, towers.WearStartMinutes);
        Assert.AreEqual(FordsMatchConfig.Default.Towers.WearPerMinute, towers.WearPerMinute);
        Assert.IsTrue(towers.WearStartMinutes >= 15f, "balance runs read the first 15 minutes, before any wear");
        Assert.IsTrue(towers.WearPerMinute > 0f, "even sides must finish");
    }

    [TestMethod]
    public void TheShippedHeroFactors_MakeAHeroWorthItsPrice_AndAreTheDefault()
    {
        Assert.AreEqual(FordsMatchConfig.Default.Heroes.DamageFactor, Catalog().Match.Heroes.DamageFactor);
        Assert.AreEqual(FordsMatchConfig.Default.Heroes.DamageTakenFactor, Catalog().Match.Heroes.DamageTakenFactor);
        Assert.IsTrue(Catalog().Match.Heroes.DamageFactor > 1f, "a hero's blows must beat a soldier's");
        Assert.IsTrue(Catalog().Match.Heroes.DamageTakenFactor < 1f, "a hero must outlast a soldier");
    }

    [TestMethod]
    public void TheShippedAiReserves_KeepHeroesToSurplusGold_AndAreTheDefaults()
    {
        foreach (var name in new[] { "easy", "normal", "hard" })
        {
            Assert.AreEqual(FordsMatchConfig.Default.AiProfiles[name].HeroReserveGold, Catalog().Match.AiProfiles[name].HeroReserveGold, name);
            Assert.IsTrue(Catalog().Match.AiProfiles[name].HeroReserveGold >= 500, name + " would spend its unit gold on heroes");
        }
    }

    [TestMethod]
    public void TheShippedUpgrades_AreTheDefaults()
    {
        var shipped = Catalog().Match.UpgradesOrDefault;
        var defaults = FordsMatchConfig.Default.UpgradesOrDefault;

        CollectionAssert.AreEqual(defaults.Select(t => t.Id).ToArray(), shipped.Select(t => t.Id).ToArray());
        for (var i = 0; i < defaults.Count; i++)
        {
            Assert.AreEqual(defaults[i].Name, shipped[i].Name);
            Assert.AreEqual(defaults[i].Effect, shipped[i].Effect);
            Assert.AreEqual(defaults[i].StepPerLevel, shipped[i].StepPerLevel, 0.0001f);
            CollectionAssert.AreEqual(defaults[i].Prices.ToArray(), shipped[i].Prices.ToArray());
        }
    }

    [DataTestMethod]
    [DataRow("rivendell", "TAOM_FOW_Trait_Rivendell_SlowWaves")]
    [DataRow("mirkwood", "TAOM_FOW_Trait_Mirkwood_SlowWaves")]
    public void AnElfFactionWithSlowFreeWaves_SaysSoInAWeakness(string faction, string key)
    {
        var shipped = Shipped(faction);
        Assert.IsTrue(shipped.FeatsOrNone.WaveInterval > 1f);
        Assert.IsTrue(shipped.Traits.Any(t => !t.Strength && t.Text.Contains(key)), faction);
    }

    [TestMethod]
    public void Mordor_SendsTheLargestFreeWaves_BecauseOnlyASwarmBeatsTheElves()
    {
        var factions = Catalog().Factions;
        var mordor = Shipped("mordor");
        var largest = mordor.FreeWaves.Max(w => w.Units.Sum(u => u.Count));
        foreach (var other in factions.Where(f => f.FactionId != "mordor"))
            Assert.IsTrue(other.FreeWaves.All(w => w.Units.Sum(u => u.Count) < largest), other.FactionId);
        Assert.IsTrue(mordor.FreeWaves.All(w => w.Units.Sum(u => u.Count) >= 25), "every Mordor wave is a swarm");
    }

    [TestMethod]
    public void Rivendell_HasTheSmallestFreeWaves_AndMordorsOrcsCanHurtArmour()
    {
        var rivendell = Shipped("rivendell");
        var mordor = Shipped("mordor");

        Assert.IsTrue(rivendell.FreeWaves.All(w => w.Units.Sum(u => u.Count) == 3), "evidence: 2027 orcs fell to Rivendell's free waves");
        Assert.AreEqual(2f, rivendell.FeatsOrNone.WaveInterval);
        CollectionAssert.DoesNotContain(mordor.FreeWaves.SelectMany(w => w.Units).Select(u => u.TroopId).ToList(), "fow_mordor_orc_recruit", "an L1 orc's blow does nothing to armour");
        Assert.AreEqual("fow_mordor_orc_fighter", mordor.Units.Single(o => o.OfferId == "orc_rabble").TroopId);
    }

    [TestMethod]
    public void OnlyHaradAndDolGuldur_PoisonTheirArrows_AndHaradsIsTheStronger()
    {
        var poisoners = Catalog().Factions.Where(f => f.FeatsOrNone.PoisonPerSecond > 0f).Select(f => f.FactionId).ToArray();

        CollectionAssert.AreEquivalent(new[] { "harad", "dolguldur" }, poisoners);
        Assert.IsTrue(Shipped("harad").FeatsOrNone.PoisonPerSecond > Shipped("dolguldur").FeatsOrNone.PoisonPerSecond);
        Assert.IsTrue(Shipped("harad").FeatsOrNone.PoisonSeconds > 0f);
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
    public void SubModule_LoadsTheTowerArrowForCustomGameOnly()
    {
        var node = XDocument.Load(Path.Combine(ModuleRoot, "SubModule.xml")).Descendants("XmlNode")
            .Single(n => (string?)n.Element("XmlName")?.Attribute("path") == "fords_of_war/fords_items");

        Assert.AreEqual("Items", (string)node.Element("XmlName")!.Attribute("id")!);
        CollectionAssert.AreEqual(new[] { "CustomGame" },
            node.Descendants("GameType").Select(g => (string)g.Attribute("value")!).ToArray());
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
