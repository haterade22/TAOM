using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.SeasonedHosts;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeasonedHosts;

/// <summary>
/// The "World/Seasoned Hosts" group is user-editable and MCM deserializes a hand-edited json verbatim, so every value is
/// validated here: a non-finite float reverts to the compiled default and the rest clamp to the slider range.
/// </summary>
[TestClass]
public class SeasonedHostsSettingsProviderTests
{
    private static SeasonedHostsSettingsProvider With(TaomSettings settings) => new(settings);

    [TestMethod]
    public void Properties_NoSettingsYet_AreTheCompiledDefaults()
    {
        var sut = new SeasonedHostsSettingsProvider();

        Assert.AreEqual(0.25f, sut.TrainingBonus);
        Assert.AreEqual(1.25f, sut.SkillXpMultiplier);
        Assert.IsTrue(sut.LearningNeverStops);
        Assert.IsFalse(sut.NoStewardXp);
    }

    [TestMethod]
    public void Properties_TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        var fallback = new SeasonedHostsSettingsProvider();
        var sut = With(new TaomSettings());

        Assert.AreEqual(fallback.TrainingBonus, sut.TrainingBonus);
        Assert.AreEqual(fallback.SkillXpMultiplier, sut.SkillXpMultiplier);
        Assert.AreEqual(fallback.LearningNeverStops, sut.LearningNeverStops);
        Assert.AreEqual(fallback.NoStewardXp, sut.NoStewardXp);
    }

    [TestMethod]
    public void Properties_ValuesInRange_PassThrough()
    {
        var sut = With(new TaomSettings
        {
            SeasonedHostsTrainingBonus = 0.6f,
            SeasonedHostsSkillXpMultiplier = 1.5f,
            SeasonedHostsLearningNeverStops = false,
            SeasonedHostsNoStewardXp = true,
        });

        Assert.AreEqual(0.6f, sut.TrainingBonus);
        Assert.AreEqual(1.5f, sut.SkillXpMultiplier);
        Assert.IsFalse(sut.LearningNeverStops);
        Assert.IsTrue(sut.NoStewardXp);
    }

    [DataTestMethod]
    [DataRow(-0.5f, 0f)]
    [DataRow(float.MinValue, 0f)]
    [DataRow(1.5f, 1f)]
    [DataRow(float.MaxValue, 1f)]
    public void TrainingBonus_OutOfRange_ClampsToTheSliderRange(float configured, float expected) =>
        Assert.AreEqual(expected, With(new TaomSettings { SeasonedHostsTrainingBonus = configured }).TrainingBonus);

    [DataTestMethod]
    [DataRow(0.5f, 1f)]
    [DataRow(-3f, 1f)]
    [DataRow(float.MinValue, 1f)]
    [DataRow(2.5f, 2f)]
    [DataRow(float.MaxValue, 2f)]
    public void SkillXpMultiplier_OutOfRange_ClampsToTheSliderRangeAndNeverBelowOne(float configured, float expected) =>
        Assert.AreEqual(expected, With(new TaomSettings { SeasonedHostsSkillXpMultiplier = configured }).SkillXpMultiplier);

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void TrainingBonus_NonFinite_RevertsToTheCompiledDefault(float configured) =>
        Assert.AreEqual(SeasonedHostsSettingsProvider.DefaultTrainingBonus, With(new TaomSettings { SeasonedHostsTrainingBonus = configured }).TrainingBonus);

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void SkillXpMultiplier_NonFinite_RevertsToTheCompiledDefault(float configured) =>
        Assert.AreEqual(SeasonedHostsSettingsProvider.DefaultSkillXpMultiplier, With(new TaomSettings { SeasonedHostsSkillXpMultiplier = configured }).SkillXpMultiplier);

    [TestMethod]
    public void TrainingBonusAndSkillXpMultiplier_ASliderStep_AreReadAsTheHundredthItShows()
    {
        var sut = With(new TaomSettings { SeasonedHostsTrainingBonus = 0.2999999f, SeasonedHostsSkillXpMultiplier = 1.3499999f });

        Assert.AreEqual(0.3f, sut.TrainingBonus);
        Assert.AreEqual(1.35f, sut.SkillXpMultiplier);
    }

    [TestMethod]
    public void TaomSettings_DeclaresTheGroupOrderOnce()
    {
        var source = RepoPaths.ReadSource("Main/Features/TaomSettings.cs", stripComments: true);

        Assert.IsTrue(source.Contains("[SettingPropertyGroup(\"{=taom_mcm_group_world}World/{=taom_mcm_group_seasoned_hosts}Seasoned Hosts\", GroupOrder = 73)]"));
        Assert.AreEqual(1, source.Split(new[] { "GroupOrder = 73" }, System.StringSplitOptions.None).Length - 1,
            "GroupOrder 73 is used by another group");
    }

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.SeasonedHostsTrainingBonus), "SettingPropertyFloatingIntegerAttribute", "taom_seasoned_hosts_training_bonus")]
    [DataRow(nameof(TaomSettings.SeasonedHostsSkillXpMultiplier), "SettingPropertyFloatingIntegerAttribute", "taom_seasoned_hosts_skill_xp")]
    [DataRow(nameof(TaomSettings.SeasonedHostsLearningNeverStops), "SettingPropertyBoolAttribute", "taom_seasoned_hosts_learning_floor")]
    [DataRow(nameof(TaomSettings.SeasonedHostsNoStewardXp), "SettingPropertyBoolAttribute", "taom_seasoned_hosts_no_steward_xp")]
    public void EachProperty_SitsInTheSeasonedHostsGroupAppliesWithoutARestartAndCarriesKeyedText(string name, string kind, string key)
    {
        var property = typeof(TaomSettings).GetProperty(name)!;

        Assert.AreEqual("World/Seasoned Hosts", McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName") as string));
        var value = Attribute(property, kind);
        Assert.AreEqual(false, Read(value, "RequireRestart"));
        StringAssert.StartsWith((string)Read(value, "DisplayName"), "{=" + key + "}");
        StringAssert.StartsWith((string)Read(value, "HintText"), "{=" + key + "_hint}");
    }

    [TestMethod]
    public void Sliders_MatchTheProviderRanges()
    {
        var bonus = Attribute(typeof(TaomSettings).GetProperty(nameof(TaomSettings.SeasonedHostsTrainingBonus))!, "SettingPropertyFloatingIntegerAttribute");
        var xp = Attribute(typeof(TaomSettings).GetProperty(nameof(TaomSettings.SeasonedHostsSkillXpMultiplier))!, "SettingPropertyFloatingIntegerAttribute");

        Assert.AreEqual((double)SeasonedHostsSettingsProvider.MinTrainingBonus, System.Convert.ToDouble(Read(bonus, "MinValue")));
        Assert.AreEqual((double)SeasonedHostsSettingsProvider.MaxTrainingBonus, System.Convert.ToDouble(Read(bonus, "MaxValue")));
        Assert.AreEqual((double)SeasonedHostsSettingsProvider.MinSkillXpMultiplier, System.Convert.ToDouble(Read(xp, "MinValue")));
        Assert.AreEqual((double)SeasonedHostsSettingsProvider.MaxSkillXpMultiplier, System.Convert.ToDouble(Read(xp, "MaxValue")));
    }
}
