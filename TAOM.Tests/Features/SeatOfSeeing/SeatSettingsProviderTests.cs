using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ServerSettings;
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
        Assert.IsFalse(sut.HeroFightsOn, "the hand-off stays off until a run in the game proves it (build-spec section 8)");
        Assert.IsTrue(sut.TakeSoldierAfterFall, "it acts only after the fall, in a battle, on its own key");
        Assert.IsFalse(sut.RiseWithPanel, "section 1 promises vanilla behaviour to a player who never asks for the lift");
        Assert.IsTrue(sut.EdgeScroll, "it acts only with the cursor shown");
        Assert.AreEqual(1f, sut.PanSpeed);
    }

    [TestMethod]
    public void TheFallbackDefaults_AreTheMcmPropertyDefaults()
    {
        var settings = new TaomSettings();

        Assert.AreEqual(SeatSettingsProvider.DefaultEnabled, settings.SeatEnabled);
        Assert.AreEqual(SeatSettingsProvider.DefaultBeginBattlesInSeat, settings.BeginBattlesInSeat);
        Assert.AreEqual(SeatSettingsProvider.DefaultCursorWithPanel, settings.SeatCursorWithPanel);
        Assert.AreEqual(SeatSettingsProvider.DefaultKeepPanelOpen, settings.SeatKeepPanelOpen);
        Assert.AreEqual(SeatSettingsProvider.DefaultHeroFightsOn, settings.SeatHeroFightsOn);
        Assert.AreEqual(SeatSettingsProvider.DefaultTakeSoldierAfterFall, settings.SeatTakeSoldierAfterFall);
        Assert.AreEqual(SeatSettingsProvider.DefaultRiseWithPanel, settings.SeatRiseWithPanel);
        Assert.AreEqual(SeatSettingsProvider.DefaultEdgeScroll, settings.SeatEdgeScroll);
        Assert.AreEqual(SeatSettingsProvider.DefaultPanSpeed, settings.SeatPanSpeed);
        Assert.IsTrue(SeatSettingsProvider.DefaultEdgeScroll);
        Assert.AreEqual(1f, SeatSettingsProvider.DefaultPanSpeed, "1 is the speed the Seat always flew at");
        Assert.IsFalse(SeatSettingsProvider.DefaultRiseWithPanel, "a new setting whose default is off, so a player who never asks sees vanilla behaviour");
        Assert.IsFalse(SeatSettingsProvider.DefaultHeroFightsOn, "a default is never flipped under an unchanged name once it ships");
        Assert.IsTrue(SeatSettingsProvider.DefaultTakeSoldierAfterFall);
        Assert.IsTrue(SeatSettingsProvider.DefaultCursorWithPanel);
        Assert.IsFalse(SeatSettingsProvider.DefaultKeepPanelOpen);
        Assert.IsTrue(SeatSettingsProvider.DefaultEnabled);
        Assert.IsFalse(SeatSettingsProvider.DefaultBeginBattlesInSeat, "a player who never asks sees vanilla behaviour (build-spec section 1)");
    }

    [TestMethod]
    public void SavedValues_AreRead()
    {
        var settings = new TaomSettings { SeatEnabled = false, BeginBattlesInSeat = true, SeatCursorWithPanel = false, SeatKeepPanelOpen = true,
            SeatHeroFightsOn = true, SeatTakeSoldierAfterFall = false, SeatRiseWithPanel = true, SeatEdgeScroll = false, SeatPanSpeed = 2.5f };

        var sut = new SeatSettingsProvider(settings);

        Assert.IsFalse(sut.Enabled);
        Assert.IsTrue(sut.BeginBattlesInSeat);
        Assert.IsFalse(sut.CursorWithPanel);
        Assert.IsTrue(sut.KeepPanelOpen);
        Assert.IsTrue(sut.HeroFightsOn);
        Assert.IsFalse(sut.TakeSoldierAfterFall);
        Assert.IsTrue(sut.RiseWithPanel);
        Assert.IsFalse(sut.EdgeScroll);
        Assert.AreEqual(2.5f, sut.PanSpeed);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    [DataRow(0f)]
    [DataRow(0.24f)]
    [DataRow(4.01f)]
    [DataRow(-1f)]
    public void ASavedPanSpeed_ThatIsNotARealNumberInRange_ReadsAsTheDefault(float saved)
    {
        var sut = new SeatSettingsProvider(new TaomSettings { SeatPanSpeed = saved });

        Assert.AreEqual(SeatSettingsProvider.DefaultPanSpeed, sut.PanSpeed, "a bad value never reaches the camera");
    }

    [DataTestMethod]
    [DataRow(0.25f)]
    [DataRow(1f)]
    [DataRow(4f)]
    public void ASavedPanSpeed_InTheSlidersRange_IsRead(float saved)
    {
        Assert.AreEqual(saved, new SeatSettingsProvider(new TaomSettings { SeatPanSpeed = saved }).PanSpeed);
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
        Assert.AreEqual(Group, McmText.Plain(Read(group, "GroupName") as string));
        var order = (int)Read(group, "GroupOrder");
        Assert.AreNotEqual(0, order, "MCM ignores a GroupOrder on any later property, so the first carries it");

        var others = typeof(TaomSettings).GetProperties()
            .Select(p => Attribute(p, "SettingPropertyGroupAttribute"))
            .Where(a => a != null && McmText.Plain(Read(a, "GroupName") as string) != Group && (int)Read(a, "GroupOrder") == order)
            .Select(a => McmText.Plain(Read(a!, "GroupName") as string))
            .ToList();
        Assert.AreEqual(0, others.Count, "another group uses GroupOrder " + order + ": " + string.Join(", ", others));
    }

    [DataTestMethod]
    [DataRow(nameof(TaomSettings.SeatEnabled))]
    [DataRow(nameof(TaomSettings.BeginBattlesInSeat))]
    [DataRow(nameof(TaomSettings.SeatCursorWithPanel))]
    [DataRow(nameof(TaomSettings.SeatKeepPanelOpen))]
    [DataRow(nameof(TaomSettings.SeatHeroFightsOn))]
    [DataRow(nameof(TaomSettings.SeatTakeSoldierAfterFall))]
    [DataRow(nameof(TaomSettings.SeatRiseWithPanel))]
    [DataRow(nameof(TaomSettings.SeatEdgeScroll))]
    public void EachSetting_SitsInTheSeatGroup_NeedsNoRestart_AndStatesItsDefault(string name)
    {
        var property = typeof(TaomSettings).GetProperty(name)!;

        var group = Attribute(property, "SettingPropertyGroupAttribute")!;
        Assert.AreEqual(Group, McmText.Plain(Read(group, "GroupName") as string));
        var bool_ = Attribute(property, "SettingPropertyBoolAttribute")!;
        Assert.AreEqual(false, Read(bool_, "RequireRestart"));
        var hint = (string)Read(bool_, "HintText");
        StringAssert.Contains(hint, "Default: ");
        StringAssert.StartsWith(hint, "{=taom_seat_mcm_");
    }

    [TestMethod]
    public void TheTwoSettingsThatStopTheTimeKeysAndLiftTheView_SayWhatTheyDo()
    {
        var enabled = (string)Read(Attribute(typeof(TaomSettings).GetProperty(nameof(TaomSettings.SeatEnabled))!, "SettingPropertyBoolAttribute")!, "HintText");
        var rise = (string)Read(Attribute(typeof(TaomSettings).GetProperty(nameof(TaomSettings.SeatRiseWithPanel))!, "SettingPropertyBoolAttribute")!, "HintText");

        StringAssert.Contains(enabled, "mission time keys", "the contract has the hint say that the Seat off also stops keys 533 to 535");
        StringAssert.Contains(rise, "target lock", "a lift drops the target lock, and no new lock starts until the view has sunk");
        StringAssert.Contains(rise, "third person");
    }

    [TestMethod]
    public void ThePanSpeedSlider_SitsInTheSeatGroup_RunsFromAQuarterToFour_AndStatesItsDefault()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.SeatPanSpeed))!;

        Assert.AreEqual(Group, McmText.Plain(Read(Attribute(property, "SettingPropertyGroupAttribute")!, "GroupName") as string));
        var slider = Attribute(property, "SettingPropertyFloatingIntegerAttribute")!;
        Assert.AreEqual((double)SeatSettingsProvider.MinPanSpeed, Convert.ToDouble(Read(slider, "MinValue")), 1e-6);
        Assert.AreEqual((double)SeatSettingsProvider.MaxPanSpeed, Convert.ToDouble(Read(slider, "MaxValue")), 1e-6);
        Assert.AreEqual(false, Read(slider, "RequireRestart"));
        var hint = (string)Read(slider, "HintText");
        StringAssert.Contains(hint, "Default: 1");
        StringAssert.StartsWith(hint, "{=taom_seat_mcm_pan_speed_hint}");
        StringAssert.StartsWith(Read(slider, "DisplayName") as string, "{=taom_seat_mcm_pan_speed}");
    }

    [TestMethod]
    public void TheTwoCameraFeelSettings_AreAfterTheSevenOlderOnes_InTheGroupsOrder()
    {
        int OrderOf(string name) => (int)Read(Attribute(typeof(TaomSettings).GetProperty(name)!, name == nameof(TaomSettings.SeatPanSpeed)
            ? "SettingPropertyFloatingIntegerAttribute" : "SettingPropertyBoolAttribute")!, "Order");

        Assert.AreEqual(7, OrderOf(nameof(TaomSettings.SeatEdgeScroll)));
        Assert.AreEqual(8, OrderOf(nameof(TaomSettings.SeatPanSpeed)));
        Assert.AreEqual(6, OrderOf(nameof(TaomSettings.SeatRiseWithPanel)));
    }
}
