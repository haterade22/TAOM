using TAOM.Features.ShadowfaxScreens.Diagnostics;

namespace TAOM.Features.ShadowfaxScreens.Quests;

/// <summary>The QuestsVM constructor's Harmony state: the service whose scope the prefix opened, and the timer window.</summary>
public sealed class QuestOpenCall
{
    internal QuestOpenCall(QuestLogDeferService? service, ScreenOpenWindow? window)
    {
        Service = service;
        Window = window;
    }

    public QuestLogDeferService? Service { get; }

    public ScreenOpenWindow? Window { get; }
}
