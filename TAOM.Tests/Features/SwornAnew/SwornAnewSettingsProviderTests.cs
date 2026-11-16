using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
using TAOM.Features;
using TAOM.Features.SwornAnew;

namespace TAOM.Tests.Features.SwornAnew;

/// <summary>
/// The one MCM switch. The default is on and persisted by MCM per property, so the compiled default and the fallback before MCM
/// is up must be the same constant: flipping it later would not reach an existing TAOM.json (csharp-architecture.md, MCM defaults).
/// </summary>
[TestClass]
public class SwornAnewSettingsProviderTests
{
    [TestMethod]
    public void From_NoSettingsYet_IsTheCompiledDefault_On()
    {
        Assert.IsTrue(SwornAnewSettingsProvider.From(null));
        Assert.AreEqual(SwornAnewSettingsProvider.DefaultEnabled, SwornAnewSettingsProvider.From(null));
        Assert.IsTrue(SwornAnewSettingsProvider.DefaultEnabled);
    }

    [TestMethod]
    public void From_TheMcmDefault_MatchesTheNoSettingsFallback()
    {
        Assert.AreEqual(SwornAnewSettingsProvider.From(null), SwornAnewSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_TheSwitchOff_IsOff()
    {
        Assert.IsFalse(SwornAnewSettingsProvider.From(new TaomSettings { SwornAnewEnabled = false }));
    }

    [TestMethod]
    public void Enabled_ReadsTheSettingsItWasGiven_LiveAfterTheFirstRead()
    {
        var settings = new TaomSettings { SwornAnewEnabled = true };
        var provider = new SwornAnewSettingsProvider(settings);

        Assert.IsTrue(provider.Enabled);
        settings.SwornAnewEnabled = false;
        Assert.IsFalse(provider.Enabled, "MCM edits its one settings object in place, so a change applies at once");
    }

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheProperty_SitsInItsWorldGroupAtAnUnusedOrder_AppliesWithoutARestart_AndSaysWhatItGates()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.SwornAnewEnabled))!;

        var group = Attribute(property, "SettingPropertyGroupAttribute");
        Assert.AreEqual("World/Sworn Anew", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(71, Read(group, "GroupOrder"));
        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual("Enable Sworn Anew", McmText.Plain(Read(value, "DisplayName") as string));
        Assert.AreEqual(false, Read(value, "RequireRestart"), "read at each conversation question");
        StringAssert.Contains((string)Read(value, "HintText"), "captured");
    }

    [TestMethod]
    public void NoOtherSettingsGroup_UsesGroupOrder71()
    {
        var holders = typeof(TaomSettings).GetProperties()
            .Where(p => p.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "SettingPropertyGroupAttribute"
                && (int)Read(a, "GroupOrder") == 71))
            .Select(p => p.Name)
            .ToList();

        CollectionAssert.AreEqual(new[] { nameof(TaomSettings.SwornAnewEnabled) }, holders);
    }
}
