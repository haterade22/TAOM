using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleBalance;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.BattleBalance;

[TestClass]
public class BattleBalanceSettingsProviderTests
{
    // TaomSettings.Instance is null in test (MCM v5 not loaded) → provider returns the
    // compiled defaults. These pin the no-MCM fallbacks so the ctor-cache refactor can't
    // silently change them. Same contract as NameplateFadeSettingsProviderTests.
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

    // PERF-04: GetDefaultTroopPower reads up to seven of these per troop per simulation round, so
    // no getter may walk MCM's settings lookup; the constructor takes the reference once.
    [TestMethod]
    public void Getters_NeverReadTaomSettingsInstance_TheConstructorDoes()
    {
        // MCM declares Instance on a generic base (GlobalSettings<T>), so match any declaring type
        // TaomSettings derives from rather than one class name.
        bool IsInstanceGetter(MethodBase m) =>
            m.Name == "get_Instance" && m.DeclaringType != null && m.DeclaringType.IsAssignableFrom(typeof(TaomSettings));

        var getters = typeof(BattleBalanceSettingsProvider)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.GetGetMethod())
            .ToList();
        Assert.AreEqual(12, getters.Count, "one getter per setting; update this count with the interface");

        var offenders = getters
            .Where(g => IlCallScanner.ExtractCalledMethods(g, g.GetMethodBody().GetILAsByteArray()).Any(IsInstanceGetter))
            .Select(g => g.Name)
            .ToList();
        CollectionAssert.AreEqual(new string[0], offenders, "getters must read the cached reference");

        var ctor = typeof(BattleBalanceSettingsProvider).GetConstructor(System.Type.EmptyTypes);
        Assert.IsTrue(IlCallScanner.ExtractCalledMethods(ctor, ctor.GetMethodBody().GetILAsByteArray()).Any(IsInstanceGetter),
            "the constructor takes the settings reference once");
    }
}
