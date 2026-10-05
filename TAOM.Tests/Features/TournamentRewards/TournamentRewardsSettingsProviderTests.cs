using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.TournamentRewards;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// The provider's fallbacks (MCM absent, a fresh install) must agree with the compiled MCM defaults, the MCM
/// slider ranges must equal what the rules accept, and a value outside the slider's range (MCM's slider clamps,
/// its settings file loader does not) must be pulled back before it reaches the rules' int casts.
/// </summary>
[TestClass]
public class TournamentRewardsSettingsProviderTests
{
    // --- Fallbacks and compiled defaults ---

    [TestMethod]
    public void Fallbacks_MatchTheCompiledDefaults()
    {
        var provider = new TournamentRewardsSettingsProvider();
        var compiled = new TaomSettings();

        Assert.AreEqual(compiled.TournamentMaxBetPerRound, provider.MaxBetPerRound);
        Assert.AreEqual(compiled.TournamentRenownMultiplier, provider.RenownMultiplier);
        Assert.AreEqual(compiled.TournamentInfluenceMultiplier, provider.InfluenceMultiplier);
    }

    [TestMethod]
    public void CompiledDefaults_AreUnlimitedBetsAndTheDesignedMultipliers()
    {
        var compiled = new TaomSettings();

        Assert.AreEqual(0, compiled.TournamentMaxBetPerRound, "0 = unlimited");
        Assert.AreEqual(1.0f, compiled.TournamentRenownMultiplier);
        Assert.AreEqual(1.0f, compiled.TournamentInfluenceMultiplier);
    }

    // --- Slider ranges equal what the rules accept ---

    [TestMethod]
    public void RenownSlider_SpansExactlyTheRulesRange()
    {
        var (min, max) = SliderRange(nameof(TaomSettings.TournamentRenownMultiplier));

        Assert.AreEqual(0.0, min);
        Assert.AreEqual((double)TournamentRewardRules.MaxMultiplier, max);
    }

    [TestMethod]
    public void InfluenceSlider_SpansExactlyTheRulesRange()
    {
        var (min, max) = SliderRange(nameof(TaomSettings.TournamentInfluenceMultiplier));

        Assert.AreEqual(0.0, min);
        Assert.AreEqual((double)TournamentRewardRules.MaxMultiplier, max);
    }

    [TestMethod]
    public void MaxBetSlider_SpansExactlyTheProvidersClamp()
    {
        var (min, max) = SliderRange(nameof(TaomSettings.TournamentMaxBetPerRound));

        Assert.AreEqual(0.0, min);
        Assert.AreEqual((double)TournamentRewardRules.MaxBetSetting, max);
    }

    // --- A value outside the slider's range is pulled back (MCM's JSON loader does not clamp) ---

    [DataTestMethod]
    [DataRow(50f, TournamentRewardRules.MaxMultiplier)]
    [DataRow(1e9f, TournamentRewardRules.MaxMultiplier)]
    [DataRow(-2f, 0f)]
    [DataRow(2.5f, 2.5f)]
    [DataRow(float.NaN, 1f)]
    [DataRow(float.PositiveInfinity, 1f)]
    public void RenownMultiplier_McmValueOutsideTheSlider_IsClamped(float mcm, float expected)
    {
        var provider = new TournamentRewardsSettingsProvider(new TaomSettings { TournamentRenownMultiplier = mcm });

        Assert.AreEqual(expected, provider.RenownMultiplier);
    }

    [DataTestMethod]
    [DataRow(50f, TournamentRewardRules.MaxMultiplier)]
    [DataRow(1e9f, TournamentRewardRules.MaxMultiplier)]
    [DataRow(-2f, 0f)]
    [DataRow(2.5f, 2.5f)]
    [DataRow(float.NaN, 1f)]
    [DataRow(float.PositiveInfinity, 1f)]
    public void InfluenceMultiplier_McmValueOutsideTheSlider_IsClamped(float mcm, float expected)
    {
        var provider = new TournamentRewardsSettingsProvider(new TaomSettings { TournamentInfluenceMultiplier = mcm });

        Assert.AreEqual(expected, provider.InfluenceMultiplier);
    }

    [DataTestMethod]
    [DataRow(int.MaxValue, TournamentRewardRules.MaxBetSetting)]
    [DataRow(-5, 0)]
    [DataRow(500, 500)]
    public void MaxBetPerRound_McmValueOutsideTheSlider_IsClamped(int mcm, int expected)
    {
        var provider = new TournamentRewardsSettingsProvider(new TaomSettings { TournamentMaxBetPerRound = mcm });

        Assert.AreEqual(expected, provider.MaxBetPerRound);
    }

    [TestMethod]
    public void Provider_ReadsThroughTheSettingsObject()
    {
        // MCM edits its one registered TaomSettings in place, so an edit after the provider is built must show.
        var mcm = new TaomSettings();
        var provider = new TournamentRewardsSettingsProvider(mcm);
        Assert.AreEqual(1f, provider.RenownMultiplier);

        mcm.TournamentRenownMultiplier = 3f;

        Assert.AreEqual(3f, provider.RenownMultiplier);
    }

    // MCM's slider attributes take (displayName, minValue, maxValue, ...) as constructor arguments.
    private static (double Min, double Max) SliderRange(string property)
    {
        var slider = typeof(TaomSettings).GetProperty(property, BindingFlags.Public | BindingFlags.Instance)!
            .GetCustomAttributesData()
            .Single(a => a.AttributeType.Name is "SettingPropertyFloatingIntegerAttribute" or "SettingPropertyIntegerAttribute");
        return (System.Convert.ToDouble(slider.ConstructorArguments[1].Value),
                System.Convert.ToDouble(slider.ConstructorArguments[2].Value));
    }
}
