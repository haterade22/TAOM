namespace TAOM.Features.MarshalsWord;

/// <summary>The outlines as the session drives them (build-spec 8.2), so a session test fakes them without an <c>Agent</c>.</summary>
internal interface IMarshalOutlines
{
    int PaintedCount { get; }

    void Pass(double now, bool show);

    void ClearAll();

    void Drop();
}
