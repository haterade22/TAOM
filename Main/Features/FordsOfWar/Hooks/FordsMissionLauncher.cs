using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// Opens the Fords of War mission (build-spec section 4). The client's game manager calls <see cref="Launch"/> from
/// <c>OnLoadFinished</c>. The scene is a plain non-campaign mission with two <see cref="CustomBattleCombatant"/>s built from the
/// factions' cultures: West is the Defender and the player's side (the player commands as general), East the Attacker. NO player
/// agent is ever spawned. The behavior list is the lord's-hall list (BannerlordMissions.cs:273-291) cut down to what a match with
/// no deployment, no morale and no battle-end logic needs; the director issues every movement order itself. All the types come
/// from TaleWorlds.MountAndBlade, which a headless server has (ADR-013).
/// </summary>
internal sealed class FordsMissionLauncher
{
    internal const BattleSideEnum WestSide = BattleSideEnum.Defender;
    internal const BattleSideEnum EastSide = BattleSideEnum.Attacker;

    // Spring at noon is this mode's own pick: the installed OpenCustomBattleMission defaults timeOfDay to 6f (BannerlordMissions.cs:109).
    // A scene that forces its own atmosphere overrides this (Patch16 AtmospherePersistence).
    private const string Season = "spring";
    private const int TimeOfDay = 12;

    private static readonly Type[] Order =
    {
        typeof(MissionCombatantsLogic),
        typeof(AgentHumanAILogic),
        typeof(AmmoSupplyLogic),
        typeof(MissionHardBorderPlacer),
        typeof(MissionBoundaryPlacer),
        typeof(MissionOptionsComponent),
        typeof(FordsOfWarMissionLogic),
    };

    private readonly FordsConfigCatalog _catalog;
    private readonly FordsSession _session;
    private readonly IModLogger _logger;

    public FordsMissionLauncher(FordsConfigCatalog catalog, FordsSession session, IModLogger logger)
    {
        _catalog = catalog;
        _session = session;
        _logger = logger;
    }

    /// <summary>The behavior types <see cref="CreateBehaviors"/> builds, in order. The combatants logic comes first so the teams exist for every later behavior.</summary>
    internal static IReadOnlyList<Type> BehaviorTypes() => Order;

    /// <summary>True once the mission is open. Never throws: a setup that cannot load logs an error and returns false.</summary>
    public bool Launch(FordsMatchSetup setup)
    {
        try
        {
            if (!_catalog.TryGetMap(setup.SceneName, out var map)
                || !_catalog.TryGetFaction(setup.WestFaction, out var west)
                || !_catalog.TryGetFaction(setup.EastFaction, out var east))
            {
                _logger.LogError($"[FordsOfWar] the setup names a scene or faction the config does not have ({setup}); nothing launched");
                return false;
            }

            var westCulture = MBObjectManager.Instance?.GetObject<BasicCultureObject>(west.CultureId);
            var eastCulture = MBObjectManager.Instance?.GetObject<BasicCultureObject>(east.CultureId);
            if (westCulture == null || eastCulture == null)
            {
                _logger.LogError($"[FordsOfWar] culture '{west.CultureId}' or '{east.CultureId}' is not registered in this game; nothing launched");
                return false;
            }

            var sameCulture = westCulture.StringId == eastCulture.StringId;
            var westCombatant = Combatant("{=!}Fords of War, West", westCulture, WestSide, false);
            var eastCombatant = Combatant("{=!}Fords of War, East", eastCulture, EastSide, sameCulture);

            MissionState.OpenNew(FordsOfWarMission.MissionName, Record(map.SceneId), _ => CreateBehaviors(setup, westCombatant, eastCombatant));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FordsOfWar] opening the mission failed: {ex}");
            return false;
        }
    }

    /// <summary>One instance per entry of <see cref="BehaviorTypes"/>, in the same order.</summary>
    internal IReadOnlyList<MissionBehavior> CreateBehaviors(FordsMatchSetup setup, CustomBattleCombatant west, CustomBattleCombatant east) =>
        new MissionBehavior[]
        {
            new MissionCombatantsLogic(null, west, west, east, Mission.MissionTeamAITypeEnum.NoTeamAI, isPlayerSergeant: false),
            new AgentHumanAILogic(),
            new AmmoSupplyLogic(new List<BattleSideEnum> { WestSide, EastSide }),
            new MissionHardBorderPlacer(),
            new MissionBoundaryPlacer(),
            new MissionOptionsComponent(),
            new FordsOfWarMissionLogic(setup, _catalog, _session, _logger),
        };

    private static MissionInitializerRecord Record(string scene) =>
        new(scene)
        {
            DoNotUseLoadingScreen = false,
            PlayingInCampaignMode = false,
            AtmosphereOnCampaign = BannerlordMissions.CreateAtmosphereInfoForMission(Season, TimeOfDay),
            SceneLevels = "",
            DecalAtlasGroup = 2,
        };

    // The vanilla custom battle's banner: the culture's own, and when both sides share a culture the second one swaps its
    // colors so the two armies still look different (CustomBattleHelper.GetCustomBattleParties).
    private static CustomBattleCombatant Combatant(string name, BasicCultureObject culture, BattleSideEnum side, bool swapColors)
    {
        var banner = new Banner(culture.Banner, culture.Color, culture.Color2);
        if (swapColors)
        {
            var primary = banner.GetPrimaryColor();
            banner.ChangePrimaryColor(banner.GetFirstIconColor());
            banner.ChangeIconColors(primary);
        }

        return new CustomBattleCombatant(new TextObject(name), culture, banner) { Side = side };
    }
}
