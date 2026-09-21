using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion.GarrisonSwap;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Tests.Features.CultureConversion;

/// <summary>
/// The data gate for the garrison and militia swap.
///
/// Why this exists: the swap's fail-safe is to KEEP a stack it cannot place, so a hole in a
/// culture's authored roster does not crash and does not delete troops. It just silently does
/// nothing, and a Mordor-held Gondor city quietly keeps its Gondor wall. That is exactly the class
/// of failure nobody notices for months, so the holes are made into a build failure here instead.
///
/// TAOM's rosters really are uneven. Measured 2026-09-20 across the 16 <c>troops_*.xml</c> files:
/// Mirkwood fields nothing at all at tiers 4, 5 or 6; Goblin, Blue Craig and the Misty Mountain
/// orcs field no cavalry at any tier; Dunland, Dale and Umbar stop at tier 6. The mapper's fallback
/// ladder is what covers those, and <see cref="EveryConversionTargetCanReplaceEveryTierAndRole"/> is the
/// test that proves it actually does, against the shipped data rather than a fixture.
///
/// Data-only, so it needs no running game and finishes in milliseconds. Tier is derived here with
/// the engine's own formula, <c>clamp(ceil((level-5)/5), 0, 10)</c> — TAOM raises MaxCharacterTier
/// to 10 in <c>TaomCharacterStatsModel</c>. At runtime the adapter asks the engine instead.
/// </summary>
[TestClass]
public class GarrisonCultureCoverageTests
{
    private const int MaxTier = 10;

    private static readonly TroopRole[] AllRoles =
    {
        TroopRole.Infantry, TroopRole.Ranged, TroopRole.Cavalry, TroopRole.HorseArcher,
    };

    private static readonly string[] MilitiaAttributes =
    {
        "melee_militia_troop", "ranged_militia_troop", "melee_elite_militia_troop", "ranged_elite_militia_troop",
    };

    private sealed class Troop
    {
        public string Id = "";
        public string Culture = "";
        public int Tier;
        public TroopRole Role;
        public string Occupation = "";
        public List<string> UpgradeTargets = new List<string>();
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string ModuleData() => Path.Combine(FindRepoRoot(), "Main", "_Module", "ModuleData");

    private static int TierFromLevel(int level)
    {
        var tier = (int)Math.Ceiling((level - 5) / 5.0);
        return tier < 0 ? 0 : (tier > MaxTier ? MaxTier : tier);
    }

    private static TroopRole RoleFromGroup(string? group) => group switch
    {
        "Infantry" => TroopRole.Infantry,
        "Ranged" => TroopRole.Ranged,
        "Cavalry" => TroopRole.Cavalry,
        "HorseArcher" => TroopRole.HorseArcher,
        _ => TroopRole.Unknown,
    };

    /// <summary>
    /// Every NPCCharacter TAOM ships, not just <c>troops/</c>. The adapter walks the live
    /// <c>CharacterObject</c> graph, and several pooled lines live in <c>characters/</c>.
    /// </summary>
    private static Dictionary<string, Troop> LoadTroops()
    {
        var moduleData = ModuleData();
        Assert.IsTrue(Directory.Exists(moduleData), $"ModuleData not found at {moduleData}");

        var troops = new Dictionary<string, Troop>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(moduleData, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Split(Path.DirectorySeparatorChar).Contains("Languages"))
                continue;

            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (System.Xml.XmlException) { continue; }
            if (doc.Root == null)
                continue;

            foreach (var node in doc.Descendants("NPCCharacter"))
            {
                var id = (string?)node.Attribute("id");
                var level = (string?)node.Attribute("level");
                if (string.IsNullOrEmpty(id) || !int.TryParse(level, out var lvl) || troops.ContainsKey(id!))
                    continue;

                troops[id!] = new Troop
                {
                    Id = id!,
                    Culture = ((string?)node.Attribute("culture") ?? "").Replace("Culture.", ""),
                    Tier = TierFromLevel(lvl),
                    Role = RoleFromGroup((string?)node.Attribute("default_group")),
                    Occupation = (string?)node.Attribute("occupation") ?? "Soldier",
                    UpgradeTargets = node.Descendants("upgrade_target")
                        .Select(u => ((string?)u.Attribute("id") ?? "").Replace("NPCCharacter.", ""))
                        .Where(u => !string.IsNullOrEmpty(u))
                        .ToList(),
                };
            }
        }

