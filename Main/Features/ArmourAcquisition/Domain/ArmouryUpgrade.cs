using System.Collections.Generic;

namespace TAOM.Features.ArmourAcquisition.Domain;

/// <summary>One stack of the player's party inventory: an item, its quality modifier, how many.</summary>
public sealed class InventoryPiece
{
    public InventoryPiece(string itemId, string? modifierId, int count)
    {
        ItemId = itemId;
        ModifierId = string.IsNullOrEmpty(modifierId) ? null : modifierId;
        Count = count;
    }

    public string ItemId { get; }

    public string? ModifierId { get; }

    public int Count { get; }
}

/// <summary>What the player holds that an upgrade can be paid with.</summary>
public sealed class ArmouryWallet
{
    public ArmouryWallet(int gold, IReadOnlyDictionary<string, int> materials, float? resourceAmount)
    {
        Gold = gold;
        Materials = materials;
        ResourceAmount = resourceAmount;
    }

    public int Gold { get; }

    public IReadOnlyDictionary<string, int> Materials { get; }

    /// <summary>The player's kingdom special resource balance; null when the kingdom has none (the cost is waived).</summary>
    public float? ResourceAmount { get; }

    public int CountOf(string itemId) => Materials.TryGetValue(itemId, out var n) ? n : 0;
}

/// <summary>
/// Why an upgrade cannot be done: on an offer, before the player chooses; from an upgrade, why it was not
/// done (None: it was).
/// </summary>
public enum UpgradeBlock
{
    None,
    ArmouryLevelTooLow,
    NotEnoughGold,
    MissingMaterials,
    NotEnoughResource,
    NoLongerCarried,
}

/// <summary>One upgrade the armoury can offer: a carried piece, what it becomes, and the price.</summary>
public sealed class UpgradeOffer
{
    public UpgradeOffer(string sourceItemId, string? modifierId, string targetItemId, ArmourClass targetClass,
        int gold, IReadOnlyList<UpgradeMaterial> materials, float specialResource, int requiredLevel, UpgradeBlock block)
    {
        SourceItemId = sourceItemId;
        ModifierId = modifierId;
        TargetItemId = targetItemId;
        TargetClass = targetClass;
        Gold = gold;
        Materials = materials;
        SpecialResource = specialResource;
        RequiredLevel = requiredLevel;
        Block = block;
    }

    public string SourceItemId { get; }

    /// <summary>The quality modifier the piece carries; the upgraded piece keeps it (Mike, 2026-09-27).</summary>
    public string? ModifierId { get; }

    public string TargetItemId { get; }

    public ArmourClass TargetClass { get; }

    public int Gold { get; }

    public IReadOnlyList<UpgradeMaterial> Materials { get; }

    public float SpecialResource { get; }

    public int RequiredLevel { get; }

    public UpgradeBlock Block { get; }

    public bool CanUpgrade => Block == UpgradeBlock.None;
}
