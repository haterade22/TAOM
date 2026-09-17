using System;
using System.Collections.Generic;

namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>The behaviours a plan may weight. The first twenty-two map to the <c>Behavior*</c>
/// types the engine registers on every field formation
/// (<c>TeamAIGeneral.OnUnitAddedToFormationForTheFirstTime</c>); the last five are TAOM's own
/// <c>BehaviorComponent</c>s, which the applier adds to a formation the first time a plan names
/// them. The mapping lives in one place, <c>BehaviorWeightApplier</c>.</summary>
public enum BehaviorKind
{
    Charge,
    TacticalCharge,
    Advance,
    CautiousAdvance,
    HoldHighGround,
    Defend,
    DefensiveRing,
    FireFromInfantryCover,
    Skirmish,
    SkirmishLine,
    ScreenedSkirmish,
    ProtectFlank,
    CavalryScreen,
    Flank,
    Vanguard,
    HorseArcherSkirmish,
    MountedSkirmish,
    PullBack,
    Regroup,
    Reserve,
    Retreat,
    Stop,
    BracedDefend,
    BracedAdvance,
    InfantrySkirmish,
    CycleCharge,
    EnvelopWing,

    /// <summary>TAOM: a foot charge at the nearest enemy foot formation, braced against
    /// horse (<c>BehaviorFootCharge</c>). Every foot row's charge.</summary>
    FootCharge,
}

/// <summary>The formation slots a tactic assigns. <c>LeftCavalry</c>/<c>RightCavalry</c> exist
/// under the 1/1/2/1 split, <c>Cavalry</c> under 1/1/1/1, <c>SecondInfantry</c> under 2/1/2/1
/// and <c>LeftWing</c>/<c>RightWing</c> under 3/1/2/1, <c>Vanguard</c> under the mumakil split
/// (the team's HeavyCavalry formation, kept out of the cavalry consolidation).</summary>
public enum FormationRole
{
    MainInfantry,
    Archers,
    LeftCavalry,
    RightCavalry,
    Cavalry,
    RangedCavalry,
    SecondInfantry,
    LeftWing,
    RightWing,
    Vanguard,
}

/// <summary>How many formations of each class the tactic keeps (infantry/ranged/cavalry/horse
/// archers): vanilla's default <c>AssignTacticFormations1121</c>, the cavalry-led 1/1/1/1 of
/// <c>TacticFrontalCavalryCharge</c>, TAOM's two-line 2/1/2/1, the three-block 3/1/2/1 of the
/// envelopment (centre plus two wings), and 1/1/2/1 with the HeavyCavalry formation kept apart
/// as a vanguard.</summary>
public enum FormationSplit
{
    OneOneTwoOne,
    OneOneOneOne,
    TwoOneTwoOne,
    ThreeOneTwoOne,
    OneOneTwoOneVanguard,
}

public enum TacticPhase
{
    Defend,
    Engage,
}

public readonly struct BehaviorWeight
{
    public BehaviorWeight(BehaviorKind kind, float weight)
    {
        Kind = kind;
        Weight = weight;
    }

    public BehaviorKind Kind { get; }
    public float Weight { get; }
}

public sealed class FormationPlan
{
    public FormationPlan(FormationRole role, params BehaviorWeight[] weights)
    {
        Role = role;
        Weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    public FormationRole Role { get; }
    public IReadOnlyList<BehaviorWeight> Weights { get; }
}

public sealed class PhasePlan
{
    public PhasePlan(params FormationPlan[] formations)
    {
        Formations = formations ?? throw new ArgumentNullException(nameof(formations));
    }

    public IReadOnlyList<FormationPlan> Formations { get; }
}

/// <summary>
/// One TAOM tactic as data: the split, the two phase tables, and the two lifecycle knobs vanilla
/// varies per tactic. Immutable, shared by every team, read on the async AI thread.
/// </summary>
public sealed class DoctrinePlan
{
    public DoctrinePlan(string name, FormationSplit split, bool alwaysEngaged, float battleJoinedSeconds, PhasePlan defend, PhasePlan engage)
        : this(name, split, alwaysEngaged, battleJoinedSeconds, defend, engage, DefaultRace, VolleyTunables.FireAtWill)
    {
    }

    public DoctrinePlan(string name, FormationSplit split, bool alwaysEngaged, float battleJoinedSeconds, PhasePlan defend, PhasePlan engage, RaceTunables race)
        : this(name, split, alwaysEngaged, battleJoinedSeconds, defend, engage, race, VolleyTunables.FireAtWill)
    {
    }

    public DoctrinePlan(string name, FormationSplit split, bool alwaysEngaged, float battleJoinedSeconds, PhasePlan defend, PhasePlan engage, RaceTunables race, VolleyTunables volley)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Split = split;
        AlwaysEngaged = alwaysEngaged;
        BattleJoinedSeconds = battleJoinedSeconds;
        Defend = defend ?? throw new ArgumentNullException(nameof(defend));
        Engage = engage ?? throw new ArgumentNullException(nameof(engage));
        Race = race;
        Volley = volley;
    }

    /// <summary>A line forms in about six seconds plus three per hundred men; three seconds of
    /// margin against the engine's 10 s speed cache.</summary>
    public static readonly RaceTunables DefaultRace = new RaceTunables(formUpSeconds: 6f, formUpSecondsPerUnit: 0.03f, marginSeconds: 3f);

    public string Name { get; }
    public FormationSplit Split { get; }

    /// <summary>Skip the Defend phase entirely: the tactic engages from its first tick.</summary>
    public bool AlwaysEngaged { get; }

    /// <summary>Seconds of closing distance (at the enemy's top speed) under which the battle
    /// counts as joined; vanilla uses 5 for infantry-led tactics and 7 for the cavalry one.</summary>
    public float BattleJoinedSeconds { get; }

    public PhasePlan Defend { get; }
    public PhasePlan Engage { get; }

    /// <summary>How the plan's position-holding tactics decide between marching to the high
    /// ground and forming where they stand (<see cref="HighGroundRace"/>).</summary>
    public RaceTunables Race { get; }

    /// <summary>When the plan's archers may loose (<see cref="VolleyDecision"/>); fire at will
    /// unless the plan says otherwise.</summary>
    public VolleyTunables Volley { get; }

    public bool HasVolleyControl => !Volley.IsFireAtWill;
}
