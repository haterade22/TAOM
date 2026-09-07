using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.TroopProgression;

/// <summary>
/// Pins the one rule a player can read straight off the troop tree: upgrading a troop never makes
/// it worse.
///
/// <para>
/// A troop's 8 combat skills come only from its <c>&lt;skills&gt;</c> block. Nothing in TAOM writes
/// them at runtime, and <c>CharacterObject.GetSkillValue</c> returns 0 for a skill the block never
/// declares, so an omitted <c>&lt;skill&gt;</c> element is a silent drop to zero rather than
/// "unchanged".
/// </para>
///
/// <para>
/// Three things shipped at once and none of the existing gates saw any of them:
/// <c>validate_moduledata.py</c> resolved <c>upgrade_target</c> as a reference and never read a
/// level or a skill value; <c>analyze_troop_balance.py</c> excluded every name-matched militia,
/// which is exactly where the worst edge in the game was hiding
/// (<c>gondor_ano_archer_militia</c>, level 11 wearing level-21 stats, out-statting its own upgrade
/// target by 145 points across seven of its eight skills); and the whole graph was read from
/// <c>troops/</c> alone, so the 16 villager sources in <c>characters/npcs_*.xml</c> were invisible.
/// </para>
///
/// <para>
/// Militia are the one exemption. They take the level-21 baseline regardless of their real level so
/// village defence stays costly, which makes a militia promoting into another militia flat by
/// design. That exemption reads the culture bindings in <c>taom_spcultures.xml</c> and
/// <c>spcultures.xslt</c>, never the word "militia" in a name. Name matching is what produced the
/// bug.
/// </para>
/// </summary>
[TestClass]
public class TroopUpgradeSkillMonotonicityTests
{
    private static readonly string[] CombatSkills =
    {
        "Athletics", "Riding", "OneHanded", "TwoHanded", "Polearm", "Bow", "Crossbow", "Throwing"
    };

    /// <summary>
    /// Upgrade edges where a child deliberately re-specialises OFF a skill its parent carried
    /// for REAL, per skill. Not the ordinary inert baseline noise this gate protects: the parent
    /// actually carries the weapon, so raising the child back to it would undo the
    /// specialisation. MIRRORED in <c>rebalance_troops.py</c>'s
    /// <c>RESPECIALIZATION_EXEMPT_EDGES</c> and <c>taom_schema.py</c>'s
    /// <c>_RESPECIALIZATION_EXEMPT_EDGES</c>. All three must agree, exactly as the militia
    /// binding regex below is kept in lockstep: otherwise the writer floors a value, this gate
    /// calls it a regression, and the clamp puts it straight back. Adding an entry is a
    /// deliberate act, so state why.
    /// </summary>
    private static readonly Dictionary<(string Source, string Target), HashSet<string>>
        RespecializationExemptEdges = new Dictionary<(string, string), HashSet<string>>
    {
        // sagarun_crossbowman carries a real crossbow at 160. Its naffatun child throws
        // javelins and carries neither bow nor crossbow, so both are floored rather than
        // inherited (#554); Throwing takes the ranged curve in their place.
        [("sagarun_crossbowman", "sagarun_naffatun")] =
            new HashSet<string>(StringComparer.Ordinal) { "Bow", "Crossbow" },
    };

    /// <summary>
    /// Troops bound to a culture militia slot. Pinned by id, not just by count: asserting only the
    /// count let the exemption be swapped back to the name-substring rule that caused the bug
    /// without the suite noticing, because both sets happen to be the same size minus one.
    /// </summary>
    private static readonly string[] ExpectedMilitiaCultures =
    {
        "dale", "dolguldur", "dunland", "erebor", "goblin", "gondor", "gundabad", "harad",
        "isengard", "lindon", "mirkwood", "mordor", "rhun", "rivendell", "rohan"
    };

    private sealed class Troop
    {
        public string Id;
        public string File;
        public int Level;
        public string Group;
        public bool Templated;
        public Dictionary<string, int> Skills = new Dictionary<string, int>();
        public List<string> Upgrades = new List<string>();
    }

