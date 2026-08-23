using System;
using HarmonyLib;
using Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Refuge.Hooks;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Library;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// Drift-guards for every engine surface Patch75 and the refuge menus bind that the compiler
/// cannot fully certify: the clan-screen patch reaches a PRIVATE method (<c>OnPartySelection</c>)
/// by string and constructs <c>ClanPartyItemVM</c> at a pinned arity, the encounter patch targets
/// <c>PlayerEncounter.DoMeeting</c> through a Harmony attribute (string-bound method name), and
/// the menu controller calls two Helpers screen-openers whose overload shape has drifted across
/// engine versions before. Each failure mode past the compiler is a silent no-op in game; it must
/// fail here first (Patch74NameplateBindingTests shape).
/// </summary>
[TestClass]
public class Patch75BindingTests
{
    private const string ExpectedCategory = "Patch75_Refuge";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    // ---- ClanPartiesVM surface (RefugeClanScreenPatch) ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ClanPartiesVM_RefreshPartiesList_ResolvesAgainstInstalledEngine()
    {
        RequireGame();
        var method = AccessTools.Method(typeof(ClanPartiesVM), "RefreshPartiesList", Type.EmptyTypes);
        Assert.IsNotNull(method,
            "ClanPartiesVM.RefreshPartiesList() did not resolve; Patch75's clan-screen postfix "
            + "would fail to apply and refuges silently vanish from clan management.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ClanPartiesVM_PrivateOnPartySelection_ResolvesWithItemVmParameter()
    {
        RequireGame();
        var method = AccessTools.Method(
            typeof(ClanPartiesVM), "OnPartySelection", new[] { typeof(ClanPartyItemVM) });
        Assert.IsNotNull(method,
            "ClanPartiesVM.OnPartySelection(ClanPartyItemVM) did not resolve; the patch's cached "
            + "reflection handle would be null and refuge rows would stop being selectable "
            + "(the postfix no-ops on a null handle).");
        Assert.IsFalse(method.IsStatic,
            "OnPartySelection went static; Delegate.CreateDelegate bound to the VM instance would throw.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ClanPartiesVM_Garrisons_IsBindingListOfClanPartyItemVm()
    {
        RequireGame();
        var property = AccessTools.Property(typeof(ClanPartiesVM), "Garrisons");
        Assert.IsNotNull(property, "ClanPartiesVM.Garrisons is gone; refuges have no list to join.");
        Assert.AreEqual(typeof(MBBindingList<ClanPartyItemVM>), property.PropertyType,
            "Garrisons changed element/collection type; the postfix's Add would not compile "
            + "against the new shape at the next build, but a binary drift lands here first.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ClanPartyItemVM_SevenArgumentConstructor_Resolves()
    {
        RequireGame();
        var ctor = AccessTools.Constructor(typeof(ClanPartyItemVM), new[]
        {
            typeof(PartyBase),
            typeof(Action<ClanPartyItemVM>),
            typeof(Action),
            typeof(Action),
            typeof(ClanPartyItemVM.ClanPartyType),
            typeof(IDisbandPartyCampaignBehavior),
            typeof(ITeleportationCampaignBehavior),
        });
        Assert.IsNotNull(ctor,
            "ClanPartyItemVM's 7-argument constructor did not resolve; the refuge garrison row "
            + "construction in Patch75 no longer matches the engine.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ClanPartyItemVM_WageSuppressionSurface_StaysWritable()
    {
        // The postfix suppresses the non-functional wage control post-construction:
        // ShouldPartyHaveExpense=false hides the slider panel (ClanPartiesRightPanel.xml binds
        // its IsVisible to it), ExpenseItem=null drops the dead VM, and PartyWageSubTitleText is
        // recomposed to the honest 0 (the figure is never charged for a refuge). Name is set on
        // building rows. All four must stay public writable instance properties.
        RequireGame();
        foreach (var name in new[]
        {
            "ShouldPartyHaveExpense", "ExpenseItem", "PartyWageSubTitleText", "Name",
        })
        {
            var property = AccessTools.Property(typeof(ClanPartyItemVM), name);
            Assert.IsNotNull(property, "ClanPartyItemVM." + name + " is gone; the wage-suppression "
                + "or building-label half of Patch75 would not compile against the new engine, but "
                + "a binary drift lands here first.");
            Assert.IsTrue(property!.CanWrite && property.GetSetMethod() != null,
                "ClanPartyItemVM." + name + " lost its public setter; the post-construct override "
                + "would stop compiling / silently fail.");
        }
    }

    [TestMethod]
    public void ClanPartyType_Garrison_KeepsOrdinalThree()
    {
        // The source module hard-cast (ClanPartyType)3; TAOM uses the named member, and this pin
        // exists so an engine reorder of the enum is noticed as a semantic change, not just a
        // differently-styled row.
        Assert.AreEqual(3, (int)ClanPartyItemVM.ClanPartyType.Garrison);
    }

    // ---- PlayerEncounter surface (RefugeEncounterPatch) ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PlayerEncounter_DoMeeting_IsStaticParameterless()
    {
        RequireGame();
        var method = AccessTools.Method(typeof(PlayerEncounter), "DoMeeting", Type.EmptyTypes);
        Assert.IsNotNull(method,
            "PlayerEncounter.DoMeeting() did not resolve; Patch75's prefix would fail to apply and "
            + "meeting a refuge would open vanilla's stranger-conversation flow.");
        Assert.IsTrue(method.IsStatic, "DoMeeting is no longer static; the parameterless prefix "
            + "shape Patch75 relies on has changed.");
    }

    // ---- Helpers screen-openers (RefugeMenuController) ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PartyScreenHelper_ManageTroopsAndPrisoners_OverloadResolves()
    {
        RequireGame();
        var method = AccessTools.Method(
            typeof(PartyScreenHelper), "OpenScreenAsManageTroopsAndPrisoners",
            new[] { typeof(MobileParty), typeof(PartyScreenClosedDelegate) });
        Assert.IsNotNull(method,
            "PartyScreenHelper.OpenScreenAsManageTroopsAndPrisoners(MobileParty, "
            + "PartyScreenClosedDelegate) did not resolve; the refuge manage/deposit screens are dead.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void InventoryScreenHelper_OpenScreenAsStash_Resolves()
    {
        RequireGame();
        var method = AccessTools.Method(
            typeof(InventoryScreenHelper), "OpenScreenAsStash", new[] { typeof(ItemRoster) });
        Assert.IsNotNull(method,
            "InventoryScreenHelper.OpenScreenAsStash(ItemRoster) did not resolve; the refuge "
            + "goods-stash screen is dead.");
    }

    // ---- Category pins (SubModule applies the category by string) ----

    [TestMethod]
    public void BothPatches_CarryThePatch75Category()
    {
        foreach (var patchType in new[] { typeof(RefugeClanScreenPatch), typeof(RefugeEncounterPatch) })
        {
            var attributes = patchType.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false);
            Assert.AreEqual(1, attributes.Length, patchType.Name + " lost its HarmonyPatchCategory attribute.");
            Assert.AreEqual(ExpectedCategory, ((HarmonyPatchCategory)attributes[0]).info.category,
                patchType.Name + " drifted from the category SubModule applies; Harmony would apply "
                + "nothing and report nothing for it.");
        }
    }
}
