using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.MapPerf;

/// <summary>
/// Every line the Patch101 campaign map profiler writes; every other file logs only these members.
///
/// Data lines (<c>[MapProfile]</c> every 5 s of wall clock while the map ticks, <c>[MapProfileSummary]</c>
/// when a campaign session ends) follow plan 028's conventions: invariant culture, ms with two decimals,
/// KB as whole bytes / 1024 or <c>na</c> when the allocation counter is unavailable, <c>t</c> as whole
/// seconds since the session's first frame boundary, <c>top=none</c> when no entry ran, and <c>top=</c>
/// last because its value holds commas; an entry is <c>&lt;Type&gt;:&lt;ms&gt;/&lt;calls&gt;/&lt;maxMs&gt;/&lt;KB&gt;</c>.
/// <c>MapProfileLinesTests</c> pins each line literally.
///
/// Status lines carry <see cref="StatusTag"/> (<c>[MapProfiler]</c>, which contains no data tag) and never
/// start their body with <c>key=value</c>, so plan 029's log tool reads them as prose. An exception
/// message is quoted with its square brackets turned into parentheses for the same reason.
/// </summary>
public static class MapProfileLines
{
    public const string StatusTag = "[MapProfiler]";

    public const string OffLine =
        StatusTag + " off: 'Enable Map Profiler' is off at game start (or MCM was not ready); no patches installed";

    public const string RestartNeededLine =
        StatusTag + " on in MCM but it was off at the first game start, so no patches are installed and nothing is measured; restart the game to measure";

    public const string NotInstalledLine =
        StatusTag + " on in MCM but the install at the first game start failed, so nothing is measured; see the [MapProfiler] install line and [PatchApply]";

    private const string Ms = "0.00";

    public static string BuildMapProfile(double tSeconds, MapWindow w, int parties, bool allocAvailable)
    {
        var sb = new StringBuilder(384);
        sb.Append("[MapProfile] t=+").Append(Seconds(tSeconds)).Append("s frames=").Append(Int(w.Frames));
        sb.Append(" wallMs=").Append(Num(w.WallMs));
        sb.Append(" realTickMs=").Append(Num(w.RealTickMs));
        sb.Append(" mapScreenMs=").Append(Num(w.MapScreenMs));
        sb.Append(" otherMs=").Append(Num(w.OtherMs));
        sb.Append(" allocKB=").Append(Kb(w.AllocBytes, allocAvailable));
        sb.Append(" speed=").Append(MapSpeed.Token(w.Speed));
        sb.Append(" parties=").Append(parties < 0 ? "na" : Int(parties));
        sb.Append(" mapStateMs=").Append(Num(w.MapStateMs));
        sb.Append(" campaignTickMs=").Append(Num(w.CampaignTickMs));
        sb.Append(" tickEventMs=").Append(Num(w.TickEventMs));
        sb.Append(" appTickMs=").Append(Num(w.AppTickMs));
        sb.Append(" taomMs=").Append(Num(w.TaomMs));
        sb.Append(" maxFrameMs=").Append(Num(w.MaxFrameMs));
        sb.Append(" skipped=").Append(Int(w.Skipped));
        sb.Append(" gc0=").Append(Int(w.Gc0)).Append(" gc1=").Append(Int(w.Gc1)).Append(" gc2=").Append(Int(w.Gc2));
        AppendTop(sb, w.Top, allocAvailable);
        return sb.ToString();
    }

    public static string BuildSummary(string reason, MapSummary s, bool allocAvailable)
    {
        var sb = new StringBuilder(384);
        sb.Append("[MapProfileSummary] reason=").Append(reason);
        sb.Append(" session=").Append(Int(s.Session));
        sb.Append(" frames=").Append(Int(s.Frames));
        sb.Append(" wallMs=").Append(Num(s.WallMs));
        sb.Append(" realTickMs=").Append(Num(s.RealTickMs));
        sb.Append(" mapScreenMs=").Append(Num(s.MapScreenMs));
        sb.Append(" otherMs=").Append(Num(s.OtherMs));
        sb.Append(" allocKB=").Append(Kb(s.AllocBytes, allocAvailable));
        sb.Append(" mapStateMs=").Append(Num(s.MapStateMs));
        sb.Append(" campaignTickMs=").Append(Num(s.CampaignTickMs));
        sb.Append(" tickEventMs=").Append(Num(s.TickEventMs));
        sb.Append(" appTickMs=").Append(Num(s.AppTickMs));
        sb.Append(" taomMs=").Append(Num(s.TaomMs));
        sb.Append(" maxFrameMs=").Append(Num(s.MaxFrameMs));
        sb.Append(" windows=").Append(Int(s.Windows));
        sb.Append(" skippedLoading=").Append(Int(s.SkippedLoading));
        sb.Append(" skippedNotTop=").Append(Int(s.SkippedNotTop));
        sb.Append(" skippedGap=").Append(Int(s.SkippedGap));
        sb.Append(" byspeed=");
        if (s.BySpeed.Count == 0)
            sb.Append("none");
        for (var i = 0; i < s.BySpeed.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            var b = s.BySpeed[i];
            sb.Append(MapSpeed.Token(b.Speed)).Append(':').Append(Int(b.Frames)).Append('/').Append(Num(b.WallMs));
        }
        AppendTop(sb, s.Top, allocAvailable);
        return sb.ToString();
    }

