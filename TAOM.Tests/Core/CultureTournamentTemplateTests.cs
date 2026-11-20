using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Config validation for the per-culture tournament team templates (#422, part 1).
///
/// Vanilla's TournamentFightMissionController.GetTeamWeaponEquipmentList reads
/// <c>PlayerEncounter.EncounterSettlement.Culture.TournamentTeamTemplatesFor{One,Two,Four}Participant</c>,
/// picks one NPCCharacter and gives roster i of it to team member i. Every TAOM culture used to list
/// vanilla's tournament_template_empire_* (or its aserai, sturgia, vlandia twin) and the six cultures
/// spcultures.xslt renames still listed vanilla's own Calradian ones. They now list the
/// taom_tournament_* templates that tools/generate_tournament_templates.py derives from each culture's
/// troops into characters/taom_tournament_templates.xml.
///
/// What goes wrong if the data drifts: a list naming an id no file defines leaves the culture with an
/// empty template list (the controller then falls back to the Calradian empire set), and a template with
/// fewer rosters than its team size makes <c>teamWeaponEquipmentList[num]</c> throw when the match starts.
/// </summary>
[TestClass]
public class CultureTournamentTemplateTests
{
    private static readonly string[] RenamedCultures = { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" };

    private static readonly (string Tag, string Size, int TeamSize)[] Lists =
    {
        ("tournament_team_templates_one_participant", "one", 1),
        ("tournament_team_templates_two_participant", "two", 2),
        ("tournament_team_templates_four_participant", "four", 4),
    };

    [TestMethod]
    public void EveryTaomTownCulture_ListsThreeOwnTemplatesPerTeamSize()
    {
        var defined = DefinedTemplates();
        var checkedCultures = 0;
        var offenders = new List<string>();

        foreach (var culture in XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_spcultures.xml")).Descendants("Culture"))
        {
            var id = (string)culture.Attribute("id");
            if (string.IsNullOrEmpty(id) || CultureDataFixture.HideoutOnlyCultures.Contains(id)
                || CultureDataFixture.SettlementlessBanditCultures.Contains(id))
                continue;

            checkedCultures++;
            offenders.AddRange(ListOffenders(id, culture, defined));
        }

        Assert.IsTrue(checkedCultures >= 24, "fewer cultures than the 24 TAOM town cultures were checked: the check tested too little");
        Assert.AreEqual(0, offenders.Count, string.Join("\n", offenders));
    }

    [TestMethod]
    public void SpculturesXslt_RenamedCultures_ListTheirOwnTemplatesNotVanillas()
    {
        var moduleData = CultureDataFixture.ModuleDataPath();
        var transform = CompiledXslt.Get(Path.Combine(moduleData, "spcultures.xslt"), XsltLoadStyle.FrameworkDefaults);

        // The stub carries all three vanilla lists on purpose: the stylesheet must replace each, not append to it.
        var stub = new XDocument(new XElement("SPCultures",
            RenamedCultures.Select(id => new XElement("Culture", new XAttribute("id", id),
                Lists.Select(list => new XElement(list.Tag,
                    new XElement("template", new XAttribute("name", $"NPCCharacter.tournament_template_empire_{list.Size}_participant_set_v1"))))))));
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(stub.CreateReader(), null, writer);

        var defined = DefinedTemplates();
        var cultures = output.Descendants("Culture").ToList();
        Assert.AreEqual(RenamedCultures.Length, cultures.Count);

        var offenders = new List<string>();
        foreach (var culture in cultures)
            offenders.AddRange(ListOffenders((string)culture.Attribute("id"), culture, defined));

        Assert.AreEqual(0, offenders.Count, string.Join("\n", offenders));
    }

    private static IEnumerable<string> ListOffenders(string cultureId, XElement culture, Dictionary<string, XElement> defined)
    {
        foreach (var (tag, size, teamSize) in Lists)
        {
            var listed = culture.Elements(tag).SelectMany(l => l.Elements())
                .Select(e => CultureDataFixture.StripPrefix((string)e.Attribute("name") ?? (string)e.Attribute("id") ?? ""))
                .ToList();

            var expected = Enumerable.Range(1, 3).Select(n => $"taom_tournament_{cultureId}_{size}_participant_set_v{n}").ToList();
            if (!listed.SequenceEqual(expected))
            {
                yield return $"{cultureId} <{tag}> lists [{string.Join(", ", listed)}], expected [{string.Join(", ", expected)}]";
                continue;
            }

            foreach (var id in listed)
            {
                if (!defined.TryGetValue(id, out var template))
                {
                    yield return $"{cultureId} -> {id} is not defined in taom_tournament_templates.xml";
                    continue;
                }

                var owner = CultureDataFixture.StripPrefix((string)template.Attribute("culture") ?? "");
                if (!string.Equals(owner, cultureId, StringComparison.Ordinal))
                    yield return $"{id} has culture {owner}, listed by {cultureId}";

                var rosters = template.Element("Equipments")?.Elements("EquipmentRoster").Where(r => (string?)r.Attribute("civilian") != "true").ToList() ?? new List<XElement>();
                if (rosters.Count != teamSize)
                    yield return $"{id} has {rosters.Count} rosters for a team of {teamSize}";

                foreach (var slot in rosters.SelectMany(r => r.Elements("equipment")).Select(e => (string)e.Attribute("slot")).Distinct())
                {
                    if (!new[] { "Item0", "Item1", "Item2", "Item3", "Horse", "HorseHarness" }.Contains(slot))
                        yield return $"{id} carries slot {slot}: the fighter's own armour fills the armour slots";
                }
            }
        }
    }

    private static Dictionary<string, XElement> DefinedTemplates()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "characters", "taom_tournament_templates.xml");
        Assert.IsTrue(File.Exists(path), $"taom_tournament_templates.xml not found at {path}");
        return XDocument.Load(path).Descendants("NPCCharacter").ToDictionary(e => (string)e.Attribute("id"), e => e, StringComparer.Ordinal);
    }
}
