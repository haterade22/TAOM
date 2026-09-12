using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Shipped-data gate for the eight hideout boss party templates (#564).
///
/// A boss party template sizes the boss PARTY that sits in a hideout, not the boss fight: the
/// engine sizes the fight from the whole hideout, and Patch86 pins it at boss + N. The template
/// still matters twice over. <c>HideoutCampaignBehavior.ArrangeHideoutTroopCountsForMission</c>
/// never trims a boss party (v1.4.8 :615), so a fat one overflows every cap; and the pinned
/// <c>1/1</c> boss stack is what <c>SelectBossAgent</c> and <c>MapEventHelper</c> look for through
/// <c>Culture.BanditBoss</c>. So: exactly one <c>1/1</c> stack, it is the culture's
/// <c>bandit_boss</c>, and the soldier stacks sum to min 3 / max 4 (vanilla's own shape is boss +
/// chief + 2-4 raiders). The numbers used to be solved by <c>tools/rebalance_template_power.py</c>
/// at 67-97 bodies; that tool no longer touches boss templates, and this test is what keeps the
/// hand-authored shape from drifting.
/// </summary>
[TestClass]
public class HideoutBossPartyTemplateTests
{
    private const int SoldierMinSum = 3;
    private const int SoldierMaxSum = 4;

    private sealed class Stack
    {
        public int Min;
        public int Max;
        public string Troop = "";
    }

    private sealed class BanditCulture
    {
        public string Id = "";
        public string Boss = "";
        public string BossTemplate = "";
    }

    private static Dictionary<string, List<Stack>>? _templates;
    private static List<BanditCulture>? _cultures;

    private static Dictionary<string, List<Stack>> Templates => _templates ??= LoadTemplates();
    private static List<BanditCulture> Cultures => _cultures ??= LoadBanditCultures();

    private static Dictionary<string, List<Stack>> LoadTemplates()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_partyTemplates.xml");
        Assert.IsTrue(File.Exists(path), $"taom_partyTemplates.xml not found at {path}");
        return XDocument.Load(path).Descendants("MBPartyTemplate").ToDictionary(
            t => (string)t.Attribute("id"),
            t => t.Descendants("PartyTemplateStack").Select(s => new Stack
            {
                Min = (int)s.Attribute("min_value"),
                Max = (int)s.Attribute("max_value"),
                Troop = ((string)s.Attribute("troop")).Replace("NPCCharacter.", ""),
            }).ToList(),
            StringComparer.Ordinal);
    }

    private static List<BanditCulture> LoadBanditCultures()
    {
        var path = Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_spcultures.xml");
        Assert.IsTrue(File.Exists(path), $"taom_spcultures.xml not found at {path}");
        return XDocument.Load(path).Descendants("Culture")
            .Where(c => (string)c.Attribute("is_bandit") == "true")
            .Select(c => new BanditCulture
            {
                Id = (string)c.Attribute("id"),
                Boss = ((string)c.Attribute("bandit_boss") ?? "").Replace("NPCCharacter.", ""),
                BossTemplate = ((string)c.Attribute("bandit_boss_party_template") ?? "").Replace("PartyTemplate.", ""),
            })
            .ToList();
    }

    private static IEnumerable<KeyValuePair<string, List<Stack>>> BossTemplates()
        => Templates.Where(kv => kv.Key.EndsWith("_boss_party_template", StringComparison.Ordinal));

    [TestMethod]
    public void BossTemplateSweep_IsNotVacuous()
    {
        // A renamed file or folder would empty both indexes and every assertion below would pass
        // against nothing. TAOM ships eight bandit cultures, each with its own boss template.
        Assert.AreEqual(8, Cultures.Count, "bandit culture count changed: " + string.Join(", ", Cultures.Select(c => c.Id)));
        Assert.AreEqual(8, BossTemplates().Count(), "boss template count changed: " + string.Join(", ", BossTemplates().Select(kv => kv.Key)));
    }

    [TestMethod]
    public void EveryBanditCulture_BindsABossTemplateThatExists()
    {
        foreach (var culture in Cultures)
        {
            Assert.IsFalse(string.IsNullOrEmpty(culture.Boss), $"{culture.Id} declares no bandit_boss");
            Assert.IsFalse(string.IsNullOrEmpty(culture.BossTemplate), $"{culture.Id} declares no bandit_boss_party_template");
            Assert.IsTrue(Templates.ContainsKey(culture.BossTemplate),
                $"{culture.Id} binds {culture.BossTemplate}, which taom_partyTemplates.xml does not define");
        }
    }

    [TestMethod]
    public void EveryBossTemplate_HasExactlyOnePinnedStack_MatchingTheCultureBanditBoss()
    {
        foreach (var culture in Cultures)
        {
            var stacks = Templates[culture.BossTemplate];
            var pinned = stacks.Where(s => s.Min == 1 && s.Max == 1 && s.Troop == culture.Boss).ToList();
            Assert.AreEqual(1, pinned.Count,
                $"{culture.BossTemplate} must carry exactly one 1/1 stack of {culture.Boss} (the troop SelectBossAgent looks for); found {pinned.Count}");
            Assert.IsFalse(stacks.Any(s => s.Troop == culture.Boss && (s.Min != 1 || s.Max != 1)),
                $"{culture.BossTemplate} has a non-pinned {culture.Boss} stack");
        }
    }

    [TestMethod]
    public void EveryBossTemplate_SoldierStacks_SumToMaxFourMinThree()
    {
        foreach (var culture in Cultures)
        {
            var soldiers = Templates[culture.BossTemplate].Where(s => s.Troop != culture.Boss).ToList();
            var minSum = soldiers.Sum(s => s.Min);
            var maxSum = soldiers.Sum(s => s.Max);
            Assert.AreEqual(SoldierMaxSum, maxSum,
                $"{culture.BossTemplate} soldier stacks sum to max {maxSum}; the boss party is 1 boss + {SoldierMinSum}-{SoldierMaxSum} by decision (#564), and ArrangeHideoutTroopCountsForMission never trims a boss party");
            Assert.AreEqual(SoldierMinSum, minSum,
                $"{culture.BossTemplate} soldier stacks sum to min {minSum}; expected {SoldierMinSum}");
            Assert.IsTrue(soldiers.Count >= 1, $"{culture.BossTemplate} has no soldier stack at all");
        }
    }

    [TestMethod]
    public void EveryBossTemplate_NoStackHasMinAboveMaxOrZeroMax()
    {
        foreach (var kv in BossTemplates())
        {
            foreach (var stack in kv.Value)
            {
                Assert.IsTrue(stack.Max >= 1, $"{kv.Key}: {stack.Troop} has max {stack.Max}");
                Assert.IsTrue(stack.Min >= 1 && stack.Min <= stack.Max,
                    $"{kv.Key}: {stack.Troop} has min {stack.Min} / max {stack.Max}");
            }
        }
    }

    [TestMethod]
    public void EveryBossTemplate_IsBoundByExactlyOneBanditCulture()
    {
        // A template nothing binds is dead data (troops.md); one bound twice would share a boss.
        foreach (var kv in BossTemplates())
        {
            var binders = Cultures.Where(c => c.BossTemplate == kv.Key).Select(c => c.Id).ToList();
            Assert.AreEqual(1, binders.Count, $"{kv.Key} is bound by [{string.Join(", ", binders)}]");
        }
    }
}
