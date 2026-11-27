using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.IncidentVoices;

/// <summary>The pure part of <c>taom.incident</c>: reads the id typed and answers one line.</summary>
internal static class IncidentCommands
{
    internal const string Usage = "Usage: taom.incident <incident id>  (for example camp_fever or incident_camp_fever). Cheat mode, campaign map only.";

    internal static string Run(List<string> args, IncidentVoiceService service, IIncidentVoiceAdapter adapter)
    {
        if (args == null || args.Count != 1)
            return Usage;
        var id = service.ResolveId(args[0]);
        if (id == null)
            return "TAOM does not list an incident '" + args[0] + "'. " + Usage;
        return adapter.Queue(id, service.ClosedReason(id));
    }
}
