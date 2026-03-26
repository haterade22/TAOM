using System;

namespace TAOM.Features.AtmospherePersistence;

public static class AtmosphereOverrideService
{
    private const string ForceAtmoMarker = "forceatmo";

    public static bool RequiresAtmosphereOverride(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        return sceneName.IndexOf(ForceAtmoMarker, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
