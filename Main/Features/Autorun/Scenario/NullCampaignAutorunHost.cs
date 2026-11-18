using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Features.Autorun.Scenario;

/// <summary>
/// Core's default campaign autorun host (ADR-013): there is no menu, no game and nothing to quit. TAOM.Client replaces it on a
/// rendering client; a dedicated server never builds the driver, so none of this is ever called there.
/// </summary>
internal sealed class NullCampaignAutorunHost : ICampaignAutorunHost
{
    public AutorunScreen Screen => AutorunScreen.Other;

    public bool TimeLocked => false;

    public double ElapsedDays => double.NaN;

    public string? HideInquiry() => null;

    public bool SceneNotificationActive => false;

    public void HideSceneNotification()
    {
    }

    public bool CloseEventWindow() => false;

    public bool StartNewCampaign() => false;

    public bool FinishVideo() => false;

    public bool TryFinishCreation(string cultureId, out string? problem)
    {
        problem = null;
        return false;
    }

    public string PrepareMap() => "";

    public bool KeepFastForward(float speedUpMultiplier) => false;

    public void RestoreFocusSetting()
    {
    }

    public void EndCampaign()
    {
    }

    public void WriteFile(string name, string text)
    {
    }

    public void Quit()
    {
    }

    public bool EventWindowOpenOrWaiting => false;

    public bool OpenStoryMenu(string missionId, out string message)
    {
        message = "no campaign";
        return false;
    }

    public bool StoryMenuShown => false;

    public string? OpenMenuId => null;

    public bool ChooseOpenMission() => false;

    public bool ChooseBackToMap() => false;

    public StoryOutcome? StoryVerdict => null;

    public string RunConsoleCommand(string commandLine) => "";
}
