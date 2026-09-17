namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>
/// The four Phase B doctrines as weight tables over vanilla behaviours. Read beside the engine:
/// a formation's <c>FormationAI</c> picks the behaviour with the highest
/// <c>GetAIWeight() * WeightFactor</c> every 0.5 s, so a weight here is a preference among
/// behaviours whose own weights already encode distance, power and terrain. What each behaviour
/// does with arrangement is what the doctrine actually looks like in the field:
/// <c>Defend</c> goes ShieldWall at its position when the formation has shields,
/// <c>Advance</c> goes ShieldWall under fire, <c>DefensiveRing</c> forms a Circle sized around
/// the archers' <c>FireFromInfantryCover</c> Square, <c>Vanguard</c> rides ahead of the infantry.
/// </summary>
public static class DoctrinePlans
{
    private static BehaviorWeight W(BehaviorKind kind, float weight) => new BehaviorWeight(kind, weight);

    private static FormationPlan FlankGuard(FormationRole role) =>
        new FormationPlan(role, W(BehaviorKind.ProtectFlank, 1f), W(BehaviorKind.CavalryScreen, 1f));

    private static FormationPlan HorseArchers() =>
        new FormationPlan(FormationRole.RangedCavalry, W(BehaviorKind.MountedSkirmish, 1f), W(BehaviorKind.HorseArcherSkirmish, 1f));

    /// <summary>Dwarves defending: the infantry walks to the navmesh high ground and stands in a
    /// wall (<c>BehaviorDefend</c> with the high ground as its position); cavalry guards the
    /// flanks and never goes hunting; once the battle is joined the wall still holds and only
    /// counter-charges when the engine's own charge weight is high.</summary>
    public static readonly DoctrinePlan ShieldWallDefender = new DoctrinePlan(
        "ShieldWallDefender", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Defend, 1f), W(BehaviorKind.TacticalCharge, 0.3f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Defend, 1f), W(BehaviorKind.TacticalCharge, 0.6f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.Skirmish, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()));

    /// <summary>Dwarves attacking: the infantry advances as one body (<c>BehaviorAdvance</c>,
    /// ShieldWall under fire), archers screened behind it, cavalry on the flanks; once joined the
    /// wall commits harder and the cavalry may take a flank that opens.</summary>
    public static readonly DoctrinePlan ShieldWallAttacker = new DoctrinePlan(
        "ShieldWallAttacker", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.TacticalCharge, 0.5f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.TacticalCharge, 0.8f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f)),
            new FormationPlan(FormationRole.LeftCavalry, W(BehaviorKind.ProtectFlank, 1f), W(BehaviorKind.CavalryScreen, 1f), W(BehaviorKind.Flank, 0.5f)),
            new FormationPlan(FormationRole.RightCavalry, W(BehaviorKind.ProtectFlank, 1f), W(BehaviorKind.CavalryScreen, 1f), W(BehaviorKind.Flank, 0.5f)),
            HorseArchers()));

    /// <summary>Mordor, Gundabad, Isengard: no cautious phase. The infantry comes on from the first
    /// tick, <c>Charge</c> weighted above <c>TacticalCharge</c> so it is a mass rather than a
    /// line; archers shoot without a screen; cavalry charges and flanks.</summary>
    public static readonly DoctrinePlan InfantryMass = new DoctrinePlan(
        "InfantryMass", FormationSplit.OneOneTwoOne, alwaysEngaged: true, battleJoinedSeconds: 5f,
        defend: Engaged(),
        engage: Engaged());

    private static PhasePlan Engaged() => new PhasePlan(
        new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Charge, 1.5f), W(BehaviorKind.TacticalCharge, 1f)),
        new FormationPlan(FormationRole.Archers, W(BehaviorKind.Skirmish, 1f), W(BehaviorKind.Charge, 0.3f)),
        new FormationPlan(FormationRole.LeftCavalry, W(BehaviorKind.TacticalCharge, 1f), W(BehaviorKind.Flank, 1f)),
        new FormationPlan(FormationRole.RightCavalry, W(BehaviorKind.TacticalCharge, 1f), W(BehaviorKind.Flank, 1f)),
        HorseArchers());

    /// <summary>Rohan: one cavalry block leads (<c>Vanguard</c> ahead of the infantry) with the
    /// engine's 7 s cavalry join threshold; once joined it charges through and flanks while the
    /// infantry advances behind it.</summary>
    public static readonly DoctrinePlan CavalryDominance = new DoctrinePlan(
        "CavalryDominance", FormationSplit.OneOneOneOne, alwaysEngaged: false, battleJoinedSeconds: 7f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f)),
            new FormationPlan(FormationRole.Cavalry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.Vanguard, 1f)),
            HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.TacticalCharge, 1f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f)),
            new FormationPlan(FormationRole.Cavalry, W(BehaviorKind.TacticalCharge, 1.2f), W(BehaviorKind.Flank, 1f)),
            HorseArchers()));

    /// <summary>Elves defending: the infantry rings the archers on the high ground
    /// (<c>DefensiveRing</c> around the <c>FireFromInfantryCover</c> Square), cavalry guards the
    /// flanks. The ring holds in both phases; the weight function, not the plan, decides when a
    /// ring is no longer a defence.</summary>
    public static readonly DoctrinePlan ArcherRing = new DoctrinePlan(
        "ArcherRing", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: Ring(),
        engage: Ring(),
        // A circle around a square settles slower than a line, and two formations must arrive.
        race: new RaceTunables(formUpSeconds: 10f, formUpSecondsPerUnit: 0.04f, marginSeconds: 4f));

    private static PhasePlan Ring() => new PhasePlan(
        new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.DefensiveRing, 1f), W(BehaviorKind.TacticalCharge, 0.3f)),
        new FormationPlan(FormationRole.Archers, W(BehaviorKind.FireFromInfantryCover, 1f), W(BehaviorKind.Skirmish, 0.5f)),
        FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers());
}
