namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// One frame of commander camera input, already read from the keys. PanForward and PanRight run -1..1; Rotate is +1 for a turn left
/// (Q) and -1 for a turn right (E); ZoomNotches is positive for the wheel rolled away (zoom in); DragPixelsX and DragPixelsY are the
/// cursor movement since the last frame while the right button is held; Fast is Shift.
/// </summary>
internal readonly struct FordsCameraInput
{
    public FordsCameraInput(float panForward, float panRight, float rotate, float zoomNotches, float dragPixelsX, float dragPixelsY, bool fast)
    {
        PanForward = panForward;
        PanRight = panRight;
        Rotate = rotate;
        ZoomNotches = zoomNotches;
        DragPixelsX = dragPixelsX;
        DragPixelsY = dragPixelsY;
        Fast = fast;
    }

    public float PanForward { get; }
    public float PanRight { get; }
    public float Rotate { get; }
    public float ZoomNotches { get; }
    public float DragPixelsX { get; }
    public float DragPixelsY { get; }
    public bool Fast { get; }
}
