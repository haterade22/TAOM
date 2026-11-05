namespace TAOM.Client.Features.MarshalsWord;

/// <summary>
/// What the view read on one screen frame: the held keys, the three pressed keys, whether the order panel is open, whether the show-indicators
/// key (game key 5) is down, and real time in seconds.
/// </summary>
internal readonly struct MarshalScreenInput
{
    internal MarshalScreenInput(bool queueKeyDown, bool widthsKeyDown, bool panelOpen, float realTime, bool markKeyDown = false,
        bool indicatorsDown = false, bool firePressed = false, bool wordPressed = false, bool holdPressed = false)
    {
        QueueKeyDown = queueKeyDown;
        WidthsKeyDown = widthsKeyDown;
        PanelOpen = panelOpen;
        RealTime = realTime;
        MarkKeyDown = markKeyDown;
        IndicatorsDown = indicatorsDown;
        FirePressed = firePressed;
        WordPressed = wordPressed;
        HoldPressed = holdPressed;
    }

    internal bool QueueKeyDown { get; }

    internal bool WidthsKeyDown { get; }

    internal bool PanelOpen { get; }

    internal float RealTime { get; }

    internal bool MarkKeyDown { get; }

    internal bool IndicatorsDown { get; }

    internal bool FirePressed { get; }

    internal bool WordPressed { get; }

    internal bool HoldPressed { get; }
}
