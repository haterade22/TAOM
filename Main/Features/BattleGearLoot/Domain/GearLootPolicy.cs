using System;
using TAOM.Features.CultureConversion.GarrisonSwap.Domain;

namespace TAOM.Features.BattleGearLoot.Domain;

public static class GearLootPolicy
{
    public static bool IsEligible(GearPiece piece, Func<string, bool> isNamedWeapon) =>
        !string.IsNullOrEmpty(piece.ItemId)
        && piece.IsMerchandise
        && !piece.IsBanner
        && !piece.IsQuestItem
        && !CreatureMountRiders.IsLockedCreatureMonster(piece.MountMonsterId)
        && !isNamedWeapon(piece.ItemId);
}
