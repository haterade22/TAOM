using System.IO;
using System.Linq;
using SandBox;
using SandBox.AdvancedStartOptions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Client.Features.EventWindow;
using TAOM.Features.Autorun.Scenario;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.StoryMissions.Hooks;

namespace TAOM.Client.Features.Autorun;

/// <summary>
/// The engine half of the unattended campaign driver (#1075, <see cref="ScenarioAutorunDriver"/>). It lives in TAOM.Client, so a
/// dedicated server never loads it and can never be armed. Only the client's application tick calls it, so every call runs on the
/// main thread. Each engine fact is cited in docs/features/scenario-autorun.md "Engine facts" (v1.5.7).
/// For a story mission (#1368) it opens the dev story menu through <see cref="StoryDevMenuBehavior"/>'s own Open and picks the menu's options on
/// the path a person's click takes (<c>MenuContext.InvokeConsequence</c> after <c>GameMenu.GetMenuOptionConditionsHold</c>), from the application
/// tick: never from the console and never inside the campaign tick. It skips the console command's cheat-mode gate, which is safe because only
/// the arm file starts the driver.
/// </summary>
internal sealed class CampaignAutorunHost : ICampaignAutorunHost
{
    private readonly string _outFolder;
    private string? _lastInquiryTitle;
    private bool? _focusSettingBefore;
    private StoryOutcome? _storyVerdict;

    public CampaignAutorunHost(ScenarioPlan plan)
    {
        _outFolder = plan.OutFolder;
        // The engine does not expose the open inquiry, so the title is taken when one is shown. Only an armed run builds this host.
        InformationManager.OnShowInquiry += (data, _, _) => _lastInquiryTitle = data?.TitleText ?? "";
        InformationManager.OnShowTextInquiry += (data, _, _) => _lastInquiryTitle = data?.TitleText ?? "";
    }

    public AutorunScreen Screen =>
        GameStateManager.Current?.ActiveState switch
        {
            InitialState when Game.Current == null => AutorunScreen.MainMenu,
            VideoPlaybackState => AutorunScreen.Video,
            CharacterCreationState => AutorunScreen.CharacterCreation,
            MapState map => map.AtMenu ? AutorunScreen.MapMenu : AutorunScreen.Map,
            MissionState => AutorunScreen.Mission,
            _ => AutorunScreen.Other,
        };

    public string? HideInquiry()
    {
        if (!InformationManager.IsAnyInquiryActive())
            return null;

        InformationManager.HideInquiry();
        var title = _lastInquiryTitle ?? "";
        _lastInquiryTitle = null;
        return title;
    }

    public bool SceneNotificationActive => MBInformationManager.GetIsAnySceneNotificationActive() == true;

    public void HideSceneNotification() => MBInformationManager.HideSceneNotification();

    // The story event windows share the Realms popup slot and open one per frame from their queue. In Running, RealmsClient.Tick opens
    // a queued window earlier in this same application tick (ClientSubModule.OnApplicationTick), so the driver closes it before the
    // game state ticks again, outside any layer tick. RealmsPopups.Close runs the same RealmsPopupLayer.Close as the Continue button;
    // its teardown lifts the campaign pause (UnregisterActiveStateDisableRequest).
    public bool CloseEventWindow()
    {
        if (!TAOM.Client.Features.Realms.Popups.RealmsPopups.IsOpen)
            return false;

        TAOM.Client.Features.Realms.Popups.RealmsPopups.Close();
        return true;
    }

    // What the start options screen's Confirm does when nothing changed (GauntletCampaignStartingOptionsView.OnConfirm): the menu
    // button opens that screen whenever the options list is not empty (SandBoxViewSubModule.StartGame), and vanilla's provider fills it.
    public bool StartNewCampaign()
    {
        // Focus loss opens the map's escape menu, which stops the campaign tick. Off from the start, so no focus loss before the map
        // settles can stop the run. In memory only: the driver restores it at every game end, before the engine saves the config.
        _focusSettingBefore ??= BannerlordConfig.StopGameOnFocusLost;
        BannerlordConfig.StopGameOnFocusLost = false;
        var options = AdvancedStartOptionsManager.CreateCampaignStartOptions();
        MBGameManager.StartNewGame(new SandBoxGameManager(() => new Campaign(CampaignGameMode.Campaign, options.GetChangedOptions())));
        return true;
    }

    public bool FinishVideo()
    {
        if (GameStateManager.Current?.ActiveState is not VideoPlaybackState video)
            return false;

        video.OnVideoFinished();
        return true;
    }

    // At the culture stage: pick the culture as the culture screen does, take every later stage out, and advance. With no stage left
    // NextStage runs ApplyFinalEffects (culture, start position, every content handler's finalize, TAOM's included) and finishes
    // creation, as vanilla's last stage does. No narrative option is selected, so TAOM assigns the career itself.
    public bool TryFinishCreation(string cultureId, out string? problem)
    {
        problem = null;
        if (GameStateManager.Current?.ActiveState is not CharacterCreationState state)
            return false;

        var manager = state.CharacterCreationManager;
        if (manager?.CurrentStage is not CharacterCreationCultureStage)
            return false;

        var culture = manager.CharacterCreationContent.GetCultures().FirstOrDefault(c => c.StringId == cultureId);
        if (culture == null)
        {
            problem = "character creation does not offer culture '" + cultureId + "'";
            return false;
        }

        // As the culture screen does (CharacterCreationCultureStageVM): SetSelectedCulture names the clan from the hero's culture.
        Hero.MainHero.Culture = culture;
        manager.CharacterCreationContent.SetSelectedCulture(culture, manager);
        manager.RemoveStage<CharacterCreationFaceGeneratorStage>();
        manager.RemoveStage<CharacterCreationNarrativeStage>();
        manager.RemoveStage<CharacterCreationBannerEditorStage>();
        manager.RemoveStage<CharacterCreationClanNamingStage>();
        manager.RemoveStage<CharacterCreationReviewStage>();
        manager.RemoveStage<CharacterCreationOptionsStage>();
        manager.NextStage();
        return true;
    }

