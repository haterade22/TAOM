using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.AiPartySize;

namespace TAOM.Tests.Core;

/// <summary>
/// Config validation for the bandit/caravan strength parity shipped in
/// <c>taom_partyTemplates.xml</c>. These read the shipped XML off disk and recompute the same
/// number the engine compares, so they pin the invariant rather than the numbers.
///
/// <para><b>Why the invariant is a cliff and not a slope.</b> A party runs when
/// <c>DefaultMobilePartyAIModel.CalculateInitiativeScoresForEnemy</c> produces an
/// <c>avoidScore</c> above 1, and that score is scaled by
/// <c>num4 = ClampFloat((L &lt; 1) ? ClampFloat(1/L, 0.05, 3) : 0, 0.05, 3)</c> where <c>L</c> is
/// own strength over threat strength. At <c>L &gt;= 1</c> the term collapses to the 0.05 floor and
/// the score can never reach 1; below 1 it saturates at 3 almost at once. So a caravan marginally
/// stronger than a warband keeps trading and one marginally weaker runs, and a caravan eight times
/// weaker runs no harder than one twice as weak. Partial increases buy nothing.</para>
///
/// <para><b>Why fleeing matters beyond the flight itself.</b> Fleeing sets
/// <c>MobilePartyAi.IsAlerted</c>, and <c>CaravansCampaignBehavior.HourlyTickParty</c> will not
/// pick a new destination while a caravan is alerted or fleeing. A permanently threatened caravan
/// therefore stops trading, which is the economic failure this parity work exists to remove.</para>
///
/// <para>Strength is <c>sum(healthy * GetDefaultTroopPower) * moraleFactor</c>
/// (<c>DefaultMilitaryPowerModel.GetPowerOfParty</c>). The morale term is common to both sides and
/// is not modelled here; the margin below is what absorbs an asymmetry in it.</para>
/// </summary>
[TestClass]
public class CaravanBanditParityTests
{
    /// <summary>
    /// Every caravan roster must out-power the strongest roaming warband by at least this much.
    /// The surplus is headroom for the morale factor, which ranges 0.7 to 1.0
    /// (<c>MBMath.Map(Morale, 20, 40, 0.7f, 1f)</c>) and is applied to each side independently, so
    /// an unlucky pairing can move the comparison by up to 30%.
    /// </summary>
    private const double RequiredParityMargin = 1.05;

    /// <summary>The band every roaming raider warband must land in, in power.</summary>
    private const double RaiderPowerFloor = 70.0;
    private const double RaiderPowerCeiling = 90.0;

    /// <summary>
    /// The smallest member cap the engine can hand a caravan: base 20 plus the 10 a notable with
    /// Power under 100 contributes (<c>DefaultPartySizeLimitModel.CalculateMobilePartyMemberSizeLimit</c>).
    /// </summary>
    private const double SmallestVanillaCaravanCap = 30.0;

    // ---------------------------------------------------------------- shipped data

    // Parsed once per class, not once per test. LoadTroopsFromDisk walks every XML under troops/
    // and characters/ and XDocument.Load()s each one; seven of the nine tests below need it, so
    // without this the same few hundred files were parsed seven times for identical results.
    private static Dictionary<string, Troop> _troops;
    private static Dictionary<string, List<Stack>> _templates;

    private static Dictionary<string, Troop> Troops => _troops;
    private static Dictionary<string, List<Stack>> Templates => _templates;

    [ClassInitialize]
    public static void LoadShippedDataOnce(TestContext _)
    {
        _troops = LoadTroopsFromDisk();
        _templates = LoadTemplatesFromDisk();
    }


    private sealed class Troop
    {
        public int Level;
        public bool Mounted;
    }

    private sealed class Stack
    {
        public int Min;
        public int Max;
        public string Troop;
    }

