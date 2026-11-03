using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Features.DevConsole;

namespace TAOM.Features.LongDefeat.Spike;

/// <summary>The spike's console command (#1079). Start it from the main menu; the tagged log lines are the result.</summary>
public static class LongDefeatCheats
{
    private const int MaxCount = 1000;

    private const string StartUsage =
        "Format is \"taom.ld_start <helms|minas> [waves] [wave size]\".\n"
        + "Starts the Long Defeat spike from the main menu. Default: 3 waves of 120. At 100 or fewer the attackers leave their siege engines.\n"
        + "Example: taom.ld_start helms 5 150";

    [CommandLineFunctionality.CommandLineArgumentFunction("ld_start", "taom")]
    public static string LdStart(List<string> strings) =>
        TaomConsole.RunAtMainMenu(strings, StartUsage, args =>
        {
            if (args.Count == 0 || LongDefeatSpikeFortress.For(args[0]) == null)
                return StartUsage;
            var options = new LongDefeatSpikeOptions { Fortress = args[0], DefendersAi = false };
            if (args.Count > 1)
            {
                if (!DevConsoleArgs.TryParseCount(args[1], 1, MaxCount, out var waves, out var error))
                    return error;
                options.Waves = waves;
            }

            if (args.Count > 2)
            {
                if (!DevConsoleArgs.TryParseCount(args[2], 1, MaxCount, out var size, out var error))
                    return error;
                options.WaveSize = size;
            }

            var manager = IoC.Resolve<ILongDefeatGameManagerFactory>().Create(options);
            if (manager == null)
                return "The client is not loaded, so nothing can start.";
            MBGameManager.StartNewGame(manager);
            return $"Starting: {options}";
        });
}
