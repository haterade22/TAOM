using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class CareerConfigProviderTests
{
    private IPathService _pathService;
    private IModLogger _logger;
    private CareerConfigProvider _provider;
    private string _tempDir;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "taom_test_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "career_system"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);

        _logger = Substitute.For<IModLogger>();
        _provider = new CareerConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void LoadCareers_ValidXml_ReturnsCareers()
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""25"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root"">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups><Group id=""wb_brutality"" /></ChoiceGroups>
  </Career>
</Careers>");

        var careers = _provider.LoadCareers();

        Assert.AreEqual(1, careers.Count);
        Assert.AreEqual("warboss", careers[0].Id);
        Assert.AreEqual("Warboss", careers[0].DisplayName);
        Assert.AreEqual("wb_root", careers[0].RootChoiceId);
        Assert.AreEqual(1, careers[0].EligibleCultureIds.Count);
        Assert.AreEqual("mordor", careers[0].EligibleCultureIds[0]);
        Assert.AreEqual(1, careers[0].ChoiceGroupIds.Count);
    }

    [TestMethod]
    public void LoadCareers_WithRankNames_ParsesPerTierRankNames()
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""30"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root""
          rank1_name=""{=k1}Boy"" rank2_name=""{=k2}Boss"" rank3_name=""{=k3}Warlord"">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups><Group id=""wb_brutality"" /></ChoiceGroups>
  </Career>
</Careers>");

        var careers = _provider.LoadCareers();

        Assert.AreEqual(1, careers.Count);
        Assert.AreEqual("{=k1}Boy", careers[0].Rank1Name);
        Assert.AreEqual("{=k2}Boss", careers[0].Rank2Name);
        Assert.AreEqual("{=k3}Warlord", careers[0].Rank3Name);
    }

    [TestMethod]
    public void LoadCareers_WithoutRankNames_DefaultsToEmpty()
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""30"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root"" />
</Careers>");

        var careers = _provider.LoadCareers();
        Assert.AreEqual("", careers[0].Rank1Name);
        Assert.AreEqual("", careers[0].Rank2Name);
        Assert.AreEqual("", careers[0].Rank3Name);
    }

    [TestMethod]
    public void GetMaxPerkPoints_FromXml_ReturnsConfiguredValue()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""25""></Careers>");
        Assert.AreEqual(25, _provider.GetMaxPerkPoints());
    }

    [TestMethod]
    public void LoadCareers_MissingFile_ReturnsEmpty()
    {
        var careers = _provider.LoadCareers();
        Assert.AreEqual(0, careers.Count);
    }

    [TestMethod]
    public void LoadChoices_ValidXml_ReturnsChoicesAndGroups()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""wb_root"" group_id="""" type=""Passive"" description=""Root"" icon_sprite=""icon"">
    <PassiveEffect type=""TroopDamage"" magnitude=""0.05"" operation=""Add"" is_percentage=""true"" />
    <Mutations>
      <Mutation target_id=""rally_horde"" property=""Duration""
                calculator=""skill_scaling"" skill=""Leadership"" factor=""0.02"" operation=""Add"" />
    </Mutations>
  </Choice>
  <ChoiceGroup id=""wb_brutality"" career_id=""warboss"" tier=""1"">
    <Choice id=""wb_brut_key"" type=""Keystone"" description=""Keystone"" icon_sprite=""icon"" />
    <Choice id=""wb_brut_p1"" type=""Passive"" description=""Passive 1"" icon_sprite=""icon"">
      <PassiveEffect type=""Damage"" magnitude=""0.10"" operation=""Add"" is_percentage=""true"" attack_type_mask=""Melee"" />
    </Choice>
  </ChoiceGroup>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        var groups = _provider.LoadChoiceGroups();

        Assert.AreEqual(3, choices.Count);

        var root = choices[0];
        Assert.AreEqual("wb_root", root.Id);
        Assert.AreEqual(ChoiceType.Passive, root.Type);
        Assert.AreEqual(1, root.Passives.Count);
        Assert.AreEqual(PassiveEffectType.TroopDamage, root.Passives[0].EffectType);
        Assert.AreEqual(0.05f, root.Passives[0].Magnitude, 0.001f);
        Assert.AreEqual(1, root.Mutations.Count);
        Assert.AreEqual("skill_scaling", root.Mutations[0].CalculatorId);
        Assert.AreEqual("Leadership", root.Mutations[0].Params["skill"]);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("wb_brutality", groups[0].Id);
        Assert.AreEqual("warboss", groups[0].CareerId);
        Assert.AreEqual(1, groups[0].Tier);
        Assert.AreEqual(2, groups[0].ChoiceIds.Count);
    }

    // -- #766: a choice inside a <ChoiceGroup> takes the group's id as its GroupId --

    [TestMethod]
    public void LoadChoices_ChoicesInsideGroupWithoutGroupIdAttribute_TakeTheGroupsId()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <ChoiceGroup id=""g1"" career_id=""warboss"" tier=""1"">
    <Choice id=""g1_key"" type=""Keystone"" description=""Keystone"" icon_sprite=""icon"" />
    <Choice id=""g1_p1"" type=""Passive"" description=""Passive 1"" icon_sprite=""icon"" />
  </ChoiceGroup>
</CareerChoices>");

        var choices = _provider.LoadChoices();

        Assert.AreEqual(2, choices.Count);
        Assert.AreEqual("g1", choices.First(c => c.Id == "g1_key").GroupId);
        Assert.AreEqual("g1", choices.First(c => c.Id == "g1_p1").GroupId);
    }

    [TestMethod]
    public void LoadChoices_NestedChoiceWithGroupIdAttribute_UsesParentIdAndWarns()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <ChoiceGroup id=""g1"" career_id=""warboss"" tier=""1"">
    <Choice id=""g1_odd"" group_id=""other"" type=""Passive"" description=""x"" icon_sprite=""icon"" />
    <Choice id=""g1_same"" group_id=""g1"" type=""Passive"" description=""y"" icon_sprite=""icon"" />
    <Choice id=""g1_empty"" group_id="""" type=""Passive"" description=""z"" icon_sprite=""icon"" />
  </ChoiceGroup>
