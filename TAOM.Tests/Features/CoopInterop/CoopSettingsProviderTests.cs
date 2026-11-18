using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.ServerSettings;

// The Co-op settings group: the switch for the load order warning that TAOM.CoopCompat gives a joining player, which
// the co-op server process reads at each hello; a dedicated server needs a restart.

namespace TAOM.Tests.Features.CoopInterop;

[TestClass]
public class CoopSettingsProviderTests
{
    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void LoadOrderWarning_NoSettingsObject_IsOn()
    {
        Assert.IsTrue(new CoopSettingsProvider(null).LoadOrderWarning);
    }

    [TestMethod]
    public void LoadOrderWarning_FreshSettingsObject_IsOn()
    {
        Assert.IsTrue(new TaomSettings().CoopLoadOrderWarning);
        Assert.IsTrue(new CoopSettingsProvider(new TaomSettings()).LoadOrderWarning);
    }

    [TestMethod]
    public void LoadOrderWarning_ReadsTheSettingsObjectLive()
    {
        var settings = new TaomSettings();
        var provider = new CoopSettingsProvider(settings);

        settings.CoopLoadOrderWarning = false;
        Assert.IsFalse(provider.LoadOrderWarning, "MCM edits its one settings object in place, so the provider must not copy the value");
    }

    [TestMethod]
    public void TheSetting_SitsInTheCoopGroup_AtOrder8_AsABool_NotNeedingARestart()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.CoopLoadOrderWarning))!;
        var group = Attribute(property, "SettingPropertyGroupAttribute");
        var flag = Attribute(property, "SettingPropertyBoolAttribute");

        Assert.AreEqual("Co-op", McmText.Plain(Read(group, "GroupName") as string));
        Assert.AreEqual(8, Read(group, "GroupOrder"));
        Assert.AreEqual("Warn players whose load order differs from the host's", McmText.Plain(Read(flag, "DisplayName") as string));
        Assert.AreEqual(false, Read(flag, "RequireRestart"));
    }

    [TestMethod]
    public void TheHint_SaysTheServerReadsIt_FromItsFile_AfterARestart()
    {
        // TAOM.CoopCompat reads the switch in the co-op server process; a dedicated server loads its settings from
        // taom-server-settings.json once per process (ServerSettingsSource), so the hosting player's menu is not what counts.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.CoopLoadOrderWarning))!;
        var hint = McmText.Plain(Read(Attribute(property, "SettingPropertyBoolAttribute"), "HintText") as string);

        StringAssert.Contains(hint, "The co-op server process reads this switch each time a player joins");
        StringAssert.Contains(hint, "a player's own value is not read");
        StringAssert.Contains(hint, "taom-server-settings.json (CoopLoadOrderWarning)");
        StringAssert.Contains(hint, "restart the server");
        Assert.IsFalse(hint.Contains("no restart"), hint);
    }

    [TestMethod]
    public void NoOtherGroupTakesOrder8()
    {
        // Later co-op switches join this group; list them here as they arrive.
        var holders = typeof(TaomSettings).GetProperties()
            .Where(p => p.GetCustomAttributes(inherit: false).Any(a => a.GetType().Name == "SettingPropertyGroupAttribute" && (int)Read(a, "GroupOrder") == 8))
            .Select(p => p.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { nameof(TaomSettings.CoopLoadOrderWarning) }, holders);
    }

    [TestMethod]
    public void TheLoadOrderSwitch_IsExcludedFromTheFingerprint()
        => Assert.IsTrue(CoopSettingsRelevance.IsExcluded(nameof(TaomSettings.CoopLoadOrderWarning)));
}
