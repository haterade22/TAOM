using TAOM.Core.Validation;
using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Features.CultureDoctrine.Doctrines;

public enum TargetClass
{
    /// <summary>Melee foot: the formation a line must beat.</summary>
    Infantry,

    /// <summary>Archers: foot, but they pull back (<c>BehaviorSkirmish</c>), so the infantry
    /// beside them is preferred within <see cref="TargetSelection.ArchersOverInfantryFactor"/>.</summary>
    Archers,

    /// <summary>Cavalry or horse archers: never chased while foot remain, braced for while a
    /// threat.</summary>
    Horse,
}

/// <summary>
/// Which enemy formation a foot formation goes for. Vanilla's <c>BehaviorCharge</c> and
/// <c>BehaviorTacticalCharge</c> run at <c>CachedClosestEnemyFormation</c> whatever its class
/// (`BehaviorCharge.cs:18`, `BehaviorTacticalCharge.cs:62,151`), so a passing eored, usually the
/// nearest thing, turns the whole line to chase horse it will never catch. Here the target is
/// the nearest enemy infantry, or the nearest archers when no infantry stands within
/// <see cref="ArchersOverInfantryFactor"/> times their distance; horse are a target only when no
/// foot is left. A horse formation the caller marks as a <c>threat</c> (melee cavalry of real
/// size, riding at us or already among us: <see cref="CavalryThreat"/>) counts for
/// <see cref="CavalryThreat(in EngagementTunables, bool)"/> inside
/// <see cref="EngagementTunables.CavalryMattersMetres"/>, which the wall answers by bracing
/// where it stands, not by turning; a wall already braced keeps the square until they are
/// beyond <see cref="ReleaseFactor"/> times that distance, so a formation cycling at the edge
/// does not flip it. One instance per behaviour, refilled per tick with <see cref="Reset"/>
/// then <see cref="Consider"/> per enemy formation; no allocation. A NaN distance is never
/// nearest and never a threat.
/// </summary>
public sealed class TargetSelection
{
    public const int None = -1;

    /// <summary>Infantry is preferred over archers while it stands within this factor of the
    /// archers' distance.</summary>
    public const float ArchersOverInfantryFactor = 1.5f;

    /// <summary>A braced wall releases only once the threat is beyond this factor times
    /// <see cref="EngagementTunables.CavalryMattersMetres"/>.</summary>
    public const float ReleaseFactor = 1.5f;

    private int _infantry = None;
    private int _archers = None;
    private int _horse = None;
    private float _infantryDistance = float.PositiveInfinity;
    private float _archersDistance = float.PositiveInfinity;
    private float _horseDistance = float.PositiveInfinity;
    private float _nearestThreat = float.PositiveInfinity;

    public void Reset()
    {
        _infantry = None;
        _archers = None;
        _horse = None;
        _infantryDistance = float.PositiveInfinity;
        _archersDistance = float.PositiveInfinity;
        _horseDistance = float.PositiveInfinity;
        _nearestThreat = float.PositiveInfinity;
    }

    /// <summary><paramref name="index"/> is the caller's handle for the formation;
    /// <paramref name="threat"/> is the caller's verdict on a horse formation (ignored for
    /// foot).</summary>
    public void Consider(int index, TargetClass targetClass, float distance, bool threat)
    {
        if (!FiniteFloatValidator.IsFinite(distance))
            return;
        switch (targetClass)
        {
            case TargetClass.Infantry:
                if (distance < _infantryDistance)
                {
                    _infantryDistance = distance;
                    _infantry = index;
                }
                return;
            case TargetClass.Archers:
                if (distance < _archersDistance)
                {
                    _archersDistance = distance;
                    _archers = index;
                }
                return;
            default:
                if (distance < _horseDistance)
                {
                    _horseDistance = distance;
                    _horse = index;
                }
                if (threat && distance < _nearestThreat)
                    _nearestThreat = distance;
                return;
        }
    }

    /// <summary>The nearest infantry (within the factor of the nearest archers), else the
    /// nearest archers, else the nearest horse, else <see cref="None"/>.</summary>
    public int Target
    {
        get
        {
            if (_infantry != None && (_archers == None || _infantryDistance <= _archersDistance * ArchersOverInfantryFactor))
                return _infantry;
            if (_archers != None)
                return _archers;
            return _horse;
        }
    }

    /// <summary>A threat horse formation is inside the distance that matters, or, for a wall
    /// already <paramref name="braced"/>, inside <see cref="ReleaseFactor"/> times it.</summary>
    public bool CavalryThreat(in EngagementTunables tunables, bool braced) =>
        _nearestThreat <= tunables.CavalryMattersMetres * (braced ? ReleaseFactor : 1f);
}
