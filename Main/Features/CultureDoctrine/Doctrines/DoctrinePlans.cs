namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>
/// The eleven TAOM doctrines as weight tables over behaviours. Read beside the engine: a
/// formation's <c>FormationAI</c> picks the behaviour with the highest
/// <c>GetAIWeight() * WeightFactor</c> every 0.5 s, so a weight here is a preference among
/// behaviours whose own weights already encode distance, power and terrain. What each behaviour
/// does with arrangement is what the doctrine actually looks like in the field:
/// <c>BracedDefend</c> goes ShieldWall at its position and Square under a cavalry charge,
/// <c>BracedAdvance</c> the same on the march, <c>DefensiveRing</c> forms a Circle sized around
/// the archers' <c>FireFromInfantryCover</c> Square, <c>Vanguard</c> rides ahead of the
/// infantry, <c>CycleCharge</c> charges through, reforms and goes again, <c>EnvelopWing</c>
/// walks round the enemy's flank, <c>InfantrySkirmish</c> throws and falls back.
/// </summary>
public static class DoctrinePlans
{
    private static BehaviorWeight W(BehaviorKind kind, float weight) => new BehaviorWeight(kind, weight);

    private static FormationPlan FlankGuard(FormationRole role) =>
        new FormationPlan(role, W(BehaviorKind.ProtectFlank, 1f), W(BehaviorKind.CavalryScreen, 1f));

    private static FormationPlan HorseArchers() =>
        new FormationPlan(FormationRole.RangedCavalry, W(BehaviorKind.MountedSkirmish, 1f), W(BehaviorKind.HorseArcherSkirmish, 1f));

