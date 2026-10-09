// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <summary>
/// Engine side of the mission-start guard: the one on-screen line, localized through TAOM's {=KEY} strings. It runs on
/// the main thread inside <c>Mission.AfterStart</c>.
/// </summary>
public sealed class MissionStartGuardAdapter : IMissionStartGuardAdapter
{
    /// <summary>One whole sentence with three variables, so each language can inflect it.</summary>
    public void ShowNotice(string module, string call, string exceptionType)
    {
        var text = new TextObject("{=taom_mission_start_guard_notice}{MODULE} failed in {CALL} ({EXCEPTION}). The mission goes on, with that step cut short.");
        text.SetTextVariable("MODULE", module);
        text.SetTextVariable("CALL", call);
        text.SetTextVariable("EXCEPTION", exceptionType);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Color.FromUint(0xFFE8A0A0)));
    }
}
