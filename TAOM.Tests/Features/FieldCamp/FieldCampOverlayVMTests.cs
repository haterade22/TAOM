using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Domain;
using TAOM.Features.FieldCamp.Hooks;
using TAOM.Features.FieldCamp.UI;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// The overlay VM against mocks only (review #26 lesson: constructible without IoC or campaign
/// state). Build progress reaches the VM through the injected delegate precisely so these tests
/// can exercise the raising path: <c>CampState.BuildProgress()</c> walks
/// <c>CampaignTime.Now -&gt; Campaign.Current</c>, which is null here.
///
/// <para>Text assertions use Contains on the English fallback so they hold whether or not the
/// localization layer strips the <c>{=key}</c> prefix in a bare test process (the SupplyLines VM
/// tests use the same defensive shape).</para>
/// </summary>
[TestClass]
public class FieldCampOverlayVMTests
{
    private ICampService _camps = null!;
    private ICampSettingsProvider _settings = null!;
    private IGameMenuAdapter _menus = null!;
    private ICampMenuActivationQuery _activation = null!;
    private List<ICampOverlayContributor> _contributors = null!;
    private Func<CampState, float> _progress = null!;

    [TestInitialize]
    public void Setup()
    {
        _camps = Substitute.For<ICampService>();
        _settings = Substitute.For<ICampSettingsProvider>();
        _menus = Substitute.For<IGameMenuAdapter>();
        _activation = Substitute.For<ICampMenuActivationQuery>();
        _contributors = new List<ICampOverlayContributor>();
        _progress = _ => 1f;

        _settings.Enabled.Returns(true);
        _camps.PlayerCamp.Returns((CampState?)null);

        // All-clear activation baseline; each gate test flips exactly one guard.
        _activation.IsMapScreenClear.Returns(true);
        _activation.IsMainPartyStationary.Returns(true);
        _activation.IsMainPartyInSettlement.Returns(false);
        _activation.IsMainPartyInEncounter.Returns(false);
        _activation.IsMainPartyDisorganized.Returns(false);
    }

    private FieldCampOverlayVM CreateVm()
        => new FieldCampOverlayVM(_camps, _settings, _menus, _activation, _contributors, _progress);

    private static CampState Camp(CampType type, bool foraging = false, int foragedTotal = 0, float forageAccumulator = 0f)
        => new CampState
        {
            TypeEnum = type,
            Foraging = foraging,
            ForagedTotal = foragedTotal,
            ForageAccumulator = forageAccumulator,
        };

    // ---- Button visibility follows the master toggle (the fix over the source's hardcoded true) ----

    [TestMethod]
    public void Refresh_MasterToggleOff_HidesButtonAndDisablesIt()
    {
        _settings.Enabled.Returns(false);
        var vm = CreateVm();

        vm.Refresh();

        Assert.IsFalse(vm.ShowCampButton);
        Assert.IsFalse(vm.CanMakeCamp);
    }

    [TestMethod]
    public void Refresh_MasterToggleOn_ShowsButtonAndEnablesIt()
    {
        var vm = CreateVm();

        vm.Refresh();

        Assert.IsTrue(vm.ShowCampButton);
        Assert.IsTrue(vm.CanMakeCamp);
    }

    // ---- Caption precedence: contributor override > per-state default ----

    [TestMethod]
    public void Refresh_ContributorCaption_OverridesTheDefault()
    {
        var contributor = Substitute.For<ICampOverlayContributor>();
        contributor.CaptionOverride().Returns("Refuge Options");
        _contributors.Add(contributor);
        var vm = CreateVm();

        vm.Refresh();

        Assert.AreEqual("Refuge Options", vm.CampButtonText);
    }

    [TestMethod]
    public void Refresh_ContributorCaptionEmpty_FallsBackToMakeCamp()
    {
        var contributor = Substitute.For<ICampOverlayContributor>();
        contributor.CaptionOverride().Returns(string.Empty);
        _contributors.Add(contributor);
        var vm = CreateVm();

        vm.Refresh();

        StringAssert.Contains(vm.CampButtonText, "Make Camp");
    }