    public string PrepareMap()
    {
        var campaign = Campaign.Current;
        if (campaign == null)
            return "no campaign";

        // Process-local: the player's autosave setting in the config file is not touched.
        campaign.SandBoxManager.SandBoxSaveManager = new NoAutoSaveManager();
        // As the set_main_party_attackable 0 cheat does: an ignored party refuses interaction and no encounter forms around it. The
        // cheat also holds the parties already chasing it; the refusal covers those, and the hold here is the main party's own.
        var main = MobileParty.MainParty;
        main.IgnoreByOtherPartiesTill(CampaignTime.Never);
        main.SetMoveModeHold();
        return "autosave=off main_party=ignored,hold";
    }

    public bool KeepFastForward(float speedUpMultiplier)
    {
        var campaign = Campaign.Current;
        if (campaign == null)
            return false;

        var reset = false;
        if (campaign.SpeedUpMultiplier != speedUpMultiplier)
        {
            campaign.SpeedUpMultiplier = speedUpMultiplier;
            reset = true;
        }

        if (!campaign.TimeControlModeLock && campaign.TimeControlMode != CampaignTimeControlMode.UnstoppableFastForward)
        {
            campaign.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForward;
            reset = true;
        }

        return reset;
    }

    public bool TimeLocked => Campaign.Current?.TimeControlModeLock ?? false;

    public double ElapsedDays =>
        Campaign.Current?.Models?.CampaignTimeModel is { } model ? model.CampaignStartTime.ElapsedDaysUntilNow : double.NaN;

    public void RestoreFocusSetting()
    {
        if (_focusSettingBefore is not { } before)
            return;

        BannerlordConfig.StopGameOnFocusLost = before;
        _focusSettingBefore = null;
    }

    public void EndCampaign()
    {
        if (Campaign.Current != null)
            MBGameManager.EndGame();
    }

    public void WriteFile(string name, string text)
    {
        Directory.CreateDirectory(_outFolder);
        var path = Path.Combine(_outFolder, name);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);
    }

    public void Quit() => TaleWorlds.Engine.Utilities.QuitGame();

    // EventWindows.Tick opens a queued window whenever a game exists, a mission included, and its layer stops the active state ticking.
    public bool EventWindowOpenOrWaiting => EventWindows.IsOpen;

    public bool OpenStoryMenu(string missionId, out string message)
    {
        _storyVerdict = null;
        var behavior = Campaign.Current?.GetCampaignBehavior<StoryDevMenuBehavior>();
        if (behavior == null)
        {
            message = "the story menu is not loaded in this campaign";
            return false;
        }

        message = behavior.Open(missionId, true, verdict => _storyVerdict = verdict);
        return StoryMenuShown;
    }

    public bool StoryMenuShown => OpenMenuId == StoryDevMenuBehavior.MenuId;

    public string? OpenMenuId => MapMenuContext()?.GameMenu?.StringId;

    public bool ChooseOpenMission() => Choose(StoryDevMenuBehavior.OpenOptionId);

    public bool ChooseBackToMap() => Choose(StoryDevMenuBehavior.BackOptionId);

    public StoryOutcome? StoryVerdict => _storyVerdict;

    // The console's own dispatch: the name, then the rest split on spaces (v1.5.7 CommandLineFunctionality.CallFunction :99).
    public string RunConsoleCommand(string commandLine)
    {
        var line = (commandLine ?? "").Trim();
        var space = line.IndexOf(' ');
        var name = space < 0 ? line : line.Substring(0, space);
        var args = space < 0 ? string.Empty : line.Substring(space + 1).Trim();
        return CommandLineFunctionality.CallFunction(name, args, out _) ?? string.Empty;
    }

    // The map's own menu only. Campaign.CurrentMenuContext also returns the MapState's context under a mission (it reads the active state's
    // predecessor), where no menu shows.
    private static MenuContext? MapMenuContext() => (GameStateManager.Current?.ActiveState as MapState)?.MenuContext;

    // A click is GameMenuItemVM.ExecuteAction -> MenuContext.InvokeConsequence(Index) -> GameMenuManager.RunConsequenceOfVirtualMenuOption.
    // InvokeConsequence runs no condition: the menu shows only an option whose condition holds (GameMenuVM, GetVirtualMenuOptionConditionsHold)
    // and enables its button from IsEnabled, which the condition sets. With no repeat objects the virtual index is the list index.
    private bool Choose(string optionId)
    {
        var context = MapMenuContext();
        var menu = context?.GameMenu;
        if (context == null || menu == null || menu.StringId != StoryDevMenuBehavior.MenuId || menu.MenuRepeatObjects.Count > 0)
            return false;

        var options = menu.MenuOptions.ToList();
        var index = options.FindIndex(option => option.IdString == optionId);
        if (index < 0 || !menu.GetMenuOptionConditionsHold(Game.Current, context, index) || !options[index].IsEnabled)
            return false;

        context.InvokeConsequence(index);
        return true;
    }

    // Autosave off for this process: the SaveHandler skips an autosave when the interval is -1 or the manager says disabled.
    private sealed class NoAutoSaveManager : ISaveManager
    {
        public int GetAutoSaveInterval() => -1;

        public bool IsAutoSaveDisabled() => true;

        public void OnSaveOver(bool isSuccessful, string newSaveGameName)
        {
        }
    }
}
