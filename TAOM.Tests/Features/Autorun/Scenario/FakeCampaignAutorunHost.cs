using System;
using System.Collections.Generic;
using TAOM.Features.Autorun.Scenario;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Tests.Features.Autorun.Scenario;

/// <summary>A hand-written host: tests set the screen and the clock, and read back what the driver asked for.</summary>
internal sealed class FakeCampaignAutorunHost : ICampaignAutorunHost
{
    public AutorunScreen ScreenValue = AutorunScreen.MainMenu;
    public string? InquiryTitle;
    public bool NoticeOpen;
    public bool StartResult = true;
    public bool CreationReady = true;
    public string? CreationProblem;
    public bool FastForwardChanged;
    public bool TimeLockedValue;
    public double Days = double.NaN;
    public string Prepared = "autosave off, holding";
    public bool RestoreThrows;
    public bool WriteThrows;
    public bool ScreenThrows;

    public int Starts;
    public int VideoFinishes;
    public int CreationCalls;
    public int PrepareCalls;
    public int Restores;
    public int Ends;
    public int Quits;
    public int InquiriesHidden;
    public int NoticesHidden;
    public string? CreationCulture;
    public float LastSpeed;
    public Action? OnStart;
    public readonly Dictionary<string, string> Files = new();
    public readonly List<string> Order = new();

    public AutorunScreen Screen => ScreenThrows ? throw new InvalidOperationException("screen") : ScreenValue;

    public bool TimeLocked => TimeLockedValue;

    public double ElapsedDays => Days;

    public string? HideInquiry()
    {
        if (InquiryTitle == null)
            return null;

        InquiriesHidden++;
        return InquiryTitle;
    }

    public bool SceneNotificationActive => NoticeOpen;

    public void HideSceneNotification()
    {
        NoticesHidden++;
        NoticeOpen = false;
    }

    public bool EventWindowOpen;
    public int EventWindowsClosed;

    public bool CloseEventWindow()
    {
        if (!EventWindowOpen)
            return false;

        EventWindowsClosed++;
        EventWindowOpen = false;
        return true;
    }

    public bool StartNewCampaign()
    {
        Starts++;
        if (StartResult)
            OnStart?.Invoke();
        return StartResult;
    }

    public bool FinishVideo()
    {
        VideoFinishes++;
        return true;
    }

    public bool TryFinishCreation(string cultureId, out string? problem)
    {
        CreationCalls++;
        CreationCulture = cultureId;
        problem = CreationProblem;
        return CreationReady && CreationProblem == null;
    }

    public string PrepareMap()
    {
        PrepareCalls++;
        return Prepared;
    }

    public bool KeepFastForward(float speedUpMultiplier)
    {
        LastSpeed = speedUpMultiplier;
        return FastForwardChanged;
    }

    public void RestoreFocusSetting()
    {
        Order.Add("restore");
        Restores++;
        if (RestoreThrows)
            throw new InvalidOperationException("restore");
    }

    public void EndCampaign()
    {
        Order.Add("end");
        Ends++;
    }

    public void WriteFile(string name, string text)
    {
        Order.Add("write:" + name);
        if (WriteThrows)
            throw new InvalidOperationException("write");

        Files[name] = text;
    }

    public void Quit()
    {
        Order.Add("quit");
        Quits++;
    }

    /// <summary>A window queued behind <see cref="EventWindowOpen"/>: counts as open or waiting, and closing the open one leaves it.</summary>
    public bool EventWindowWaiting;

    public bool EventWindowOpenOrWaiting => EventWindowOpen || EventWindowWaiting;

    public bool StoryMenuOpens = true;
    public string StoryMenuMessage = "The story menu is open for 'helms_deep_defend'. Choose \"Open the mission\".";
    public bool StoryMenuShownValue;
    public string? OpenMenuIdValue;
    public bool OpenChoiceResult = true;
    public bool BackChoiceResult = true;
    public StoryOutcome? Verdict;

    public int StoryMenuCalls;
    public int OpenChoices;
    public int BackChoices;
    public string? LastMissionId;

    public bool OpenStoryMenu(string missionId, out string message)
    {
        StoryMenuCalls++;
        LastMissionId = missionId;
        message = StoryMenuMessage;
        return StoryMenuOpens;
    }

    public bool StoryMenuShown => StoryMenuShownValue;

    public string? OpenMenuId => OpenMenuIdValue;

    public bool ChooseOpenMission()
    {
        OpenChoices++;
        return OpenChoiceResult;
    }

    public bool ChooseBackToMap()
    {
        BackChoices++;
        return BackChoiceResult;
    }

    public StoryOutcome? StoryVerdict => Verdict;
}
