using TAOM.Features.Elephant;
using TAOM.Features.Mumakil;
using TAOM.Features.Spider;

namespace TAOM.Features.CultureConversion.GarrisonSwap.Domain;

/// <summary>
/// The mounts whose riders are never a REPLACEMENT in a converted garrison or militia (Mike 2026-09-29): the
/// mount-locked creatures, the same three <c>TaomAgentStatCalculateModel.CanAgentRideMount</c> refuses (the giant
/// spider, the war elephant and the Mumakil). Dol Guldur's spider riders could already be picked since the swap landed
/// (2026-09-21); the goblin tree's mountain spider riders made it the answer for every captured cavalryman in three more
/// cultures. War rams and elk ride the horse skeleton as ordinary cavalry and stay eligible. Keyed on the mount's
/// Monster, so a new spider item needs nothing here and a new creature Monster does. The adapter reads each candidate's
/// mounts; <see cref="CultureTroopIndex"/> keeps the flagged ones out of its replacement cells, not out of the
/// garrison: a garrison that already holds them keeps them, and troops already there can still upgrade into them.
/// </summary>
public static class CreatureMountRiders
{
    public static bool IsLockedCreatureMonster(string? monsterId) =>
        monsterId == SpiderConfig.SpiderMonsterId
        || monsterId == ElephantConfig.ElephantMonsterId
        || monsterId == MumakilConfig.MumakilMonsterId;
}
