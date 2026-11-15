using System;
using System.Collections.Generic;
using TAOM.Features.Autorun.Scenario;

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

    public bool HideSceneNotification()
    {
        if (!NoticeOpen)
            return false;

        NoticesHidden++;
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
}
