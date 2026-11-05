using TaleWorlds.Localization;

namespace TAOM.Features.ShaderCompileNotice;

/// <summary>
/// The render-wait notice's texts, localized on the main thread. The titles carry the placeholders
/// <see cref="CountToken"/> and <see cref="SecondsToken"/>, filled on the notice's own thread, which
/// must not touch the engine's text manager.
/// </summary>
internal sealed class RenderWaitNoticeTexts
{
    public const string CountToken = "%COUNT%";
    public const string SecondsToken = "%SECONDS%";

    private RenderWaitNoticeTexts(string compilingTitle, string compilingDetail, string stalledTitle, string stalledDetail)
    {
        CompilingTitle = compilingTitle ?? "";
        CompilingDetail = compilingDetail ?? "";
        StalledTitle = stalledTitle ?? "";
        StalledDetail = stalledDetail ?? "";
    }

    public string CompilingTitle { get; }
    public string CompilingDetail { get; }
    public string StalledTitle { get; }
    public string StalledDetail { get; }

    /// <summary>Engine boundary: main thread only.</summary>
    public static RenderWaitNoticeTexts Resolve()
    {
        var compilingTitle = new TextObject("{=taom_rw_notice_compiling_title}Preparing the battle: {COUNT} shaders compiled");
        compilingTitle.SetTextVariable("COUNT", CountToken);
        var compilingDetail = new TextObject(
            "{=taom_rw_notice_compiling_detail}The game is compiling this battle's shaders. It is not frozen. The first battle after a game update or a change to your mods can take several minutes.");
        var stalledTitle = new TextObject("{=taom_rw_notice_stalled_title}Still loading: no shader compiles for {SECONDS} s");
        stalledTitle.SetTextVariable("SECONDS", SecondsToken);
        var stalledDetail = new TextObject(
            "{=taom_rw_notice_stalled_detail}The game's own log shows no progress. If nothing changes for 5 minutes the game may be stuck: create a dump file of Bannerlord.exe in Task Manager and send your TAOM logs.");
        return new RenderWaitNoticeTexts(
            compilingTitle.ToString(), compilingDetail.ToString(), stalledTitle.ToString(), stalledDetail.ToString());
    }
}