        Assert.IsTrue(troops.Count > 800, $"Only {troops.Count} troops parsed — the troop data or its layout moved.");
        return troops;
    }

    /// <summary>
    /// Every militia troop id bound by any culture, from the native culture XML and from the XSLT
    /// that retags the six vanilla culture ids (Dunland, Harad, Rohan, Rhun, Dale, Khand).
    /// </summary>
    private static HashSet<string> LoadMilitiaTroopIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var culture in XDocument.Load(Path.Combine(ModuleData(), "taom_spcultures.xml")).Descendants("Culture"))
        {
            foreach (var attribute in MilitiaAttributes)
            {
                var value = (string?)culture.Attribute(attribute);
                if (!string.IsNullOrEmpty(value))
                    ids.Add(value!.Replace("NPCCharacter.", ""));
            }
        }

        var xsltPath = Path.Combine(ModuleData(), "spcultures.xslt");
        if (File.Exists(xsltPath))
        {
            var pattern = new Regex(
                @"<xsl:attribute\s+name=""(?<attr>[a-z_]*militia_troop)""\s*>\s*(?<value>[^<]+?)\s*</xsl:attribute>",
                RegexOptions.IgnoreCase);
            foreach (Match match in pattern.Matches(File.ReadAllText(xsltPath)))
            {
                if (MilitiaAttributes.Contains(match.Groups["attr"].Value))
                    ids.Add(match.Groups["value"].Value.Replace("NPCCharacter.", ""));
            }
        }

        Assert.IsTrue(ids.Count > 20, $"Only {ids.Count} militia troop ids found — the culture data or its layout moved.");
        return ids;
    }

    private static VolunteerRecruitmentService Recruitment()
    {
        var random = Substitute.For<IRandomProvider>();
        random.Next(Arg.Any<int>()).Returns(0);
        return new VolunteerRecruitmentService(random, Substitute.For<IModLogger>());
    }

    /// <summary>
    /// Every culture a fief can convert TO, taken from the recruitment pools themselves rather than
    /// from the troop XML.
    ///
    /// Taking it from the troop files is the bug this gate is supposed to catch: four real
    /// conversion targets (<c>lothlorien</c>, <c>battania</c>, <c>shaghana</c>, <c>abanissa</c>)
    /// author no troops of their own, so a candidate list built from <c>troops_*.xml</c> silently
    /// omitted exactly the cultures that were broken, and the gate passed while a Lothlorien
    /// conversion re-manned a whole city with practice dummies.
    /// </summary>
    private static List<string> ConversionTargets()
    {
        var targets = Recruitment().GetPooledCultureIds()
            .Where(c => !string.IsNullOrEmpty(c))
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        Assert.IsTrue(targets.Count >= 20,
            $"Only {targets.Count} recruitable cultures found — the recruitment pools moved, and a gate "
            + "built on them would be passing vacuously.");
        return targets;
    }

    /// <summary>
    /// Culture id to its bound militia slots, from BOTH sources the engine reads: the native
    /// <c>taom_spcultures.xml</c> blocks, and the output of running <c>spcultures.xslt</c> over a
    /// stub carrying the six retagged vanilla ids.
    ///
    /// Running the transform rather than grepping its <c>&lt;xsl:attribute&gt;</c> elements is the
    /// house pattern (<c>.claude/rules/tests.md</c>, and <c>CulturePartyTemplateTests</c>): reading
    /// the markup cannot see an exclusion filter and cannot express "this attribute is absent".
    /// </summary>
    private static Dictionary<string, Dictionary<string, string>> LoadMilitiaBindingsByCulture()
    {
        var bindings = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        void Record(XElement culture)
        {
            var id = (string?)culture.Attribute("id");
            if (string.IsNullOrEmpty(id))
                return;
            if (!bindings.TryGetValue(id!, out var slots))
                bindings[id!] = slots = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var attribute in MilitiaAttributes)
            {
                var value = (string?)culture.Attribute(attribute);
                if (!string.IsNullOrEmpty(value))
                    slots[attribute] = value!.Replace("NPCCharacter.", "");
            }
        }

        foreach (var culture in XDocument.Load(Path.Combine(ModuleData(), "taom_spcultures.xml")).Descendants("Culture"))
            Record(culture);

        var xsltPath = Path.Combine(ModuleData(), "spcultures.xslt");
        if (File.Exists(xsltPath))
        {
            var transform = new XslCompiledTransform();
            transform.Load(xsltPath);

            var stubRoot = new XElement("SPCultures");
            foreach (var id in new[] { "empire", "sturgia", "vlandia", "khuzait", "battania", "aserai" })
                stubRoot.Add(new XElement("Culture", new XAttribute("id", id)));

            var output = new XDocument();
            using (var writer = output.CreateWriter())
                transform.Transform(new XDocument(stubRoot).CreateReader(), null, writer);

            foreach (var culture in output.Descendants("Culture"))
                Record(culture);
        }

        Assert.IsTrue(bindings.Count > 20,
            $"Only {bindings.Count} cultures parsed for militia bindings \u2014 the culture data or its layout moved.");
        return bindings;
    }

    /// <summary>
    /// Rebuilds, from XML, the index <c>GarrisonCultureSwapAdapter</c> builds from the live engine:
    /// the upgrade-closure of the culture's recruitment pool, minus militia and non-Soldier troops.
    /// </summary>
    private static CultureTroopIndex BuildIndex(
        string cultureId, Dictionary<string, Troop> troops, HashSet<string> militiaIds,
        VolunteerRecruitmentService recruitment, ICollection<string>? unresolvedIds = null)
    {
        var unresolved = unresolvedIds ?? new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(recruitment.GetCulturePoolTroopIds(cultureId));
        var candidates = new List<CultureTroopCandidate>();

        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (string.IsNullOrEmpty(id) || !visited.Add(id))
                continue;
            if (!troops.TryGetValue(id, out var troop))
            {
                // The seam. This test walks ModuleData XML; the adapter walks the live
                // MBObjectManager graph. An id the walk cannot resolve is the moment the two stop
                // testing the same thing, so it is recorded rather than skipped.
                unresolved.Add(id);
                continue;
            }
            if (troop.Occupation == "Soldier" && !militiaIds.Contains(id))
                candidates.Add(new CultureTroopCandidate(id, troop.Tier, troop.Role));
            foreach (var up in troop.UpgradeTargets)
                pending.Enqueue(up);
        }

        return new CultureTroopIndex(cultureId, candidates);
    }

    // --- The gate that matters ---

    [TestMethod]
    public void EveryConversionTargetCanReplaceEveryTierAndRole()
    {
        var troops = LoadTroops();
        var militiaIds = LoadMilitiaTroopIds();
        var recruitment = Recruitment();
        var mapper = new TroopCultureMapper();

        var failures = new List<string>();
        var unresolved = new List<string>();
        foreach (var culture in ConversionTargets())
        {
            var index = BuildIndex(culture, troops, militiaIds, recruitment, unresolved);
            if (index.IsEmpty)
            {
                failures.Add($"{culture}: recruitable, but its pool reaches no garrison-eligible troop at all");
                continue;
            }

            for (var tier = 0; tier <= MaxTier; tier++)
            {
                foreach (var role in AllRoles)
                {
                    var incoming = new GarrisonTroopInfo(
                        "incoming_troop", "some_other_culture", tier, role, count: 10, woundedCount: 0, isHero: false);

                    var plan = mapper.MapGarrison("town_coverage_probe", culture, new[] { incoming }, index);

                    if (plan.Swaps.Count == 0)
                        failures.Add($"{culture}: no replacement for a tier-{tier} {role} troop");
                }
            }
        }

        Assert.AreEqual(0, failures.Count,
            "A culture that cannot place an incoming troop leaves that stack in its old culture, silently and forever:"
            + Environment.NewLine + string.Join(Environment.NewLine, failures));

        Assert.AreEqual(0, unresolved.Distinct().Count(),
            "This test walks ModuleData XML while the adapter walks the live MBObjectManager graph. A pool "
            + "root or upgrade target the walk cannot resolve is the moment the two stop testing the same "
            + "thing: " + string.Join(", ", unresolved.Distinct()));
    }

    [TestMethod]
    public void NoConversionTargetIndexesAPracticeDummyOrAGuard()
    {
        // The defect this gate was added for. Grouping candidates by CharacterObject.Culture swept in
        // arena dummies, settlement guards and caravan guards, and for Lothlorien and Khand it swept
        // in NOTHING ELSE: every one of their culture-tagged Soldiers is one of these. A conversion
        // re-manned a whole captured city with Practice Dummies. Deriving candidates from the
        // recruitment pool instead excludes them, because none is recruitable.
        var troops = LoadTroops();
        var militiaIds = LoadMilitiaTroopIds();
        var recruitment = Recruitment();

        var offenders = new List<string>();
        foreach (var culture in ConversionTargets())
        {
            var index = BuildIndex(culture, troops, militiaIds, recruitment);
            for (var tier = 0; tier <= MaxTier; tier++)
            {
                foreach (var role in AllRoles)
                {
                    foreach (var id in index.Candidates(role, tier))
                    {
                        if (id.Contains("practice_dummy") || id.Contains("gear_dummy")
                            || id.StartsWith("guard_", StringComparison.Ordinal)
                            || id.Contains("caravan_guard") || id.Contains("weapon_practice"))
                        {
                            offenders.Add($"{culture}: {id}");
                        }
                    }
                }
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "These are not troops a culture garrisons a fief with: "
            + Environment.NewLine + string.Join(Environment.NewLine, offenders.Distinct()));
    }

    [TestMethod]
    public void MinorFactionCulturesAreNotConversionTargets()
    {
        // The counterpart to the gates above, pinning why minor factions need no garrison data.
        // Each authors a single Bandit-occupation troop and owns no settlement, so having no
        // recruitment pool is what keeps them out of scope. If one ever gains a pool, the gates
        // above must start covering it rather than the scope being widened here by hand.
        var minorFactions = new[]
        {
            "dunland_raiders", "erebor_warriors", "gondor_soldiers", "gundabad_raiders",
            "harad_raiders", "mirkwood_stalkers", "rhun_raiders", "umbar_corsairs",
        };

        var targets = new HashSet<string>(ConversionTargets(), StringComparer.Ordinal);
        var unexpected = minorFactions.Where(targets.Contains).ToList();

        Assert.AreEqual(0, unexpected.Count,
            "A minor faction gained a recruitment pool, which makes it a conversion target with no garrison data: "
            + string.Join(", ", unexpected));
    }

    // --- Militia slot integrity, the other half of the swap ---

    [TestMethod]
    public void EveryConversionTargetBindsAllFourMilitiaSlots()
    {
        // Scoped to conversion targets, not to whatever is in taom_spcultures.xml, because six
        // targets (empire, sturgia, vlandia, khuzait, battania, aserai) bind their slots through
        // spcultures.xslt and the previous version of this gate never opened it.
        //
        // It also requires all four rather than "if any, then all". A pooled culture binding ZERO
        // slots is the state CultureMilitiaTroops.IsEmpty exists to catch, and at runtime it is
        // quiet: hasRegulars is true, so the service's warning never fires and every militia stack
        // falls onto the garrison ladder, putting regular line troops in the militia party.
        var bindings = LoadMilitiaBindingsByCulture();

        var incomplete = new List<string>();
        foreach (var culture in ConversionTargets())
        {
            if (!bindings.TryGetValue(culture, out var slots))
            {
                incomplete.Add($"{culture}: binds no militia slots at all");
                continue;
            }
            var missing = MilitiaAttributes.Where(a => !slots.ContainsKey(a)).ToList();
            if (missing.Count > 0)
                incomplete.Add($"{culture}: missing {string.Join(", ", missing)}");
        }

        Assert.AreEqual(0, incomplete.Count,
            "A conversion target with an incomplete militia set drops those stacks onto the garrison ladder "
            + "instead of mapping them slot-for-slot: " + Environment.NewLine + string.Join(Environment.NewLine, incomplete));
    }

    [TestMethod]
    public void EveryBoundMilitiaTroopActuallyExists()
    {
        var known = new HashSet<string>(LoadTroops().Keys, StringComparer.Ordinal);

        var dangling = LoadMilitiaTroopIds()
            .Where(id => !known.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, dangling.Count,
            "A militia slot pointing at a troop that no longer exists breaks both the engine's militia spawn and "
            + "this feature's slot mapping: " + string.Join(", ", dangling));
    }
}