    /// <summary>The one-line configuration header of an install attempt: what applied, which of the six core
    /// targets are patched (naming the missing ones), how many TAOM map view overrides were found and patched,
    /// the <c>CampaignEvents.Tick</c> call-site swap, the listener walk binding and the allocation counter.</summary>
    public static string BuildInstallLine(bool coreApplied, bool viewsApplied, int targetsPatched, int targetsTotal,
        IReadOnlyList<string> missing, int viewsPatched, int viewsFound, int tickEventSites, bool walkerBound, bool allocAvailable) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} install: category {1}, views category {2}, targets {3}/{4} patched (missing {5}), MapView overrides {6}/{7} patched, CampaignEvents.Tick sites {8}/1, listener walk {9}, allocation counter {10}",
            StatusTag, coreApplied ? "applied" : "failed", viewsApplied ? "applied" : "failed", targetsPatched, targetsTotal,
            missing.Count == 0 ? "none" : string.Join(",", missing), viewsPatched, viewsFound, tickEventSites,
            walkerBound ? "bound" : "unbound", allocAvailable ? "available" : "na");

    public static string BuildWalkerUnboundLine(string detail) =>
        StatusTag + " TickEvent listener walk not bound (" + Quote(detail)
        + "); tickEventMs still times the whole dispatch, but no listener is attributed and taomMs leaves the TAOM listeners out";

    public static string BuildTickEventVanillaLine(int sites) =>
        StatusTag + " CampaignEvents.Tick left vanilla (sites " + Int(sites)
        + "/1); tickEventMs reads 0, no listener is attributed, and the dispatch stays inside campaignTickMs";

    public static string BuildViewsShortLine(int patched, int found) =>
        StatusTag + " " + Int(patched) + " of " + Int(found)
        + " TAOM map view overrides patched; the others run unattributed inside mapScreenMs";

    /// <summary>Required Patch101 hooks found unpatched at a session start, a window start or a measuring session's end
    /// (PatchShield strips a swallowed throw's method by owner, and the profiler's owner is not protected): one line
    /// naming every one of them.</summary>
    public static string BuildHooksLostLine(int session, IReadOnlyList<string> lost) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} session {1}: required hooks no longer patched ({2}), most likely stripped by PatchShield after a swallowed exception (see the 'swallowed' and 'unpatched owner' lines in diag.log), so the numbers would no longer be sound: measuring stopped for this campaign session, and nothing reinstalls the hooks until a restart",
            StatusTag, session, string.Join(", ", lost));

    /// <summary>The per-session configuration header of a measuring session.</summary>
    public static string BuildSessionStartLine(int session, int topN, int fastForward, int extraFastForward) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} session {1}: measuring, top {2} entries per line, a window line every 5 s of wall clock while the map ticks, speed classes FF up to {3}x, FF2 up to {4}x, FF3 above",
            StatusTag, session, topN, fastForward, extraFastForward);

    public static string BuildSessionOffLine(int session) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} session {1}: not measuring, 'Enable Map Profiler' is off in MCM; the patches stay installed and only call through until a restart",
            StatusTag, session);

    public static string BuildNoFramesLine(int session, string reason) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} session {1} end ({2}): no frame closed while measuring, so no summary", StatusTag, session, reason);

    public static string BuildFault(string where, Exception ex) =>
        StatusTag + " " + where + " failed, measuring stopped for this campaign session: " + ex.GetType().Name + ": " + Quote(ex.Message);

    /// <summary>The install itself threw: no profiler exists, so nothing is measured until a restart.</summary>
    public static string BuildInstallFault(Exception ex) =>
        StatusTag + " install failed, nothing is measured in this process: " + ex.GetType().Name + ": " + Quote(ex.Message);

    /// <summary>The shared "Tick Profiler Top Behaviours" value read out of range (a hand-edited settings
    /// file; the MCM page clamps its own input), so the provider's default sizes the map lines.</summary>
    public static string BuildTopNFallbackLine(int raw, int used) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} MCM 'Tick Profiler Top Behaviours' reads {1}, out of range, so {2} is used for the map lines", StatusTag, raw, used);

    private static void AppendTop(StringBuilder sb, IReadOnlyList<BehaviorTotal> top, bool allocAvailable)
    {
        sb.Append(" top=");
        if (top.Count == 0)
        {
            sb.Append("none");
            return;
        }
        for (var i = 0; i < top.Count; i++)
        {
            var b = top[i];
            if (i > 0)
                sb.Append(',');
            sb.Append(b.Name).Append(':').Append(Num(b.Ms)).Append('/').Append(Int(b.Calls)).Append('/')
                .Append(Num(b.MaxMs)).Append('/').Append(Kb(b.AllocBytes, allocAvailable));
        }
    }

    private static string Num(double value) => value.ToString(Ms, CultureInfo.InvariantCulture);

    private static string Seconds(double value) => value.ToString("0", CultureInfo.InvariantCulture);

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Kb(long bytes, bool available) =>
        available ? (bytes / 1024).ToString(CultureInfo.InvariantCulture) : "na";

    private static string Quote(string? message) =>
        (message ?? string.Empty).Replace('[', '(').Replace(']', ')').Replace('\r', ' ').Replace('\n', ' ');
}
