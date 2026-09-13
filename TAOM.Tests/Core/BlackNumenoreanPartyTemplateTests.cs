using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Pins where the Black Numenorean line (<c>mordor_num_*</c>) may spawn (#584). The line is a
/// standalone elite tree with no volunteer pool, so party templates are its only route into the
/// field, and nothing in the engine complains when a stack is added to the wrong one: the sprinkle
/// this test forbids shipped for three weeks as a token handful in every Mordor lord party.
/// <c>tools/wire_black_numenorean_troops.py</c> adds any stack a template lacks, so a re-run against
/// a widened <c>LORD_TEMPLATES</c> would put them back silently. This is the gate.
/// </summary>
[TestClass]
public class BlackNumenoreanPartyTemplateTests
{
    private const string Prefix = "mordor_num_";

    /// <summary>
    /// The two houses (<c>docs/features/black-numenorean.md</c> "The Two Houses"), Sauron's per-hero
    /// template (#580) and the vassal reward, which is how a Mordor player receives one.
    /// </summary>
    private static readonly HashSet<string> AllowedTemplates = new(StringComparer.Ordinal)
    {
        "kingdom_hero_party_mordor_empire_south_1_template",
        "kingdom_hero_party_mordor_empire_south_9_template",
        "kingdom_hero_party_mordor_sauron_template",
        "vassal_reward_troops_mordor",
    };

    private static readonly string[] HouseTemplates =
    {
        "kingdom_hero_party_mordor_empire_south_1_template",
        "kingdom_hero_party_mordor_empire_south_9_template",
    };

    // Mirrors VolunteerRecruitmentServiceTests.BlackNumenoreanLine; both are checked against
    // troops_mordor.xml so a rename cannot rot either list without a test naming it.
    private static readonly string[] LineIds =
    {
        "mordor_num_initiate",
        "mordor_num_cavalry", "mordor_num_infantry", "mordor_num_archer",
        "mordor_num_vet_cavalry", "mordor_num_vet_infantry", "mordor_num_vet_archer",
        "mordor_num_knight", "mordor_num_warden", "mordor_num_marksman",
        "mordor_num_temple_knight", "mordor_num_temple_guard", "mordor_num_shadowbow",
    };

    private static Dictionary<string, List<string>> _stacksByTemplate = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_partyTemplates.xml");
        _stacksByTemplate = XDocument.Load(path)
            .Descendants("MBPartyTemplate")
            .ToDictionary(
                t => (string)t.Attribute("id")!,
                t => t.Descendants("PartyTemplateStack")
                    .Select(s => CultureDataFixture.StripPrefix((string)s.Attribute("troop")!))
                    .ToList(),
                StringComparer.Ordinal);
    }

    private static bool IsLine(string troop) => troop.StartsWith(Prefix, StringComparison.Ordinal);

    [TestMethod]
    public void BlackNumenoreanStacks_AppearOnlyInTheHousesSauronAndTheVassalReward()
    {
        var offenders = _stacksByTemplate
            .Where(kv => !AllowedTemplates.Contains(kv.Key))
            .Where(kv => kv.Value.Any(IsLine))
            .Select(kv => $"{kv.Key} ({kv.Value.Count(IsLine)} stacks)")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "Black Numenorean stacks outside the four allowed templates:\n  " + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void EveryAllowedTemplate_Exists()
    {
        foreach (var id in AllowedTemplates)
            Assert.IsTrue(_stacksByTemplate.ContainsKey(id), $"{id} is not defined in taom_partyTemplates.xml");
    }

    [TestMethod]
    public void TheTwoHouses_StillFieldTheWholeLine()
    {
        foreach (var house in HouseTemplates)
        {
            var missing = LineIds.Where(id => !_stacksByTemplate[house].Contains(id)).ToList();
            Assert.AreEqual(0, missing.Count, $"{house} lacks: {string.Join(", ", missing)}");
        }
    }

    [TestMethod]
    public void TheThirteenIds_MatchTroopsMordor()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "troops", "troops_mordor.xml");
        var defined = XDocument.Load(path)
            .Descendants("NPCCharacter")
            .Select(n => (string?)n.Attribute("id"))
            .Where(id => id != null && IsLine(id))
            .ToList();

        CollectionAssert.AreEquivalent(LineIds, defined,
            "the pinned Black Numenorean id list has drifted from troops_mordor.xml");
    }
}
