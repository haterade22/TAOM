using System.Collections.Generic;

namespace TAOM.Features.RaceAbilities.Domain;

// The compiled profiles, mirrored by the shipped race_abilities.json. First guesses, to be tuned in a
// Custom Battle (docs/features/race-abilities.md). Cooldowns are Mike's 1 to 2 minutes (2026-10-04): his
// first numbers (berserker 15 s, Uruk-hai 25 s, dwarf and elf 30 s) times four, keeping their order.
public static class RaceAbilityDefaults
{
    // Outline colours by kind of ability, the same on both sides (Mike, 2026-10-04): fury red, guard steel blue,
    // dread violet. The speed and aim abilities stay dark so a crowd stays readable.
    private const string Fury = "#E03A2E";
    private const string Guard = "#5B9BD5";
    private const string Dread = "#9B59FF";

    public static Dictionary<string, RaceAbilityProfile> RaceProfiles() => new Dictionary<string, RaceAbilityProfile>
    {
        ["berserker"] = Berserk(),
        ["uruk_hai"] = Bloodlust(),
        ["dwarf"] = StandFast(),
        ["elf"] = Swiftness(),
        ["orc"] = Swarm(),
        ["goblin"] = Scurry(),
        ["uruk"] = IronDiscipline(),
        ["pale_uruk"] = HuntersRush(),
        ["dg_uruk"] = NecromancersShadow(),
    };

    // Human soldiers only, by culture; a comma-separated key shares one profile.
    public static Dictionary<string, RaceAbilityProfile> CultureProfiles() => new Dictionary<string, RaceAbilityProfile>
    {
        ["gondor,gondor_soldiers,arthedain"] = CitadelGuard(),
        ["vlandia"] = ForthEorlingas(),
        ["sturgia"] = BardsAim(),
        ["empire,dunland_raiders"] = HillClanFury(),
        ["aserai,harad_raiders,shaghana,abanissa"] = SerpentsVenom(),
        ["khuzait,rhun_raiders"] = WainriderWall(),
        ["umbar,umbar_corsairs"] = CorsairRaid(),
        ["battania"] = VariagFerocity(),
        ["mordor,dolguldur"] = ServantsOfTheShadow(),
    };

    private static RaceAbilityTrigger T(string kind, float range = 0f, float seconds = 0f, float fraction = 0f, int count = 0) =>
        new RaceAbilityTrigger { Kind = kind, Range = range, Seconds = seconds, Fraction = fraction, Count = count };

    // ---- races ----

    // Isengard's berserkers: when hurt or when a brother falls, in melee, they stop guarding and smash
    // through every block, then stand spent for a moment.
    private static RaceAbilityProfile Berserk() => new RaceAbilityProfile
    {
        AbilityId = "berserk", CooldownSeconds = 60f, DurationSeconds = 6f, SpentSeconds = 3f, RallyRadius = 8f, WarCry = "Yell",
        Glow = Fury,
        Requires = { T("EnemyWithin", range: 3f) },
        AnyOf = { T("HealthBelow", fraction: 0.75f), T("TookDamage"), T("KinFell", range: 10f, seconds: 5f) },
        Effects = new RaceAbilityEffects
        {
            ForceCrushThrough = true, ShrugOffBlows = true, MeleeDamagePercent = 20f, SwingSpeedPercent = 15f,
            MoveSpeedPercent = 10f, MoraleFloor = 30f, BlockAbilityPercent = -60f, ParryAbilityPercent = -60f,
        },
        Spent = new RaceAbilityEffects { MoveSpeedPercent = -20f, SwingSpeedPercent = -10f },
    };

    // The Uruk-hai feed on killing: a kill or a wounded foe in reach sets it off, and every kill while it
    // lasts heals, lengthens it and shakes the enemies nearby.
    private static RaceAbilityProfile Bloodlust() => new RaceAbilityProfile
    {
        AbilityId = "bloodlust", CooldownSeconds = 100f, DurationSeconds = 8f, KillExtensionSeconds = 2f, MaxDurationSeconds = 14f,
        Glow = Fury,
        RallyRadius = 6f, WarCry = "Charge",
        AnyOf = { T("LandedKill", seconds: 1.5f), T("WoundedEnemyWithin", range: 3f, fraction: 0.5f) },
        Effects = new RaceAbilityEffects
        {
            MeleeDamagePercent = 15f, SwingSpeedPercent = 10f, KnockdownResistancePercent = 100f, HealPerKill = 8f,
            FearOnKillRadius = 6f, FearOnKillMorale = 4f, BlockAbilityPercent = -20f,
        },
    };

