using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.FellFoes;
using TAOM.Features.FellFoes.Domain;

namespace TAOM.Tests.Features.FellFoes;

/// <summary>The MCM side of Fell Foes: defaults, validation of stale values, and slider ranges that match the tunables.</summary>
[TestClass]
public class FellFoesSettingsProviderTests
{
    private static FellFoesSettingsProvider With(TaomSettings s) => new FellFoesSettingsProvider(s);

    [TestMethod]
    public void WithoutMcm_TheFallbacksAreTheShippedDefaults()
    {
        var provider = new FellFoesSettingsProvider();
        var compiled = new TaomSettings();

        Assert.AreEqual(compiled.FellFoesCorneredRouters, provider.CorneredRoutersEnabled);
        Assert.AreEqual(compiled.FellFoesArchersTargetPlayer, provider.ArchersTargetPlayerEnabled);
        Assert.AreEqual(compiled.FellFoesStragglersLeave, provider.StragglersLeaveFieldEnabled);
        Assert.AreEqual(compiled.FellFoesDebug, provider.DebugEnabled);
        Assert.AreEqual(0, provider.Tunables.Warnings.Count);
    }

    [TestMethod]
    public void CompiledDefaults_AreOnExceptDebug_AndTheNumbersAreTheTunableDefaults()
    {
        var s = new TaomSettings();

        Assert.IsTrue(s.FellFoesCorneredRouters);
        Assert.IsTrue(s.FellFoesArchersTargetPlayer);
        Assert.IsTrue(s.FellFoesStragglersLeave);
        Assert.IsFalse(s.FellFoesDebug);
        var t = With(s).Tunables;
        Assert.AreEqual(0, t.Warnings.Count);
        Assert.AreEqual(FellFoesTunables.DefaultCorneredMaxFoes, t.CorneredMaxFoes);
        Assert.AreEqual(FellFoesTunables.DefaultArcherChance, t.ArcherChance);
        Assert.AreEqual(FellFoesTunables.DefaultArcherMaxHolding, t.ArcherMaxHolding);
        Assert.AreEqual(FellFoesTunables.DefaultStragglerMaxCount, t.StragglerMaxCount);
        Assert.AreEqual(FellFoesTunables.DefaultStragglerHoldSeconds, t.StragglerHoldSeconds);
    }

    [TestMethod]
    public void ToggleValues_PassThrough()
    {
        var s = new TaomSettings
        {
            FellFoesCorneredRouters = false, FellFoesArchersTargetPlayer = false, FellFoesStragglersLeave = false,
            FellFoesDebug = true,
        };
        var provider = With(s);

        Assert.IsFalse(provider.CorneredRoutersEnabled);
        Assert.IsFalse(provider.ArchersTargetPlayerEnabled);
        Assert.IsFalse(provider.StragglersLeaveFieldEnabled);
        Assert.IsTrue(provider.DebugEnabled);
    }

    [TestMethod]
    public void AStaleNaNOrOutOfRangeNumber_RevertsToItsDefault_WithAWarning()
    {
        var s = new TaomSettings
        {
            FellFoesArcherChance = float.NaN, FellFoesStragglerHoldSeconds = float.PositiveInfinity,
            FellFoesCorneredMaxFoes = 99, FellFoesArcherMaxHolding = -4, FellFoesStragglerMaxCount = 0,
        };

        var t = With(s).Tunables;

        Assert.AreEqual(FellFoesTunables.DefaultArcherChance, t.ArcherChance);
        Assert.AreEqual(FellFoesTunables.DefaultStragglerHoldSeconds, t.StragglerHoldSeconds);
        Assert.AreEqual(FellFoesTunables.DefaultCorneredMaxFoes, t.CorneredMaxFoes);
        Assert.AreEqual(FellFoesTunables.DefaultArcherMaxHolding, t.ArcherMaxHolding);
        Assert.AreEqual(FellFoesTunables.DefaultStragglerMaxCount, t.StragglerMaxCount);
        Assert.AreEqual(5, t.Warnings.Count);
    }

    [TestMethod]
    public void TheNumbers_AreReadLive_SoAnMcmEditApplies()
    {
        var s = new TaomSettings();
        var provider = With(s);

        s.FellFoesCorneredMaxFoes = 5;

        Assert.AreEqual(5, provider.Tunables.CorneredMaxFoes);
    }

