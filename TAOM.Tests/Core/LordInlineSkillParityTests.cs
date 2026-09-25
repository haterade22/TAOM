using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Core;

/// <summary>
/// An inline <c>&lt;skills&gt;</c> block that sits beside a <c>skill_template</c> must equal the SkillSet
/// the template names, skill for skill.
///
/// <para>
/// Through v1.4.8 <c>BasicCharacterObject.Deserialize</c> read the inline block only when the template
/// did not resolve, so TAOM's lord generators kept the block as a documentation mirror of the SkillSet
/// and the SkillSet was the only source the engine saw. Since v1.5.2 the loader copies the template
/// into a fresh <c>MBCharacterSkills</c> under the character's own id and then applies the inline
/// block on top, so a mirror that drifted from its SkillSet silently changes the lord's stats (64 lords
/// in <c>characters/lords.xml</c> had drifted, both directions, when the bump landed). Parity keeps
/// v1.5.2 behaving as v1.4.8 did: the block overwrites the template with the template's own numbers.
/// </para>
///
/// <para>
/// Only skills the block lists are compared: a skill the block omits keeps the copied template value
/// on v1.5.2, which is what v1.4.8 gave. Vanilla templates (<c>spc_*</c>) live in the installed
/// <c>SandBoxCore</c>, so this reads the install and lives in <c>BindingVerification</c>.
/// <c>python tools/sync_lord_inline_skills.py --apply</c> rewrites every drifted block.
/// </para>
/// </summary>
[TestClass]
public class LordInlineSkillParityTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static readonly string[] VanillaModules = { "Native", "SandBoxCore", "SandBox", "StoryMode" };

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void EveryInlineSkillBlockBesideATemplate_MatchesThatSkillSet()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var sets = LoadSkillSets();
        Assert.IsTrue(sets.Count > 0, "No SkillSet definitions were found in the repo or the install.");

        var mismatches = new List<string>();
        int checkedBlocks = 0;
        foreach (var file in Directory.GetFiles(CultureDataFixture.ModuleDataPath(), "*.xml", SearchOption.AllDirectories))
        {
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (Exception) { continue; }   // well-formedness has its own gate

            foreach (var character in doc.Descendants("NPCCharacter"))
            {
                var template = (string)character.Attribute("skill_template");
                if (string.IsNullOrEmpty(template)) continue;
                var block = character.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("skills", StringComparison.OrdinalIgnoreCase));
                if (block == null || !block.HasElements) continue;

                checkedBlocks++;
                var id = (string)character.Attribute("id") ?? "(no id)";
                var setId = CultureDataFixture.StripPrefix(template);
                if (!sets.TryGetValue(setId, out var set))
                {
                    mismatches.Add($"{id}: skill_template \"{template}\" resolves to no SkillSet");
                    continue;
                }
                foreach (var skill in block.Elements())
                {
                    var skillId = (string)skill.Attribute("id");
                    if (skillId == null) continue;
                    if (!int.TryParse((string)skill.Attribute("value"), out var inline))
                    {
                        mismatches.Add($"{id}: {skillId} has a non-integer inline value \"{(string)skill.Attribute("value")}\"");
                        continue;
                    }
                    set.TryGetValue(skillId, out var expected);
                    if (inline != expected)
                        mismatches.Add($"{id}: {skillId} inline {inline}, SkillSet {setId} {expected}");
                }
            }
        }

        // lords.xslt writes vanilla-id lords through <xsl:template match="NPCCharacter[@id='...']"> with an
        // <xsl:attribute name="skill_template"> and a literal <skills> block; the transform output is what
        // the engine loads, so the same rule applies to the stylesheet's own literal blocks.
        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";
        foreach (var file in Directory.GetFiles(CultureDataFixture.ModuleDataPath(), "*.xslt", SearchOption.AllDirectories))
        {
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (Exception) { continue; }

            foreach (var template in doc.Descendants(xsl + "template"))
            {
                var match = (string)template.Attribute("match") ?? "";
                if (!match.StartsWith("NPCCharacter[@id='", StringComparison.Ordinal)) continue;
                var skillTemplate = template.Descendants(xsl + "attribute")
                    .FirstOrDefault(a => (string)a.Attribute("name") == "skill_template")?.Value.Trim();
                if (string.IsNullOrEmpty(skillTemplate)) continue;
                var block = template.Descendants().FirstOrDefault(e => e.Name.Namespace == XNamespace.None && e.Name.LocalName.Equals("skills", StringComparison.OrdinalIgnoreCase));
                if (block == null || !block.HasElements) continue;

                checkedBlocks++;
                var id = match.Substring("NPCCharacter[@id='".Length).TrimEnd('\'', ']', ' ');
                var setId = CultureDataFixture.StripPrefix(skillTemplate);
                if (!sets.TryGetValue(setId, out var set))
                {
                    mismatches.Add($"{id} ({Path.GetFileName(file)}): skill_template \"{skillTemplate}\" resolves to no SkillSet");
                    continue;
                }
                foreach (var skill in block.Elements())
                {
                    var skillId = (string)skill.Attribute("id");
                    if (skillId == null) continue;
                    if (!int.TryParse((string)skill.Attribute("value"), out var inline))
                    {
                        mismatches.Add($"{id} ({Path.GetFileName(file)}): {skillId} has a non-integer inline value \"{(string)skill.Attribute("value")}\"");
                        continue;
                    }
                    set.TryGetValue(skillId, out var expected);
                    if (inline != expected)
                        mismatches.Add($"{id} ({Path.GetFileName(file)}): {skillId} inline {inline}, SkillSet {setId} {expected}");
                }
            }
        }

        Assert.IsTrue(checkedBlocks > 0, "No NPCCharacter carries both a skill_template and a populated <skills> block; the data shape changed.");
        Assert.AreEqual(0, mismatches.Count,
            $"{mismatches.Count} inline skill value(s) differ from the SkillSet beside them. Since v1.5.2 the inline value wins, "
            + "so these lords no longer have the stats their template gives. Run `python tools/sync_lord_inline_skills.py --apply`."
            + Environment.NewLine + string.Join(Environment.NewLine, mismatches.Take(60)));
    }

    /// <summary>
    /// Files in engine load order (the vanilla modules, then TAOM), merged the way
    /// <c>MBObjectManager.MergeElements</c> merges before any object exists: a later file's SkillSet with
    /// an id already seen overrides the skills it lists and keeps the ones it omits, unless it carries
    /// <c>_replaceWhileMerging="true"</c>, which drops the earlier children first. Vanilla itself declares
    /// <c>infantry_heavyinfantry_level1_template_skills</c> in both SandBoxCore and SandBox with different
    /// values, so the merge path is live even before any TAOM id collides.
    /// </summary>
    private static Dictionary<string, Dictionary<string, int>> LoadSkillSets()
    {
        var files = new List<string>();
        foreach (var module in VanillaModules)
        {
            var data = Path.Combine(GameAssemblies.GameDir, "Modules", module, "ModuleData");
            if (Directory.Exists(data)) files.AddRange(Directory.GetFiles(data, "*skill_sets*.xml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal));
        }
        files.AddRange(Directory.GetFiles(CultureDataFixture.ModuleDataPath(), "*skill_sets*.xml", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal));

        var sets = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            foreach (var set in XDocument.Load(file).Descendants("SkillSet"))
            {
                var id = (string)set.Attribute("id");
                if (id == null) continue;
                var values = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var skill in set.Elements())
                {
                    var skillId = (string)skill.Attribute("id");
                    if (skillId != null && int.TryParse((string)skill.Attribute("value"), out var v)) values[skillId] = v;
                }
                var replace = string.Equals((string)set.Attribute("_replaceWhileMerging"), "true", StringComparison.OrdinalIgnoreCase);
                if (sets.TryGetValue(id, out var existing) && !replace)
                    foreach (var kv in values) existing[kv.Key] = kv.Value;
                else
                    sets[id] = values;
            }
        }
        return sets;
    }
}
