using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Hooks.Patch27_CareerSystem;
using TAOM.Features.CareerSystem.UI;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Issue #971: the career button's <c>Command.Click="ExecuteOpenCareerScreen"</c> reaches only the Patch27
/// postfix on <c>ViewModel.ExecuteCommand</c>, because UIExtenderEx adds a mixin method to the view model only
/// when it carries <c>[DataSourceMethod]</c>. The click sound is vanilla's: the postfix's <c>ExecuteDone</c> runs
/// <c>GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen</c>, which plays <c>event:/ui/default</c>
/// (v1.5.5 :127-131), so a second call in the postfix would play it twice. Engine calls, so the pins read the IL.
/// The vanilla screen and <c>UISoundsHelper</c> are not in the test bin, so the class loads the game assemblies
/// first; without them a call token would not resolve and the scan would skip it.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CareerButtonClickTests
{
    private const string CommandName = "ExecuteOpenCareerScreen";
    private const string PlayUiSound = "call UISoundsHelper::PlayUISound(String)";
    private const string PassCall = "call Outcomes::Pass(String,String,String)";
    private const string FailCall = "call Outcomes::Fail(String,String,String)";
    private const string TopScreen = "call ScreenManager::get_TopScreen()";
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    public void Patch27Postfix_ClosesTheDeveloperBeforeItOpensTheCareerScreen()
    {
        // gui-sprite-system.md: closing first prevents the GameState.HandleInitialize NullRef and the map bar input crash.
        RequireGame();
        var tokens = Fingerprint(Postfix());

        var close = tokens.IndexOf("callvirt CharacterDeveloperVM::ExecuteDone()");
        var open = tokens.IndexOf("call GauntletCareerScreen::OpenCareerScreen(Boolean)");
        Assert.IsTrue(close >= 0, "The postfix must close the character developer: " + string.Join(" | ", tokens));
        Assert.IsTrue(open > close, "The developer must close before the career screen opens: " + string.Join(" | ", tokens));
        CollectionAssert.Contains(tokens, "ldstr \"" + CommandName + "\"");
    }

    [TestMethod]
    public void Patch27Postfix_PlaysNoSoundOfItsOwn()
    {
        // ExecuteDone already plays the click; a call here would double it.
        RequireGame();
        CollectionAssert.DoesNotContain(Fingerprint(Postfix()), PlayUiSound);
    }

    [TestMethod]
    public void VanillaDeveloperClose_PlaysTheDefaultUiClick()
    {
        // The click sound's only source; an engine update that drops it would leave the button silent.
        RequireGame();
        var screen = AccessTools.TypeByName("SandBox.GauntletUI.GauntletCharacterDeveloperScreen");
        Assert.IsNotNull(screen, "SandBox.GauntletUI.GauntletCharacterDeveloperScreen did not resolve.");
        var close = AccessTools.Method(screen, "CloseCharacterDeveloperScreen");
        Assert.IsNotNull(close, "GauntletCharacterDeveloperScreen.CloseCharacterDeveloperScreen is gone.");
        var tokens = Fingerprint(close!);

        var sound = tokens.IndexOf(PlayUiSound);
        Assert.IsTrue(sound > 0, "Vanilla's close no longer plays a sound: " + string.Join(" | ", tokens));
        Assert.AreEqual("ldstr \"event:/ui/default\"", tokens[sound - 1], string.Join(" | ", tokens));
    }

    [TestMethod]
    public void CareerMixin_DeclaresNoClickHandler()
    {
        // A second handler that UIExtenderEx never adds is dead code that hides where the click is handled.
        var mixin = typeof(CareerButtonPrefab).Assembly.GetType("TAOM.Features.CareerSystem.UI.CharacterDeveloperCareerMixin");
        Assert.IsNotNull(mixin, "CharacterDeveloperCareerMixin moved; update this pin.");
        var handler = mixin!.GetMethod(CommandName,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNull(handler, "The mixin's " + CommandName + " is never added to the view model; Patch27 handles the click.");
    }

    [TestMethod]
    public void Patch27Postfix_GuardsTheCloseAndTheOpen_AndItsCatchFailsThenRethrows()
    {
        // #971 (#1072): a click that throws writes its open fail line, and the exception still propagates as before.
        RequireGame();
        var postfix = Postfix();
        var clauses = postfix.GetMethodBody()!.ExceptionHandlingClauses;
        Assert.AreEqual(1, clauses.Count, "The postfix must have exactly one try/catch.");
        var clause = clauses[0];
        Assert.AreEqual(ExceptionHandlingClauseOptions.Clause, clause.Flags);
        Assert.AreEqual(typeof(Exception), clause.CatchType);

        var guarded = Fingerprint(postfix, clause.TryOffset, clause.TryLength);
        var close = guarded.IndexOf("callvirt CharacterDeveloperVM::ExecuteDone()");
        var open = guarded.IndexOf("call GauntletCareerScreen::OpenCareerScreen(Boolean)");
        Assert.IsTrue(close >= 0 && open > close, "The try must hold the close, then the open: " + string.Join(" | ", guarded));

        var handler = Fingerprint(postfix, clause.HandlerOffset, clause.HandlerLength);
        CollectionAssert.Contains(handler, FailCall, string.Join(" | ", handler));
        var ops = IlCallScanner.ExtractOpCodes(Slice(postfix, clause.HandlerOffset, clause.HandlerLength)).ToList();
        CollectionAssert.Contains(ops, OpCodes.Rethrow, "The catch must rethrow with throw;");
        CollectionAssert.DoesNotContain(ops, OpCodes.Throw, "throw ex; loses the stack trace; use throw;");
    }

    [TestMethod]
    public void Patch27Postfix_ReportsTheOpenAfterTheTry_FromTheTopScreen()
    {
        // OpenCareerScreen can return with no screen pushed (no main hero), so the pass reads the top screen.
        RequireGame();
        var postfix = Postfix();
        var tokens = Fingerprint(postfix);
        var open = tokens.IndexOf("call GauntletCareerScreen::OpenCareerScreen(Boolean)");
        var top = tokens.IndexOf(TopScreen);
        var pass = tokens.IndexOf(PassCall);
        Assert.IsTrue(open >= 0 && top > open, "The top screen is read after the open: " + string.Join(" | ", tokens));
        Assert.IsTrue(pass > top, "The pass follows the top screen test: " + string.Join(" | ", tokens));
        // The fingerprint drops type operands, so the `is GauntletCareerScreen` test is read from the type tokens.
        CollectionAssert.Contains(IlCallScanner.ExtractReferencedMembers(postfix, postfix.GetMethodBody()!.GetILAsByteArray()!)
            .OfType<Type>().ToList(), typeof(GauntletCareerScreen), "The pass must test the top screen's type, not just that one exists.");
        // The catch handler comes before the after-try code in IL, so the last fail is the not-on-top one.
        Assert.IsTrue(tokens.LastIndexOf(FailCall) > top, "A top screen that is not the career screen must fail: " + string.Join(" | ", tokens));
        CollectionAssert.Contains(tokens, "ldstr \"taom.career-screen.open\"");
        CollectionAssert.Contains(tokens, "ldstr \"source=button\"");

        var clause = postfix.GetMethodBody()!.ExceptionHandlingClauses[0];
        CollectionAssert.DoesNotContain(Fingerprint(postfix, clause.TryOffset, clause.TryLength), PassCall,
            "The pass belongs after the try, once the open returned.");
    }

    [TestMethod]
    public void CareerScreenOnInitialize_ReportsInitAfterTheFocus()
    {
        RequireGame();
        var init = AccessTools.Method(typeof(GauntletCareerScreen), "OnInitialize");
        Assert.IsNotNull(init, "GauntletCareerScreen.OnInitialize is gone.");
        Assert.AreEqual(typeof(GauntletCareerScreen), init!.DeclaringType);
        var tokens = Fingerprint(init);

        var focus = tokens.IndexOf("call ScreenManager::TrySetFocus(ScreenLayer)");
        var pass = tokens.IndexOf(PassCall);
        Assert.IsTrue(focus >= 0 && pass > focus, "The init line is the last thing OnInitialize does: " + string.Join(" | ", tokens));
        CollectionAssert.Contains(tokens, "ldstr \"taom.career-screen.init\"");
        var mode = tokens.IndexOf("call CareerScreenChecks::Mode(Boolean)");
        var detail = tokens.IndexOf("call CareerScreenChecks::InitDetail(Boolean)");
        Assert.IsTrue(mode >= 0 && mode < pass, "The latch key comes from Mode: " + string.Join(" | ", tokens));
        Assert.IsTrue(detail >= 0 && detail < pass, "The detail comes from InitDetail: " + string.Join(" | ", tokens));
    }

    private static MethodInfo Postfix()
    {
        var postfix = AccessTools.Method(typeof(ViewModel_ExecuteCommand_CareerScreen_Patch), "Postfix");
        Assert.IsNotNull(postfix, "The Patch27 career click postfix is gone.");
        return postfix!;
    }

    private static List<string> Fingerprint(MethodInfo method) =>
        IlCallScanner.Fingerprint(method, method.GetMethodBody()!.GetILAsByteArray()!).ToList();

    // One try or handler block of the body, so a pin can say which side of the catch a call is on.
    private static List<string> Fingerprint(MethodInfo method, int offset, int length) =>
        IlCallScanner.Fingerprint(method, Slice(method, offset, length)).ToList();

    private static byte[] Slice(MethodInfo method, int offset, int length)
    {
        var slice = new byte[length];
        Array.Copy(method.GetMethodBody()!.GetILAsByteArray()!, offset, slice, 0, length);
        return slice;
    }
}
