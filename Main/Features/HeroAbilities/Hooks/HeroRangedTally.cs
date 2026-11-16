using System.Collections.Generic;

namespace TAOM.Features.HeroAbilities.Hooks;

/// <summary>
/// The counts of one ranged hero body for the #1361 end-of-mission checks (HeroRangedChecks). No engine type: the
/// mission logic hands in the hits on an enemy, keyed by the missile index a hit carries
/// (AttackCollisionData.AffectorWeaponSlotOrMissileIndex), and every missile removal. Native hands a freed index to the
/// next missile at once, so what is remembered of an index ends at its removal (MissileRemoved). Main thread only.
/// </summary>
internal sealed class HeroRangedTally
{
    // The live missiles that already counted as contact, and the victim whose shield each shield-blocked one hit.
    private readonly HashSet<int> _contact = new HashSet<int>();
    private readonly Dictionary<int, object> _shielded = new Dictionary<int, object>();
    private float _lastWindowUntil = float.NaN;

    internal HeroRangedTally(string heroId, int serial)
    {
        HeroId = heroId;
        Serial = serial;
    }

    internal string HeroId { get; }

    // The kth body of this hero in the mission, for the check key.
    internal int Serial { get; }

    internal bool PrecisionWritten { get; set; }

    // The kit's switches: with one off, its check is a skip (reason=off).
    internal bool ZeroAimError { get; set; }
    internal bool PierceShields { get; set; }
    internal bool KeepDistance { get; set; }

    // hits
    internal int Shots { get; set; }
    internal int Contact { get; set; }
    internal int PrimaryShield { get; set; }

    // pierce
    internal int ShieldHits { get; set; }
    internal int Passed { get; set; }
    internal int Broken { get; set; }

    // multishot
    internal bool ItemResolved { get; set; }
    internal int Windows { get; set; }
    internal int WindowShots { get; set; }
    internal int Fired { get; set; }
    internal int NoTarget { get; set; }
    internal int OutOfRange { get; set; }
    internal int ExtraHits { get; set; }

    // arrow-kill
    internal int BodyHits { get; set; }
    internal int Head { get; set; }
    internal int Exempt { get; set; }
    internal int DeadShooter { get; set; }
    internal int Calls { get; set; }
    internal int Kills { get; set; }

    // keep-distance (#1415): steps called, steps he walked (half the step or more by the end of the hold), threats with no
    // usable point, and seconds sampled with a melee weapon in hand
    internal int Steps { get; set; }
    internal int Moved { get; set; }
    internal int Cornered { get; set; }
    internal int MeleeSeconds { get; set; }

    /// <summary>One of his missiles met an enemy (blocked or not).</summary>
    internal void MissileHit(object? victim, int index, bool extra, bool blockedShield, bool blockedWeapon, bool shieldBroken)
    {
        if (victim == null)
            return;
        if (extra)
            ExtraHits++;
        else if (_contact.Add(index))
            Contact++;

        if (blockedShield)
        {
            ShieldHits++;
            if (!extra)
                PrimaryShield++;
            if (shieldBroken)
                Broken++;
            _shielded[index] = victim;
            return;
        }
        if (blockedWeapon)
            return;
        // The same live missile, after its shield hit, struck that victim unblocked: it passed the shield.
        if (_shielded.TryGetValue(index, out var shielded) && ReferenceEquals(shielded, victim))
        {
            Passed++;
            _shielded.Remove(index);
        }
    }

    /// <summary>The engine removed a missile (any shooter's): its index may now be the next missile's.</summary>
    internal void MissileRemoved(int index)
    {
        _contact.Remove(index);
        _shielded.Remove(index);
    }

    /// <summary>A running extra-arrow window, seen once a second; each new end time is a new window.</summary>
    internal void NoteWindow(float until)
    {
        if (float.IsNaN(until) || until == _lastWindowUntil)
            return;
        _lastWindowUntil = until;
        Windows++;
    }
}
