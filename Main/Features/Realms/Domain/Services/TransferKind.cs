namespace TAOM.Features.Realms.Domain.Services;

/// <summary>How a settlement changed hands, mapped from the game's ChangeOwnerOfSettlementDetail.</summary>
public enum TransferKind
{
    /// <summary>Taken by siege or rebellion. The title stays; the taker gains a claim.</summary>
    Conquest,

    /// <summary>Granted by the ruler, given or sold. The title follows if the giver or a member of the giver's clan held it.</summary>
    Transfer,

    /// <summary>The owner's clan was destroyed or left the realm. The title follows only if vacant.</summary>
    Other,
}