</CareerChoices>");

        var choices = _provider.LoadChoices();

        Assert.AreEqual("g1", choices.First(c => c.Id == "g1_odd").GroupId);
        Assert.AreEqual("g1", choices.First(c => c.Id == "g1_same").GroupId);
        Assert.AreEqual("g1", choices.First(c => c.Id == "g1_empty").GroupId);
        _logger.Received(1).LogWarning(Arg.Is<string>(
            s => s.Contains("g1_odd") && s.Contains("other") && s.Contains("ignored; membership is the enclosing ChoiceGroup")));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("g1_same")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("g1_empty")));
    }

    [TestMethod]
    public void LoadChoices_RootChoiceWithGroupIdAttribute_HasEmptyGroupIdAndWarns()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""root_empty"" group_id="""" type=""Passive"" description=""x"" icon_sprite=""icon"" />
  <Choice id=""root_absent"" type=""Passive"" description=""x"" icon_sprite=""icon"" />
  <Choice id=""root_set"" group_id=""declared"" type=""Passive"" description=""x"" icon_sprite=""icon"" />
</CareerChoices>");

        var choices = _provider.LoadChoices();

        Assert.AreEqual("", choices.First(c => c.Id == "root_empty").GroupId);
        Assert.AreEqual("", choices.First(c => c.Id == "root_absent").GroupId);
        Assert.AreEqual("", choices.First(c => c.Id == "root_set").GroupId);
        _logger.Received(1).LogWarning(Arg.Is<string>(
            s => s.Contains("root_set") && s.Contains("declared") && s.Contains("ignored; membership is the enclosing ChoiceGroup")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("root_empty") || s.Contains("root_absent")));
    }

    [TestMethod]
    public void LoadChoices_GroupWithDisplayName_ParsesDisplayName()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <ChoiceGroup id=""wb_brutality"" career_id=""warboss"" tier=""1"" display_name=""{=k}Path of Brutality"">
    <Choice id=""wb_brut_key"" type=""Keystone"" description=""Keystone"" icon_sprite=""icon"" />
  </ChoiceGroup>
</CareerChoices>");

        var groups = _provider.LoadChoiceGroups();
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("{=k}Path of Brutality", groups[0].DisplayName);
    }

    [TestMethod]
    public void LoadChoices_GroupWithoutDisplayName_DefaultsToEmpty()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <ChoiceGroup id=""wb_brutality"" career_id=""warboss"" tier=""1"">
    <Choice id=""wb_brut_key"" type=""Keystone"" description=""Keystone"" icon_sprite=""icon"" />
  </ChoiceGroup>
</CareerChoices>");

        var groups = _provider.LoadChoiceGroups();
        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("", groups[0].DisplayName);
    }

    [TestMethod]
    public void LoadChoices_PassiveWithAttackTypeMask_ParsesMelee()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""Damage"" magnitude=""0.1"" operation=""Add"" attack_type_mask=""Melee"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(AttackTypeMask.Melee, choices[0].Passives[0].AttackTypeMask);
    }

    [TestMethod]
    public void LoadChoices_KindMasks_ParseToTheirKind()
    {
        // #613: Blunt / Cut / Pierce are real mask members now (the damage-kind axis). Until then the
        // five shipped kind pips fell back to All and resisted every hit.
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""cblunt"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""Resistance"" magnitude=""0.05"" attack_type_mask=""Blunt"" />
  </Choice>
  <Choice id=""ccut"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""Resistance"" magnitude=""0.08"" attack_type_mask=""Cut"" />
  </Choice>
  <Choice id=""cboth"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""Resistance"" magnitude=""0.08"" attack_type_mask=""Melee, Pierce"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(AttackTypeMask.Blunt, choices[0].Passives[0].AttackTypeMask);
        Assert.AreEqual(AttackTypeMask.Cut, choices[1].Passives[0].AttackTypeMask);
        Assert.AreEqual(AttackTypeMask.Melee | AttackTypeMask.Pierce, choices[2].Passives[0].AttackTypeMask);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("attack_type_mask")));
    }

    [TestMethod]
    public void LoadChoices_UnknownAttackTypeMask_WarnsAndLeavesThePipInert()
    {
        // The consumer branches on this string; an unknown value must not silently take the widest
        // reading (csharp-architecture.md "Config Providers MUST Validate"). None never matches a hit,
        // and the shipped-XML gate refuses a None mask, so a typo fails a test instead of shipping.
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""cfire"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""Resistance"" magnitude=""0.05"" attack_type_mask=""Fire"" />
  </Choice>
  <Choice id=""cdigit"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""Resistance"" magnitude=""0.05"" attack_type_mask=""1"" />
  </Choice>
  <Choice id=""cnone"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""Resistance"" magnitude=""0.05"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(AttackTypeMask.None, choices[0].Passives[0].AttackTypeMask);
        Assert.AreEqual(AttackTypeMask.None, choices[1].Passives[0].AttackTypeMask);
        Assert.AreEqual(AttackTypeMask.All, choices[2].Passives[0].AttackTypeMask, "no attribute keeps the All default");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cfire") && s.Contains("Fire")));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("cdigit")));
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownInXml_ReturnsConfiguredValue()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""45"" />
  <Infantry damage_bonus=""15"" damage_reduction=""10"" radius=""50"" />
  <Ranged speed_bonus=""15"" ranged_damage_bonus=""20"" draw_speed_bonus=""20"" />
  <Cavalry mount_speed_bonus=""20"" charge_damage_bonus=""25"" damage_bonus=""10"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(45f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalElementMissing_ReturnsDefaultThirtySeconds()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Infantry damage_bonus=""15"" damage_reduction=""10"" radius=""50"" />
  <Ranged speed_bonus=""15"" ranged_damage_bonus=""20"" draw_speed_bonus=""20"" />
  <Cavalry mount_speed_bonus=""20"" charge_damage_bonus=""25"" damage_bonus=""10"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_TuningFileMissing_ReturnsDefaultGlobalCooldown()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        // No tuning XML written.

        var tuning = _provider.GetAbilityTuning();
        Assert.IsNotNull(tuning.Global);
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownInvalid_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""nonsense"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownNegative_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""-5"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownExceedsMaximum_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""99999"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownAtMaximum_AcceptsValue()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""3600"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(3600f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownNaN_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""NaN"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownPositiveInfinity_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""Infinity"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void GetAbilityTuning_GlobalCooldownNegativeInfinity_FallsBackToDefault()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteAbilityTuningXml(@"<?xml version='1.0'?>
<AbilityTuning>
  <Global cooldown_seconds=""-Infinity"" />
</AbilityTuning>");

        var tuning = _provider.GetAbilityTuning();
        Assert.AreEqual(30f, tuning.Global.CooldownSeconds, 0.001f);
    }

    // ── Issue B: <PassiveEffects> wrapper + value= alias (310 wrapped choices were dead) ──

    [TestMethod]
    public void LoadChoices_PassiveEffectsWrapper_ParsesNestedPassive()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffects>
      <PassiveEffect type=""TroopDamage"" value=""0.10"" />
    </PassiveEffects>
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();

        Assert.AreEqual(1, choices.Count);
        Assert.AreEqual(1, choices[0].Passives.Count, "Nested <PassiveEffects><PassiveEffect/> must be read");
        Assert.AreEqual(PassiveEffectType.TroopDamage, choices[0].Passives[0].EffectType);
        Assert.AreEqual(0.10f, choices[0].Passives[0].Magnitude, 0.001f);
    }

    [TestMethod]
    public void LoadChoices_PassiveEffectValueAttribute_ParsedAsMagnitude()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""PartySize"" value=""0.10"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(0.10f, choices[0].Passives[0].Magnitude, 0.001f, "value= must alias magnitude=");
    }

    [TestMethod]
    public void LoadChoices_MagnitudeWinsOverValueWhenBothPresent()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""PartySize"" magnitude=""4"" value=""0.10"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(4f, choices[0].Passives[0].Magnitude, 0.001f, "magnitude= takes precedence over value=");
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    public void LoadChoices_NonFiniteMagnitude_LoadsAsZero(string authored)
    {
        // ScaleByPassive (#769 E11) floors a scale at zero and relies on the loader to keep a NaN or an infinity
        // out of the cache: Math.Max(0, 1 + NaN) is NaN, which would poison every price it scales.
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""TradePrices"" magnitude=""" + authored + @""" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();

        Assert.AreEqual(0f, choices[0].Passives[0].Magnitude);
    }

    [TestMethod]
    public void LoadChoices_DirectPassiveWithMagnitude_StillParses()
    {
        // Regression: the direct (singular, magnitude=) schema must be unaffected by the
        // wrapper/value fallback.
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""PartySize"" magnitude=""2"" operation=""Add"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(PassiveEffectType.PartySize, choices[0].Passives[0].EffectType);
        Assert.AreEqual(2f, choices[0].Passives[0].Magnitude, 0.001f);
    }

    [TestMethod]
    public void LoadChoices_EmptyPassiveEffectsWrapper_YieldsNoPassivesNoThrow()
    {
        // Defensive: a wrapper with no <PassiveEffect> child (or a foreign child) must parse to an
        // empty passive list without throwing — the choice is still valid, it just grants no passive.
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffects></PassiveEffects>
  </Choice>
  <Choice id=""c2"" type=""Passive"" description=""test2"" icon_sprite=""icon"">
    <PassiveEffects><Unrelated foo=""bar"" /></PassiveEffects>
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();
        Assert.AreEqual(2, choices.Count);
        Assert.AreEqual(0, choices[0].Passives.Count);
        Assert.AreEqual(0, choices[1].Passives.Count);
    }

    // ── #769 E8: up to MaxPassivesPerChoice effects per option ──

    private System.Collections.Generic.IReadOnlyList<PassiveEffect> LoadPassivesOf(string choiceBody)
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">" + choiceBody + @"</Choice>
</CareerChoices>");
        return _provider.LoadChoices().Single().Passives;
    }

    // ── F1: a magnitude is bounded, so two accepted pips cannot sum to Infinity ──

    [TestMethod]
    [DataRow("3e38")]
    [DataRow("-3e38")]
    [DataRow("1e5")]
    [DataRow("10000.5")]
    public void LoadChoices_PassiveMagnitudeAboveTheBound_IsDroppedWithAWarning(string magnitude)
    {
        var passives = LoadPassivesOf(@"<PassiveEffect type=""DailyGold"" magnitude=""" + magnitude + @""" />");

        Assert.AreEqual(0, passives.Count);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("c1") && s.Contains("DailyGold") && s.Contains(magnitude.TrimStart('-'))));
    }

    [TestMethod]
    [DataRow("10000", 10000f)]
    [DataRow("-10000", -10000f)]
    public void LoadChoices_PassiveMagnitudeAtTheBound_Loads(string magnitude, float expected)
    {
        var passives = LoadPassivesOf(@"<PassiveEffect type=""DailyGold"" magnitude=""" + magnitude + @""" />");

        Assert.AreEqual(1, passives.Count);
        Assert.AreEqual(expected, passives[0].Magnitude);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("magnitude")));
    }

    [TestMethod]
    public void LoadChoices_ValueAliasAboveTheBound_IsDroppedToo()
    {
        var passives = LoadPassivesOf(@"<PassiveEffects><PassiveEffect type=""DailyGold"" value=""3e38"" /></PassiveEffects>");

        Assert.AreEqual(0, passives.Count);
    }

    [TestMethod]
    public void LoadChoices_NoPassive_YieldsAnEmptyListNeverNull()
    {
        var passives = LoadPassivesOf("");

        Assert.IsNotNull(passives);
        Assert.AreEqual(0, passives.Count);
    }

    [TestMethod]
    public void LoadChoices_TwoDirectPassives_BothParsedInDocumentOrder()
    {
        var passives = LoadPassivesOf(@"
    <PassiveEffect type=""Damage"" magnitude=""0.10"" />
    <PassiveEffect type=""Resistance"" magnitude=""0.05"" attack_type_mask=""Melee"" />");

        Assert.AreEqual(2, passives.Count);
        Assert.AreEqual(PassiveEffectType.Damage, passives[0].EffectType);
        Assert.AreEqual(0.10f, passives[0].Magnitude, 0.001f);
        Assert.AreEqual(PassiveEffectType.Resistance, passives[1].EffectType);
        Assert.AreEqual(AttackTypeMask.Melee, passives[1].AttackTypeMask);
    }

    [TestMethod]
    public void LoadChoices_TwoPassivesInTheWrapper_BothParsed()
    {
        var passives = LoadPassivesOf(@"
    <PassiveEffects>
      <PassiveEffect type=""Damage"" value=""0.10"" />
      <PassiveEffect type=""Health"" value=""25"" />
    </PassiveEffects>");

        Assert.AreEqual(2, passives.Count);
        Assert.AreEqual(PassiveEffectType.Damage, passives[0].EffectType);
        Assert.AreEqual(PassiveEffectType.Health, passives[1].EffectType);
        Assert.AreEqual(25f, passives[1].Magnitude, 0.001f);
    }

    [TestMethod]
    public void LoadChoices_DirectAndWrappedPassives_AreCollectedInDocumentOrder()
    {
        var passives = LoadPassivesOf(@"
    <PassiveEffects><PassiveEffect type=""Health"" value=""25"" /></PassiveEffects>
    <PassiveEffect type=""Damage"" magnitude=""0.10"" />");

        Assert.AreEqual(2, passives.Count);
        Assert.AreEqual(PassiveEffectType.Health, passives[0].EffectType, "the wrapper came first in the document");
        Assert.AreEqual(PassiveEffectType.Damage, passives[1].EffectType);
    }

    [TestMethod]
    public void LoadChoices_ThirdAndLaterPassives_AreIgnoredWithOneWarningNamingTheChoice()
    {
        var passives = LoadPassivesOf(@"
    <PassiveEffect type=""Damage"" magnitude=""0.10"" />
    <PassiveEffect type=""Resistance"" magnitude=""0.05"" />
    <PassiveEffect type=""Health"" magnitude=""25"" />
    <PassiveEffect type=""Ammo"" magnitude=""0.2"" />");

        Assert.AreEqual(CareerConfigProvider.MaxPassivesPerChoice, passives.Count);
        Assert.AreEqual(2, CareerConfigProvider.MaxPassivesPerChoice);
        Assert.AreEqual(PassiveEffectType.Resistance, passives[1].EffectType);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("c1") && s.Contains("ignored")));
    }

    [TestMethod]
    public void LoadChoices_TwoPassives_NoCapWarning()
    {
        LoadPassivesOf(@"
    <PassiveEffect type=""Damage"" magnitude=""0.10"" />
    <PassiveEffect type=""Resistance"" magnitude=""0.05"" />");

        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("choice 'c1'")));
    }

    [TestMethod]
    public void LoadChoices_PassiveInEachOfTwoWrappers_BothParsed()
    {
        var passives = LoadPassivesOf(@"
    <PassiveEffects><PassiveEffect type=""Damage"" value=""0.10"" /></PassiveEffects>
    <PassiveEffects><PassiveEffect type=""Resistance"" value=""0.05"" /></PassiveEffects>");

        Assert.AreEqual(2, passives.Count);
        Assert.AreEqual(PassiveEffectType.Resistance, passives[1].EffectType);
    }

    [TestMethod]
    public void LoadChoices_SecondPassiveOfAnUnconsumedType_LogsPhantomWarning()
    {
        LoadPassivesOf(@"
    <PassiveEffect type=""Damage"" magnitude=""0.10"" />
    <PassiveEffect type=""BuffDuration"" magnitude=""0.1"" />");

        _logger.Received().LogWarning(Arg.Is<string>(
            s => s.Contains("phantom") && s.Contains("c1") && s.Contains("BuffDuration")));
    }

    // ── #769 E9: troop_class ──

    [DataTestMethod]
    [DataRow("Infantry", TroopClassMask.Infantry)]
    [DataRow("cavalry", TroopClassMask.Cavalry)]
    [DataRow("Infantry, Ranged", TroopClassMask.Infantry | TroopClassMask.Ranged)]
    [DataRow("Ranged|Cavalry", TroopClassMask.Ranged | TroopClassMask.Cavalry)]
    [DataRow("Infantry | Ranged", TroopClassMask.Infantry | TroopClassMask.Ranged)]
    [DataRow("All", TroopClassMask.All)]
    public void LoadChoices_TroopClassOnAClassFilteredType_ParsesTheNames(string text, TroopClassMask expected)
    {
        var passives = LoadPassivesOf(@"<PassiveEffect type=""TroopDamage"" magnitude=""0.10"" troop_class=""" + text + @""" />");

        Assert.AreEqual(1, passives.Count);
        Assert.AreEqual(expected, passives[0].TroopClass);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("choice 'c1'")));
    }

    [TestMethod]
    public void LoadChoices_NoTroopClass_KeepsTheAllDefault()
    {
        var passives = LoadPassivesOf(@"<PassiveEffect type=""TroopResistance"" magnitude=""0.10"" />");

        Assert.AreEqual(TroopClassMask.All, passives[0].TroopClass);
    }

    [DataTestMethod]
    [DataRow("Infantri")]
    [DataRow("None")]
    [DataRow("")]
    [DataRow("1")]
    [DataRow("Ranged Cavalry")]
    public void LoadChoices_UnknownTroopClass_DropsThatPassiveAndWarnsNamingTheChoice(string text)
    {
        // Fail closed: a typo must never widen an infantry bonus to the whole army, so the passive is
        // gone, not defaulted to All. The sibling passive of the same choice is untouched.
        var passives = LoadPassivesOf(@"
    <PassiveEffect type=""TroopDamage"" magnitude=""0.10"" troop_class=""" + text + @""" />
    <PassiveEffect type=""Health"" magnitude=""25"" />");

        Assert.AreEqual(1, passives.Count);
        Assert.AreEqual(PassiveEffectType.Health, passives[0].EffectType);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("c1") && s.Contains("troop_class")));
    }

    [TestMethod]
    public void LoadChoices_TroopClassOnATypeThatReadsNoClass_WarnsAndIsIgnored()
    {
        // Damage (the hero's own) passes no troop class, so honouring the attribute would pretend to
        // restrict a pip it cannot restrict. The pip keeps its effect at All.
        var passives = LoadPassivesOf(@"<PassiveEffect type=""Damage"" magnitude=""0.10"" troop_class=""Infantry"" />");

        Assert.AreEqual(1, passives.Count);
        Assert.AreEqual(TroopClassMask.All, passives[0].TroopClass);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("c1") && s.Contains("troop_class") && s.Contains("Damage")));
    }

    [TestMethod]
    public void LoadChoices_UnknownTroopClassOnATypeThatReadsNoClass_IsIgnoredNotDropped()
    {
        // The attribute means nothing on this type, so there is no reading to fail closed on; the pip keeps
        // its effect at All and one warning explains the attribute.
        var passives = LoadPassivesOf(@"<PassiveEffect type=""Damage"" magnitude=""0.10"" troop_class=""Infantri"" />");

        Assert.AreEqual(1, passives.Count);
        Assert.AreEqual(TroopClassMask.All, passives[0].TroopClass);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("troop_class")));
    }

    [TestMethod]
    public void LoadChoices_TroopClassOnTheSecondPassive_ParsesPerPassive()
    {
        var passives = LoadPassivesOf(@"
    <PassiveEffect type=""TroopDamage"" magnitude=""0.10"" troop_class=""Infantry"" />
    <PassiveEffect type=""TroopResistance"" magnitude=""0.05"" troop_class=""Cavalry"" />");

        Assert.AreEqual(TroopClassMask.Infantry, passives[0].TroopClass);
        Assert.AreEqual(TroopClassMask.Cavalry, passives[1].TroopClass);
    }

    [TestMethod]
    public void ClassFiltered_IsTheSixTroopTypesAndNoHeroType()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                PassiveEffectType.TroopDamage, PassiveEffectType.TroopResistance,
                PassiveEffectType.TroopFearResistance, PassiveEffectType.TroopKnockdownResistance, PassiveEffectType.TroopCrushHold,
                PassiveEffectType.TroopTraining,
            },
            PassiveEffectConsumers.ClassFiltered.ToArray());
        Assert.IsTrue(PassiveEffectConsumers.IsClassFiltered(PassiveEffectType.TroopDamage));
        Assert.IsFalse(PassiveEffectConsumers.IsClassFiltered(PassiveEffectType.Damage));
    }

    // ── attack_type_mask only where a consumer passes a hit ──

    [TestMethod]
    public void MaskGated_IsTheFourTypesWhoseConsumerPassesAHit()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                PassiveEffectType.Damage, PassiveEffectType.Resistance,
                PassiveEffectType.TroopDamage, PassiveEffectType.TroopResistance,
            },
            PassiveEffectConsumers.MaskGated.ToArray());
        Assert.IsTrue(PassiveEffectConsumers.IsMaskGated(PassiveEffectType.TroopResistance));
        Assert.IsFalse(PassiveEffectConsumers.IsMaskGated(PassiveEffectType.TroopFearResistance));
        Assert.IsFalse(PassiveEffectConsumers.IsMaskGated(PassiveEffectType.SwingSpeed));
    }

    [TestMethod]
    public void MaskGated_EveryTypeIsConsumed_AndTheTroopOnesAreClassFiltered()
    {
        foreach (var type in PassiveEffectConsumers.MaskGated)
            Assert.IsTrue(PassiveEffectConsumers.IsConsumed(type), type.ToString());
        Assert.IsTrue(PassiveEffectConsumers.IsClassFiltered(PassiveEffectType.TroopDamage));
        Assert.IsTrue(PassiveEffectConsumers.IsClassFiltered(PassiveEffectType.TroopResistance));
    }

    [DataTestMethod]
    [DataRow("TroopFearResistance")]
    [DataRow("TroopKnockdownResistance")]
    [DataRow("TroopCrushHold")]
    [DataRow("SwingSpeed")]
    [DataRow("Health")]
    public void LoadChoices_AttackTypeMaskOnATypeThatPassesNoHit_WarnsAndIsIgnored(string type)
    {
        // The counters and every other type read no hit, so a mask on them restricts nothing. Honouring it
        // would pretend to; the pip keeps its effect at All and the warning names the choice and the type.
        var passives = LoadPassivesOf(@"<PassiveEffect type=""" + type + @""" magnitude=""0.10"" attack_type_mask=""Ranged"" />");

        Assert.AreEqual(1, passives.Count);
        Assert.AreEqual(AttackTypeMask.All, passives[0].AttackTypeMask);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("c1") && s.Contains("attack_type_mask") && s.Contains(type)));
    }

    [TestMethod]
    public void LoadChoices_UnparseableAttackTypeMaskOnATypeThatPassesNoHit_IsAllNotInert()
    {
        // The loader's "the pip is inert" reading (a None mask) holds only where a hit is matched. Where no hit
        // is passed a None bucket still counts, so the attribute is ignored there with one warning, and the
        // inert warning is not written.
        var passives = LoadPassivesOf(@"<PassiveEffect type=""TroopCrushHold"" magnitude=""0.10"" attack_type_mask=""Sideways"" />");

        Assert.AreEqual(AttackTypeMask.All, passives[0].AttackTypeMask);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("attack_type_mask")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("inert") && s.Contains("Sideways")));
    }

    [DataTestMethod]
    [DataRow("Damage", AttackTypeMask.Ranged)]
    [DataRow("Resistance", AttackTypeMask.Ranged)]
    [DataRow("TroopDamage", AttackTypeMask.Ranged)]
    [DataRow("TroopResistance", AttackTypeMask.Ranged)]
    public void LoadChoices_AttackTypeMaskOnAMaskGatedType_IsKeptWithoutAWarning(string type, AttackTypeMask expected)
    {
        var passives = LoadPassivesOf(@"<PassiveEffect type=""" + type + @""" magnitude=""0.10"" attack_type_mask=""Ranged"" />");

        Assert.AreEqual(expected, passives[0].AttackTypeMask);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("choice 'c1'")));
    }

    [TestMethod]
    public void LoadChoices_UnparseableAttackTypeMaskOnAMaskGatedType_StaysInert()
    {
        var passives = LoadPassivesOf(@"<PassiveEffect type=""TroopDamage"" magnitude=""0.10"" attack_type_mask=""Sideways"" />");

        Assert.AreEqual(AttackTypeMask.None, passives[0].AttackTypeMask);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("Sideways") && s.Contains("inert")));
    }

    // ── the two-effect cap counts authored elements ──

    [TestMethod]
    public void LoadChoices_DroppedPassiveStillCountsTowardTheCap()
    {
        // The cap is on what the author wrote, not on what survived: the first element drops (its troop_class
        // is a typo), the second parses, and the third is still ignored with the cap warning, so a dropped
        // effect can never let a third one half-apply.
        var passives = LoadPassivesOf(@"
    <PassiveEffect type=""TroopDamage"" magnitude=""0.10"" troop_class=""Infantri"" />
    <PassiveEffect type=""Resistance"" magnitude=""0.05"" />
    <PassiveEffect type=""Health"" magnitude=""25"" />");

        Assert.AreEqual(1, passives.Count);
        Assert.AreEqual(PassiveEffectType.Resistance, passives[0].EffectType);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("c1") && s.Contains("holds 3") && s.Contains("ignored")));
    }

    // ── Phantom-bonus load-time gate (PassiveEffectConsumers) ──

    [TestMethod]
    public void LoadChoices_UnconsumedPassiveType_LogsPhantomWarning()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""BuffDuration"" magnitude=""0.1"" />
  </Choice>
</CareerChoices>");

        _provider.LoadChoices();

        _logger.Received().LogWarning(Arg.Is<string>(
            s => s.Contains("phantom") && s.Contains("c1") && s.Contains("BuffDuration")));
    }

    [TestMethod]
    public void LoadChoices_UnknownPassiveType_LogsWarningAndFallsBackToSpecial()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""NoSuchEffectType"" magnitude=""0.1"" />
  </Choice>
</CareerChoices>");

        var choices = _provider.LoadChoices();

        Assert.AreEqual(PassiveEffectType.Special, choices[0].Passives[0].EffectType);
        _logger.Received().LogWarning(Arg.Is<string>(
            s => s.Contains("unknown") && s.Contains("c1") && s.Contains("NoSuchEffectType")));
    }

    [TestMethod]
    public void LoadChoices_ConsumedPassiveType_NoPhantomWarning()
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <Choice id=""c1"" type=""Passive"" description=""test"" icon_sprite=""icon"">
    <PassiveEffect type=""Damage"" magnitude=""0.1"" />
  </Choice>
</CareerChoices>");

        _provider.LoadChoices();

        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("phantom")));
    }

    // ── Issue #380 — keystone_icon attribute (per-career keystone medallion glyph) ──

    [TestMethod]
    public void LoadCareers_WithKeystoneIcon_ParsesIt()
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""30"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root"" keystone_icon=""19001"">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups><Group id=""wb_brutality"" /></ChoiceGroups>
  </Career>
</Careers>");

        var careers = _provider.LoadCareers();

        Assert.AreEqual("19001", careers[0].KeystoneIcon);
    }

    [TestMethod]
    public void LoadCareers_MissingKeystoneIcon_EmptyAndWarns()
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""30"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root"">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups><Group id=""wb_brutality"" /></ChoiceGroups>
  </Career>
</Careers>");

        var careers = _provider.LoadCareers();

        // No silent fallback glyph — a missing attribute means no medallion renders (the
        // reference module's culture fallback regressed Rivendell straight back to the
        // clan-sigil ring it existed to avoid). A warning surfaces the authoring gap.
        Assert.AreEqual("", careers[0].KeystoneIcon);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("keystone_icon") && s.Contains("warboss")));
    }

    // ── #769 E5 — max_picks on a ChoiceGroup (0 = unlimited) ──

    private void WriteGroupWithMaxPicks(string maxPicksAttr)
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30""></Careers>");
        WriteChoicesXml(@"<?xml version='1.0'?>
<CareerChoices>
  <ChoiceGroup id=""wb_fork"" career_id=""warboss"" tier=""1"" " + maxPicksAttr + @">
    <Choice id=""wb_fork_a"" type=""Passive"" description=""a"" icon_sprite=""icon"" />
  </ChoiceGroup>
</CareerChoices>");
    }

    [TestMethod]
    public void LoadChoices_GroupWithoutMaxPicks_DefaultsToUnlimited()
    {
        WriteGroupWithMaxPicks("");

        Assert.AreEqual(0, _provider.LoadChoiceGroups()[0].MaxPicks);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("max_picks")));
    }

    [TestMethod]
    public void LoadChoices_GroupWithMaxPicks_ParsesIt()
    {
        WriteGroupWithMaxPicks(@"max_picks=""2""");

        Assert.AreEqual(2, _provider.LoadChoiceGroups()[0].MaxPicks);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("max_picks")));
    }

    [TestMethod]
    public void LoadChoices_GroupWithNonIntegerMaxPicks_WarnsAndUsesUnlimited()
    {
        WriteGroupWithMaxPicks(@"max_picks=""one""");

        Assert.AreEqual(0, _provider.LoadChoiceGroups()[0].MaxPicks);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("max_picks") && s.Contains("wb_fork")));
    }

    [TestMethod]
    public void LoadChoices_GroupWithNegativeMaxPicks_WarnsAndUsesUnlimited()
    {
        WriteGroupWithMaxPicks(@"max_picks=""-1""");

        Assert.AreEqual(0, _provider.LoadChoiceGroups()[0].MaxPicks);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("max_picks") && s.Contains("wb_fork")));
    }

    // ── #769 E6 — point_levels on a Career (empty = the legacy one-point-per-level formula) ──

    private void WriteCareerWithPointLevels(string pointLevelsAttr)
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""30"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root"" " + pointLevelsAttr + @">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups><Group id=""wb_brutality"" /></ChoiceGroups>
  </Career>
