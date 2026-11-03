using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>Which hits count as a landed arrow or bolt for poison: a missile of those item types that no shield stopped. Kept out of the mission logic to hold it under the ADR-002 limit.</summary>
internal static class FordsArrowHits
{
    internal static void Report(FordsMissionDirector? director, Agent affected, Agent affector, in MissionWeapon weapon, in Blow blow, in AttackCollisionData collision)
    {
        var type = weapon.Item?.ItemType;
        if (!blow.IsMissile || collision.AttackBlockedWithShield || (type != ItemObject.ItemTypeEnum.Arrows && type != ItemObject.ItemTypeEnum.Bolts))
            return;

        try
        {
            director?.OnArrowHit(affected, affector);
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("FordsOfWar", "FordsArrowHits.Report", ex);
        }
    }
}
