using System.Collections.Generic;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// Reads each troop of the player's side from the engine's character objects, once before the run, because the domain knows no character: a character's flags give its group
/// (<see cref="BoonTargets.OfCharacter"/>), the same rule the hooks apply to an agent. A troop the engine does not know is left out, and the launcher has refused the run already.
/// </summary>
internal static class LongDefeatTroopGroups
{
    internal static IReadOnlyDictionary<string, BoonTarget> Read(RunPlan plan)
    {
        var groups = new Dictionary<string, BoonTarget>();
        var objects = MBObjectManager.Instance;
        foreach (var id in RunPlanTroops.DefenderTroopIds(plan))
        {
            var character = objects?.GetObject<BasicCharacterObject>(id);
            if (character != null)
                groups[id] = BoonTargets.OfCharacter(character.IsMounted, character.IsRanged);
        }

        return groups;
    }
}
