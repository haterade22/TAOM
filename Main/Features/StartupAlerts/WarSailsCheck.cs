using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.StartupAlerts;

/// <summary>
/// TAOM does not support War Sails (module id NavalDLC). Main/_Module/SubModule.xml lists it under
/// IncompatibleModules, which the vanilla launcher honours, but BLSE or a hand-edited module list can
/// still load both. This check reads the modules the game actually loaded, not what is installed:
/// War Sails ships with the game for every owner of the DLC.
/// </summary>
public static class WarSailsCheck
{
    public const string WarSailsModuleId = "NavalDLC";

    public static bool IsLoaded(IEnumerable<string> loadedModuleIds) =>
        loadedModuleIds.Any(id => string.Equals(id, WarSailsModuleId, StringComparison.OrdinalIgnoreCase));
}
