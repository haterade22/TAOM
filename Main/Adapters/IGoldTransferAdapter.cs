namespace TAOM.Adapters;

/// <summary>
/// Hero→player gold transfer for commander-funded wages (real gold, not minted — the
/// donor minted from nothing at 10 sites). Minted bonuses go through the existing
/// IGoldGiftAdapter instead.
/// </summary>
public interface IGoldTransferAdapter
{
    /// <summary>The hero's current gold, or 0 when unresolvable.</summary>
    int GetHeroGold(string heroId);

    /// <summary>Transfer up to <paramref name="amount"/> from the hero to the player; returns the amount actually moved.</summary>
    int TransferToPlayer(string fromHeroId, int amount);
}
