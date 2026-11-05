using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// Stage M6, the panel (build-spec 10): the provider, its three entries and their registration. The engine builds the panel from the provider's list, so these pin the
/// shapes the engine tests by value and the rules the contract states; the layout of the movie is a human row.
/// </summary>
[TestClass]
public class MarshalPanelWiringTests
{
    private const string Provider = "Client/Features/MarshalsWord/MarshalVisualOrderProvider.cs";
    private const string Orders = "Client/Features/MarshalsWord/MarshalPanelOrders.cs";

    private static string Code(string path) => Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");

    [TestMethod]
    public void TheProvider_IsAvailableOnlyWhenTheBaseIsLiveAndNotInAHideout_SoAnyOtherMissionFallsThroughToTheBaseGames()
    {
        // DefaultVisualOrderProvider.IsAvailable is false in a friendly mission (a town, an arena, Host Review). Registered after Sandbox's, TAOM's would otherwise answer first in a hideout.
        var code = Code(Provider);

        StringAssert.Contains(code, "public override bool IsAvailable() => base.IsAvailable() && MarshalHooks.IsLive && !Mission.Current.HasMissionBehavior<HideoutMissionController>();");
        StringAssert.Contains(code, "internal sealed class MarshalVisualOrderProvider : DefaultVisualOrderProvider");
    }

    [TestMethod]
    public void TheProvider_CopiesTheBaseList_AddsOneSetAtTheRulesIndex_AndNeverDropsOrRenamesABaseSet()
    {
        var code = Code(Provider);

        StringAssert.Contains(code, "var sets = new MBList<VisualOrderSet>(base.GetOrders());");
        StringAssert.Contains(code, "sets.Insert(MarshalPanelRules.SetIndex(BannerlordConfig.OrderLayoutType, sets.Count, lastIsReturn), NewSet());");
        StringAssert.Contains(code, "sets[sets.Count - 1] is SingleVisualOrderSet { Order: ReturnVisualOrder }");
        Assert.IsFalse(Regex.IsMatch(code, @"sets\.(Remove|RemoveAt|Clear|Sort|Reverse)\b"), "the engine tests the base sets and orders by id and by type, so none is dropped");
    }

    [TestMethod]
    public void TheSet_HoldsTheThreeOrdersAndEndsInAReturn_WhichTheEngineClosesTheSetOn()
    {
        var code = Code(Provider);

        StringAssert.Contains(code, "set.AddOrder(new MarshalFireModeOrder()); set.AddOrder(new MarshalWordOrder()); set.AddOrder(new MarshalHoldLineOrder()); set.AddOrder(new ReturnVisualOrder());");
        StringAssert.Contains(code, "var set = new MarshalOrderSet();");
    }

    [TestMethod]
    public void TheSet_KeepsItsOwnStringId_AndDrawsWithABaseGameIconLayer_SoItsButtonIsNotBlank()
    {
        // GenericVisualOrderSet.IconId answers the string id (cs:20-36), OrderSetVM.cs:180 copies it to OrderIconId, and IconBrushWidget looks it up as a brush layer of
        // Order.Icons.Square or Round, which draws nothing for a name the brush lacks (Brush.cs:339-346). The base game's own toggle set, built with the same flags, is drawn as order_type_toggle.
        // The test project cannot load TaleWorlds.MountAndBlade.View, so the shape is pinned in the source.
        var code = Code("Client/Features/MarshalsWord/MarshalOrderSet.cs");

        StringAssert.Contains(code, "internal sealed class MarshalOrderSet : GenericVisualOrderSet");
        StringAssert.Contains(code, "internal const string SetId = \"taom_order_type_marshal\"; internal const string SetIcon = \"order_type_toggle\";");
        StringAssert.Contains(code, ": base(SetId, new TextObject(\"{=taom_marshal_panel_set}Marshal's Word\"), useActiveOrderForIconId: false, useActiveOrderForName: false)");
        StringAssert.Contains(code, "public override string IconId => SetIcon;");
    }

