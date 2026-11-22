using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.ServerSettings;
using TAOM.Features.TrainingGrounds.Waves;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

[TestClass]
public class TrainingWavesSettingsProviderTests
{
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    private static PropertyInfo Switch => typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableTrainingWaves))!;

    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(TrainingWavesSettingsProvider.EnabledFrom(null));
        Assert.IsTrue(new TrainingWavesSettingsProvider().Enabled);
    }

    [TestMethod]
    public void TheSetting_DefaultsToTheProvidersConstant_WhichIsOn()
    {
        Assert.AreEqual(TrainingWavesSettingsProvider.DefaultEnabled, new TaomSettings().EnableTrainingWaves);
        Assert.IsTrue(TrainingWavesSettingsProvider.DefaultEnabled);
    }

    [TestMethod]
    public void TheProvider_ReadsTheSettingsObjectItWasGiven_LiveEachTime()
    {
        var settings = new TaomSettings();
        var provider = new TrainingWavesSettingsProvider(settings);

        Assert.IsTrue(provider.Enabled);
        settings.EnableTrainingWaves = false;
        Assert.IsFalse(provider.Enabled, "MCM edits its one settings object in place, so the provider must not copy the value");
        Assert.IsFalse(TrainingWavesSettingsProvider.EnabledFrom(settings));
    }

    [TestMethod]
    public void TheProvider_HasOnePublicParameterlessConstructor_AndOneInternalTestOne()
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        Assert.AreEqual(2, typeof(TrainingWavesSettingsProvider).GetConstructors(all).Length);
        Assert.AreEqual(1, typeof(TrainingWavesSettingsProvider).GetConstructors().Length);
        Assert.AreEqual(0, typeof(TrainingWavesSettingsProvider).GetConstructors()[0].GetParameters().Length);
    }

    [TestMethod]
    public void TheSwitch_SitsInTheTournamentsGroup_BesideTheOtherTrainingGroundsSwitches_NotNeedingARestart()
    {
        var group = Attribute(Switch, "SettingPropertyGroupAttribute");
        var boolAttribute = Attribute(Switch, "SettingPropertyBoolAttribute");
        var sibling = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableTidingsOfTheLists))!;

        Assert.AreEqual("Tournaments", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(Read(Attribute(sibling, "SettingPropertyGroupAttribute"), "GroupName"), Read(group, "GroupName"));
        Assert.AreEqual(false, Read(boolAttribute, "RequireRestart"));
        Assert.AreEqual("Wave Fights at the Training Grounds", McmText.Plain(Read(boolAttribute, "DisplayName") as string));
    }

    [TestMethod]
    public void TheHint_SaysWhatTheSwitchDoes_ThatItIsSinglePlayerOnly_AndItsDefault()
    {
        var hint = (string)Read(Attribute(Switch, "SettingPropertyBoolAttribute"), "HintText");

        StringAssert.Contains(hint, "wager");
        StringAssert.Contains(hint, "Single-player only");
        StringAssert.EndsWith(hint, "Default: " + (TrainingWavesSettingsProvider.DefaultEnabled ? "on." : "off."));
    }

    [TestMethod]
    public void TheHint_SaysTheTournamentBetCapAlsoLimitsAWaveWager_AndCanHideTheOffer()
    {
        var hint = (string)Read(Attribute(Switch, "SettingPropertyBoolAttribute"), "HintText");

        StringAssert.Contains(hint, "The tournament bet cap also limits a wave wager, and a very low cap hides the offer.");
    }
}
