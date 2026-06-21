using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LotrIssues;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Tests.Features.LotrIssues;

[TestClass]
public class LotrIssueConfigProviderTests
{
    private LotrIssueConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new LotrIssueConfigProvider(Substitute.For<IPathService>(), Substitute.For<IModLogger>());
    }

    private List<LotrIssueDefinition> Parse(string xml) => _sut.ParseIssues(XDocument.Parse(xml));

    // One valid issue with every field populated; per-rule tests override one attribute.
    private static string Doc(string attrs) => $@"
<LotrIssues>
  <LotrIssue id='lotr_grain' template='DeliverGoods' giver_occupation='Headman' frequency='Common'
             cultures='gondor,vlandia' count='12' count_per_difficulty='180' item_source='item:grain'
             troop_source='' reward_gold_base='0' reward_gold_per_difficulty='800' reward_renown='1'
             reward_item='' relation_min='-10'
             title_key='{{=k}}t' description_key='{{=k}}d' brief_key='{{=k}}b' accept_key='{{=k}}a'
             explanation_key='{{=k}}e' solution_accept_key='{{=k}}s' task_key='{{=k}}task' {attrs} />
</LotrIssues>";

    [TestMethod]
    public void ParseIssues_ValidIssue_ParsesAllFields()
    {
        var list = Parse(Doc(""));
        Assert.AreEqual(1, list.Count);
        var d = list[0];
        Assert.AreEqual("lotr_grain", d.Id);
        Assert.AreEqual(LotrIssueTemplate.DeliverGoods, d.Template);
        Assert.AreEqual(IssueGiverOccupation.Headman, d.GiverOccupation);
        Assert.AreEqual(IssueFrequencyTier.Common, d.Frequency);
        CollectionAssert.AreEqual(new[] { "gondor", "vlandia" }, new List<string>(d.Cultures));
        Assert.AreEqual(12, d.Count);
        Assert.AreEqual(180f, d.CountPerDifficulty);
        Assert.AreEqual("item:grain", d.ItemSource);
        Assert.AreEqual(800f, d.RewardGoldPerDifficulty);
        Assert.AreEqual(1, d.RewardRenown);
        Assert.AreEqual(-10, d.RelationMin);
        Assert.AreEqual("{=k}t", d.Text.TitleKey);
    }

    [TestMethod]
    public void ParseIssues_MissingId_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue template='DeliverGoods' giver_occupation='Headman' count='5' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_DuplicateId_SecondSkipped()
    {
        var xml = @"<LotrIssues>
          <LotrIssue id='dup' template='DeliverGoods' giver_occupation='Headman' count='5' item_source='item:grain' title_key='t' description_key='d' />
          <LotrIssue id='dup' template='ClearHideout' giver_occupation='Lord' count='3' title_key='t2' description_key='d2' />
        </LotrIssues>";
        var list = Parse(xml);
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(LotrIssueTemplate.DeliverGoods, list[0].Template);
    }

    [TestMethod]
    public void ParseIssues_UnknownTemplate_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='Nope' giver_occupation='Headman' count='5' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_UnknownGiverOccupation_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='DeliverGoods' giver_occupation='Wizard' count='5' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_UnknownFrequency_DefaultsToCommon()
    {
        var list = Parse(Doc("frequency='Sometimes'").Replace("frequency='Common'", ""));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(IssueFrequencyTier.Common, list[0].Frequency);
    }

    [TestMethod]
    public void ParseIssues_CountZero_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='DeliverGoods' giver_occupation='Headman' count='0' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_MissingTitle_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='DeliverGoods' giver_occupation='Headman' count='5' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_MissingDescription_Skipped()
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='DeliverGoods' giver_occupation='Headman' count='5' title_key='t' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_NegativeRewardGold_CoercedToZero()
    {
        var list = Parse(Doc("").Replace("reward_gold_base='0'", "reward_gold_base='-50'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(0, list[0].RewardGoldBase);
    }

    [TestMethod]
    public void ParseIssues_NaNCountPerDifficulty_CoercedToZero()
    {
        var list = Parse(Doc("").Replace("count_per_difficulty='180'", "count_per_difficulty='NaN'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(0f, list[0].CountPerDifficulty);
    }

    [TestMethod]
    public void ParseIssues_InfiniteRewardGoldPerDifficulty_CoercedToZero()
    {
        var list = Parse(Doc("").Replace("reward_gold_per_difficulty='800'", "reward_gold_per_difficulty='Infinity'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(0f, list[0].RewardGoldPerDifficulty);
    }

    [TestMethod]
    public void ParseIssues_RelationMinOutOfRange_CoercedToDefault()
    {
        var list = Parse(Doc("").Replace("relation_min='-10'", "relation_min='-500'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(-10, list[0].RelationMin);
    }

    [TestMethod]
    public void ParseIssues_DeliverGoodsBareItemSource_Skipped()
    {
        // A non-prefixed source ('grain') can't be resolved by DeliverGoods (Wave 0 = item: only) → skipped.
        var list = Parse(Doc("").Replace("item_source='item:grain'", "item_source='grain'"));
        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void ParseIssues_DeliverGoodsCategorySource_Skipped()
    {
        // category: sourcing is not implemented until a later wave → DeliverGoods issue skipped at load.
        var list = Parse(Doc("").Replace("item_source='item:grain'", "item_source='category:Grain'"));
        Assert.AreEqual(0, list.Count);
    }

    [TestMethod]
    public void ParseIssues_CombatUnknownVariant_Skipped()
        // A non-empty Combat variant that isn't an implemented mechanic (e.g. a typo) is skipped, not
        // silently coerced to DefeatRaids — surfaces the typo at load instead of changing mechanics.
        => Assert.AreEqual(0, Parse("<LotrIssues><LotrIssue id='x' template='Combat' variant='CaptureLord' giver_occupation='Lord' count='1' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_CombatValidVariant_Kept()
    {
        var list = Parse("<LotrIssues><LotrIssue id='x' template='Combat' variant='WinTournaments' giver_occupation='Lord' count='1' title_key='t' description_key='d' /></LotrIssues>");
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("WinTournaments", list[0].Variant);
    }

    [TestMethod]
    public void ParseIssues_CombatEmptyVariant_Kept()
        // Empty variant is the explicit DefeatRaids default — allowed, not skipped.
        => Assert.AreEqual(1, Parse("<LotrIssues><LotrIssue id='x' template='Combat' giver_occupation='Lord' count='1' title_key='t' description_key='d' /></LotrIssues>").Count);

    [TestMethod]
    public void ParseIssues_InvalidTroopSource_Cleared()
    {
        var list = Parse(Doc("").Replace("troop_source=''", "troop_source='legendary'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("", list[0].TroopSource);
    }

    [TestMethod]
    public void ParseIssues_ValidTroopSource_Kept()
    {
        var list = Parse(Doc("").Replace("troop_source=''", "troop_source='Mount'"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual("mount", list[0].TroopSource);
    }

    [TestMethod]
    public void ParseIssues_EmptyCultures_AppliesToAll()
    {
        var list = Parse(Doc("").Replace("cultures='gondor,vlandia'", "cultures=''"));
        Assert.AreEqual(1, list.Count);
        Assert.AreEqual(0, list[0].Cultures.Count);
        Assert.IsTrue(list[0].AppliesToCulture("anything"));
    }

    [TestMethod]
    public void ParseIssues_CultureFilter_OnlyMatchesListed()
    {
        var d = Parse(Doc(""))[0];
        Assert.IsTrue(d.AppliesToCulture("gondor"));
        Assert.IsTrue(d.AppliesToCulture("VLANDIA")); // case-insensitive
        Assert.IsFalse(d.AppliesToCulture("mordor"));
    }

    [TestMethod]
    public void ParseIssues_NullRoot_ReturnsEmpty()
        => Assert.AreEqual(0, _sut.ParseIssues(new XDocument()).Count);

    // End-to-end smoke: drive the ACTUAL shipped config through the provider's parse + validation, so a
    // malformed/invalid issue (bad item-source scheme, missing required key, wrong template) is caught
    // at test time rather than silently dropped in-game. Bump the expected count as waves add configs.
    [TestMethod]
    public void ShippedConfig_AllIssuesParse_DeliverGoodsItemSourced()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData\lotr_issues\taom_lotr_issues.xml");
        Assert.IsTrue(File.Exists(path), $"shipped config not found at {path}");

        var list = _sut.ParseIssues(XDocument.Load(path));
        Assert.AreEqual(43, list.Count, "every shipped issue must pass validation (none silently dropped)");
        foreach (var d in list)
        {
            Assert.IsTrue(
                d.Template == LotrIssueTemplate.DeliverGoods
                || d.Template == LotrIssueTemplate.DeliverPersonnel
                || d.Template == LotrIssueTemplate.Combat,
                $"{d.Id}: only DeliverGoods/DeliverPersonnel/Combat are implemented so far");
            if (d.Template == LotrIssueTemplate.DeliverGoods)
                Assert.IsTrue(d.ItemSource.StartsWith("item:", StringComparison.OrdinalIgnoreCase), $"{d.Id}: DeliverGoods must be item:-sourced");
            Assert.IsFalse(string.IsNullOrEmpty(d.Text.TitleKey), $"{d.Id}: missing title key");
            Assert.IsFalse(string.IsNullOrEmpty(d.Text.DescriptionKey), $"{d.Id}: missing description key");
            Assert.IsTrue(d.Count > 0, $"{d.Id}: count must be > 0");
        }
    }
}