    [TestMethod]
    public void Refresh_ContributorCaptionThrows_FallsBackInsteadOfCrashing()
    {
        var contributor = Substitute.For<ICampOverlayContributor>();
        contributor.CaptionOverride().Returns(_ => throw new InvalidOperationException("boom"));
        _contributors.Add(contributor);
        var vm = CreateVm();

        vm.Refresh();

        StringAssert.Contains(vm.CampButtonText, "Make Camp");
    }

    [TestMethod]
    public void Refresh_CampStanding_ButtonSaysCampOptions()
    {
        _camps.PlayerCamp.Returns(Camp(CampType.Field));
        var vm = CreateVm();

        vm.Refresh();

        StringAssert.Contains(vm.CampButtonText, "Camp Options");
    }

    // ---- Status text per type / progress ----

    [DataTestMethod]
    [DataRow(CampType.Ambush, "Ambush ready")]
    [DataRow(CampType.Lookout, "Lookout posted")]
    [DataRow(CampType.Field, "Field camp")]
    [DataRow(CampType.Fortified, "Fortified camp")]
    public void Refresh_ReadyCamp_ShowsTypeLabelWithoutBar(CampType type, string expected)
    {
        _camps.PlayerCamp.Returns(Camp(type));
        var vm = CreateVm();

        vm.Refresh();

        Assert.IsTrue(vm.IsCampActive);
        StringAssert.Contains(vm.CampStatusText, expected);
        Assert.IsFalse(vm.ProgressVisible);
    }

    [DataTestMethod]
    [DataRow(CampType.Ambush, "Setting up the ambush")]
    [DataRow(CampType.Lookout, "Raising the lookout")]
    [DataRow(CampType.Field, "Raising the field camp")]
    [DataRow(CampType.Fortified, "Raising the fortified camp")]
    public void Refresh_RaisingCamp_ShowsRaisingLabelWithBar(CampType type, string expected)
    {
        _camps.PlayerCamp.Returns(Camp(type));
        _progress = _ => 0.4f;
        var vm = CreateVm();

        vm.Refresh();

        Assert.IsTrue(vm.IsCampActive);
        StringAssert.Contains(vm.CampStatusText, expected);
        Assert.IsTrue(vm.ProgressVisible);
        Assert.AreEqual(80f, vm.ProgressFillWidth, 0.001f, "40% of the 200px track");
    }

    [TestMethod]
    public void Refresh_ForagingCamp_ShowsForagingStatusWithAccumulatorBar()
    {
        _camps.PlayerCamp.Returns(Camp(CampType.Field, foraging: true, foragedTotal: 12, forageAccumulator: 0.5f));
        var vm = CreateVm();

        vm.Refresh();

        StringAssert.Contains(vm.CampStatusText, "foraging");
        Assert.IsTrue(vm.ProgressVisible);
        Assert.AreEqual(100f, vm.ProgressFillWidth, 0.001f, "half a grain accumulated = half the track");
    }

    [TestMethod]
    public void Refresh_NoCampNoContributor_PanelHidden()
    {
        var vm = CreateVm();

        vm.Refresh();

        Assert.IsFalse(vm.IsCampActive);
        Assert.IsFalse(vm.ProgressVisible);
    }

    [TestMethod]
    public void Refresh_ContributorStatus_ShowsPanelWithBar()
    {
        var contributor = Substitute.For<ICampOverlayContributor>();
        contributor.OverlayStatus().Returns(new CampOverlayStatus("Refuge rising", 30));
        _contributors.Add(contributor);
        var vm = CreateVm();

        vm.Refresh();

        Assert.IsTrue(vm.IsCampActive);
        Assert.AreEqual("Refuge rising", vm.CampStatusText);
        Assert.IsTrue(vm.ProgressVisible);
        Assert.AreEqual(60f, vm.ProgressFillWidth, 0.001f);
    }