    private static Dictionary<string, Troop> LoadTroopsFromDisk()
    {
        var index = new Dictionary<string, Troop>(StringComparer.Ordinal);
        foreach (var folder in new[] { "troops", "characters" })
        {
            var dir = Path.Combine(CultureDataFixture.ModuleDataPath(), folder);
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.GetFiles(dir, "*.xml"))
            {
                XDocument doc;
                try { doc = XDocument.Load(file); }
                catch (System.Xml.XmlException) { continue; }
                foreach (var node in doc.Descendants("NPCCharacter"))
                {
                    var id = (string)node.Attribute("id");
                    if (id == null || index.ContainsKey(id))
                        continue;
                    // A missing level= deserializes to 1 in the engine (BasicCharacterObject),
                    // which is tier 0. EveryCaravanTroop_DeclaresAnExplicitLevel is what stops
                    // that being an accident rather than a decision.
                    var raw = (string)node.Attribute("level");
                    var group = (string)node.Attribute("default_group") ?? "";
                    index[id] = new Troop
                    {
                        Level = raw == null ? 1 : int.Parse(raw),
                        // For a non-hero troop the engine sets _isMounted from
                        // DefaultFormationClass.IsMounted() during Deserialize, so default_group
                        // decides it outright, not the equipment.
                        Mounted = group == "Cavalry" || group == "HorseArcher",
                    };
                }
            }
        }
        return index;
    }

    private static Dictionary<string, List<Stack>> LoadTemplatesFromDisk()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_partyTemplates.xml");
        Assert.IsTrue(File.Exists(path), $"taom_partyTemplates.xml not found at {path}");
        var doc = XDocument.Load(path);
        return doc.Descendants("MBPartyTemplate").ToDictionary(
            t => (string)t.Attribute("id"),
            t => t.Descendants("PartyTemplateStack").Select(s => new Stack
            {
                Min = (int)s.Attribute("min_value"),
                Max = (int)s.Attribute("max_value"),
                Troop = ((string)s.Attribute("troop")).Replace("NPCCharacter.", ""),
            }).ToList(),
            StringComparer.Ordinal);
    }

    private static int TierFor(int level)
        => Math.Max(0, Math.Min(10, (int)Math.Ceiling((level - 5) / 5.0)));

    private static double PowerFor(string troopId, Dictionary<string, Troop> troops)
    {
        Assert.IsTrue(troops.ContainsKey(troopId),
            $"party template references '{troopId}', which no troop or character XML defines");
        var troop = troops[troopId];
        int tier = TierFor(troop.Level);
        Assert.IsTrue(tier <= 6,
            $"'{troopId}' is tier {tier}; above tier 6 TAOM overrides the vanilla power curve and "
            + "this test would need to read battle_balance_config.json rather than the closed form");
        // DefaultMilitaryPowerModel.GetDefaultTroopPower. TaomMilitaryPowerModel keeps this for
        // tiers 0-6 because OverrideVanillaTierPower ships false, and adds a mounted multiplier.
        double power = (2.0 + tier) * (10.0 + tier) * 0.02;
        if (troop.Mounted)
            power *= new TaomSettings().MountedMultiplier;
        return power;
    }

    private static double RosterPower(IEnumerable<Stack> stacks, bool useMin,
        Dictionary<string, Troop> troops)
        => stacks.Sum(s => (useMin ? s.Min : s.Max) * PowerFor(s.Troop, troops));

    private static IEnumerable<KeyValuePair<string, List<Stack>>> Raiders(
        Dictionary<string, List<Stack>> all)
        => all.Where(kv => kv.Key.EndsWith("_raider_party_template", StringComparison.Ordinal));

    private static IEnumerable<KeyValuePair<string, List<Stack>>> Caravans(
        Dictionary<string, List<Stack>> all)
        => all.Where(kv => kv.Key.StartsWith("caravan_template_", StringComparison.Ordinal)
                        || kv.Key.StartsWith("elite_caravan_template_", StringComparison.Ordinal));

    // ---------------------------------------------------------------- the sweep is real

    [TestMethod]
    public void TroopIndex_AndTemplateIndex_AreNotEmpty()
    {
        // A renamed folder would empty either index silently, and every assertion below would then
        // pass against nothing, reading exactly like a clean run. Same class of hazard as the
        // validator's UPGRADE_INDEX_EMPTY.
        var troops = Troops;
        var templates = Templates;

        Assert.IsTrue(troops.Count > 500, $"only {troops.Count} troops indexed");
        Assert.IsTrue(Raiders(templates).Count() >= 8,
            $"only {Raiders(templates).Count()} raider templates found");
        Assert.IsTrue(Caravans(templates).Count() >= 30,
            $"only {Caravans(templates).Count()} caravan templates found");
    }

    // ---------------------------------------------------------------- the Rohan regression

    [TestMethod]
    public void EveryCaravanTroop_DeclaresAnExplicitLevel()
    {
        // armed_trader_rohan, caravan_master_rohan, caravan_guard_rohan and
        // veteran_caravan_guard_rohan shipped with no level= attribute and no <skills> at all.
        // The engine reads that as level 1, so tier 0, so 0.40 power: a Rohan caravan was as strong
        // as a villager party and its troops had no weapon proficiency. Nothing else in the repo
        // could see it, because no reference was broken and no file failed to parse.
        var offenders = new List<string>();
        foreach (var folder in new[] { "troops", "characters" })
        {
            var dir = Path.Combine(CultureDataFixture.ModuleDataPath(), folder);
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.GetFiles(dir, "*.xml"))
            {
                XDocument doc;
                try { doc = XDocument.Load(file); }
                catch (System.Xml.XmlException) { continue; }
                offenders.AddRange(
                    from node in doc.Descendants("NPCCharacter")
                    where (string)node.Attribute("occupation") == "CaravanGuard"
                       && node.Attribute("level") == null
                    select $"{Path.GetFileName(file)}: {(string)node.Attribute("id")}");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "every CaravanGuard troop must declare level= explicitly, or the engine reads it as "
            + "level 1 (tier 0, 0.40 power) and the caravan is as weak as a villager party:\n  "
            + string.Join("\n  ", offenders));
    }

    // ---------------------------------------------------------------- the asymmetry regression

    [TestMethod]
    public void NoCaravanTemplate_CarriesADegenerateStack()
    {
        // Rohan, Dale and Dunland carried `min_value="1" max_value="1"` on their armed_trader stack
        // where the other fourteen carried 12/15, so their caravans were roughly half the bodies for
        // no recorded reason and nothing flagged it: the reference resolved, the file parsed, and
        // every validator passed.
        var offenders = new List<string>();
        foreach (var kv in Caravans(Templates))
            offenders.AddRange(
                from s in kv.Value
                where s.Min == 1 && s.Max == 1
                select $"{kv.Key}: {RoleOf(s.Troop)} pinned at 1/1");

        Assert.AreEqual(0, offenders.Count,
            "a caravan stack pinned at 1/1 contributes one man whatever the draw, which is how "
            + "three cultures ended up with half-sized caravans:\n  " + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void EveryCaravanTemplate_FieldsAllThreeRoles()
    {
        var offenders = new List<string>();
        foreach (var kv in Caravans(Templates))
        {
            var roles = kv.Value.Select(s => RoleOf(s.Troop)).ToHashSet();
            foreach (var required in new[] { "armed_trader", "caravan_guard", "veteran_caravan_guard" })
                if (!roles.Contains(required))
                    offenders.Add($"{kv.Key}: missing {required}");
        }

        Assert.AreEqual(0, offenders.Count,
            "every caravan template must field all three roles:\n  " + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void CaravanTemplatesOfAClass_AreWithinOneBandOfEachOther()
    {
        // The templates are deliberately NOT byte-identical across cultures: harad, rhun and
        // isengard field a caravan_guard and a veteran one tier below everyone else, so the power
        // budget buys them more bodies. That is correct, and asserting one shared stack shape would
        // wrongly forbid it. What must hold is that no culture ends up in a different weight class,
        // which is what the 1/1 trader stack actually caused.
        var troops = Troops;
        var templates = Templates;

        foreach (var prefix in new[] { "caravan_template_", "elite_caravan_template_" })
        {
            var powers = templates
                .Where(kv => kv.Key.StartsWith(prefix, StringComparison.Ordinal))
                .ToDictionary(kv => kv.Key, kv => RosterPower(kv.Value, useMin: true, troops));

            double spread = powers.Values.Max() / powers.Values.Min();
            Assert.IsTrue(spread <= 1.15,
                $"'{prefix}*' rosters span {spread:F2}x in power, so some cultures' caravans are in "
                + "a different weight class from others:\n  "
                + string.Join("\n  ", powers.OrderBy(kv => kv.Value)
                    .Select(kv => $"{kv.Key}: {kv.Value:F1}")));
        }
    }

    private static string RoleOf(string troopId)
    {
        // Longest first: "veteran_caravan_guard_x" also matches "caravan_guard" if tested loosely.
        foreach (var role in new[] { "veteran_caravan_guard", "caravan_guard", "armed_trader" })
            if (troopId.StartsWith(role + "_", StringComparison.Ordinal))
                return role;
        return troopId;
    }

    // ---------------------------------------------------------------- the balance invariants

    [TestMethod]
    public void EveryRaiderTemplate_LandsInTheIntendedPowerBand()
    {
        // A flat max_value cannot do this: the eight raider cultures run from T1,T2,T2,T3 to
        // T2,T3,T4,T4, so at a shared per-stack count their warbands spanned 64 to 112 power.
        // tools/rebalance_template_power.py solves each for the same budget instead.
        var troops = Troops;
        var offenders = new List<string>();
        foreach (var kv in Raiders(Templates))
        {
            double power = RosterPower(kv.Value, useMin: false, troops);
            if (power < RaiderPowerFloor || power > RaiderPowerCeiling)
                offenders.Add($"{kv.Key}: {power:F1}");
        }

        Assert.AreEqual(0, offenders.Count,
            $"every raider warband's full roster must land in [{RaiderPowerFloor}, "
            + $"{RaiderPowerCeiling}] power, or caravan parity holds for some cultures and not "
            + "others:\n  " + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void EveryCaravanTemplate_AtItsWeakestDraw_OutpowersTheStrongestWarband()
    {
        // The invariant the whole change exists to establish, stated at the worst case for the
        // caravan against the best case for the bandit.
        //
        // The caravan side uses min, not max, because a non-player caravan spawns at
        // `min + (max - min) * r` with one uniform r per party
        // (GetInitialPartySizeRatioForMobileParty returns party.RandomFloat()). Asserting the
        // midpoint would leave roughly half of all caravans below the line and still parked.
        //
        // Boss templates are excluded from the bandit side: BanditSpawnCampaignBehavior.AddBossParty
        // calls .Ai.DisableAi() on them, so they never leave their hideout and a caravan cannot meet
        // one on the road.
        var troops = Troops;
        var templates = Templates;

        double strongestWarband = Raiders(templates)
            .Max(kv => RosterPower(kv.Value, useMin: false, troops));

        var offenders = new List<string>();
        foreach (var kv in Caravans(templates))
        {
            double weakestDraw = RosterPower(kv.Value, useMin: true, troops);
            double ratio = weakestDraw / strongestWarband;
            if (ratio < RequiredParityMargin)
                offenders.Add($"{kv.Key}: {weakestDraw:F1} vs {strongestWarband:F1} -> L = {ratio:F2}");
        }

        Assert.AreEqual(0, offenders.Count,
            $"every caravan's weakest possible roster must out-power the strongest roaming warband "
            + $"by at least {RequiredParityMargin:F2}x, or avoidScore clears 1 and the caravan "
            + "flees, goes IsAlerted, and stops choosing trade destinations:\n  "
            + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void EliteCaravans_AreStrongerThanRegularOnes()
    {
        var troops = Troops;
        var templates = Templates;

        double regular = templates
            .Where(kv => kv.Key.StartsWith("caravan_template_", StringComparison.Ordinal))
            .Min(kv => RosterPower(kv.Value, useMin: true, troops));
        double elite = templates
            .Where(kv => kv.Key.StartsWith("elite_caravan_template_", StringComparison.Ordinal))
            .Min(kv => RosterPower(kv.Value, useMin: true, troops));

        Assert.IsTrue(elite > regular,
            $"an elite caravan ({elite:F1}) must out-power a regular one ({regular:F1}); vanilla "
            + "gates elite spawning on the owning notable's Power, so it has to mean something");
    }

    // ---------------------------------------------------------------- the C#/XML coupling

    [TestMethod]
    public void TheLargestCaravanRoster_FitsUnderTheSmallestCapTheEngineCanGiveIt()
    {
        // The coupling that a normal review misses, because the two halves live in different
        // languages. If the templates outgrow the cap, DesertionCampaignBehavior sheds a quarter of
        // the excess every day with no morale condition (it gates on IsLordParty || IsCaravan ||
        // IsGarrison) and GetOverPartySizeEffect, which is 1/(count/limit) - 1, costs the caravan
        // half its speed at twice the cap. That ships a strictly worse game than changing nothing.
        var troops = Troops;
        var templates = Templates;

        int largestRoster = Caravans(templates).Max(kv => kv.Value.Sum(s => s.Max));
        // CaravanPartyComponent.InitializeCaravanOnCreation inserts one CaravanMaster at the front
        // of the roster, on top of whatever the template drew.
        largestRoster += 1;

        double cap = SmallestVanillaCaravanCap + AiPartySizeService.DefaultCaravanFlatBonus;

        Assert.IsTrue(cap >= largestRoster,
            $"the caravan cap is {cap} but the widest shipped template can spawn {largestRoster} "
            + "men including the CaravanMaster. Raise AiPartySizeService.DefaultCaravanFlatBonus or "
            + "lower the caravan power budget in tools/rebalance_template_power.py; they are two "
            + "halves of one change.");
    }
}
