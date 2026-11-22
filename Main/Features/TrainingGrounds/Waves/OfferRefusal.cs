namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>
/// Why the wave fight is not on offer at the training master, first reason found. <c>None</c> is an open offer. The first four hide the
/// line (it would mean nothing there); the rest show it greyed, with the reason as its explanation.
/// </summary>
internal enum OfferRefusal
{
    None,

    /// <summary>The MCM switch is off, or the cap leaves no stake the rules allow.</summary>
    Disabled,

    /// <summary>A co-op session, a dedicated server, or a peer that is not the authority.</summary>
    NotSinglePlayer,

    /// <summary>No mission arena is attached, the partner is not the practice master, or there is no town.</summary>
    NotArena,

    /// <summary>Vanilla's own practice debrief is still due.</summary>
    Practising,

    /// <summary>The town holds a tournament; the yard is kept for it.</summary>
    Tournament,

    Wounded,

    Disguised,

    /// <summary>The town has already run a wave fight today and its rules allow one a day.</summary>
    RanToday,

    /// <summary>The purse is under the smallest stake.</summary>
    Poor,
}

/// <summary>Which of the master's greetings is due after a wave: the player cleared it, lost it, or cleared the last one.</summary>
internal enum DebriefKind
{
    None,
    Cleared,
    Lost,
    Completed,
}
