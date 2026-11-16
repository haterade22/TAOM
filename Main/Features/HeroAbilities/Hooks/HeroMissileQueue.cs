using System.Collections.Concurrent;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroAbilities.Domain;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities.Hooks;

internal enum HeroMissileKind
{
    // OnAgentShootMissile: one of his own shots (an added extra arrow raises no behaviour call, Mission.cs:4954-4979).
    Shot,

    // OnMissileHit: one of his missiles met an agent, blocked or not.
    MissileHit,

    // OnAgentHit: a registered missile blow, or a blocked one through Mission.OnAgentHitBlocked (Mission.cs:5537-5550,
    // isBlocked: true); the block flags tell them apart.
    BodyHit,

    // Mission.OnMissileRemovedEvent (Mission.cs:5770): a missile, any shooter's, is gone and native may give its index to
    // the next missile at once. It carries the index only: no hero, no kit.
    Removed,
}

/// <summary>
/// What a missile callback saw, copied out of the engine's structs on the calling thread. The victim's team is read
/// there because a slot's managed Team stays the dead agent's (csharp-architecture.md "Mission-scope agent handles").
/// </summary>
internal readonly struct HeroMissileEvent
{
    private HeroMissileEvent(HeroMissileKind kind, Agent hero, HeroRangedConfig kit, Agent? victim, Team? victimTeam, Vec3f position,
        Vec3f velocity, int missileIndex, string itemId, bool blockedShield, bool blockedWeapon, bool shieldBroken, bool headshot, int damage)
    {
        Kind = kind;
        Hero = hero;
        Kit = kit;
        Victim = victim;
        VictimTeam = victimTeam;
        Position = position;
        Velocity = velocity;
        MissileIndex = missileIndex;
        ItemId = itemId;
        BlockedShield = blockedShield;
        BlockedWeapon = blockedWeapon;
        ShieldBroken = shieldBroken;
        Headshot = headshot;
        Damage = damage;
    }

    internal HeroMissileKind Kind { get; }
    internal Agent Hero { get; }
    internal HeroRangedConfig Kit { get; }
    internal Agent? Victim { get; }
    internal Team? VictimTeam { get; }
    internal Vec3f Position { get; }
    internal Vec3f Velocity { get; }
    internal int MissileIndex { get; }
    internal string ItemId { get; }
    internal bool BlockedShield { get; }
    internal bool BlockedWeapon { get; }
    internal bool ShieldBroken { get; }
    internal bool Headshot { get; }
    internal int Damage { get; }

    // An arrow is an extra by its item alone: no one else carries the kit's extra arrow, and native gives a freed missile
    // index to the next missile at once, so an index would misread his next arrow.
    internal bool IsExtraArrow => Kit.ExtraArrowItem.Length > 0 && ItemId == Kit.ExtraArrowItem;

    internal static HeroMissileEvent Shot(Agent hero, HeroRangedConfig kit, Vec3 position, Vec3 velocity) =>
        new(HeroMissileKind.Shot, hero, kit, null, null, new Vec3f(position.x, position.y, position.z), new Vec3f(velocity.x, velocity.y, velocity.z), -1, "", false, false, false, false, 0);

    internal static HeroMissileEvent Hit(Agent hero, HeroRangedConfig kit, Agent? victim, Team? victimTeam, int missileIndex, string itemId,
        bool blockedShield, bool blockedWeapon, bool shieldBroken) =>
        new(HeroMissileKind.MissileHit, hero, kit, victim, victimTeam, default, default, missileIndex, itemId, blockedShield, blockedWeapon, shieldBroken, false, 0);

    internal static HeroMissileEvent Body(Agent hero, HeroRangedConfig kit, Agent victim, Team? victimTeam, int missileIndex, string itemId,
        bool blockedShield, bool blockedWeapon, bool headshot, int damage) =>
        new(HeroMissileKind.BodyHit, hero, kit, victim, victimTeam, default, default, missileIndex, itemId, blockedShield, blockedWeapon, false, headshot, damage);

    // Hero and Kit are null on a removal; the drain reads Kind first.
    internal static HeroMissileEvent Removed(int missileIndex) =>
        new(HeroMissileKind.Removed, null!, null!, null, null, default, default, missileIndex, "", false, false, false, false, 0);
}

/// <summary>The missile callbacks enqueue on any thread (#634); the mission tick drains on the main thread.</summary>
internal sealed class HeroMissileQueue
{
    private readonly ConcurrentQueue<HeroMissileEvent> _queue = new ConcurrentQueue<HeroMissileEvent>();

    internal void Enqueue(HeroMissileEvent e) => _queue.Enqueue(e);

    internal bool TryDequeue(out HeroMissileEvent e) => _queue.TryDequeue(out e);

    internal void Clear()
    {
        while (_queue.TryDequeue(out _))
        {
        }
    }
}
