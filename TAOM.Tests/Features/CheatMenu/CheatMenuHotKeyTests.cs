using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CheatMenu;
using TAOM.Features.CheatMenu.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Features.MarshalsWord;
using TAOM.Features.RealmBorders.UI;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SpecialResources;
using TAOM.Features.TimeAcceleration;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// The cheat menu's map key, wired the way the realm border keys are: the module registers the category at process load,
/// the behavior attaches the map view from its tick, the view asks the opener's key gate, and the Options labels live in
/// global_strings.xml. Drop any link and the key does nothing or shows as a raw id, with no error anywhere. The gate itself
/// (switch, co-op, free map) is tested in CheatMenuPolicyTests.
/// </summary>
[TestClass]
public class CheatMenuHotKeyTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module", "ModuleData"));

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void HotKeyCategory_RegistersTheKeyWhereTheOptionsScreenShowsIt()
    {
        var category = new TaomCheatMenuHotKeyCategory();
        var key = category.RegisteredGameKeys.Where(k => k != null).Single(k => k.Id == TaomCheatMenuHotKeyCategory.OpenCheatMenuKeyId);

        Assert.AreEqual(TaomCheatMenuHotKeyCategory.OpenCheatMenuKeyId, key.Id);
        Assert.AreEqual(550, key.Id);
        Assert.AreEqual("TaomOpenCheatMenu", key.StringId);
        Assert.AreEqual(GameKeyContext.GameKeyContextType.Default, category.Type);
        // Below TotalGameKeyCount the Options label would reuse a vanilla key's name.
        Assert.IsTrue(key.Id >= (int)GameKeyDefinition.TotalGameKeyCount, $"{key.StringId} id {key.Id}");
        Assert.AreEqual(GameKeyMainCategories.CampaignMapCategory, key.MainCategoryId);
        Assert.AreEqual(TaomCheatMenuHotKeyCategory.CategoryId, key.GroupId);
        Assert.IsNotNull(key.KeyboardKey, "the key ships unbound");
        Assert.AreEqual(InputKey.H, key.KeyboardKey.InputKey);
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void HotKeyCategory_RegistersTheBattleKeyUnderAction()
    {
        var category = new TaomCheatMenuHotKeyCategory();
        var keys = category.RegisteredGameKeys.Where(k => k != null).ToList();
        var key = keys.Single(k => k.Id == TaomCheatMenuHotKeyCategory.OpenBattleCheatMenuKeyId);

        Assert.AreEqual(2, keys.Count);
        Assert.AreEqual(551, key.Id);
        Assert.AreEqual("TaomOpenBattleCheatMenu", key.StringId);
        Assert.AreEqual(GameKeyMainCategories.ActionCategory, key.MainCategoryId);
        Assert.AreEqual(TaomCheatMenuHotKeyCategory.CategoryId, key.GroupId);
        Assert.IsNotNull(key.KeyboardKey, "the key ships unbound");
        Assert.AreEqual(InputKey.H, key.KeyboardKey.InputKey);
    }

    [TestMethod]
    public void GlobalStrings_NameAndDescribeTheKey()
    {
        // The Options screen looks up str_key_name.<category>_<id> in this file and no other.
        var ids = XDocument.Load(Path.Combine(ModuleDataPath, "global_strings.xml")).Descendants("string")
            .Select(s => (string)s.Attribute("id")).ToList();

        var id = TaomCheatMenuHotKeyCategory.OpenCheatMenuKeyId;
        CollectionAssert.Contains(ids, $"str_key_name.{TaomCheatMenuHotKeyCategory.CategoryId}_{id}");
        CollectionAssert.Contains(ids, $"str_key_description.{TaomCheatMenuHotKeyCategory.CategoryId}_{id}");
        var battle = TaomCheatMenuHotKeyCategory.OpenBattleCheatMenuKeyId;
        CollectionAssert.Contains(ids, $"str_key_name.{TaomCheatMenuHotKeyCategory.CategoryId}_{battle}");
        CollectionAssert.Contains(ids, $"str_key_description.{TaomCheatMenuHotKeyCategory.CategoryId}_{battle}");
    }

    [TestMethod]
    public void HotKeyId_FollowsEveryOtherTaomKeyId()
    {
        var others = new[]
        {
            TaomTimeControlHotKeyCategory.FastForwardKeyId, TaomTimeControlHotKeyCategory.ExtraFastForwardKeyId,
            TaomTimeControlHotKeyCategory.TurboKeyId, TaomCareerHotKeyCategory.AbilityActivationKeyId,
            TaomRealmBordersHotKeyCategory.ToggleBordersKeyId, TaomRealmBordersHotKeyCategory.CycleMapModeKeyId,
            TaomSeatHotKeyCategory.TakeTheSeatKeyId, TaomSeatHotKeyCategory.KeepToKeyId, TaomSeatHotKeyCategory.TakeThisSoldierKeyId,
            TaomSeatHotKeyCategory.QuickenTimeKeyId, TaomSeatHotKeyCategory.SlowTimeKeyId, TaomSeatHotKeyCategory.StayTimeKeyId,
            TaomSeatHotKeyCategory.HideTheInterfaceKeyId,
            TaomMarshalHotKeyCategory.QueueKeyId, TaomMarshalHotKeyCategory.MarkKeyId, TaomMarshalHotKeyCategory.KeepWidthsKeyId,
            TaomMarshalHotKeyCategory.FireModeKeyId, TaomMarshalHotKeyCategory.WordKeyId, TaomMarshalHotKeyCategory.HoldLineKeyId,
        };

        Assert.IsTrue(TaomCheatMenuHotKeyCategory.OpenCheatMenuKeyId > others.Max(),
            "the cheat menu key follows every other TAOM key id, so no two categories interleave");
    }

    [TestMethod]
    public void Module_RegistersTheKeyAtProcessLoad()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/CheatMenuModule.cs", stripComments: true);

        StringAssert.Contains(src, "if (phase != ApplyPhase.ProcessLoad)");
        StringAssert.Contains(src, "TaomCheatMenuHotKeyCategory.Register();");
        StringAssert.Contains(src, "registrator.Register<ICheatMenuMapViewAdapter, NullCheatMenuMapViewAdapter>(Reuse.Singleton);");
        var category = RepoPaths.ReadSource("Main/Features/CheatMenu/TaomCheatMenuHotKeyCategory.cs", stripComments: true);
        StringAssert.Contains(category, "HotKeyManager.RegisterContext(new TaomCheatMenuHotKeyCategory())");
        Assert.IsFalse(category.Contains("RegisterInitialContexts"), "RegisterInitialContexts would drop every other mod's keys");
    }

    [TestCategory("RequiresGame")]
    [TestMethod]
    public void Behavior_OnTick_AsksTheMapViewAdapterToAttach()
    {
        GameAssemblies.EnsureLoaded();
        var adapter = Substitute.For<ICheatMenuMapViewAdapter>();
        var service = new CheatMenuService(Substitute.For<ICheatWorldAdapter>(), Substitute.For<ISpecialResourceService>(),
            Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>(), new CheatPartyRules(),
            Substitute.For<IPartySizeCacheInvalidator>());
        var behavior = new CheatMenuCampaignBehavior(service, Substitute.For<ICheatMenuSettingsProvider>(),
            Substitute.For<ICheatWorldAdapter>(), adapter, new CheatPartyRules());

        behavior.OnTick(0f);

        adapter.Received(1).EnsureAttached();
    }

    [TestMethod]
    public void Behavior_ListensToTheTick()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuCampaignBehavior.cs", stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick)");
    }

    [TestMethod]
    public void MapView_GoesThroughTheOpenersKeyGate()
    {
        var src = RepoPaths.ReadSource("Client/Features/CheatMenu/UI/CheatMenuMapView.cs", stripComments: true);

        StringAssert.Contains(src, "_opener.TryOpenFromHotKey(_pressed)");
        Assert.IsFalse(src.Contains(".TryOpen()"), "the view must not skip the key gate");
    }

    [TestMethod]
    public void BattleView_IsADefaultView_ThatGoesThroughTheOpenersKeyGate()
    {
        var src = RepoPaths.ReadSource("Client/Features/CheatMenu/UI/BattleCheatMenuView.cs", stripComments: true);

        StringAssert.Contains(src, "[DefaultView]");
        StringAssert.Contains(src, "public sealed class BattleCheatMenuView : MissionView");
        StringAssert.Contains(src, "_opener?.TryOpenOnKey(_pressed!)");
        StringAssert.Contains(src, "TaomCheatMenuHotKeyCategory.OpenBattleCheatMenuKeyId");
        Assert.IsFalse(src.Contains(".TryOpen()"), "the view must not skip the key gate");
        // Ctrl+H stays vanilla's; no list while another menu has the cursor.
        StringAssert.Contains(src, "InputKey.LeftControl");
        StringAssert.Contains(src, "InputKey.LeftAlt");
        StringAssert.Contains(src, "mission.IsOrderMenuOpen");
        StringAssert.Contains(src, "InformationManager.IsAnyInquiryActive()");
        StringAssert.Contains(src, "MissionGauntletSingleplayerEscapeMenu");
    }

    [TestMethod]
    public void BattleView_StaysOffTheKey_InTheModesThatReadRawH()
    {
        // Fords of War fields a hero on H (FordsCommandHotkeys) and The Long Defeat sounds the horn on H
        // (LongDefeatPanelHost); a [DefaultView] is in their missions too, so one press would do both. The check names
        // the views that read the key: story missions add LongDefeatMissionLogic but not LongDefeatHudView.
        var view = RepoPaths.ReadSource("Client/Features/CheatMenu/UI/BattleCheatMenuView.cs", stripComments: true);
        var busy = Regex.Match(view, @"private bool Busy\(\)[\s\S]*?\n    \}").Value;

        StringAssert.Contains(busy, "GetMissionBehavior<FordsCommandView>() != null");
        StringAssert.Contains(busy, "GetMissionBehavior<LongDefeatHudView>() != null");
        Assert.IsFalse(busy.Contains("LongDefeatMissionLogic"), "story missions carry LongDefeatMissionLogic and no H reader");
        foreach (var reader in new[] { "Client/Features/FordsOfWar/FordsCommandHotkeys.cs", "Client/Features/LongDefeat/LongDefeatPanelHost.cs" })
            StringAssert.Contains(RepoPaths.ReadSource(reader, stripComments: true), "IsKeyPressed(InputKey.H)",
                $"{reader} no longer reads H: drop its mode from Busy()");
    }

    [TestMethod]
    public void NullMapViewAdapter_DoesNothing() => new NullCheatMenuMapViewAdapter().EnsureAttached();
}