    [TestMethod]
    public void Refresh_ContributorStatusNegativePercent_ShowsLineWithoutBar()
    {
        var contributor = Substitute.For<ICampOverlayContributor>();
        contributor.OverlayStatus().Returns(new CampOverlayStatus("Refuge standing", -1));
        _contributors.Add(contributor);
        var vm = CreateVm();

        vm.Refresh();

        Assert.IsTrue(vm.IsCampActive);
        Assert.IsFalse(vm.ProgressVisible);
    }

    // ---- ProgressFillWidth 0..200 clamp ----

    [DataTestMethod]
    [DataRow(-5, 0f)]
    [DataRow(0, 0f)]
    [DataRow(50, 100f)]
    [DataRow(100, 200f)]
    [DataRow(150, 200f)]
    public void ProgressInt_AnyValue_FillWidthClampedOntoTrack(int percent, float expectedWidth)
    {
        var vm = CreateVm();

        vm.ProgressInt = percent;

        Assert.AreEqual(expectedWidth, vm.ProgressFillWidth, 0.001f);
    }

    // ---- ExecuteOpenCampMenu gate matrix: every guard blocks on its own ----

    [TestMethod]
    public void ExecuteOpenCampMenu_AllClear_ActivatesTheBaseMenu()
    {
        var vm = CreateVm();

        vm.ExecuteOpenCampMenu();

        _menus.Received(1).Activate(FieldCampCampaignBehavior.BaseMenuId);
    }

    [TestMethod]
    public void ExecuteOpenCampMenu_FeatureDisabled_Blocks()
    {
        _settings.Enabled.Returns(false);
        var vm = CreateVm();

        vm.ExecuteOpenCampMenu();

        _menus.DidNotReceiveWithAnyArgs().Activate(default!);
    }

    [TestMethod]
    public void ExecuteOpenCampMenu_MapScreenNotClear_Blocks()
    {
        _activation.IsMapScreenClear.Returns(false);
        var vm = CreateVm();

        vm.ExecuteOpenCampMenu();

        _menus.DidNotReceiveWithAnyArgs().Activate(default!);
    }

    [TestMethod]
    public void ExecuteOpenCampMenu_PartyMoving_Blocks()
    {
        _activation.IsMainPartyStationary.Returns(false);
        var vm = CreateVm();

        vm.ExecuteOpenCampMenu();

        _menus.DidNotReceiveWithAnyArgs().Activate(default!);
    }

    [TestMethod]
    public void ExecuteOpenCampMenu_InSettlement_Blocks()
    {
        _activation.IsMainPartyInSettlement.Returns(true);
        var vm = CreateVm();

        vm.ExecuteOpenCampMenu();

        _menus.DidNotReceiveWithAnyArgs().Activate(default!);
    }

    [TestMethod]
    public void ExecuteOpenCampMenu_InEncounter_Blocks()
    {
        _activation.IsMainPartyInEncounter.Returns(true);
        var vm = CreateVm();

        vm.ExecuteOpenCampMenu();

        _menus.DidNotReceiveWithAnyArgs().Activate(default!);
    }

    [TestMethod]
    public void ExecuteOpenCampMenu_Disorganized_Blocks()
    {
        _activation.IsMainPartyDisorganized.Returns(true);
        var vm = CreateVm();

        vm.ExecuteOpenCampMenu();

        _menus.DidNotReceiveWithAnyArgs().Activate(default!);
    }

    [TestMethod]
    public void Refresh_GateBlocked_CanMakeCampFalseButButtonStaysVisible()
    {
        _activation.IsMainPartyStationary.Returns(false);
        var vm = CreateVm();

        vm.Refresh();

        Assert.IsTrue(vm.ShowCampButton, "the button stays visible; only its enabled state follows the gate");
        Assert.IsFalse(vm.CanMakeCamp);
    }
}
