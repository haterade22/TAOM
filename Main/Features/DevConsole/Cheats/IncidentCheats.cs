using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.IncidentVoices;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// <c>taom.incident &lt;id&gt;</c> (cheat mode, campaign map) queues one of the 101 vanilla incidents, typed as <c>incident_camp_fever</c> or
/// <c>camp_fever</c>, for the next campaign tick, so a person can see its Middle-earth text. TAOM's own gates apply: an incident closed
/// for your people says why, and vanilla's own condition (a town, a battle, a skill) must hold where you stand. It only routes to
/// <see cref="IncidentCommands"/> (docs/features/incident-voices.md).
/// </summary>
public static class IncidentCheats
{
    [CommandLineFunctionality.CommandLineArgumentFunction("incident", "taom")]
    public static string Incident(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, IncidentCommands.Usage, args =>
            IncidentCommands.Run(args, IoC.Resolve<IncidentVoiceService>(), IoC.Resolve<IIncidentVoiceAdapter>()));
}
