using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Dependencies.Foundation;

/// <summary>Why TAOM is missing from a game that loaded its companion modules.</summary>
public enum MissingTaomReason
{
    /// <summary>TAOM loaded, or none of its companion modules did: nothing to say.</summary>
    None,

    /// <summary>War Sails (NavalDLC) is loaded. TAOM's SubModule.xml lists it under IncompatibleModules, so the launcher dropped TAOM.</summary>
    WarSails,

    /// <summary>TAOM is not ticked in the launcher, or it failed to load.</summary>
    NotLoaded,
}

/// <summary>
/// With War Sails ticked, the v1.5.7 launcher honours TAOM's IncompatibleModules entry and drops TAOM itself, but
/// still starts TAOM.Dependencies, TAOM_Map and the Armory (owner's start 2026-11-15 20:31, rgl_log line 61). TAOM's
/// own War Sails alert (Main/Features/StartupAlerts) never runs then, and the player gets the plain War Sails menu.
/// This module loads either way, so it is the one place that can say why. Reads the modules the game actually
/// loaded, not what is installed.
/// </summary>
public static class MissingTaomCheck
{
    public const string TaomModuleId = "TAOM";
    public const string MapModuleId = "TAOM_Map";
    public const string DependenciesModuleId = "TAOM.Dependencies";
    public const string WarSailsModuleId = "NavalDLC";

    public static MissingTaomReason Evaluate(IEnumerable<string> loadedModuleIds)
    {
        var loaded = new HashSet<string>(loadedModuleIds ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (loaded.Contains(TaomModuleId)) return MissingTaomReason.None;
        if (!loaded.Contains(MapModuleId) && !loaded.Contains(DependenciesModuleId)) return MissingTaomReason.None;
        return loaded.Contains(WarSailsModuleId) ? MissingTaomReason.WarSails : MissingTaomReason.NotLoaded;
    }
}
