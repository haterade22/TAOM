using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Composition;
using TAOM.Features.CheatMenu;
using TAOM.Features.CheatMenu.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CheatMenu;

/// <summary>
/// The cheat menu's wiring: the map button is appended only while the menu is shown (Patch151, pure part), the module is
/// registered with its patch category and behavior, and the menu buttons and the console commands call the same service
/// methods (source pins, comments stripped).
/// </summary>
[TestClass]
public class CheatMenuWiringTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    private static INavigationElement Fake(string id)
    {
        var element = Substitute.For<INavigationElement>();
        element.StringId.Returns(id);
        return element;
    }

    private static INavigationElement[] Vanilla() => new[] { Fake("escape_menu"), Fake("party"), Fake("taom_fiefs") };

    [TestMethod]
    public void AppendWhenShown_Shown_AppendsOneAtTheEnd()
    {
        var vanilla = Vanilla();
        var ours = Fake(CheatMenuIds.NavigationElementId);

        var result = Patch151_CheatMenuNavigation.AppendWhenShown(vanilla, shown: true, () => ours);

        Assert.AreEqual(4, result.Length);
        Assert.AreSame(ours, result[3]);
        CollectionAssert.AreEqual(vanilla, result.Take(3).ToArray());
    }

    [TestMethod]
    public void AppendWhenShown_NotShown_LeavesTheBarAndNeverBuildsTheButton()
    {
        var vanilla = Vanilla();
        var built = false;

        var result = Patch151_CheatMenuNavigation.AppendWhenShown(vanilla, shown: false, () => { built = true; return Fake("x"); });

        Assert.AreSame(vanilla, result);
        Assert.IsFalse(built);
    }

    [TestMethod]
    public void AppendWhenShown_AlreadyThereOrNoButton_Unchanged()
    {
        var withOurs = Vanilla().Concat(new[] { Fake(CheatMenuIds.NavigationElementId) }).ToArray();

        Assert.AreSame(withOurs, Patch151_CheatMenuNavigation.AppendWhenShown(withOurs, true, () => Fake(CheatMenuIds.NavigationElementId)));
        var vanilla = Vanilla();
        Assert.AreSame(vanilla, Patch151_CheatMenuNavigation.AppendWhenShown(vanilla, true, () => null));
    }

    [TestMethod]
    public void Module_IsRegisteredWithItsCategoryAndBehavior()
    {
        var module = FeatureModules.All.OfType<CheatMenuModule>().Single();

        CollectionAssert.AreEqual(new[] { "Patch151_CheatMenu", "Patch152_BattleCheatMenu" },
            module.PatchCategories.Select(d => d.Category).ToArray());
        Assert.IsTrue(module.PatchCategories.All(d => d.Phase == ApplyPhase.GameInit));
        Assert.AreEqual(typeof(CheatMenuCampaignBehavior), module.CampaignBehaviors.Single().BehaviorType);
    }

    [TestMethod]
    public void MenuButtons_CallTheService()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuMenus.cs", stripComments: true);

        foreach (var call in new[]
                 {
                     "service.GiveGold(GoldStep)", "service.AddSpecialResource(ResourceStep)", "service.AddInfluence(InfluenceStep)",
                     "service.AddRenown(RenownStep)", "service.AddRelation(id, RelationStep)", "service.Heal()",
                     "service.AddTroops(elite: false, TroopStep)", "service.AddTroops(elite: true, TroopStep)",
                     "service.AddFactionTroop(id, TroopStep)", "service.OpenCheatPartyScreen()",
                 })
            StringAssert.Contains(src, call);
    }

    [TestMethod]
    public void EveryCheatButton_RunsInsideTheGuard()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuMenus.cs", stripComments: true);

        StringAssert.Contains(src, "_ => Guarded(id, act), isLeave: false");
        Assert.IsFalse(Regex.IsMatch(src, @"_ => act\(\), isLeave: false"), "a cheat button outside the guard");
    }

    [TestMethod]
    public void EveryPickListChoice_RunsInsideTheGuard()
    {
        // The teleport and relation cheats run when the player picks from the list, not on the menu click.
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuMenus.cs", stripComments: true);

        // logged under the option id, as the buttons are, not under the TextObject title (#1297)
        StringAssert.Matches(src, new Regex(@"affirmativeAction: chosen =>\s*Guarded\(optionId, \(\) =>"));
        Assert.AreEqual(3, Regex.Matches(src, @"Pick\(""taom_cheat_opt_relation""|Pick\(""taom_cheat_troops_any""|Pick\(id, ").Count, "every Pick call passes its option id");
        Assert.IsFalse(Regex.IsMatch(src, @"affirmativeAction: chosen =>\s*\{"), "a pick list choice outside the guard");
    }

    // MBTextManager.SetTextVariable is global: any other text with a {STATE} tag would read the switch's word. The option's own
    // TextObject is args.Text (GameMenuOption.GetConditionsHold builds MenuCallbackArgs with its Text, v1.5.7).
    [TestMethod]
    public void UnlimitedPartyLabel_SetsItsStateOnItsOwnText()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuMenus.cs", stripComments: true);

        StringAssert.Contains(src, "args.Text.SetTextVariable(\"STATE\"");
        Assert.IsFalse(src.Contains("MBTextManager.SetTextVariable(\"STATE\""), "the global text variable leaks into other texts");
    }

    // Low-morale desertion still applies to the main party with the switch on; only the culture-based desertion is skipped.
    [TestMethod]
    public void UnlimitedPartyTexts_SayEnemyTroopsKeepOnlyTheCultureRule()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuMenus.cs", stripComments: true);

        foreach (var key in new[] { "taom_cheat_unlimited_party_hint", "taom_cheat_unlimited_party_on" })
        {
            var literal = Regex.Match(src, @"""\{=" + key + @"\}(?<text>[^""]*)""");
            Assert.IsTrue(literal.Success, key + " is gone");
            StringAssert.Contains(literal.Groups["text"].Value, "do not desert because of their culture", key);
        }
    }

    [TestMethod]
    public void Guarded_AThrowingCheat_DoesNotEscapeTheMenuClick() =>
        CheatMenuMenus.Guarded("test", () => throw new System.InvalidOperationException("boom"));

    [TestMethod]
    public void TeleportButton_ClosesTheMenuBeforeTheMove()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuMenus.cs", stripComments: true);

        StringAssert.Matches(src, new Regex(@"GameMenu\.ExitToLast\(\);\s*CheatMenuTexts\.Show\(service\.Teleport\(id\)\);"));
    }

    [TestMethod]
    public void ConsoleCommands_CallTheSameService()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Cheats/CheatMenuCheats.cs", stripComments: true);

        foreach (var call in new[]
                 {
                     "Service().GiveGold(n)", "Service().AddInfluence(n)", "Service().AddRenown(n)", "Service().AddTroops(elite, n)",
                     "Service().Heal()", "Service().AddRelation(kingdom, n)", "Service().Teleport(args[0])",
                 })
            StringAssert.Contains(src, call);
    }

    [TestMethod]
    public void Behavior_RegistersTheMenusAtSessionLaunch()
    {
        var src = RepoPaths.ReadSource("Main/Features/CheatMenu/Hooks/CheatMenuCampaignBehavior.cs", stripComments: true);

        StringAssert.Contains(src, "CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched)");
        StringAssert.Contains(src, "CheatMenuMenus.Register(starter, _service, _settings, _world)");
    }
}
