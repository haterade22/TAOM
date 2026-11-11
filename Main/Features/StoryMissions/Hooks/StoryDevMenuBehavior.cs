using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.StoryMissions.Config;

namespace TAOM.Features.StoryMissions.Hooks;

/// <summary>
/// The dev entry of the story missions (#1105): <c>taom.story_open</c> opens this menu on the campaign map, and its "Open the mission" option
/// (a <c>LeaveType.Mission</c> option, as Host Review's) opens the mission from the menu click, never from a console call or a campaign tick. When the
/// mission ends the engine returns to this menu and "Back to the map" leaves it. The pending request lives on this behavior, which the module builds
/// new for every campaign, so a second campaign starts clean. Holds no saved data and writes nothing to the campaign.
/// </summary>
public sealed class StoryDevMenuBehavior : CampaignBehaviorBase
{
    internal const string MenuId = "taom_story_dev";
    private const string BodyVariable = "TAOM_STORY_DEV";

    private readonly StoryMissionCatalog _catalog;
    private readonly StoryMissionLauncher _launcher;
    private readonly IGameMenuAdapter _menus;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IEncounterAdapter _encounter;
    private string? _pendingId;
    private bool _pendingAuto;

    internal StoryDevMenuBehavior(StoryMissionCatalog catalog, StoryMissionLauncher launcher, IGameMenuAdapter menus, ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated, IEnlistmentStateQuery enlistment, IEncounterAdapter encounter)
    {
        _catalog = catalog;
        _launcher = launcher;
        _menus = menus;
        _coop = coop;
        _dedicated = dedicated;
        _enlistment = enlistment;
        _encounter = encounter;
    }

    public override void RegisterEvents() => CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenu);

    public override void SyncData(IDataStore dataStore)
    {
    }

    /// <summary>Stores the request and opens the menu. Returns the line the console prints.</summary>
    internal string Open(string missionId, bool auto)
    {
        // Read live on every use (ICoopSessionProvider's contract): no mission runs on a co-op authority.
        if (!CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated))
            return "Story missions cannot open in a co-op session.";
        // Host Review's refusals (HostReviewService.GetBlock). The menu switch replaces any open menu, so without them the mission
        // could run over a live encounter or battle, where other features count it as a campaign battle.
        if (_enlistment.IsEnlisted)
            return "Story missions cannot open while you are enlisted.";
        if (_encounter.HasCurrent || _encounter.IsMainPartyInMapEvent)
            return "Story missions cannot open during an encounter or a battle. Return to the campaign map first.";
        var openMenu = _menus.CurrentMenuId;
        if (openMenu != null && openMenu != MenuId)
            return $"Story missions open only from the campaign map. Close the '{openMenu}' menu first.";
        if (_catalog.Missions.Count == 0)
            return "There are no story missions loaded. Check story_missions/story_missions.json and the taom log.";
        if (!_catalog.TryGet(missionId, out var mission))
            return $"Unknown story mission '{missionId}'. Known: {string.Join(", ", _catalog.Missions.Select(m => m.Id))}.";

        _pendingId = mission.Id;
        _pendingAuto = auto;
        if (_menus.EnsureMenuOpen(MenuId))
            return $"The story menu is open for '{mission.Id}'. Choose \"Open the mission\".";

        _pendingId = null;
        return "The story menu could not open. The taom log says why.";
    }

    private void AddMenu(CampaignGameStarter starter)
    {
        starter.AddGameMenu(MenuId, "{=!}{" + BodyVariable + "}", new OnInitDelegate(OnInit), GameMenu.MenuOverlayType.None);
        starter.AddGameMenuOption(MenuId, "taom_story_dev_open", "{=!}Open the mission", OpenCondition, OpenConsequence, isLeave: false, index: 0);
        starter.AddGameMenuOption(MenuId, "taom_story_dev_back", "{=!}Back to the map", BackCondition, args => _menus.ExitToLast(), isLeave: true, index: 1);
    }

    private void OnInit(MenuCallbackArgs args)
    {
        var text = _pendingId == null
            ? "No story mission is waiting. Run taom.story_open <mission> again."
            : $"Story mission '{_pendingId}' is ready{(_pendingAuto ? ", and the bot will play the lord" : "")}. Time stands until you open it.";
        MBTextManager.SetTextVariable(BodyVariable, text, false);
    }

    private bool OpenCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Mission;
        return _pendingId != null;
    }

    private static bool BackCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Leave;
        return true;
    }

    private void OpenConsequence(MenuCallbackArgs args)
    {
        var id = _pendingId;
        _pendingId = null;
        if (id != null && !_launcher.Launch(id, _pendingAuto))
            InformationManager.DisplayMessage(new InformationMessage($"Story mission '{id}' could not open. The taom log says why.", Colors.Red));
    }
}
