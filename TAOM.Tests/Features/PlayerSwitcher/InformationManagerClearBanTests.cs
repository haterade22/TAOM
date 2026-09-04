using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.PlayerSwitcher;

/// <summary>
/// Assembly-wide ban on calling <c>InformationManager.Clear()</c>.
///
/// The name is a trap. It clears no information: it is the process-teardown routine that NULLS
/// every static delegate the UI subscribes to (InformationManager.cs:140-151 on v1.4.8), among them
/// <c>OnShowTooltip</c>, <c>OnHideTooltip</c>, <c>OnShowInquiry</c>, <c>OnShowTextInquiry</c>,
/// <c>OnHideInquiry</c> and <c>DisplayMessageInternal</c>.
///
/// Those statics ARE the transport. <c>ShowTooltip</c> is nothing but
/// <c>OnShowTooltip?.Invoke(type, args)</c> (:74-77), so nulling it disables every tooltip in the
/// game at once, along with every inquiry popup and every on-screen message. A null-conditional
/// invoke throws nothing and logs nothing, so the failure is completely silent.
///
/// It is also unrecoverable in-process. <c>GauntletInformationView</c> subscribes in its private
/// constructor, and <c>Initialize()</c> only builds one when <c>_current == null</c>, which stays
/// non-null after the first construction. Nothing re-subscribes, so tooltips stay dead until the
/// game is restarted; loading another save does not help.
///
/// This shipped inside <c>PlayerIdentityAdapter.ClearPendingNotifications</c>, called on every
/// Player Switcher handover. The symptom was so far from the cause that it cost two falsified root
/// causes before this one: playing any taken-over lord (Faramir, then Denethor) gave a campaign with
/// no tooltips anywhere from the moment it loaded, while every input gate read healthy, every widget
/// tree was intact, and no log line existed anywhere. A brand new character was unaffected only
/// because <c>HeroSwitchService.Execute</c> returns early and never reaches the call.
///
/// If a future feature genuinely needs to hide queued notifications, use
/// <c>MBInformationManager.HideInformations()</c>, which is what the adapter now calls.
/// </summary>
[TestClass]
public class InformationManagerClearBanTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    // Fully qualified: TaleWorlds.Library also defines a TestContext, and this file needs that
    // namespace for InformationManager itself.
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _)
        => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void TaomAssembly_AllMethodBodies_NeverCallInformationManagerClear()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var banned = typeof(InformationManager).GetMethod(nameof(InformationManager.Clear));
        Assert.IsNotNull(banned,
            "InformationManager.Clear no longer exists on the installed engine. Re-verify the hazard, then retire this ban test.");

        var violations = IlCallScanner.FindCallers(
            typeof(TAOM.IoC).Assembly,
            target => IlCallScanner.SameMethod(target, banned),
            out var unreadable,
            out var scanned);

        if (scanned < 1000)
            Assert.Inconclusive($"Only {scanned} method bodies scanned (expected thousands): assembly enumeration problem, not a genuine pass.");

        if (violations.Count > 0 || unreadable.Count > 0)
        {
            var sb = new StringBuilder();
            if (violations.Count > 0)
            {
                sb.AppendLine($"{violations.Count} method(s) call InformationManager.Clear(), which nulls the static "
                            + "OnShowTooltip / inquiry / message delegates and silently disables every tooltip, popup "
                            + "and on-screen message in the game until it is restarted.");
                sb.AppendLine("Use MBInformationManager.HideInformations() to hide queued notifications instead:");
                foreach (var v in violations.Distinct()) sb.AppendLine("  " + v);
            }
            if (unreadable.Count > 0)
            {
                sb.AppendLine($"{unreadable.Count} method bodies could not be read, so the ban cannot vouch for them:");
                foreach (var u in unreadable.Take(20)) sb.AppendLine("  " + u);
            }
            Assert.Fail(sb.ToString());
        }
    }
}
