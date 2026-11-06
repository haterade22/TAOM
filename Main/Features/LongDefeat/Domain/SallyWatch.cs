using System;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// One sally wave's tally, from the lord's move out to the clear (phase2-plan.md Slice 10, spike S9). Main thread only. It counts the hits by and on the lord and his horse, keeps the seconds he has
/// been out and the stretch he has been outside the mission boundary (<c>MissionBoundaryCrossingHandler</c> retreats the mission after 10 s of it), says when the 5 s line is due (he is more than 20 m
/// outside the keep), and decides the contact verdict: a hit inside the first 180 s within 40 m of the sally point.
/// </summary>
internal sealed class SallyWatch
{
    public const float ContactMetres = 40f;
    public const float ContactSeconds = 180f;
    public const float OutsideKeepMetres = 20f;
    public const float LineSeconds = 5f;

    private float _lineClock;

    public SallyWatch(int wave, LdPlace point, LdPoint at, int deathsAtOut)
    {
        Wave = wave;
        Point = point;
        LastAt = at;
        DeathsAtOut = deathsAtOut;
    }

    public int Wave { get; }

    public LdPlace Point { get; }

    public int DeathsAtOut { get; }

    public float Seconds { get; private set; }

    /// <summary>Where the lord stood at the last read; the place a hit is placed at.</summary>
    public LdPoint LastAt { get; private set; }

    public int Given { get; private set; }

    public int Taken { get; private set; }

    public int MountTaken { get; private set; }

    public int Kills { get; private set; }

    /// <summary>The seconds he has been outside the boundary in a row now, and the longest such stretch of the wave.</summary>
    public float Outside { get; private set; }

    public float MaxOutside { get; private set; }

    public bool ContactJudged { get; set; }

    /// <summary>The 180 s have passed and nothing counted as contact.</summary>
    public bool Silent => !ContactJudged && Seconds > ContactSeconds;

    public string Counts => $"hits_given={Given} hits_taken={Taken} mount_hits={MountTaken} kills={Kills}";

    /// <summary>
    /// One tick: the clock runs, a lord who stands is placed and his stretch outside the boundary is kept. True when the 5 s line is due: the clock reached 5 s and he is more than 20 m on the
    /// ground from the keep (with no keep, never). The line clock starts over each time it reaches 5 s, due or not.
    /// </summary>
    public bool Advance(float dt, LdLordFacts lord, LdPoint? keep)
    {
        Seconds += dt;
        _lineClock += dt;
        if (lord.Present)
        {
            LastAt = lord.At;
            Outside = lord.Inside ? 0f : Outside + dt;
            MaxOutside = Math.Max(MaxOutside, Outside);
        }

        if (_lineClock < LineSeconds)
            return false;

        _lineClock = 0f;
        return keep is { } k && lord.Present && LdSpace.Flat(lord.At, k) > OutsideKeepMetres;
    }

    /// <summary>
    /// Counts a hit. True exactly once: the first hit that falls in the first 180 s with the lord within 40 m of the sally point, which is the contact. A hit outside the window or too far away still
    /// counts in the totals.
    /// </summary>
    public bool Add(LdContact contact)
    {
        switch (contact.Kind)
        {
            case LdContactKind.Given:
                Given++;
                if (contact.Kill)
                    Kills++;
                break;
            case LdContactKind.Taken:
                Taken++;
                break;
            case LdContactKind.MountTaken:
                MountTaken++;
                break;
            default:
                return false;
        }

        if (ContactJudged || Seconds > ContactSeconds || !(LdSpace.Flat(LastAt, Point.Point) <= ContactMetres))
            return false;

        ContactJudged = true;
        return true;
    }
}