    private static string ResolveModuleData()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Main", "_Module", "ModuleData");
            if (Directory.Exists(Path.Combine(candidate, "troops")))
                return candidate;
        }

        // A data gate that cannot find its data has checked nothing. Assert.Inconclusive would
        // leave `dotnet test` green while all three invariants silently went unenforced.
        Assert.Fail("ModuleData/troops was not found within 12 parent directories of " +
                    AppDomain.CurrentDomain.BaseDirectory +
                    ". This suite gates shipped troop data, so it fails rather than skipping.");
        return null;
    }

    /// <summary>
    /// Every upgrade source in the game, from both files that hold one. The villager entries in
    /// characters/npcs_*.xml upgrade into their culture's tier-1 troop, and six of those edges
    /// regressed while this graph was built from troops/ alone.
    /// </summary>
    private static Dictionary<string, Troop> ParseTroops(string moduleData)
    {
        var troops = new Dictionary<string, Troop>();
        var files = Directory.GetFiles(Path.Combine(moduleData, "troops"), "troops_*.xml")
            .Concat(Directory.GetFiles(Path.Combine(moduleData, "characters"), "npcs_*.xml"));

        foreach (var file in files)
        {
            foreach (var npc in XDocument.Load(file).Descendants("NPCCharacter"))
            {
                var id = (string)npc.Attribute("id");
                if (string.IsNullOrEmpty(id)) continue;

                var troop = new Troop
                {
                    Id = id,
                    File = Path.GetFileName(file),
                    Group = (string)npc.Attribute("default_group") ?? "Infantry",
                    Templated = !string.IsNullOrEmpty((string)npc.Attribute("skill_template")),
                };
                int level;
                int.TryParse((string)npc.Attribute("level") ?? "0", out level);
                troop.Level = level;

                var skills = npc.Element("skills");
                if (skills != null)
                {
                    // Element("skills").Elements("skill"), not Descendants: a Descendants sweep
                    // would also pick up any <skill> living outside the troop's own block.
                    foreach (var skill in skills.Elements("skill"))
                    {
                        var sid = (string)skill.Attribute("id");
                        if (string.IsNullOrEmpty(sid)) continue;
                        int value;
                        int.TryParse((string)skill.Attribute("value") ?? "0", out value);
                        troop.Skills[sid] = value;
                    }
                }

                var targets = npc.Element("upgrade_targets");
                if (targets != null)
                {
                    foreach (var target in targets.Elements("upgrade_target"))
                    {
                        var tid = (string)target.Attribute("id");
                        if (string.IsNullOrEmpty(tid)) continue;
                        const string prefix = "NPCCharacter.";
                        if (tid.StartsWith(prefix, StringComparison.Ordinal)) tid = tid.Substring(prefix.Length);
                        troop.Upgrades.Add(tid);
                    }
                }

                troops[id] = troop;
            }
        }
        return troops;
    }

    /// <summary>
    /// The authoritative militia set: troop ids bound to a culture militia slot. Two encodings are
    /// in use, an attribute in taom_spcultures.xml and an xsl:attribute element in spcultures.xslt
    /// (Dale, Dunland, Rhun and Rohan use the latter), so both are matched. The pattern is kept
    /// character-for-character in step with MILITIA_BINDING_RE in tools/rebalance_troops.py and
    /// _MILITIA_BINDING_RE in tools/taom_schema.py: if the three ever disagree, the writer and the
    /// gate classify the same troop differently.
    /// </summary>
    private static HashSet<string> LoadMilitiaBoundIds(string moduleData)
    {
        var comments = new Regex("<!--.*?-->", RegexOptions.Singleline);
        var pattern = new Regex(
            "(?<![A-Za-z0-9_])(?:melee_|ranged_)?(?:elite_)?militia_troop[\"']?\\s*(?:=\\s*[\"']|>)\\s*"
            + "NPCCharacter[.]([A-Za-z0-9_]+)");
        var bound = new HashSet<string>();
        foreach (var name in new[] { "taom_spcultures.xml", "spcultures.xslt" })
        {
            var path = Path.Combine(moduleData, name);
            if (!File.Exists(path)) continue;
            // A commented-out <Culture> block is not a live binding, and counting one would
            // silently widen the exemption.
            var text = comments.Replace(File.ReadAllText(path), "");
            foreach (Match m in pattern.Matches(text))
                bound.Add(m.Groups[1].Value);
        }
        return bound;
    }

    private static int SkillOf(Troop troop, string skill)
    {
        int value;
        return troop.Skills.TryGetValue(skill, out value) ? value : 0;
    }

    [TestMethod]
    public void EveryUpgradeTarget_HasNoSkillLowerThanTheTroopItUpgradesFrom()
    {
        var moduleData = ResolveModuleData();
        var troops = ParseTroops(moduleData);
        var militia = LoadMilitiaBoundIds(moduleData);
        var failures = new List<string>();

        foreach (var source in troops.Values.OrderBy(t => t.Id, StringComparer.Ordinal))
        {
            foreach (var targetId in source.Upgrades)
            {
                Troop target;
                if (!troops.TryGetValue(targetId, out target)) continue;

                // Militia are pinned to the level-21 baseline by design, so a militia promoting
                // into another militia is flat rather than an increase. Only militia-to-militia is
                // exempt; a militia that feeds a real line is checked like anything else.
                if (militia.Contains(source.Id) && militia.Contains(target.Id)) continue;

                // A templated character's real skills live in a SkillSet outside these files, so
                // its inline block is not what the engine reads. SkillTemplate_NeverShadows...
                // owns that case; judging the edge here would compare the wrong numbers.
                if (source.Templated || target.Templated) continue;

                HashSet<string> exempt;
                if (!RespecializationExemptEdges.TryGetValue((source.Id, target.Id), out exempt))
                    exempt = null;

                var drops = CombatSkills
                    .Where(s => (exempt == null || !exempt.Contains(s))
                                && SkillOf(target, s) < SkillOf(source, s))
                    .Select(s => s + " " + SkillOf(source, s) + " to " + SkillOf(target, s))
                    .ToList();

                if (drops.Count > 0)
                {
                    failures.Add(source.File + ": " + source.Id + " (L" + source.Level + " " + source.Group +
                                 ") -> " + target.Id + " (L" + target.Level + " " + target.Group + "): " +
                                 string.Join(", ", drops));
                }
            }
        }

        Assert.AreEqual(0, failures.Count,
            "Upgrading these troops lowers a skill. A skill absent from the target skills block " +
            "counts as 0, which is what the engine reads." + Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [TestMethod]
    public void EveryTroop_DeclaresAllEightCombatSkills()
    {
        var moduleData = ResolveModuleData();

        var missing = ParseTroops(moduleData).Values
            .Where(t => !t.Templated && t.File.StartsWith("troops_", StringComparison.Ordinal))
            .Select(t => new { Troop = t, Gaps = CombatSkills.Where(s => !t.Skills.ContainsKey(s)).ToList() })
            .Where(x => x.Gaps.Count > 0)
            .OrderBy(x => x.Troop.Id, StringComparer.Ordinal)
            .Select(x => x.Troop.File + ": " + x.Troop.Id + " missing " + string.Join(", ", x.Gaps))
            .ToList();

        Assert.AreEqual(0, missing.Count,
            "A partial skills block reads as 0 for every skill it omits, which turns an upgrade " +
            "into a silent stat wipe." + Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    /// <summary>
    /// A resolvable <c>skill_template</c> makes the inline <c>&lt;skills&gt;</c> block unreachable:
    /// <c>BasicCharacterObject.Deserialize</c> only calls <c>DefaultCharacterSkills.Init</c> when
    /// the template reference came back null (v1.4.8, BasicCharacterObject.cs:337-358). A character
    /// carrying both declares two different skill sets and the engine silently takes the template.
    /// 44 militia shipped that way, wearing vanilla Calradian values while every TAOM tool reported
    /// the authored ones (#523).
    /// </summary>
    [TestMethod]
    public void SkillTemplate_NeverShadowsAnInlineSkillsBlock()
    {
        var moduleData = ResolveModuleData();

        var conflicted = ParseTroops(moduleData).Values
            .Where(t => t.Templated && t.Skills.Count > 0)
            .OrderBy(t => t.Id, StringComparer.Ordinal)
            .Select(t => t.File + ": " + t.Id + " declares " + t.Skills.Count +
                         " inline skills that the engine discards in favour of its skill_template")
            .ToList();

        Assert.AreEqual(0, conflicted.Count,
            "These characters declare inline skills AND a skill_template. The engine reads the " +
            "template and throws the inline block away, so the authored values never reach the " +
            "game. Drop one of the two." + Environment.NewLine +
            string.Join(Environment.NewLine, conflicted));
    }

    [TestMethod]
    public void MilitiaExemption_IsBoundByCultureNotByName()
    {
        var moduleData = ResolveModuleData();
        var troops = ParseTroops(moduleData);
        var militia = LoadMilitiaBoundIds(moduleData);

        // Pinned by identity, not just by size. The name-substring rule that caused the bug
        // produces a set of 61 that contains all of these plus gondor_ano_archer_militia, so a
        // count-only assertion of 60 would not have caught a revert to it.
        var expected = new HashSet<string>(
            ExpectedMilitiaCultures.SelectMany(c => new[]
            {
                c + "_militia_spearman", c + "_militia_archer",
                c + "_militia_veteran_spearman", c + "_militia_veteran_archer",
            }));

        CollectionAssert.AreEquivalent(
            expected.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            militia.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            "The set of troops a culture binds to a militia slot changed. Militia take the " +
            "level-21 baseline regardless of level, so anything that enters this set gets a large " +
            "silent stat change. Update ExpectedMilitiaCultures deliberately.");

        var undefined = militia.Where(id => !troops.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.AreEqual(0, undefined.Count,
            "A culture binds a militia troop that no troop file defines: " + string.Join(", ", undefined));

        // gondor_ano_archer_militia is the trap this whole file exists for: its NAME says militia,
        // its bindings say it is an ordinary Anorien line troop.
        Assert.IsFalse(militia.Contains("gondor_ano_archer_militia"),
            "gondor_ano_archer_militia is a line troop, not a culture militia troop. If it is ever " +
            "bound as militia, the level-21 baseline returns and so does the 145-point drop into " +
            "gondor_ano_skirmisher.");
    }
}
