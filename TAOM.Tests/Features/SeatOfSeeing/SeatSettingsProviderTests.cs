using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The MCM values behind the Seat (build-spec 5.9). The lazy accessor and the keep-and-read-through behaviour are pinned for every
/// provider by HotPathSettingsProvidersTests; these pin the defaults and that a saved value is read.
/// </summary>
[TestClass]
public class SeatSettingsProviderTests
{
    [TestMethod]
    public void BeforeMcmIsUp_TheSeatIsOn_AndBattlesBeginAtTheHero()
    {
        // The provider's public constructor asks MCM, which is not up in a unit-test process.
        var sut = new SeatSettingsProvider();

        Assert.IsTrue(sut.Enabled);
        Assert.IsFalse(sut.BeginBattlesInSeat);
        Assert.IsTrue(sut.CursorWithPanel, "the pointer is the point of stage S2");
        Assert.IsFalse(sut.KeepPanelOpen, "a player who never asks sees vanilla behaviour");
    }

    [TestMethod]
    public void TheFallbackDefaults_AreTheMcmPropertyDefaults()
    {
        var settings = new TaomSettings();

        Assert.AreEqual(SeatSettingsProvider.DefaultEnabled, settings.SeatEnabled);
        Assert.AreEqual(SeatSettingsProvider.DefaultBeginBattlesInSeat, settings.BeginBattlesInSeat);
        Assert.AreEqual(SeatSettingsProvider.DefaultCursorWithPanel, settings.SeatCursorWithPanel);
        Assert.AreEqual(SeatSettingsProvider.DefaultKeepPanelOpen, settings.SeatKeepPanelOpen);
        Assert.IsTrue(SeatSettingsProvider.DefaultCursorWithPanel);
        Assert.IsFalse(SeatSettingsProvider.DefaultKeepPanelOpen);
        Assert.IsTrue(SeatSettingsProvider.DefaultEnabled);
        Assert.IsFalse(SeatSettingsProvider.DefaultBeginBattlesInSeat, "a player who never asks sees vanilla behaviour (build-spec section 1)");
    }

    [TestMethod]
    public void SavedValues_AreRead()
    {
        var settings = new TaomSettings { SeatEnabled = false, BeginBattlesInSeat = true, SeatCursorWithPanel = false, SeatKeepPanelOpen = true };

        var sut = new SeatSettingsProvider(settings);

        Assert.IsFalse(sut.Enabled);
        Assert.IsTrue(sut.BeginBattlesInSeat);
        Assert.IsFalse(sut.CursorWithPanel);
        Assert.IsTrue(sut.KeepPanelOpen);
    }

    [TestMethod]
    public void AnEditToTheSettingsObject_ShowsOnTheNextRead()
    {
        var settings = new TaomSettings();
        var sut = new SeatSettingsProvider(settings);

        settings.SeatEnabled = false;

        Assert.IsFalse(sut.Enabled);
    }

    private const string Group = "Seat of Seeing";

    private static object? Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheGroup_IsOpenedByItsFirstProperty_WithAnOrderNoOtherGroupUses()
    {
        var first = typeof(TaomSettings).GetProperty(nameof(TaomSettings.SeatEnabled))!;
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

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.SeatEnabled))]
    [DataRow(nameof(TaomSettings.BeginBattlesInSeat))]
    [DataRow(nameof(TaomSettings.SeatCursorWithPanel))]
    [DataRow(nameof(TaomSettings.SeatKeepPanelOpen))]
    public void EachSetting_SitsInTheSeatGroup_NeedsNoRestart_AndStatesItsDefault(string name)
    {
        var property = typeof(TaomSettings).GetProperty(name)!;

        var group = Attribute(property, "SettingPropertyGroupAttribute")!;
        Assert.AreEqual(Group, Read(group, "GroupName"));
        var bool_ = Attribute(property, "SettingPropertyBoolAttribute")!;
        Assert.AreEqual(false, Read(bool_, "RequireRestart"));
        var hint = (string)Read(bool_, "HintText");
        StringAssert.Contains(hint, "Default: ");
        StringAssert.StartsWith(hint, "{=taom_seat_mcm_");
    }
}
