using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace TAOM.Features.CultureDoctrine.Domain;

/// <summary>
/// Which formation a culture's named troops spawn into, by troop StringId. The engine asks
/// <c>Mission.GetAgentTroopClass</c> for every spawned troop and honours a subscriber to
/// <c>GetAgentTroopClass_Override</c> before its own rule (`Mission.cs:2555-2567`); a row here
/// answers with one of the eight regular classes (Infantry, Ranged, Cavalry, HorseArcher,
/// Skirmisher, HeavyInfantry, LightCavalry, HeavyCavalry). Every vanilla tactic then folds the
/// extra classes back into four (<c>ManageFormationCounts</c>), so a routed formation only
/// survives under a TAOM tactic that keeps its slot: the Harad mumakil ride as
/// <c>HeavyCavalry</c> and <c>TaomTacticMumakVanguard</c> leads with that formation.
/// Immutable; read at spawn on the main thread.
/// </summary>
public sealed class FormationRouting
{
    public static readonly FormationRouting None = new FormationRouting(new Dictionary<string, FormationClass>());

    private readonly Dictionary<string, FormationClass> _byTroop;

    public FormationRouting(IDictionary<string, FormationClass> byTroop)
    {
        _byTroop = new Dictionary<string, FormationClass>(byTroop ?? throw new ArgumentNullException(nameof(byTroop)), StringComparer.Ordinal);
    }

    public int Count => _byTroop.Count;
    public bool IsEmpty => _byTroop.Count == 0;
    public IEnumerable<string> TroopIds => _byTroop.Keys;

    /// <summary>The distinct classes this routing sends troops into.</summary>
    public IEnumerable<FormationClass> Classes
    {
        get
        {
            var seen = new HashSet<FormationClass>();
            foreach (var value in _byTroop.Values)
                if (seen.Add(value))
                    yield return value;
        }
    }

    public bool TryRoute(string? troopId, out FormationClass formationClass)
    {
        if (troopId != null && _byTroop.TryGetValue(troopId, out formationClass))
            return true;
        formationClass = default;
        return false;
    }

    /// <summary>The routable classes: the engine's regular formations, indices 0 to 7.</summary>
    public static bool IsRoutable(FormationClass formationClass) =>
        formationClass >= FormationClass.Infantry && formationClass < FormationClass.NumberOfRegularFormations;

    private static readonly Dictionary<string, FormationClass> RoutableByName = new Dictionary<string, FormationClass>(StringComparer.OrdinalIgnoreCase)
    {
        { "Infantry", FormationClass.Infantry },
        { "Ranged", FormationClass.Ranged },
        { "Cavalry", FormationClass.Cavalry },
        { "HorseArcher", FormationClass.HorseArcher },
        { "Skirmisher", FormationClass.Skirmisher },
        { "HeavyInfantry", FormationClass.HeavyInfantry },
        { "LightCavalry", FormationClass.LightCavalry },
        { "HeavyCavalry", FormationClass.HeavyCavalry },
    };

    public static IEnumerable<string> RoutableNames => RoutableByName.Keys;

    /// <summary>Case-insensitive parse over the eight class names only: no integers and none of
    /// the enum's count aliases (<c>NumberOfDefaultFormations</c> is also 4), so a typo is a
    /// warning rather than a silently different formation.</summary>
    public static bool TryParseClass(string? name, out FormationClass formationClass)
    {
        formationClass = default;
        return !string.IsNullOrWhiteSpace(name) && RoutableByName.TryGetValue(name!.Trim(), out formationClass);
    }
}
