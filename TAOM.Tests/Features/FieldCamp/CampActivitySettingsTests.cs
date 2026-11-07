using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Domain;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The three camp work settings (#860): defaults, the validation of each (a stale json2 value falls back to the default, a NaN
/// included), the MCM ranges agreeing with the provider's, the player-local co-op class, and the saved fields of the record.
/// </summary>
[TestClass]
public class CampActivitySettingsTests
{
    [TestMethod]
    public void TheDefaults_AreTenHours_TenPerTenMen_AndACapOfThirty()
    {
        var sut = new CampSettingsProvider(new TaomSettings());

        Assert.AreEqual(10f, sut.CampActivityHours);
        Assert.AreEqual(10f, sut.CampActivityYieldPerTenMen);
        Assert.AreEqual(30, sut.CampActivityYieldCap);
    }

    [TestMethod]
    public void NoSettingsObject_FallsBackToTheDefaults()
    {
        var sut = new CampSettingsProvider();

        Assert.AreEqual(CampSettingsProvider.DefaultActivityHours, sut.CampActivityHours);
        Assert.AreEqual(CampSettingsProvider.DefaultActivityYieldPerTenMen, sut.CampActivityYieldPerTenMen);
        Assert.AreEqual(CampSettingsProvider.DefaultActivityYieldCap, sut.CampActivityYieldCap);
    }

    [DataTestMethod]
    [DataRow(1f, 1f)]
    [DataRow(48f, 48f)]
    [DataRow(24f, 24f)]
    [DataRow(0.5f, 10f)]
    [DataRow(48.5f, 10f)]
    [DataRow(-4f, 10f)]
    [DataRow(float.NaN, 10f)]
    [DataRow(float.PositiveInfinity, 10f)]
    [DataRow(float.NegativeInfinity, 10f)]
    public void Hours_AreValidated(float raw, float expected) =>
        Assert.AreEqual(expected, new CampSettingsProvider(new TaomSettings { CampActivityHours = raw }).CampActivityHours);

    [DataTestMethod]
    [DataRow(0f, 0f)]
    [DataRow(50f, 50f)]
    [DataRow(7.5f, 7.5f)]
    [DataRow(-0.1f, 10f)]
    [DataRow(50.1f, 10f)]
    [DataRow(float.NaN, 10f)]
    [DataRow(float.PositiveInfinity, 10f)]
    public void YieldPerTenMen_IsValidated(float raw, float expected) =>
        Assert.AreEqual(expected, new CampSettingsProvider(new TaomSettings { CampActivityYieldPerTenMen = raw }).CampActivityYieldPerTenMen);

    [DataTestMethod]
    [DataRow(0, 0)]
    [DataRow(200, 200)]
    [DataRow(45, 45)]
    [DataRow(-1, 30)]
    [DataRow(201, 30)]
    [DataRow(int.MaxValue, 30)]
    [DataRow(int.MinValue, 30)]
    public void YieldCap_IsValidated(int raw, int expected) =>
        Assert.AreEqual(expected, new CampSettingsProvider(new TaomSettings { CampActivityYieldCap = raw }).CampActivityYieldCap);

    // ---- Rest healing and start in camp (#860 B and C)

    [TestMethod]
    public void TheNewDefaults_AreFifteenAndNineteenAHealingDay_AndStartInCampOff()
    {
        var sut = new CampSettingsProvider(new TaomSettings());

        Assert.AreEqual(15f, sut.CampRestHealRegularsPerDay);
        Assert.AreEqual(19f, sut.CampRestHealHeroesPerDay);
        Assert.IsFalse(sut.CampStartInCamp);
        Assert.IsFalse(new CampSettingsProvider().CampStartInCamp);
    }

    [DataTestMethod]
    [DataRow(0f, 0f)]
    [DataRow(50f, 50f)]
    [DataRow(22.5f, 22.5f)]
    [DataRow(-0.5f, 15f)]
    [DataRow(50.5f, 15f)]
    [DataRow(float.NaN, 15f)]
    [DataRow(float.PositiveInfinity, 15f)]
    public void RegularsHealing_IsValidated(float raw, float expected) =>
        Assert.AreEqual(expected, new CampSettingsProvider(new TaomSettings { CampRestHealRegularsPerDay = raw }).CampRestHealRegularsPerDay);

    [DataTestMethod]
    [DataRow(0f, 0f)]
    [DataRow(50f, 50f)]
    [DataRow(30f, 30f)]
    [DataRow(-1f, 19f)]
    [DataRow(51f, 19f)]
    [DataRow(float.NaN, 19f)]
    [DataRow(float.NegativeInfinity, 19f)]
    public void HeroesHealing_IsValidated(float raw, float expected) =>
        Assert.AreEqual(expected, new CampSettingsProvider(new TaomSettings { CampRestHealHeroesPerDay = raw }).CampRestHealHeroesPerDay);

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.CampRestHealRegularsPerDay))]
    [DataRow(nameof(TaomSettings.CampRestHealHeroesPerDay))]
    public void TheHealingSliders_RangeZeroToFifty_InFieldCamps_AndTheHintSaysOnlyWithThePanelOpen(string property)
    {
        var attribute = Attribute(property, "SettingPropertyFloatingIntegerAttribute");

        Assert.AreEqual(0.0, System.Convert.ToDouble(Read(attribute, "MinValue")));
        Assert.AreEqual(50.0, System.Convert.ToDouble(Read(attribute, "MaxValue")));
        Assert.AreEqual("Field Camps", Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName"));
        var hint = (string)Read(attribute, "HintText");
        StringAssert.Contains(hint, "only while the camp panel is open (Camp Options, then Manage camp)");
        StringAssert.Contains(hint, "set both to 0 to turn rest healing off");
        StringAssert.Contains(hint, property.Contains("Regulars") ? "Extra wounded troops healed per day" : "Extra hit points");
    }

    [TestMethod]
    public void TheNewSettings_ArePlayerLocal()
    {
        foreach (var name in new[] { nameof(TaomSettings.CampRestHealRegularsPerDay), nameof(TaomSettings.CampRestHealHeroesPerDay), nameof(TaomSettings.CampStartInCamp) })
        {
            Assert.IsTrue(CoopSettingsRelevance.IsExcluded(name), name);
            Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(typeof(TaomSettings).GetProperty(name)!), name);
        }
    }

    [TestMethod]
    public void StartInCamp_ShipsOff_InFieldCamps()
    {
        Assert.IsFalse(new TaomSettings().CampStartInCamp);
        Assert.AreEqual("Field Camps", Read(Attribute(nameof(TaomSettings.CampStartInCamp), "SettingPropertyGroupAttribute"), "GroupName"));
    }

    private static object Attribute(string property, string typeName) =>
        typeof(TaomSettings).GetProperty(property)!.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.CampActivityHours), "SettingPropertyFloatingIntegerAttribute", 1.0, 48.0)]
    [DataRow(nameof(TaomSettings.CampActivityYieldPerTenMen), "SettingPropertyFloatingIntegerAttribute", 0.0, 50.0)]
    [DataRow(nameof(TaomSettings.CampActivityYieldCap), "SettingPropertyIntegerAttribute", 0.0, 200.0)]
    public void TheSliderRange_IsTheRangeTheProviderAccepts(string property, string attributeType, double min, double max)
    {
        var attribute = Attribute(property, attributeType);

        Assert.AreEqual(min, System.Convert.ToDouble(Read(attribute, "MinValue")));
        Assert.AreEqual(max, System.Convert.ToDouble(Read(attribute, "MaxValue")));
        Assert.AreEqual("Field Camps", Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName"));
    }

    [TestMethod]
    public void TheThreeSettings_ArePlayerLocal_NotSimulationRelevant()
    {
        foreach (var name in new[] { nameof(TaomSettings.CampActivityHours), nameof(TaomSettings.CampActivityYieldPerTenMen), nameof(TaomSettings.CampActivityYieldCap) })
        {
            Assert.IsTrue(CoopSettingsRelevance.IsExcluded(name), name + " must be on the player-local list");
            Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(typeof(TaomSettings).GetProperty(name)!), name);
        }
    }

    // ---- The saved fields

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void TheCampRecord_SavesTheFourWorkFields_UnderUniqueIds_ContinuingTheBlock()
    {
        var ids = typeof(CampState).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => (field: f.Name, attribute: f.GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == "SaveableFieldAttribute")))
            .Where(x => x.attribute != null)
            .ToDictionary(x => x.field, x => System.Convert.ToInt16(Read(x.attribute!, "LocalSaveId")));

        Assert.AreEqual(108, ids[nameof(CampState.Activity)]);
        Assert.AreEqual(109, ids[nameof(CampState.ActivityHoursDone)]);
        Assert.AreEqual(110, ids[nameof(CampState.ActivityTargetHours)]);
        Assert.AreEqual(111, ids[nameof(CampState.InjuryRolled)]);
        Assert.AreEqual(ids.Count, ids.Values.Distinct().Count(), "a save id used twice corrupts the record");
    }

    [TestMethod]
    public void AFreshRecord_AndAnOldSave_HaveNoWork()
    {
        var state = new CampState();

        Assert.AreEqual(0, state.Activity);
        Assert.AreEqual(0f, state.ActivityHoursDone);
        Assert.AreEqual(0f, state.ActivityTargetHours);
        Assert.IsFalse(state.InjuryRolled);
    }

    [TestMethod]
    public void ClearActivity_ZeroesAllFourFields_AndLeavesTheCampAlone()
    {
        var state = new CampState
        {
            TypeEnum = CampType.Fortified, Foraging = true, ForagedTotal = 7,
            Activity = 3, ActivityHoursDone = 4f, ActivityTargetHours = 10f, InjuryRolled = true,
        };

        state.ClearActivity();

        Assert.AreEqual(0, state.Activity);
        Assert.AreEqual(0f, state.ActivityHoursDone);
        Assert.AreEqual(0f, state.ActivityTargetHours);
        Assert.IsFalse(state.InjuryRolled);
        Assert.AreEqual(CampType.Fortified, state.TypeEnum);
        Assert.IsTrue(state.Foraging);
        Assert.AreEqual(7, state.ForagedTotal);
    }
}
