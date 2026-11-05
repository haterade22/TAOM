using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.SaveLoadDiagnostics.Cheats;

/// <summary>
/// Asks for a per-type census on the next save (plan 114). The census already runs by itself on the
/// first save after any reload in a session; this is for a baseline before the first reload.
///
/// SHAPE IS LOAD-BEARING, NOT STYLE: a [CommandLineArgumentFunction] whose signature is not exactly
/// `public static string Name(List&lt;string&gt;)` throws inside the engine's unguarded discovery loop,
/// and this class has no static field initializers beyond a const, because anything touching engine
/// or IoC state at type-init time runs during that same pass. Pinned by ConsoleCommandBindingTests.
/// </summary>
public static class SaveCensusCheats
{
    private const string Usage =
        "Format is \"taom.arm_save_census\".\n"
        + "Asks for a per-type census of the next save, written to taom_debug as [SaveLoad] phase=SaveCensus.";

    [CommandLineFunctionality.CommandLineArgumentFunction("arm_save_census", "taom")]
    public static string ArmSaveCensus(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, _ =>
        {
            var wasArmed = IoC.Resolve<ISaveLoadDiagnosticsService>().ArmSaveCensus();
            return $"Save census armed ({(wasArmed ? "was armed" : "was idle")}). Save the game, then read [SaveLoad] phase=SaveCensus in taom_debug.";
        });
}
