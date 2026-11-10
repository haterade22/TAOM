namespace TAOM.Features.Realms.Domain.Services;

public enum TitleEventKind
{
    HolderChanged,
    ClaimGained,

    /// <summary>An office changed holder; the event's title id is the office id.</summary>
    OfficeChanged,
}

public enum TitleEventReason
{
    /// <summary>Passed to an heir under the title's succession law.</summary>
    Inheritance,

    /// <summary>Passed to the clan's next leader.</summary>
    ClanSuccession,

    /// <summary>Followed the realm's new ruler.</summary>
    RealmSuccession,

    /// <summary>The holder handed over the settlement peacefully: royal grant, gift or sale.</summary>
    Transfer,

    /// <summary>The title was vacant and the new owner of its settlement took it up.</summary>
    Possession,

    /// <summary>No eligible heir; the title is now vacant.</summary>
    Vacated,

    /// <summary>Took the settlement while someone else held the title.</summary>
    Conquest,

    /// <summary>A clan leader handed a lesser title to one of their children.</summary>
    Grant,

    /// <summary>A claimant bought a claim on the title.</summary>
    Purchase,

    /// <summary>A claimant pressed their claim and took the title from its holder.</summary>
    Usurpation,

    /// <summary>The title had no heir and its strongest claimant took it up.</summary>
    Claim,

    /// <summary>The holder when the titles were made (chronicle only).</summary>
    Start,

    /// <summary>A vacant title was formed by a hero who holds most of the lands below it.</summary>
    Formation,

    /// <summary>A vacant title was taken up by a hero with an ancestral claim on it who serves its realm.</summary>
    Restoration,

    /// <summary>A liege or clan leader took the title back from its holder.</summary>
    Revocation,

    /// <summary>A child gained an ancestral claim from a dead parent's titles or claims.</summary>
    Ancestry,

    /// <summary>A ruler appointed the hero to an office, or the office follows the ruler.</summary>
    Appointment,

    /// <summary>The office holder died, left the realm or was dismissed; the office is vacant.</summary>
    Dismissal,
}

/// <summary>Something that happened to a title, returned so the game layer can report it.</summary>
public sealed class TitleEvent
{
    public TitleEvent(TitleEventKind kind, TitleEventReason reason, string titleId, string? heroId, string? previousHolderId)
    {
        Kind = kind;
        Reason = reason;
        TitleId = titleId;
        HeroId = heroId;
        PreviousHolderId = previousHolderId;
    }

    public TitleEventKind Kind { get; }
    public TitleEventReason Reason { get; }
    public string TitleId { get; }

    /// <summary>The new holder for <see cref="TitleEventKind.HolderChanged"/> (null when vacated), the claimant for a claim.</summary>
    public string? HeroId { get; }

    public string? PreviousHolderId { get; }
}