    // Dwarves brace against a charge or a crowd: no crush-through, no flinching, less damage, a tighter
    // guard, slower feet. Kin within 8 m brace with them, so the line locks as one.
    private static RaceAbilityProfile StandFast() => new RaceAbilityProfile
    {
        AbilityId = "stand_fast", CooldownSeconds = 120f, DurationSeconds = 10f, RallyRadius = 8f, WarCry = "Yell",
        Glow = Guard,
        Requires = { T("EnemyWithin", range: 20f) },
        AnyOf = { T("CavalryClosing", range: 20f), T("EnemiesWithin", range: 5f, count: 3), T("HealthBelow", fraction: 0.5f) },
        Effects = new RaceAbilityEffects
        {
            // No knock-back resistance: shrugging off already prevents every knock-back that reads it.
            HoldAgainstCrush = true, ShrugOffBlows = true, DamageReductionPercent = 20f, KnockdownResistancePercent = 200f,
            BlockAbilityPercent = 25f, MoveSpeedPercent = -15f, MoraleFloor = 25f,
        },
    };

    // Elves quicken: archers draw, reload and aim faster with a target in range, and every elf moves and
    // parries faster when a foe closes; a mounted elf's horse quickens too. Kin within 10 m loose with them.
    private static RaceAbilityProfile Swiftness() => new RaceAbilityProfile
    {
        AbilityId = "swiftness", CooldownSeconds = 120f, DurationSeconds = 8f, RallyRadius = 10f,
        AnyOf = { T("RangedTargetWithin", range: 30f), T("EnemyWithin", range: 6f) },
        Effects = new RaceAbilityEffects
        {
            MoveSpeedPercent = 20f, AccelerationPercent = 25f, DrawSpeedPercent = 25f, ReloadSpeedPercent = 15f,
            MissileSpeedPercent = 10f, AimErrorPercent = -30f, ParryAbilityPercent = 20f, MountSpeedPercent = 10f,
        },
    };

    // Orcs are brave in a crowd: three or more kin close by and a foe in reach set off a frenzy that grows
    // with every orc or goblin near, and burns out into a moment of cowardice.
    private static RaceAbilityProfile Swarm() => new RaceAbilityProfile
    {
        AbilityId = "swarm", CooldownSeconds = 80f, DurationSeconds = 8f, SpentSeconds = 3f, RallyRadius = 6f, WarCry = "Yell",
        Glow = Fury,
        KinRaces = { "goblin" },
        KinBonus = new RaceAbilityKinBonus { Radius = 6f, PerKinPercent = 3f, MaxKin = 5 },
        Requires = { T("EnemyWithin", range: 4f), T("KinWithin", range: 6f, count: 3) },
        Effects = new RaceAbilityEffects { MeleeDamagePercent = 5f, SwingSpeedPercent = 10f, MoraleOnEnd = -8f },
        Spent = new RaceAbilityEffects { MoveSpeedPercent = -10f },
    };

    // Goblins scurry: a burst of speed when a foe comes near or a wound bites. They count as kin for a Swarm
    // through the orcs' kinRaces; Scurry reads no kin of its own.
    private static RaceAbilityProfile Scurry() => new RaceAbilityProfile
    {
        AbilityId = "scurry", CooldownSeconds = 80f, DurationSeconds = 6f, SpentSeconds = 3f, RallyRadius = 6f, WarCry = "Grunt",
        AnyOf = { T("EnemyWithin", range: 8f), T("HealthBelow", fraction: 0.5f) },
        Effects = new RaceAbilityEffects { MoveSpeedPercent = 25f, AccelerationPercent = 30f, SwingSpeedPercent = 20f },
        Spent = new RaceAbilityEffects { MoveSpeedPercent = -15f },
    };