    [TestMethod]
    public void TheEntries_HaveTaomStringIds_AndOnlyVanillaIconIds_SoNoVanillaLookupMeetsAnOrderOfItsOwnId()
    {
        // GauntletOrderUIHandler.cs:582, MissionOrderVM.cs:97 and MissionOrderTroopControllerVM.cs:815, 839 test the ids order_movement_charge, order_movement_advance and order_type_facing by value.
        var code = Code(Orders);

        foreach (var id in new[] { "taom_marshal_fire", "taom_marshal_word", "taom_marshal_hold" })
            StringAssert.Contains(code, ": base(\"" + id + "\"", id);
        foreach (var icon in new[] { "order_toggle_fire", "order_movement_skirmish", "order_form_close" })
            StringAssert.Contains(code, "\"" + icon + "\"", icon);
        Assert.IsFalse(Regex.IsMatch(code, @"order_movement_(charge|advance)|order_type_(facing|movement|form|toggle)|order_toggle_facing"), "an icon the engine tests by value is never reused");
        StringAssert.Contains(code, "protected override string GetIconId() => _iconId;");
    }

    [TestMethod]
    public void AClick_OnlyStoresThePressForTheSelectedCompanies_AndWritesNoAgentFormationOrOrder()
    {
        // ExecuteOrder runs in the handler's screen tick (GauntletOrderUIHandler.cs:470-471), which overlaps the AI job; the mission tick applies the press.
        var code = Code(Orders);

        StringAssert.Contains(code, "if (orderController != MarshalHooks.PlayerController) return;");
        StringAssert.Contains(code, "MarshalHooks.PanelPress(_action, companies);");
        Assert.IsFalse(Regex.IsMatch(code, @"\.(SetOrder\w*|SetMovementOrder|SetFiringOrder|SetAgentFlags|SetTargetFormation|Issue|SelectFormation|ClearSelectedFormations)\("), "a click writes nothing");
    }

    [TestMethod]
    public void TheActiveState_ReadsOnlyTaomsOwnTables()
    {
        var code = Code(Orders);

        StringAssert.Contains(code, "OnGetFormationHasOrder(Formation formation) => MarshalFireTable.ModeOf(MarshalOrderAdapter.CompanyOf(formation)) != FireMode.Free;");
        StringAssert.Contains(code, "OnGetFormationHasOrder(Formation formation) => MarshalFireTable.HoldOf(MarshalOrderAdapter.CompanyOf(formation));");
        StringAssert.Contains(code, "OnGetFormationHasOrder(Formation formation) => null;");
        Assert.IsFalse(Regex.IsMatch(code, @"OnGetFormationHasOrder[^;]*(Agent|Mission\.|FormationsIncluding|GetReadonlyMovementOrderReference)"));
    }

    [TestMethod]
    public void TheModule_RegistersTheNullRegistrar_AndCallsTheRegistrarOnceAtProcessLoad_InsideItsOwnTry()
    {
        // A failure must leave the panel as the base game builds it, and the keys still work; the factory rejects a second provider of the same type.
        var code = Code("Main/Features/MarshalsWord/MarshalsWordModule.cs");

        StringAssert.Contains(code, "registrator.Register<IMarshalOrderProviderRegistrar, NullMarshalOrderProviderRegistrar>(Reuse.Singleton);");
        StringAssert.Contains(code, "if (phase != ApplyPhase.ProcessLoad) return;");
        StringAssert.Contains(code, "RegisterOrderProvider(resolver);");
        StringAssert.Contains(code, "try { resolver.Resolve<IMarshalOrderProviderRegistrar>().Register(); } catch (Exception ex)");
        Assert.AreEqual(1, Regex.Matches(code, @"IMarshalOrderProviderRegistrar>\(\)\.Register\(\);").Count);
    }

    [TestMethod]
    public void TheRegistrar_RegistersOneProviderThroughTheFactory()
    {
        var code = Code("Client/Features/MarshalsWord/MarshalOrderProviderRegistrar.cs");

        StringAssert.Contains(code, "public void Register() => VisualOrderFactory.RegisterProvider(new MarshalVisualOrderProvider());");
    }

    [TestMethod]
    public void TheHookSink_RoutesAPanelClickToTheSession_AndIgnoresItWithNoLiveSession()
    {
        var code = Code("Main/Features/MarshalsWord/MarshalHooks.cs");

        StringAssert.Contains(code, "internal static void PanelPress(PanelAction action, IReadOnlyList<int> companies) => _sink?.PanelPress(action, companies);");
    }
}
