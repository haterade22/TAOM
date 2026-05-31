using System.IO;
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
        Assert.IsNotNull(root.Passive);
        Assert.AreEqual(PassiveEffectType.TroopDamage, root.Passive.EffectType);
        Assert.AreEqual(0.05f, root.Passive.Magnitude, 0.001f);
        Assert.IsTrue(root.Passive.IsPercentage);
        Assert.AreEqual(1, root.Mutations.Count);
        Assert.AreEqual("skill_scaling", root.Mutations[0].CalculatorId);
        Assert.AreEqual("Leadership", root.Mutations[0].Params["skill"]);

        Assert.AreEqual(1, groups.Count);
        Assert.AreEqual("wb_brutality", groups[0].Id);
        Assert.AreEqual("warboss", groups[0].CareerId);
        Assert.AreEqual(1, groups[0].Tier);
        Assert.AreEqual(2, groups[0].ChoiceIds.Count);
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
        Assert.AreEqual(AttackTypeMask.Melee, choices[0].Passive.AttackTypeMask);
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
        Assert.IsNotNull(choices[0].Passive, "Nested <PassiveEffects><PassiveEffect/> must be read");
        Assert.AreEqual(PassiveEffectType.TroopDamage, choices[0].Passive.EffectType);
        Assert.AreEqual(0.10f, choices[0].Passive.Magnitude, 0.001f);
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
        Assert.AreEqual(0.10f, choices[0].Passive.Magnitude, 0.001f, "value= must alias magnitude=");
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
        Assert.AreEqual(4f, choices[0].Passive.Magnitude, 0.001f, "magnitude= takes precedence over value=");
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
        Assert.AreEqual(PassiveEffectType.PartySize, choices[0].Passive.EffectType);
        Assert.AreEqual(2f, choices[0].Passive.Magnitude, 0.001f);
    }

    [TestMethod]
    public void LoadChoices_EmptyPassiveEffectsWrapper_YieldsNullPassiveNoThrow()
    {
        // Defensive: a wrapper with no <PassiveEffect> child (or a foreign child) must parse to a
        // null passive without throwing — the choice is still valid, it just grants no passive.
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
        Assert.IsNull(choices[0].Passive);
        Assert.IsNull(choices[1].Passive);
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
}