    // Mordor's black uruks hold the line when it sags: no panic, less damage, a firmer guard.
    private static RaceAbilityProfile IronDiscipline() => new RaceAbilityProfile
    {
        AbilityId = "iron_discipline", CooldownSeconds = 120f, DurationSeconds = 10f, RallyRadius = 8f, WarCry = "Yell",
        Glow = Guard,
        Requires = { T("EnemyWithin", range: 5f) },
        AnyOf = { T("HealthBelow", fraction: 0.6f), T("MoraleBelow", fraction: 0.6f), T("EnemiesWithin", range: 4f, count: 3) },
        Effects = new RaceAbilityEffects
        {
            MoraleFloor = 60f, DamageReductionPercent = 15f, BlockAbilityPercent = 15f, KnockbackResistancePercent = 100f,
            MoveSpeedPercent = -10f,
        },
    };

    // Gundabad's pale uruks run their prey down: an enemy at 4 to 15 m sets off a closing sprint.
    private static RaceAbilityProfile HuntersRush() => new RaceAbilityProfile
    {
        AbilityId = "hunters_rush", CooldownSeconds = 100f, DurationSeconds = 6f, SpentSeconds = 3f, RallyRadius = 6f, WarCry = "Charge",
        Requires = { T("EnemyWithin", range: 15f), T("NoEnemyWithin", range: 4f) },
        Effects = new RaceAbilityEffects
        {
            MoveSpeedPercent = 30f, AccelerationPercent = 40f, KnockdownResistancePercent = 100f, MeleeDamagePercent = 10f,
        },
        Spent = new RaceAbilityEffects { MoveSpeedPercent = -15f },
    };

    // Dol Guldur's uruks carry the Necromancer's dread: pressed by two or more, they chill the enemies
    // around them.
    private static RaceAbilityProfile NecromancersShadow() => new RaceAbilityProfile
    {
        AbilityId = "necromancer_shadow", CooldownSeconds = 120f, DurationSeconds = 8f, RallyRadius = 6f, WarCry = "Yell",
        Glow = Dread,
        Requires = { T("EnemiesWithin", range: 6f, count: 2) },
        Effects = new RaceAbilityEffects { FearAuraRadius = 8f, FearAuraMoralePerSecond = 2f, MeleeDamagePercent = 10f },
    };

    // ---- human cultures ----

    // Gondor's line closes ranks against a crowd or a charge.
    private static RaceAbilityProfile CitadelGuard() => new RaceAbilityProfile
    {
        AbilityId = "citadel_guard", CooldownSeconds = 120f, DurationSeconds = 10f, RallyRadius = 8f, WarCry = "Yell",
        Glow = Guard,
        Requires = { T("EnemyWithin", range: 15f) },
        AnyOf = { T("EnemiesWithin", range: 5f, count: 2), T("CavalryClosing", range: 15f), T("HealthBelow", fraction: 0.5f) },
        Effects = new RaceAbilityEffects
        {
            BlockAbilityPercent = 30f, DamageReductionPercent = 10f, KnockbackResistancePercent = 100f, MoraleFloor = 40f,
            MoveSpeedPercent = -10f,
        },
    };

    // Rohan's riders: in the saddle with the enemy ahead, the whole eored spurs on.
    private static RaceAbilityProfile ForthEorlingas() => new RaceAbilityProfile
    {
        AbilityId = "forth_eorlingas", CooldownSeconds = 120f, DurationSeconds = 10f, RallyRadius = 12f, WarCry = "Charge",
        Requires = { T("Mounted"), T("EnemyWithin", range: 30f) },
        Effects = new RaceAbilityEffects
        {
            MountSpeedPercent = 15f, MeleeDamagePercent = 15f, SwingSpeedPercent = 10f, DismountResistancePercent = 150f,
            MoraleFloor = 40f,
        },
    };

    // Dale's bowmen, Bard's heirs: a target in range and their aim steadies.
    private static RaceAbilityProfile BardsAim() => new RaceAbilityProfile
    {
        AbilityId = "bards_aim", CooldownSeconds = 120f, DurationSeconds = 8f, RallyRadius = 10f,
        Requires = { T("RangedTargetWithin", range: 40f) },
        Effects = new RaceAbilityEffects
        {
            DrawSpeedPercent = 20f, AimErrorPercent = -40f, MissileSpeedPercent = 15f, RangedDamagePercent = 10f,
        },
    };

