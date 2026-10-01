using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleCorpses;

namespace TAOM.Tests.Features.BattleCorpses;

/// <summary>
/// The provider's fallbacks (MCM absent, a fresh install) must agree with the compiled MCM defaults and
/// the policy's own, and the MCM slider ranges must equal the policy's valid range: a slider that drifts
/// wider would let the player pick a value the policy silently replaces with its default.
/// </summary>
[TestClass]
public class BattleCorpseSettingsProviderTests
{
    [TestMethod]
    public void Fallbacks_MatchTheCompiledDefaults()
    {
        var provider = new BattleCorpseSettingsProvider();
        var compiled = new TaomSettings();

        Assert.AreEqual(compiled.EnableBattleCorpseCleanup, provider.IsCleanupEnabled);
        Assert.AreEqual(compiled.BattleCorpseFadeSeconds, provider.FadeSeconds);
        Assert.AreEqual(compiled.BattleCorpseCap, provider.CorpseCap);
        Assert.AreEqual(compiled.ShowBattleSettingsAdvice, provider.IsAdviceEnabled);
    }

    [TestMethod]
    public void CompiledDefaults_AreMikesValues()
    {
        var compiled = new TaomSettings();

        Assert.IsTrue(compiled.EnableBattleCorpseCleanup);
        Assert.AreEqual(BattleCorpsePolicy.DefaultFadeSeconds, compiled.BattleCorpseFadeSeconds);
        Assert.AreEqual(BattleCorpsePolicy.DefaultCorpseCap, compiled.BattleCorpseCap);
        Assert.AreEqual(60f, BattleCorpsePolicy.DefaultFadeSeconds);
        Assert.AreEqual(25, BattleCorpsePolicy.DefaultCorpseCap);
        Assert.IsTrue(compiled.ShowBattleSettingsAdvice);
    }

    [TestMethod]
    public void FadeSlider_SpansExactlyThePolicyRange()
    {
        var (min, max) = SliderRange(nameof(TaomSettings.BattleCorpseFadeSeconds));

        Assert.AreEqual((double)BattleCorpsePolicy.MinFadeSeconds, min);
        Assert.AreEqual((double)BattleCorpsePolicy.MaxFadeSeconds, max);
    }

    [TestMethod]
    public void CapSlider_SpansExactlyThePolicyRange()
    {
        var (min, max) = SliderRange(nameof(TaomSettings.BattleCorpseCap));

        Assert.AreEqual((double)BattleCorpsePolicy.MinCorpseCap, min);
        Assert.AreEqual((double)BattleCorpsePolicy.MaxCorpseCap, max);
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