</Careers>");
    }

    [TestMethod]
    public void LoadCareers_WithoutPointLevels_IsEmpty()
    {
        WriteCareerWithPointLevels("");

        Assert.AreEqual(0, _provider.LoadCareers()[0].PointLevels.Count);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("point_levels")));
    }

    [TestMethod]
    public void LoadCareers_WithPointLevels_ParsesTheAscendingList()
    {
        WriteCareerWithPointLevels(@"point_levels=""1,5,10,15,20,25""");

        CollectionAssert.AreEqual(new[] { 1, 5, 10, 15, 20, 25 }, _provider.LoadCareers()[0].PointLevels.ToArray());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("point_levels")));
    }

    [TestMethod]
    public void LoadCareers_PointLevelsWithSpaces_Parses()
    {
        WriteCareerWithPointLevels(@"point_levels=""1, 5 ,10""");

        CollectionAssert.AreEqual(new[] { 1, 5, 10 }, _provider.LoadCareers()[0].PointLevels.ToArray());
    }

    [TestMethod]
    public void LoadCareers_PointLevelsNonInteger_WarnsAndUsesLegacy()
    {
        WriteCareerWithPointLevels(@"point_levels=""1,five,10""");

        Assert.AreEqual(0, _provider.LoadCareers()[0].PointLevels.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("point_levels") && s.Contains("warboss")));
    }

    [TestMethod]
    public void LoadCareers_PointLevelsUnsorted_WarnsAndUsesLegacy()
    {
        WriteCareerWithPointLevels(@"point_levels=""1,10,5""");

        Assert.AreEqual(0, _provider.LoadCareers()[0].PointLevels.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("point_levels") && s.Contains("warboss")));
    }

    [TestMethod]
    public void LoadCareers_PointLevelsDuplicate_WarnsAndUsesLegacy()
    {
        WriteCareerWithPointLevels(@"point_levels=""1,5,5""");

        Assert.AreEqual(0, _provider.LoadCareers()[0].PointLevels.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("point_levels")));
    }

    [TestMethod]
    public void LoadCareers_PointLevelsNonPositive_WarnsAndUsesLegacy()
    {
        WriteCareerWithPointLevels(@"point_levels=""0,5""");

        Assert.AreEqual(0, _provider.LoadCareers()[0].PointLevels.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("point_levels")));
    }

    [TestMethod]
    public void LoadCareers_PointLevelsEmptyEntry_WarnsAndUsesLegacy()
    {
        WriteCareerWithPointLevels(@"point_levels=""1,,5""");

        Assert.AreEqual(0, _provider.LoadCareers()[0].PointLevels.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("point_levels")));
    }

    private void WriteCareersXml(string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, "career_system", "taom_careers.xml"), content);
    }

    private void WriteChoicesXml(string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, "career_system", "taom_career_choices.xml"), content);
    }

    private void WriteAbilityTuningXml(string content)
    {
        File.WriteAllText(Path.Combine(_tempDir, "career_system", "taom_ability_tuning.xml"), content);
    }

    // ── #769 E7: retrain price attributes on the <Careers> root ──

    private void WriteCareersRoot(string rootAttrs)
    {
        WriteCareersXml(@"<?xml version='1.0'?><Careers max_perk_points=""30"" " + rootAttrs + "></Careers>");
    }

    [TestMethod]
    public void GetRetrainGold_AttributesAbsent_UseDefaults()
    {
        WriteCareersRoot("");

        Assert.AreEqual(1000, _provider.GetRetrainGoldBase());
        Assert.AreEqual(250, _provider.GetRetrainGoldPerLevel());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("retrain_gold")));
    }

    [TestMethod]
    public void GetRetrainGold_ValidAttributes_AreRead()
    {
        WriteCareersRoot(@"retrain_gold_base=""1500"" retrain_gold_per_level=""0""");

        Assert.AreEqual(1500, _provider.GetRetrainGoldBase());
        Assert.AreEqual(0, _provider.GetRetrainGoldPerLevel());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("retrain_gold")));
    }

    [TestMethod]
    public void GetRetrainGold_NonInteger_WarnsAndUsesTheDefault()
    {
        WriteCareersRoot(@"retrain_gold_base=""lots"" retrain_gold_per_level=""2.5""");

        Assert.AreEqual(1000, _provider.GetRetrainGoldBase());
        Assert.AreEqual(250, _provider.GetRetrainGoldPerLevel());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("retrain_gold_base")));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("retrain_gold_per_level")));
    }

    [TestMethod]
    public void GetRetrainGold_Negative_WarnsAndUsesTheDefault()
    {
        WriteCareersRoot(@"retrain_gold_base=""-5"" retrain_gold_per_level=""-1""");

        Assert.AreEqual(1000, _provider.GetRetrainGoldBase());
        Assert.AreEqual(250, _provider.GetRetrainGoldPerLevel());
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("retrain_gold_base")));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("retrain_gold_per_level")));
    }

    [TestMethod]
    public void GetRetrainGold_CareersFileMissing_UsesDefaults()
    {
        Assert.AreEqual(1000, _provider.GetRetrainGoldBase());
        Assert.AreEqual(250, _provider.GetRetrainGoldPerLevel());
    }

    // ── #769 E12: tree_version on a Career ──

    private void WriteCareerWithTreeVersion(string treeVersionAttr)
    {
        WriteCareersXml(@"<?xml version='1.0'?>
<Careers max_perk_points=""30"">
  <Career id=""warboss"" display_name=""Warboss"" description=""A brute.""
          portrait_sprite=""wb_sprite"" ability_template_id=""rally_horde""
          min_clan_tier=""0"" root_choice_id=""wb_root"" " + treeVersionAttr + @">
    <EligibleCultures><Culture id=""mordor"" /></EligibleCultures>
    <ChoiceGroups><Group id=""wb_brutality"" /></ChoiceGroups>
  </Career>
</Careers>");
    }

    [TestMethod]
    public void LoadCareers_WithoutTreeVersion_IsOne()
    {
        WriteCareerWithTreeVersion("");

        Assert.AreEqual(1, _provider.LoadCareers()[0].TreeVersion);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("tree_version")));
    }

    [TestMethod]
    public void LoadCareers_WithTreeVersion_ParsesIt()
    {
        WriteCareerWithTreeVersion(@"tree_version=""2""");

        Assert.AreEqual(2, _provider.LoadCareers()[0].TreeVersion);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("tree_version")));
    }

    [TestMethod]
    public void LoadCareers_TreeVersionNonInteger_WarnsAndUsesOne()
    {
        WriteCareerWithTreeVersion(@"tree_version=""two""");

        Assert.AreEqual(1, _provider.LoadCareers()[0].TreeVersion);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("tree_version") && s.Contains("warboss")));
    }

    [TestMethod]
    public void LoadCareers_TreeVersionBelowOne_WarnsAndUsesOne()
    {
        WriteCareerWithTreeVersion(@"tree_version=""0""");

        Assert.AreEqual(1, _provider.LoadCareers()[0].TreeVersion);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("tree_version") && s.Contains("warboss")));
    }
}
