namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// The notice's texts, already localized on the game thread. The titles carry the placeholders
/// <see cref="CountToken"/> and <see cref="TotalToken"/>, filled in by <see cref="ShaderCompileProgress.Title"/>
/// on the notice's own thread, which must not touch the engine's text manager.
/// </summary>
public sealed class ShaderCompileNoticeTexts
{
    public const string CountToken = "%COUNT%";
    public const string TotalToken = "%TOTAL%";

    public ShaderCompileNoticeTexts(string titleWithTotal, string titleCountOnly, string detail)
    {
        TitleWithTotal = titleWithTotal ?? "";
        TitleCountOnly = titleCountOnly ?? "";
        Detail = detail ?? "";
    }

    public string TitleWithTotal { get; }

    public string TitleCountOnly { get; }

    public string Detail { get; }
}
