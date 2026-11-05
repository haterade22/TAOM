using System.Linq;
using System.Reflection;
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
/// (v1.4.8 :139-143), so a second call in the postfix would play it twice. Engine calls, so the pins read the IL.
/// The vanilla screen and <c>UISoundsHelper</c> are not in the test bin, so the class loads the game assemblies
/// first; without them a call token would not resolve and the scan would skip it.
/// </summary>
[TestClass]
public class CareerButtonClickTests
{
    private const string CommandName = "ExecuteOpenCareerScreen";
    private const string PlayUiSound = "call UISoundsHelper::PlayUISound(String)";
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

    private static MethodInfo Postfix()
    {
        var postfix = AccessTools.Method(typeof(ViewModel_ExecuteCommand_CareerScreen_Patch), "Postfix");
        Assert.IsNotNull(postfix, "The Patch27 career click postfix is gone.");
        return postfix!;
    }

    private static System.Collections.Generic.List<string> Fingerprint(MethodInfo method) =>
        IlCallScanner.Fingerprint(method, method.GetMethodBody()!.GetILAsByteArray()!).ToList();
}
