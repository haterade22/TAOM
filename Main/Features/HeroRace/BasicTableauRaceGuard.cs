using System.Collections.Generic;

namespace TAOM.Features.HeroRace;

/// <summary>
/// Implements <see cref="IBasicTableauRaceGuard"/> via an allow-list of races whose head meshes
/// carry the morph data the agentless <c>BasicCharacterTableau</c> static-morph build requires.
/// Today only the vanilla human base qualifies — every TAOM custom-race head lacks the data
/// (issue #295), so they would AV in the Save/Load hero preview. When the head morph data is
/// authored and the head tpac recompiled for a race, add its id to <see cref="TableauSafeRaces"/>
/// to restore a true-to-race preview for it.
/// </summary>
public class BasicTableauRaceGuard : IBasicTableauRaceGuard
{
    /// <summary>The vanilla human race id (the engine's default base; see EyeHeightAdjustmentHook).</summary>
    public const int HumanBaseRace = 0;

    private static readonly HashSet<int> TableauSafeRaces = new HashSet<int> { HumanBaseRace };

    // TEMP-URUK-TABLEAU-TEST: pass-through so the Load Game preview feeds the REAL race to the
    // agentless native build — empirically decides whether uruk heads AV like dwarf heads (#295)
    // or render fine (race-correct-preview plan, decision gate A vs B). DO NOT COMMIT.
    public int ResolveSafeRace(int race) => race;

    private int ResolveSafeRaceProduction(int race) =>
        TableauSafeRaces.Contains(race) ? race : HumanBaseRace;
}
