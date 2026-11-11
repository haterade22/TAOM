using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.LostFooting;

namespace TAOM.Tests.Features.LostFooting;

/// <summary>The MCM side of the Lost Footing census: its default, the group, and plain-English text.</summary>
[TestClass]
public class LostFootingSettingsProviderTests
{
    private const string Group = "Battle Tactics/Lost Footing";

    [TestMethod]
    public void WithoutMcm_TheFallbackIsTheShippedDefault_WhichIsOn()
    {
        var provider = new LostFootingSettingsProvider();

        Assert.AreEqual(new TaomSettings().LostFootingCensus, provider.CensusEnabled);
        Assert.IsTrue(provider.CensusEnabled);
    }

    [TestMethod]
    public void TheToggle_PassesThrough_AndIsReadLive()
    {
        var s = new TaomSettings { LostFootingCensus = false };
        var provider = new LostFootingSettingsProvider(s);

        Assert.IsFalse(provider.CensusEnabled);
        s.LostFootingCensus = true;
        Assert.IsTrue(provider.CensusEnabled);
    }

    [TestMethod]
    public void TheRescue_IsOnByDefault_AndPassesThroughLive()
    {
        Assert.IsTrue(new LostFootingSettingsProvider().RescueEnabled, "Mike, 2026-11-10: the rescue runs in every battle");
        var s = new TaomSettings { LostFootingRescue = false };
        var provider = new LostFootingSettingsProvider(s);

        Assert.IsFalse(provider.RescueEnabled);
        s.LostFootingRescue = true;
        Assert.IsTrue(provider.RescueEnabled);
    }

    private static object? Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheGroup_IsOpenedByItsFirstProperty_WithAnOrderNoOtherGroupUses()
    {
        var first = typeof(TaomSettings).GetProperty(nameof(TaomSettings.LostFootingCensus))!;
        var group = Attribute(first, "SettingPropertyGroupAttribute")!;
        Assert.AreEqual(Group, Read(group, "GroupName"));
        var order = (int)Read(group, "GroupOrder");
        Assert.AreNotEqual(0, order, "MCM ignores a GroupOrder on any later property, so the first carries it");

        var others = typeof(TaomSettings).GetProperties()
            .Select(p => Attribute(p, "SettingPropertyGroupAttribute"))
            .Where(a => a != null && (string)Read(a, "GroupName") != Group && (int)Read(a, "GroupOrder") == order)
            .Select(a => (string)Read(a!, "GroupName"))
            .ToList();
        Assert.AreEqual(0, others.Count, "another group uses GroupOrder " + order + ": " + string.Join(", ", others));
    }

    [TestMethod]
    public void TheToggle_IsPlainEnglish_NeedsNoRestart_AndDefaultsOn()
    {
        var p = typeof(TaomSettings).GetProperty(nameof(TaomSettings.LostFootingCensus))!;
        var value = p.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == "SettingPropertyBoolAttribute");

        Assert.AreEqual(false, Read(value, "RequireRestart"));
        var hint = (string)Read(value, "HintText");
        var name = (string)Read(value, "DisplayName");
        Assert.IsFalse(hint.Contains("{=") || name.Contains("{="), "plain English, like its neighbours");
        Assert.IsFalse(hint.IndexOf((char)0x2014) >= 0 || hint.IndexOf((char)0x2013) >= 0, "no em or en dash");
        StringAssert.Contains(hint, "Default: on.");
        StringAssert.Contains(hint, "[LostFooting]");
        Assert.IsTrue(new TaomSettings().LostFootingCensus);
    }
}
