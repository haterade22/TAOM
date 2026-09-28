using TaleWorlds.Localization;

namespace TAOM.Features.CreatureBandits.Diagnostics;

internal enum CreatureDiagEventKind
{
    HitTaken,
    BiteLanded,
    Removed,
    KilledByCreature,
    Panicked,
    Fled,
    Alarmed,
    Deleted,
    Backstopped,
    Mounted,
}

/// <summary>
/// One engine callback about a creature (#692), captured as primitives on whatever thread the engine raised it and
/// drained on the main thread, where it is logged. Never holds an Agent: a recycled slot would answer for its next
/// tenant (csharp-architecture.md, "Mission-scope agent handles"). The other agent's name travels as its TextObject (a
/// field read) and is resolved on the main thread when the line is written: the localizer's state is shared, not
/// thread-safe (MBTextManager).
/// </summary>
internal readonly struct CreatureDiagEvent
{
    internal CreatureDiagEvent(CreatureDiagEventKind kind, int serial, float time, int threadId, bool onMain,
        string otherTroop = "-", TextObject? otherName = null, int otherIndex = -1, bool otherIsMount = false,
        bool byPlayer = false, int damage = 0, bool missile = false, bool blocked = false, bool charge = false,
        int weaponClass = -1, string detail = "-", float healthAfter = float.NaN, bool aimed = false)
    {
        Kind = kind;
        Serial = serial;
        Time = time;
        ThreadId = threadId;
        OnMain = onMain;
        OtherTroop = otherTroop;
        _otherName = otherName;
        OtherIndex = otherIndex;
        OtherIsMount = otherIsMount;
        ByPlayer = byPlayer;
        Damage = damage;
        Missile = missile;
        Blocked = blocked;
        Charge = charge;
        WeaponClass = weaponClass;
        Detail = detail;
        HealthAfter = healthAfter;
        Aimed = aimed;
    }

    internal CreatureDiagEventKind Kind { get; }
    internal int Serial { get; }
    internal float Time { get; }
    internal int ThreadId { get; }
    internal bool OnMain { get; }
    internal string OtherTroop { get; }
    private readonly TextObject? _otherName;

    /// <summary>The other agent's name. Main thread only: it runs the localizer.</summary>
    internal string OtherName => _otherName?.ToString() ?? "-";
    internal int OtherIndex { get; }
    internal bool OtherIsMount { get; }
    internal bool ByPlayer { get; }
    internal int Damage { get; }
    internal bool Missile { get; }
    internal bool Blocked { get; }
    internal bool Charge { get; }
    internal int WeaponClass { get; }
    internal string Detail { get; }
    internal float HealthAfter { get; }
    /// <summary>The affector was targeting a creature at the last 1 s census (aimed, not incidental).</summary>
    internal bool Aimed { get; }
}
