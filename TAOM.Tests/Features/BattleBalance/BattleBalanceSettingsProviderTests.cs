using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleBalance;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BattleBalance;

[TestClass]
public class BattleBalanceSettingsProviderTests
{
    // TaomSettings.Instance is null in test: MCM is never initialised (BaseSettingsProvider.Instance
    // is set only in MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot), so the provider returns the
    // compiled defaults. These pin the no-MCM fallbacks so the caching refactor can't silently change
    // them. Same contract as NameplateFadeSettingsProviderTests.
    [TestMethod]
    public void EnableCustomTroopPower_NoMcm_DefaultsTrue()
        => Assert.IsTrue(new BattleBalanceSettingsProvider().EnableCustomTroopPower);

    [TestMethod]
    public void OverrideVanillaTierPower_NoMcm_DefaultsFalse()
        => Assert.IsFalse(new BattleBalanceSettingsProvider().OverrideVanillaTierPower);

    [TestMethod]
    public void Tier7Power_NoMcm_Defaults2_91()
        => Assert.AreEqual(2.91f, new BattleBalanceSettingsProvider().Tier7Power, 0.0001f);

    [TestMethod]
    public void Tier10Power_NoMcm_Defaults3_96()
        => Assert.AreEqual(3.96f, new BattleBalanceSettingsProvider().Tier10Power, 0.0001f);

    [TestMethod]
    public void HeroMultiplier_NoMcm_Defaults1_5()
        => Assert.AreEqual(1.5f, new BattleBalanceSettingsProvider().HeroMultiplier, 0.0001f);

    [TestMethod]
    public void MountedMultiplier_NoMcm_Defaults1_2()
        => Assert.AreEqual(1.2f, new BattleBalanceSettingsProvider().MountedMultiplier, 0.0001f);

    // All twelve fallbacks agree with the MCM compiled defaults, or the feature behaves one way
    // before TAOM.json is first written and another way after (the #559 pin shape).
    [TestMethod]
    public void EveryFallback_EqualsTheMcmCompiledDefault()
    {
        var provider = new BattleBalanceSettingsProvider();
        var mcm = new TaomSettings();
        foreach (var p in typeof(IBattleBalanceSettingsProvider).GetProperties())
            Assert.AreEqual(typeof(TaomSettings).GetProperty(p.Name).GetValue(mcm), p.GetValue(provider), p.Name);
    }

    // Read THROUGH, never snapshotted or cached on first read: MCM edits its one registered
    // TaomSettings in place, so a setting changed after the provider is built, and after every getter
    // has been read once, must reach its own getter and no other. One setting is edited per pass on a
    // fresh TaomSettings, because three bools share the default true and flipping them all together
    // cannot tell them apart: a getter wired to another setting fails the pass that edits either one,
    // and a getter that caches its first read fails the pass that edits its own setting.
    [TestMethod]
    public void Getters_ReadThroughTheSettings_SoLiveMcmEditsApply()
    {
        var props = typeof(IBattleBalanceSettingsProvider).GetProperties();
        foreach (var edited in props)
        {
            var mcm = new TaomSettings();
            var provider = new BattleBalanceSettingsProvider(mcm);
            foreach (var p in props)
                Assert.AreEqual(typeof(TaomSettings).GetProperty(p.Name).GetValue(mcm), p.GetValue(provider), p.Name + " before any edit");

            var setting = typeof(TaomSettings).GetProperty(edited.Name);
            setting.SetValue(mcm, setting.PropertyType == typeof(bool)
                ? !(bool)setting.GetValue(mcm)
                : (object)((float)setting.GetValue(mcm) + 1f));

            foreach (var p in props)
                Assert.AreEqual(typeof(TaomSettings).GetProperty(p.Name).GetValue(mcm), p.GetValue(provider), p.Name + " after editing " + edited.Name);
        }
    }

    // The internal test constructor must not break DryIoc's constructor selection: a second PUBLIC
    // constructor throws UnableToSelectSinglePublicConstructorFromMultiple at registration (IoC.cs:127,
    // the container build), before any resolve.
    [TestMethod]
    public void Provider_ResolvesFromARealContainer()
    {
        using var container = new Container();
        BattleBalanceIoC.RegisterBattleBalanceFeature(container);
        Assert.IsInstanceOfType(container.Resolve<IBattleBalanceSettingsProvider>(), typeof(BattleBalanceSettingsProvider));
    }

    // PERF-04: GetDefaultTroopPower reads up to seven of these per call, and the engine calls it per
    // casualty, twice per XP-scored hit and once per roster row in every strength sum, so no getter
    // may walk MCM's settings lookup. The reference is taken by the private Settings accessor on its
    // first non-null read, never in the constructor, so an early resolve cannot pin a null.
    [TestMethod]
    public void Getters_NeverReadTaomSettingsInstance_TheLazyAccessorDoes()
    {
        // MCM declares Instance on a generic base (GlobalSettings<T>), so match any declaring type
        // TaomSettings derives from rather than one class name.
        bool IsInstanceGetter(MethodBase m) =>
            m.Name == "get_Instance" && m.DeclaringType != null && m.DeclaringType.IsAssignableFrom(typeof(TaomSettings));
        bool CallsInstance(MethodBase m) =>
            IlCallScanner.ExtractCalledMethods(m, m.GetMethodBody().GetILAsByteArray()).Any(IsInstanceGetter);

        var getters = typeof(BattleBalanceSettingsProvider)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetGetMethod())
            .ToList();
        Assert.AreEqual(typeof(IBattleBalanceSettingsProvider).GetProperties().Length, getters.Count,
            "one public getter per interface member");

        var offenders = getters.Where(CallsInstance).Select(g => g.Name).ToList();
        Assert.AreEqual(0, offenders.Count, "getters must read the cached reference: " + string.Join(", ", offenders));

        foreach (var ctor in typeof(BattleBalanceSettingsProvider).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            Assert.IsFalse(CallsInstance(ctor), "a constructor read would pin a null if the provider is resolved before MCM is up");

        var accessor = typeof(BattleBalanceSettingsProvider)
            .GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance)?.GetGetMethod(true);
        Assert.IsNotNull(accessor, "the private lazy Settings accessor");
        Assert.IsTrue(CallsInstance(accessor), "the lazy accessor takes the settings reference");
    }
}
