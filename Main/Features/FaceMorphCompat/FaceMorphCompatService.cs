using System;
using System.Collections.Generic;
using TAOM.Core.Domain;

namespace TAOM.Features.FaceMorphCompat;

public class FaceMorphCompatService : IFaceMorphCompatService
{
    // The only races whose heads are the complete vanilla meshes (head_male_a / head_female_a) with
    // full static-morph data. Every other race added in LOTRLOME skins.xml uses a custom head mesh
    // that can crash the CPU/static-morph path. Extend THIS set (never the inverse) if a future
    // custom race ships complete static-morph data and you want to keep its crowds on the cheaper
    // static path — over-forcing GPU morph is harmless, under-forcing risks the native AV.
    private static readonly HashSet<string> VanillaMeshRaces =
        new(StringComparer.OrdinalIgnoreCase) { "human" };

    private readonly IRaceManager _raceManager;

    public FaceMorphCompatService(IRaceManager raceManager)
    {
        _raceManager = raceManager;
    }

    public bool ShouldForceGpuMorph(int raceId)
    {
        // Validate-before-lookup (.claude/rules/csharp-architecture.md): GetRaceNameFromId coerces an
        // unknown id to the "human" fallback. We must NOT let that mask an invalid id into the
        // not-forced (human) branch — an unrecognized race is treated as potentially-custom and forced
        // onto the safe GPU path. Forcing is harmless; failing to force risks the native AV.
        if (!_raceManager.IsValidRaceId(raceId))
            return true;

        var name = _raceManager.GetRaceNameFromId(raceId);
        return !VanillaMeshRaces.Contains(name);
    }
}
