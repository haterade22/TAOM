using System;

namespace TAOM.Features.CareerSystem.UI;

// The [Check] lines for #971 (#1072): the career button opens the career screen, and the screen initialises.
// tools/ingame_checks/971.toml reads them.
internal static class CareerScreenChecks
{
    internal const string OpenId = "taom.career-screen.open";
    internal const string InitId = "taom.career-screen.init";
    internal const string OpenPassDetail = "source=button";
    internal const string NotOnTop = "not-on-top";

    // A throw in the screen's constructor arrives wrapped by Activator.CreateInstance, so name the innermost type.
    internal static string FailReason(Exception ex) => ex.GetBaseException().GetType().Name;

    internal static string OpenFailDetail(string reason) => "source=button reason=" + reason;

    // The dialogue's career switch opens the screen in switch mode; only the button opens it in normal mode.
    internal static string Mode(bool switchMode) => switchMode ? "switch" : "normal";

    internal static string InitDetail(bool switchMode) => "mode=" + Mode(switchMode);
}
