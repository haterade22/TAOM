namespace TAOM.Features.BattleGearLoot.Domain;

public sealed record GearPiece(int Slot, string ItemId, bool IsMerchandise, bool IsBanner, bool IsQuestItem, string? MountMonsterId);
