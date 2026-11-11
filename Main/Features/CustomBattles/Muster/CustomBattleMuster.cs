using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.CustomBattles.Hooks;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.CustomBattles.Muster;

/// <summary>
/// The hand-over between the muster screen (TAOM.Client, ADR-013) and the battle it starts. The screen leaves the
/// army in <see cref="Pending"/>; the GetCustomBattleParties postfix builds the sides from it (once), sets
/// <see cref="Applied"/> and leaves the roles to hand out in <see cref="Plan"/>; the mission behaviors read the plan.
/// Every battle started from the vanilla screen resets it (Pending is null there), so roles only follow a muster.
/// Main thread only: the screen, the helper and OnMissionTick all run there.
/// </summary>
internal static class CustomBattleMuster
{
    internal sealed class Role
    {
        public BattleSideEnum Side;
        public BasicCharacterObject Unit;
        public BasicCharacterObject? Captain;
        public bool Banner;
        public bool Horn;
    }

    private static ISideCommanderFilter? _commanders;
    private static IModLogger? _logger;

    /// <summary>The army the screen hands over for the battle it starts (used once).</summary>
    internal static ArmySpec? Pending;

    /// <summary>The roles of the battle being started, for <see cref="CustomBattleRolesBehavior"/>.</summary>
    internal static List<Role> Plan = new List<Role>();

    /// <summary>True when this battle's sides came from a muster: the role and horn behaviors join only then.</summary>
    internal static bool Applied;

    internal static void Initialize(ISideCommanderFilter commanders, IModLogger logger)
    {
        _commanders = commanders;
        _logger = logger;
    }

    internal static void Log(string message) => _logger?.LogInfo("[CustomBattleMuster] " + message);

    /// <summary>TAOM's curated commander ids for a culture, the list the vanilla screen offers (custom_battle_commanders.json).</summary>
    internal static IReadOnlyList<string> CuratedCommanderIds(string cultureId)
    {
        if (_commanders == null || string.IsNullOrEmpty(cultureId)) return Array.Empty<string>();
        try
        {
            return _commanders.ResolveCommandersForCulture(cultureId).Select(c => c.StringId).ToList();
        }
        catch (Exception e)
        {
            Log("curated commanders for " + cultureId + ": " + e.Message);
            return Array.Empty<string>();
        }
    }

    /// <summary>A faction's commander: the first curated one, else the culture's highest lord, else its highest hero.</summary>
    internal static BasicCharacterObject? CommanderOf(BasicCultureObject? culture)
    {
        if (culture == null) return null;
        foreach (var id in CuratedCommanderIds(culture.StringId))
        {
            var c = MBObjectManager.Instance.GetObject<BasicCharacterObject>(id);
            if (c != null) return c;
        }
        return MBObjectManager.Instance.GetObjectTypeList<BasicCharacterObject>()
            .Where(c => c.IsHero && c.Culture == culture)
            .OrderByDescending(c => c.StringId.StartsWith("lord_", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(c => c.Level)
            .FirstOrDefault();
    }

    /// <summary>Adds the role and horn behaviors to a Custom Battle mission whose sides came from a muster.</summary>
    internal static void AddMissionBehaviors(Mission mission)
    {
        if (mission == null || !Applied) return;
        var roles = new CustomBattleRolesBehavior();
        mission.AddMissionBehavior(roles);
        mission.AddMissionBehavior(new CustomBattleHornCallBehavior(roles));
    }
}
