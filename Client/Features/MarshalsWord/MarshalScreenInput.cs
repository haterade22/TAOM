namespace TAOM.Client.Features.MarshalsWord;

/// <summary>What the view read on one screen frame: the two held keys, whether the order panel is open, and real time in seconds.</summary>
internal readonly struct MarshalScreenInput
{
    internal MarshalScreenInput(bool queueKeyDown, bool widthsKeyDown, bool panelOpen, float realTime)
    {
        QueueKeyDown = queueKeyDown;
        WidthsKeyDown = widthsKeyDown;
        PanelOpen = panelOpen;
        RealTime = realTime;
    }

    internal bool QueueKeyDown { get; }

    internal bool WidthsKeyDown { get; }

    internal bool PanelOpen { get; }

    internal float RealTime { get; }
}
