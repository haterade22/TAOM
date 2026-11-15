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
using TAOM.Features.Autorun.Scenario;

namespace TAOM.Client.Features.Autorun;

/// <summary>
/// The engine half of the unattended campaign driver (#1075, <see cref="ScenarioAutorunDriver"/>). It lives in TAOM.Client, so a
/// dedicated server never loads it and can never be armed. Only the client's application tick calls it, so every call runs on the
/// main thread. Each engine fact is cited in docs/features/scenario-autorun.md "Engine facts" (v1.5.7).
/// </summary>
internal sealed class CampaignAutorunHost : ICampaignAutorunHost
{
    private readonly string _outFolder;
    private string? _lastInquiryTitle;
    private bool? _focusSettingBefore;

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

    public bool HideSceneNotification()
    {
        if (MBInformationManager.GetIsAnySceneNotificationActive() != true)
            return false;

        MBInformationManager.HideSceneNotification();
        return true;
    }

    // What the menu's new-game button does with the start options a player leaves unchanged (SandBoxViewSubModule.StartGame). The
    // button itself opens the options screen first, because TAOM's start options provider makes the options non-empty.
    public bool StartNewCampaign()
    {
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
        // Focus loss opens the map's escape menu, which stops the campaign tick. Put back in RestoreFocusSetting.
        _focusSettingBefore = BannerlordConfig.StopGameOnFocusLost;
        BannerlordConfig.StopGameOnFocusLost = false;
        // The engine's own "not attackable" cheat: AI parties never target or join a battle against the main party.
        var main = MobileParty.MainParty;
        main.IgnoreByOtherPartiesTill(CampaignTime.Never);
        main.SetMoveModeHold();
        return "autosave=off focus_pause=off main_party=ignored,hold";
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