    private static FormationPlan ScreenedArchers() =>
        new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f));

    /// <summary>Elven volley control: loose inside 80% of the average adjusted range, hold again
    /// once the target is back beyond 95%.</summary>
    public static readonly VolleyTunables ElvenVolley = new VolleyTunables(releaseFraction: 0.8f, holdFraction: 0.95f);

    /// <summary>Dwarves defending: the infantry walks to the position the high-ground race
    /// picks and stands in a wall that squares up against horse (<c>BracedDefend</c>); cavalry
    /// guards the flanks and never goes hunting; once the battle is joined the wall still holds
    /// and only counter-charges when the engine's own charge weight is high.</summary>
    public static readonly DoctrinePlan ShieldWallDefender = new DoctrinePlan(
        "ShieldWallDefender", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.BracedDefend, 1f), W(BehaviorKind.TacticalCharge, 0.3f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.BracedDefend, 1f), W(BehaviorKind.TacticalCharge, 0.6f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.Skirmish, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()));

    /// <summary>Dwarves attacking: the infantry advances as one body (<c>BracedAdvance</c>,
    /// ShieldWall under fire, Square under horse), archers screened behind it, cavalry on the
    /// flanks; once joined the wall commits harder and the cavalry may take a flank that opens.</summary>
    public static readonly DoctrinePlan ShieldWallAttacker = new DoctrinePlan(
        "ShieldWallAttacker", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.BracedAdvance, 1f), W(BehaviorKind.TacticalCharge, 0.5f)),
            ScreenedArchers(),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.BracedAdvance, 1f), W(BehaviorKind.TacticalCharge, 0.8f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f)),
            new FormationPlan(FormationRole.LeftCavalry, W(BehaviorKind.ProtectFlank, 1f), W(BehaviorKind.CavalryScreen, 1f), W(BehaviorKind.Flank, 0.5f)),
            new FormationPlan(FormationRole.RightCavalry, W(BehaviorKind.ProtectFlank, 1f), W(BehaviorKind.CavalryScreen, 1f), W(BehaviorKind.Flank, 0.5f)),
            HorseArchers()));

    /// <summary>Dwarves defending with the men for two lines: the front stands as the single
    /// wall does, the second line stands a few paces behind it and commits with a charge once
    /// the battle is joined, the way a reserve should.</summary>
    public static readonly DoctrinePlan TwoLineWall = new DoctrinePlan(
        "TwoLineWall", FormationSplit.TwoOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.BracedDefend, 1f), W(BehaviorKind.TacticalCharge, 0.3f)),
            new FormationPlan(FormationRole.SecondInfantry, W(BehaviorKind.BracedDefend, 1f), W(BehaviorKind.TacticalCharge, 0.2f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.BracedDefend, 1f), W(BehaviorKind.TacticalCharge, 0.6f)),
            new FormationPlan(FormationRole.SecondInfantry, W(BehaviorKind.TacticalCharge, 1f), W(BehaviorKind.BracedDefend, 0.5f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.Skirmish, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()));

    /// <summary>Mordor, Gundabad, Dol Guldur: no cautious phase. The infantry comes on from the
    /// first tick, <c>Charge</c> weighted above <c>TacticalCharge</c> so it is a mass rather than
    /// a line; archers shoot without a screen; cavalry charges and flanks.</summary>
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

    /// <summary>The orc horde with the numbers to wrap: the centre advances as a line and charges
    /// on contact, two wings walk round the enemy's flanks (<c>EnvelopWing</c>) and come in from
    /// the sides; archers shoot, cavalry charges and flanks. No cautious phase.</summary>
    public static readonly DoctrinePlan Envelop = new DoctrinePlan(
        "Envelop", FormationSplit.ThreeOneTwoOne, alwaysEngaged: true, battleJoinedSeconds: 5f,
        defend: Enveloping(),
        engage: Enveloping());

    private static PhasePlan Enveloping() => new PhasePlan(
        new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.TacticalCharge, 1f)),
        new FormationPlan(FormationRole.LeftWing, W(BehaviorKind.EnvelopWing, 1f), W(BehaviorKind.TacticalCharge, 0.5f)),
        new FormationPlan(FormationRole.RightWing, W(BehaviorKind.EnvelopWing, 1f), W(BehaviorKind.TacticalCharge, 0.5f)),
        new FormationPlan(FormationRole.Archers, W(BehaviorKind.Skirmish, 1f), W(BehaviorKind.Charge, 0.3f)),
        new FormationPlan(FormationRole.LeftCavalry, W(BehaviorKind.TacticalCharge, 1f), W(BehaviorKind.Flank, 1f)),
        new FormationPlan(FormationRole.RightCavalry, W(BehaviorKind.TacticalCharge, 1f), W(BehaviorKind.Flank, 1f)),
        HorseArchers());

    /// <summary>Rohan, Rhun, Khand: one cavalry block leads (<c>Vanguard</c> ahead of the
    /// infantry) with the engine's 7 s cavalry join threshold; once joined it cycle-charges
    /// (through, reform, again) and flanks while the infantry advances behind it.</summary>
    public static readonly DoctrinePlan CavalryDominance = new DoctrinePlan(
        "CavalryDominance", FormationSplit.OneOneOneOne, alwaysEngaged: false, battleJoinedSeconds: 7f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f)),
            ScreenedArchers(),
            new FormationPlan(FormationRole.Cavalry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.Vanguard, 1f)),
            HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.TacticalCharge, 1f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f)),
            new FormationPlan(FormationRole.Cavalry, W(BehaviorKind.CycleCharge, 1.2f), W(BehaviorKind.TacticalCharge, 0.8f), W(BehaviorKind.Flank, 0.8f)),
            HorseArchers()));

    /// <summary>Rohan defending: two cavalry blocks screen the flanks against horse and hold
    /// beside the infantry, which stands where the high-ground race puts it; once joined the
    /// eoreds cycle-charge from the flanks.</summary>
    public static readonly DoctrinePlan EoredScreen = new DoctrinePlan(
        "EoredScreen", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 7f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Defend, 1f), W(BehaviorKind.TacticalCharge, 0.3f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Defend, 1f), W(BehaviorKind.TacticalCharge, 0.7f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.Skirmish, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            new FormationPlan(FormationRole.LeftCavalry, W(BehaviorKind.CycleCharge, 1.2f), W(BehaviorKind.Flank, 0.8f), W(BehaviorKind.ProtectFlank, 0.5f)),
            new FormationPlan(FormationRole.RightCavalry, W(BehaviorKind.CycleCharge, 1.2f), W(BehaviorKind.Flank, 0.8f), W(BehaviorKind.ProtectFlank, 0.5f)),
            HorseArchers()));

    /// <summary>Elves defending: the infantry rings the archers on the high ground
    /// (<c>DefensiveRing</c> around the <c>FireFromInfantryCover</c> Square), cavalry guards the
    /// flanks, the archers hold their arrows until the enemy is in effective range. The ring
    /// holds in both phases; the weight function, not the plan, decides when a ring is no
    /// longer a defence.</summary>
    public static readonly DoctrinePlan ArcherRing = new DoctrinePlan(
        "ArcherRing", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: Ring(),
        engage: Ring(),
        // A circle around a square settles slower than a line, and two formations must arrive.
        race: new RaceTunables(formUpSeconds: 10f, formUpSecondsPerUnit: 0.04f, marginSeconds: 4f),
        volley: ElvenVolley);

    private static PhasePlan Ring() => new PhasePlan(
        new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.DefensiveRing, 1f), W(BehaviorKind.TacticalCharge, 0.3f)),
        new FormationPlan(FormationRole.Archers, W(BehaviorKind.FireFromInfantryCover, 1f), W(BehaviorKind.Skirmish, 0.5f)),
        FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers());

    /// <summary>Elves attacking: the infantry advances carefully with the archers shooting
    /// from a line ahead of it (<c>CautiousAdvance</c> plus <c>SkirmishLine</c>, vanilla's
    /// harassment pairing), cavalry screens; once joined the infantry closes and the archers
    /// skirmish behind it. Volley control throughout.</summary>
    public static readonly DoctrinePlan ArcherAdvance = new DoctrinePlan(
        "ArcherAdvance", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.CautiousAdvance, 1f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.TacticalCharge, 0.7f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f)),
            new FormationPlan(FormationRole.LeftCavalry, W(BehaviorKind.ProtectFlank, 1f), W(BehaviorKind.CavalryScreen, 1f), W(BehaviorKind.Flank, 0.7f)),
            new FormationPlan(FormationRole.RightCavalry, W(BehaviorKind.ProtectFlank, 1f), W(BehaviorKind.CavalryScreen, 1f), W(BehaviorKind.Flank, 0.7f)),
            HorseArchers()),
        race: DoctrinePlan.DefaultRace,
        volley: ElvenVolley);

    /// <summary>Gondor, Rhun, Dale defending: a full-scale attack with a hold phase. The
    /// infantry stands where the race puts it until the battle is joined, then advances as a
    /// body; archers shoot from a line in front, then screened behind; cavalry guards the flanks
    /// until Engage, then charges and flanks.</summary>
    public static readonly DoctrinePlan DisciplinedLineDefender = new DoctrinePlan(
        "DisciplinedLineDefender", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Defend, 1f), W(BehaviorKind.TacticalCharge, 0.3f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: DisciplinedEngage());

    /// <summary>The same line attacking: a cautious advance behind the archers' line until
    /// joined, then the advance and the charge.</summary>
    public static readonly DoctrinePlan DisciplinedLineAttacker = new DoctrinePlan(
        "DisciplinedLineAttacker", FormationSplit.OneOneTwoOne, alwaysEngaged: false, battleJoinedSeconds: 5f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.CautiousAdvance, 1f), W(BehaviorKind.TacticalCharge, 0.3f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.SkirmishLine, 1f), W(BehaviorKind.ScreenedSkirmish, 1f)),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: DisciplinedEngage());

    private static PhasePlan DisciplinedEngage() => new PhasePlan(
        new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.TacticalCharge, 0.8f), W(BehaviorKind.Defend, 0.5f)),
        new FormationPlan(FormationRole.Archers, W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f)),
        new FormationPlan(FormationRole.LeftCavalry, W(BehaviorKind.TacticalCharge, 1f), W(BehaviorKind.Flank, 1f), W(BehaviorKind.ProtectFlank, 0.5f)),
        new FormationPlan(FormationRole.RightCavalry, W(BehaviorKind.TacticalCharge, 1f), W(BehaviorKind.Flank, 1f), W(BehaviorKind.ProtectFlank, 0.5f)),
        HorseArchers());

    /// <summary>Dunland attacking: the throwing infantry closes to javelin range, throws, falls
    /// back from foot that closes, and once the javelins are spent (<c>InfantrySkirmish</c>
    /// weighs 0) charges; archers skirmish, the raiders' cavalry flanks and charges. No
    /// cautious phase.</summary>
    public static readonly DoctrinePlan HitAndRun = new DoctrinePlan(
        "HitAndRun", FormationSplit.OneOneTwoOne, alwaysEngaged: true, battleJoinedSeconds: 5f,
        defend: Raiding(),
        engage: Raiding());

    private static PhasePlan Raiding() => new PhasePlan(
        new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.InfantrySkirmish, 1f), W(BehaviorKind.TacticalCharge, 0.7f), W(BehaviorKind.Advance, 0.5f)),
        new FormationPlan(FormationRole.Archers, W(BehaviorKind.Skirmish, 1f), W(BehaviorKind.ScreenedSkirmish, 0.5f)),
        new FormationPlan(FormationRole.LeftCavalry, W(BehaviorKind.Flank, 1f), W(BehaviorKind.TacticalCharge, 0.8f)),
        new FormationPlan(FormationRole.RightCavalry, W(BehaviorKind.Flank, 1f), W(BehaviorKind.TacticalCharge, 0.8f)),
        HorseArchers());

    /// <summary>Harad attacking with mumakil: the beasts ride ahead of the infantry
    /// (<c>Vanguard</c>) and charge once joined; the infantry advances behind them, archers
    /// shoot from a line, cavalry guards the flanks until Engage, then flanks.</summary>
    public static readonly DoctrinePlan MumakVanguard = new DoctrinePlan(
        "MumakVanguard", FormationSplit.OneOneTwoOneVanguard, alwaysEngaged: false, battleJoinedSeconds: 7f,
        defend: new PhasePlan(
            new FormationPlan(FormationRole.Vanguard, W(BehaviorKind.Vanguard, 1f), W(BehaviorKind.Advance, 0.8f)),
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f)),
            ScreenedArchers(),
            FlankGuard(FormationRole.LeftCavalry), FlankGuard(FormationRole.RightCavalry), HorseArchers()),
        engage: new PhasePlan(
            new FormationPlan(FormationRole.Vanguard, W(BehaviorKind.TacticalCharge, 1.2f), W(BehaviorKind.Charge, 1f)),
            new FormationPlan(FormationRole.MainInfantry, W(BehaviorKind.Advance, 1f), W(BehaviorKind.TacticalCharge, 1f)),
            new FormationPlan(FormationRole.Archers, W(BehaviorKind.ScreenedSkirmish, 1f), W(BehaviorKind.Skirmish, 1f)),
            new FormationPlan(FormationRole.LeftCavalry, W(BehaviorKind.TacticalCharge, 1f), W(BehaviorKind.Flank, 1f)),
            new FormationPlan(FormationRole.RightCavalry, W(BehaviorKind.TacticalCharge, 1f), W(BehaviorKind.Flank, 1f)),
            HorseArchers()));
}
