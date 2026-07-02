using System;
using System.Collections.Generic;
using TAOM.Core.Domain;

namespace TAOM.Features.HeroRace;

/// <summary>
/// Implements <see cref="IBasicTableauRaceGuard"/> via an allow-list of races the agentless
/// <c>BasicCharacterTableau</c> static-morph build is known to render without access-violating.
///
/// Safety is EMPIRICAL, per race: a race joins <see cref="TableauSafeRaceNames"/> only after an
/// in-game Load Game render test with the coercion disabled (uruk verified 2026-07-02). Dwarf is
/// verified UNSAFE — issue #295 recorded the native failure ("No morph data found for face mesh",
/// dwarf head.eye = 0 morph targets) — so it and every unverified race coerce to the human base
/// until verified or until head morph data is authored asset-side.
///
/// Names, not ints: race ids are skins.xml merge-order indices assigned at load, so they shift
/// with the module set. The id is resolved per call via <see cref="IRaceManager"/>, whose data is
/// the native FaceGen race table — available on the cold main menu because the engine's
/// OnLoadCommonFinished callback runs <c>FaceGen.CreateInstance()</c> before the initial screen.
/// Any resolution failure fails safe to the human base: worst case is a human-headed thumbnail,
/// never a CTD.
/// </summary>
public class BasicTableauRaceGuard : IBasicTableauRaceGuard
{
    /// <summary>The vanilla human race id (the engine's default base; see EyeHeightAdjustmentHook).</summary>
    public const int HumanBaseRace = 0;

    private static readonly HashSet<string> TableauSafeRaceNames =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "uruk" };

    private readonly IRaceManager _raceManager;

    public BasicTableauRaceGuard(IRaceManager raceManager) => _raceManager = raceManager;

    public int ResolveSafeRace(int race)
    {
        if (race == HumanBaseRace)
            return HumanBaseRace;

        try
        {
            // Validate before lookup: GetRaceNameFromId falls back to "human" for unknown ids;
            // an invalid id must coerce, not ride the fallback (csharp-architecture.md rule).
            if (!_raceManager.IsValidRaceId(race))
                return HumanBaseRace;

            return TableauSafeRaceNames.Contains(_raceManager.GetRaceNameFromId(race))
                ? race
                : HumanBaseRace;
        }
        catch (Exception)
        {
            // A throw here would propagate into the cold-menu render tick — the exact crash
            // class this guard exists to prevent. The human base is always renderable.
            return HumanBaseRace;
        }
    }
}
