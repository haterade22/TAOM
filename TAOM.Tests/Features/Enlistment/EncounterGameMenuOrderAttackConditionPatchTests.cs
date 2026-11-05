using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #830: the postfix on the encounter menu's 'Send troops' condition only ever narrows what vanilla
/// decided. It never sets the result to false (that would hide the option), never re-enables a
/// disabled option, and fails open with one log line. No test calls IoC.Configure, so each installs
/// its own container in IoC's private field and restores it, as the sibling battle-exit patch tests do.
/// Building a MenuCallbackArgs and a TextObject runs engine code, hence RequiresGame.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class EncounterGameMenuOrderAttackConditionPatchTests
{
    private static readonly FieldInfo? ContainerField =
        typeof(TAOM.IoC).GetField("_container", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly FieldInfo? ServiceField =
        typeof(EncounterGameMenuOrderAttackConditionPatch).GetField("_menuService", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly FieldInfo? FaultField =
        typeof(EncounterGameMenuOrderAttackConditionPatch).GetField("_faultLogged", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly FieldInfo? TextManagerField =
        typeof(TaleWorlds.Core.GameTexts).GetField("_gameTextManager", BindingFlags.NonPublic | BindingFlags.Static);

    private object? _savedContainer;
    private object? _savedTextManager;

    [TestInitialize]
    public void Setup()
    {
        Assert.IsNotNull(ContainerField, "IoC._container moved: update this test");
        Assert.IsNotNull(ServiceField, "the patch's _menuService cache moved: update this test");
        Assert.IsNotNull(FaultField, "the patch's _faultLogged latch moved: update this test");
        _savedContainer = ContainerField!.GetValue(null);
        Assert.IsNotNull(TextManagerField, "GameTexts._gameTextManager moved: update this test");
        _savedTextManager = TextManagerField!.GetValue(null);
        ServiceField!.SetValue(null, null);
        FaultField!.SetValue(null, false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ContainerField?.SetValue(null, _savedContainer);
        TextManagerField?.SetValue(null, _savedTextManager);
        ServiceField?.SetValue(null, null);
        FaultField?.SetValue(null, false);
    }

    private static MenuCallbackArgs NewArgs() => new MenuCallbackArgs((MenuContext)null!, null!);

    private static IEnlistmentMenuService Install(SitOutVerdict verdict)
    {
        var service = Substitute.For<IEnlistmentMenuService>();
        service.EvaluateSitOut().Returns(verdict);
        var container = new Container();
        container.RegisterInstance(service);
        ContainerField!.SetValue(null, container);
        return service;
    }

    [TestMethod]
    public void Postfix_VanillaResultFalse_StaysFalseAndResolvesNothing()
    {
        var logger = Substitute.For<IModLogger>();
        using var container = new Container();
        container.RegisterInstance(logger);
        ContainerField!.SetValue(null, container);
        var args = NewArgs();
        var result = false;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

        Assert.IsFalse(result, "never un-hides, never re-hides: the result is left alone");
        Assert.IsTrue(args.IsEnabled);
        Assert.IsNull(ServiceField!.GetValue(null));
        logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void Postfix_VanillaAlreadyDisabled_ResolvesNothingAndKeepsVanillaTooltip()
    {
        var service = Install(SitOutVerdict.RefusedField);
        var args = NewArgs();
        args.IsEnabled = false;
        var result = true;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

        Assert.IsTrue(result);
        Assert.IsFalse(args.IsEnabled);
        Assert.IsNull(args.Tooltip, "a vanilla-disabled option (low morale) keeps its own tooltip");
        service.DidNotReceive().EvaluateSitOut();
    }

    [DataTestMethod]
    [DataRow(SitOutVerdict.RefusedField, "taom_enlist_sitout_refused_tip")]
    [DataRow(SitOutVerdict.RefusedSiege, "taom_enlist_sitout_siege_tip")]
    public void Postfix_RefusedVerdict_DisablesWithTheTaomTooltipAndKeepsTheOptionVisible(SitOutVerdict verdict, string key)
    {
        Install(verdict);
        var args = NewArgs();
        var result = true;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

        Assert.IsTrue(result, "the option stays visible; only IsEnabled changes");
        Assert.IsFalse(args.IsEnabled);
        Assert.IsNotNull(args.Tooltip);
        StringAssert.Contains(args.Tooltip.Value, "{=" + key + "}", "tooltip is the TAOM text for this verdict");
    }

    [DataTestMethod]
    [DataRow(SitOutVerdict.WoundedFree, "taom_enlist_sitout_wounded_tip")]
    [DataRow(SitOutVerdict.OverwhelmingCost, "taom_enlist_sitout_odds_tip")]
    public void Postfix_AllowedVerdictWithText_StaysEnabledAndExplains(SitOutVerdict verdict, string key)
    {
        Install(verdict);
        var args = NewArgs();
        var result = true;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

        Assert.IsTrue(result);
        Assert.IsTrue(args.IsEnabled);
        Assert.IsNotNull(args.Tooltip);
        StringAssert.Contains(args.Tooltip.Value, "{=" + key + "}");
    }

    [DataTestMethod]
    [DataRow(SitOutVerdict.Unchanged)]
    [DataRow(SitOutVerdict.ForcedOpen)]
    public void Postfix_UnchangedOrForcedOpen_TouchesNothing(SitOutVerdict verdict)
    {
        Install(verdict);
        var args = NewArgs();
        var result = true;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

        Assert.IsTrue(result);
        Assert.IsTrue(args.IsEnabled);
        Assert.IsNull(args.Tooltip);
    }

    [TestMethod]
    public void Postfix_EveryVerdictOnADisabledOption_ChangesNothingAndAsksNobody()
    {
        foreach (SitOutVerdict verdict in System.Enum.GetValues(typeof(SitOutVerdict)))
        {
            var service = Install(verdict);
            ServiceField!.SetValue(null, null);
            var args = NewArgs();
            args.IsEnabled = false;
            var result = true;

            EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

            Assert.IsTrue(result, $"{verdict} must not change the result");
            Assert.IsFalse(args.IsEnabled, $"{verdict} must never re-enable a disabled option");
            Assert.IsNull(args.Tooltip, $"{verdict} must leave vanilla's tooltip on a disabled option");
            service.DidNotReceive().EvaluateSitOut();
        }
    }

    [TestMethod]
    public void Postfix_EveryVerdictOnAnEnabledOption_KeepsTheResultTrue()
    {
        foreach (SitOutVerdict verdict in System.Enum.GetValues(typeof(SitOutVerdict)))
        {
            Install(verdict);
            ServiceField!.SetValue(null, null);
            var args = NewArgs();
            var result = true;

            EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

            Assert.IsTrue(result, $"{verdict} must not hide the option");
        }
    }

    /// <summary>
    /// Installs a text manager that knows only the engine's two-line joiner (restored in cleanup). The
    /// template uses a plain separator so the tests read the variables, not a resolved string.
    /// </summary>
    private static void InstallJoiner()
    {
        var manager = new TaleWorlds.Core.GameTextManager();
        manager.AddGameText("str_string_newline_string").AddVariation("{STR1}|{STR2}");
        TextManagerField!.SetValue(null, manager);
    }

    [TestMethod]
    public void Compose_WithVanillaTooltip_PutsTaomInFirstAndVanillaInSecond()
    {
        InstallJoiner();
        var taom = new TextObject("{=taom_enlist_sitout_wounded_tip}x");
        var vanilla = new TextObject("{=vanilla_power}Power context");

        var composed = EncounterGameMenuOrderAttackConditionPatch.Compose(taom, vanilla);

        Assert.IsTrue(composed.Attributes.TryGetValue("STR1", out var first));
        Assert.IsTrue(composed.Attributes.TryGetValue("STR2", out var second));
        Assert.AreSame(taom, first, "TAOM's line goes first");
        Assert.AreSame(vanilla, second, "vanilla's tooltip is kept under it");
    }

    [DataTestMethod]
    [DataRow(SitOutVerdict.WoundedFree, "taom_enlist_sitout_wounded_tip")]
    [DataRow(SitOutVerdict.OverwhelmingCost, "taom_enlist_sitout_odds_tip")]
    public void Postfix_AllowedVerdictWithAVanillaTooltip_KeepsItUnderTaomsLine(SitOutVerdict verdict, string key)
    {
        InstallJoiner();
        Install(verdict);
        var vanilla = new TextObject("{=vanilla_power}Power context");
        var args = NewArgs();
        args.Tooltip = vanilla;
        var result = true;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

        Assert.IsTrue(args.IsEnabled);
        Assert.IsTrue(args.Tooltip.Attributes.TryGetValue("STR1", out var first));
        Assert.IsTrue(args.Tooltip.Attributes.TryGetValue("STR2", out var second));
        StringAssert.Contains(((TextObject)first).Value, "{=" + key + "}");
        Assert.AreSame(vanilla, second);
    }

    [TestMethod]
    public void Compose_NoVanillaTooltip_IsJustTheTaomLine()
    {
        var taom = new TaleWorlds.Localization.TextObject("{=taom_enlist_sitout_wounded_tip}x");

        Assert.AreSame(taom, EncounterGameMenuOrderAttackConditionPatch.Compose(taom, null));
    }

    [TestMethod]
    public void Postfix_ServiceCannotBeResolved_LeavesVanillaAndLogsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        using var container = new Container();
        container.RegisterInstance(logger); // no menu service registered, so resolving it throws
        ContainerField!.SetValue(null, container);
        var args = NewArgs();
        var result = true;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);
        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);
        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

        Assert.IsTrue(result);
        Assert.IsTrue(args.IsEnabled);
        logger.Received(1).LogError(NSubstitute.Arg.Is<string>(s => s.Contains("order_attack_on_condition")));
    }

    [TestMethod]
    public void Postfix_ServiceThrows_LeavesVanillaAndLogsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        var service = Substitute.For<IEnlistmentMenuService>();
        service.EvaluateSitOut().Returns(_ => throw new System.InvalidOperationException("engine"));
        using var container = new Container();
        container.RegisterInstance(logger);
        container.RegisterInstance(service);
        ContainerField!.SetValue(null, container);
        var args = NewArgs();
        var result = true;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);
        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

        Assert.IsTrue(result);
        Assert.IsTrue(args.IsEnabled);
        logger.Received(1).LogError(NSubstitute.Arg.Any<string>());
    }

    [TestMethod]
    public void Postfix_NoContainerAtAll_DoesNotThrow()
    {
        ContainerField!.SetValue(null, null);
        var args = NewArgs();
        var result = true;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(args, ref result);

        Assert.IsTrue(result);
        Assert.IsTrue(args.IsEnabled);
    }

    [TestMethod]
    public void Postfix_NullArgs_DoesNotThrow()
    {
        Install(SitOutVerdict.RefusedField);
        var result = true;

        EncounterGameMenuOrderAttackConditionPatch.Postfix(null!, ref result);

        Assert.IsTrue(result);
    }
}