    // MCM reads these attributes by name; MCMv5 is runtime-only here, so the tests do too.
    private static object? Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    private static (double Min, double Max) Range(string property)
    {
        var p = typeof(TaomSettings).GetProperty(property)!;
        var attr = p.GetCustomAttributes(inherit: false)
            .Single(a => a.GetType().Name is "SettingPropertyIntegerAttribute" or "SettingPropertyFloatingIntegerAttribute");
        return (Convert.ToDouble(Read(attr, "MinValue")), Convert.ToDouble(Read(attr, "MaxValue")));
    }

    [TestMethod]
    public void EverySliderEnd_IsAcceptedByTheTunables_WithoutAWarning()
    {
        foreach (var pick in new[] { 0, 1 })
        {
            double V(string p) => pick == 0 ? Range(p).Min : Range(p).Max;
            var s = new TaomSettings
            {
                FellFoesCorneredMaxFoes = (int)V(nameof(TaomSettings.FellFoesCorneredMaxFoes)),
                FellFoesArcherChance = (float)V(nameof(TaomSettings.FellFoesArcherChance)),
                FellFoesArcherMaxHolding = (int)V(nameof(TaomSettings.FellFoesArcherMaxHolding)),
                FellFoesStragglerMaxCount = (int)V(nameof(TaomSettings.FellFoesStragglerMaxCount)),
                FellFoesStragglerHoldSeconds = (float)V(nameof(TaomSettings.FellFoesStragglerHoldSeconds)),
            };

            Assert.AreEqual(0, With(s).Tunables.Warnings.Count, "slider end " + pick);
        }
    }

    [TestMethod]
    public void TheGroup_IsOpenedByItsFirstProperty_WithAnOrderNoOtherGroupUses()
    {
        var first = typeof(TaomSettings).GetProperty(nameof(TaomSettings.FellFoesCorneredRouters))!;
        var group = Attribute(first, "SettingPropertyGroupAttribute")!;
        Assert.AreEqual("Battle Tactics/Fell Foes", McmText.Plain(Read(group, "GroupName") as string));
        var order = (int)Read(group, "GroupOrder");
        Assert.AreNotEqual(0, order, "MCM ignores a GroupOrder on any later property, so the first carries it");

        var others = typeof(TaomSettings).GetProperties()
            .Select(p => Attribute(p, "SettingPropertyGroupAttribute"))
            .Where(a => a != null && McmText.Plain(Read(a, "GroupName") as string) != "Battle Tactics/Fell Foes" && (int)Read(a, "GroupOrder") == order)
            .Select(a => McmText.Plain(Read(a!, "GroupName") as string))
            .ToList();
        Assert.AreEqual(0, others.Count, "another group uses GroupOrder " + order + ": " + string.Join(", ", others));
    }

    [TestMethod]
    public void EveryFellFoesProperty_SitsInTheGroup_AndNeedsNoRestart()
    {
        var mine = typeof(TaomSettings).GetProperties().Where(p => p.Name.StartsWith("FellFoes", StringComparison.Ordinal)).ToList();

        Assert.AreEqual(9, mine.Count);
        foreach (var p in mine)
        {
            Assert.AreEqual("Battle Tactics/Fell Foes", McmText.Plain(Read(Attribute(p, "SettingPropertyGroupAttribute")!, "GroupName") as string), p.Name);
            var value = p.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name.StartsWith("SettingProperty", StringComparison.Ordinal)
                                                                      && !a.GetType().Name.StartsWith("SettingPropertyGroup", StringComparison.Ordinal));
            Assert.AreEqual(false, Read(value, "RequireRestart"), p.Name);
            var hint = (string)Read(value, "HintText");
            Assert.IsTrue(hint.StartsWith("{=taom_mcm_") && ((string)Read(value, "DisplayName")).StartsWith("{=taom_mcm_"), p.Name + ": keyed, like its neighbours (#1385)");
            Assert.IsFalse(hint.IndexOf((char)0x2014) >= 0 || hint.IndexOf((char)0x2013) >= 0, p.Name + ": no em or en dash");
        }
    }
}
