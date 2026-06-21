namespace TAOM.Adapters;

/// <summary>
/// Wraps the sealed <c>Hero</c> receiving an issue's completion reward, so the pure
/// <c>ILotrIssueService</c> can grant gold/renown/items without referencing TaleWorlds types (ADR-007).
/// </summary>
public interface ILotrIssueRewardAdapter
{
    bool IsValid { get; }

    void AddGold(int amount);

    void AddRenown(int amount);

    /// <summary>Add <paramref name="count"/> of the item with the given StringId; no-op if the id is unknown.</summary>
    void AddItemToInventory(string itemId, int count);
}