    // Dunland's hillmen: a wound or a fallen clansman in melee turns them wild.
    private static RaceAbilityProfile HillClanFury() => new RaceAbilityProfile
    {
        AbilityId = "hillclan_fury", CooldownSeconds = 80f, DurationSeconds = 6f, SpentSeconds = 3f, RallyRadius = 6f, WarCry = "Yell",
        Glow = Fury,
        Requires = { T("EnemyWithin", range: 3f) },
        AnyOf = { T("TookDamage"), T("KinFell", range: 8f, seconds: 5f) },
        Effects = new RaceAbilityEffects
        {
            MeleeDamagePercent = 15f, SwingSpeedPercent = 10f, MoveSpeedPercent = 10f, BlockAbilityPercent = -30f,
        },
        Spent = new RaceAbilityEffects { MoveSpeedPercent = -10f },
    };

    // The Haradrim: venomed arrows at range, a bite in close.
    private static RaceAbilityProfile SerpentsVenom() => new RaceAbilityProfile
    {
        AbilityId = "serpent_venom", CooldownSeconds = 120f, DurationSeconds = 8f, RallyRadius = 10f,
        AnyOf = { T("RangedTargetWithin", range: 35f), T("EnemyWithin", range: 4f) },
        Effects = new RaceAbilityEffects { RangedDamagePercent = 20f, AimErrorPercent = -15f, MeleeDamagePercent = 10f },
    };

    // Rhun's Easterlings lock their wall against a crowd or horse.
    private static RaceAbilityProfile WainriderWall() => new RaceAbilityProfile
    {
        AbilityId = "wainrider_wall", CooldownSeconds = 120f, DurationSeconds = 10f, RallyRadius = 8f, WarCry = "Yell",
        Glow = Guard,
        Requires = { T("EnemyWithin", range: 15f) },
        AnyOf = { T("EnemiesWithin", range: 5f, count: 2), T("CavalryClosing", range: 15f) },
        Effects = new RaceAbilityEffects
        {
            BlockAbilityPercent = 20f, DamageReductionPercent = 10f, KnockdownResistancePercent = 100f, SwingSpeedPercent = 10f,
        },
    };

    // Umbar's corsairs: blooded or bloodied in close, they press like a boarding party.
    private static RaceAbilityProfile CorsairRaid() => new RaceAbilityProfile
    {
        AbilityId = "corsair_raid", CooldownSeconds = 80f, DurationSeconds = 6f, SpentSeconds = 3f, RallyRadius = 6f, WarCry = "Charge",
        Glow = Fury,
        Requires = { T("EnemyWithin", range: 4f) },
        AnyOf = { T("TookDamage"), T("LandedKill", seconds: 2f) },
        Effects = new RaceAbilityEffects { SwingSpeedPercent = 20f, MoveSpeedPercent = 15f, MeleeDamagePercent = 10f },
        Spent = new RaceAbilityEffects { MoveSpeedPercent = -10f },
    };

    // Khand's Variags: close in, they fight savage and hard to unseat.
    private static RaceAbilityProfile VariagFerocity() => new RaceAbilityProfile
    {
        AbilityId = "variag_ferocity", CooldownSeconds = 100f, DurationSeconds = 8f, RallyRadius = 8f, WarCry = "Charge",
        Glow = Fury,
        Requires = { T("EnemyWithin", range: 6f) },
        Effects = new RaceAbilityEffects
        {
            MeleeDamagePercent = 15f, SwingSpeedPercent = 10f, KnockdownResistancePercent = 100f, DismountResistancePercent = 100f,
        },
    };

    // The Shadow's men (Mordor's and Dol Guldur's human soldiers): a kill or a wound hardens them into dread.
    private static RaceAbilityProfile ServantsOfTheShadow() => new RaceAbilityProfile
    {
        AbilityId = "shadow_servants", CooldownSeconds = 120f, DurationSeconds = 8f, RallyRadius = 6f, WarCry = "Yell",
        Glow = Dread,
        Requires = { T("EnemyWithin", range: 4f) },
        AnyOf = { T("LandedKill", seconds: 2f), T("HealthBelow", fraction: 0.5f) },
        Effects = new RaceAbilityEffects
        {
            FearOnKillRadius = 6f, FearOnKillMorale = 5f, MoraleFloor = 50f, MeleeDamagePercent = 10f,
        },
    };
}
