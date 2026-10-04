using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapPerf;
using TAOM.Features.MissionPerf;

namespace TAOM.Tests.Features.MapPerf;

/// <summary>
/// The Patch101 line contract, pinned literally: the two data lines (<c>[MapProfile]</c>,
/// <c>[MapProfileSummary]</c>) and every <c>[MapProfiler]</c> status line. Status lines must never carry a
/// data tag nor start their body with <c>key=value</c>, so plan 029's log tool reads them as prose.
/// </summary>
[TestClass]
public class MapProfileLinesTests
{
    private const string MapProfileLiteral =
        "[MapProfile] t=+65s frames=300 wallMs=5000.00 realTickMs=400.50 mapScreenMs=1200.25 otherMs=2283.75 allocKB=3072 speed=FF parties=2091 mapStateMs=1500.75 campaignTickMs=950.40 tickEventMs=310.20 appTickMs=15.25 taomMs=115.85 maxFrameMs=48.30 skipped=2 gc0=3 gc1=1 gc2=0 top=FieldCommissionBehavior:60.10/300/1.25/256,RealmBordersMapView:40.50/300/0.90/64";

    private const string SummaryLiteral =
        "[MapProfileSummary] reason=gameEnd session=1 frames=9000 wallMs=150000.00 realTickMs=12000.50 mapScreenMs=36000.25 otherMs=68500.00 allocKB=102400 mapStateMs=45000.75 campaignTickMs=28000.00 tickEventMs=9300.50 appTickMs=499.00 taomMs=3600.25 maxFrameMs=912.40 windows=30 skippedLoading=180 skippedNotTop=12 skippedGap=7 byspeed=Stop:3000/50000.00,FF:6000/100000.00 top=FieldCommissionBehavior:1800.50/9000/2.50/7680,RealmBordersMapView:1300.75/9000/1.75/1024";

    private const string InstallLiteral =
        "[MapProfiler] install: category applied, views category applied, targets 6/6 patched (missing none), MapView overrides 3/3 patched, CampaignEvents.Tick sites 1/1, listener walk bound, allocation counter available";

    private static readonly string[] DataTags =
    {
        "[MapProfile]", "[MapProfileSummary]", "[TickProfile]", "[Hitch]", "[PerfContext]", "[MissionPerf]", "[TickSummary]", "[MapLoad]",
    };

    private static MapWindow SampleWindow(BehaviorTotal[]? top = null) => new MapWindow(300, 5000, 400.5, 1200.25, 2283.75,
        3_145_728, MapSpeedClass.FF, 1500.75, 950.4, 310.2, 15.25, 115.85, 48.3, 2, 3, 1, 0,
        top ?? new[]
        {
            new BehaviorTotal("FieldCommissionBehavior", 60.1, 300, 1.25, 262_144),
            new BehaviorTotal("RealmBordersMapView", 40.5, 300, 0.9, 65_536),
        });

    private static MapSummary SampleSummary(BehaviorTotal[]? top = null) => new MapSummary(1, 9000, 150000, 12000.5,
        36000.25, 68500, 104_857_600, 45000.75, 28000, 9300.5, 499, 3600.25, 912.4, 30, 180, 12, 7,
        new[] { new SpeedTotal(MapSpeedClass.Stop, 3000, 50000), new SpeedTotal(MapSpeedClass.FF, 6000, 100000) },
        top ?? new[]
        {
            new BehaviorTotal("FieldCommissionBehavior", 1800.5, 9000, 2.5, 7_864_320),
            new BehaviorTotal("RealmBordersMapView", 1300.75, 9000, 1.75, 1_048_576),
        });

    [TestMethod]
    public void BuildMapProfile_SampleWindow_MatchesThePinnedLiteral()
    {
        Assert.AreEqual(MapProfileLiteral, MapProfileLines.BuildMapProfile(65.2, SampleWindow(), 2091, true));
    }

    [TestMethod]
    public void BuildSummary_SampleSession_MatchesThePinnedLiteral()
    {
        Assert.AreEqual(SummaryLiteral, MapProfileLines.BuildSummary("gameEnd", SampleSummary(), true));
    }

    [TestMethod]
    public void BuildInstallLine_AllFound_MatchesThePinnedLiteral()
    {
        Assert.AreEqual(InstallLiteral,
            MapProfileLines.BuildInstallLine(true, true, 6, 6, Array.Empty<string>(), 3, 3, 1, true, true));
    }

    [TestMethod]
    public void BuildInstallLine_MissingTargets_NamesThem()
    {
        var line = MapProfileLines.BuildInstallLine(false, true, 4, 6,
            new[] { "MapScreen.OnFrameTick", "Campaign.Tick" }, 3, 3, 0, false, false);

        StringAssert.Contains(line, "targets 4/6 patched (missing MapScreen.OnFrameTick,Campaign.Tick)");
        StringAssert.Contains(line, "category failed");
        Assert.AreEqual(
            "[MapProfiler] install: category failed, views category applied, targets 4/6 patched (missing MapScreen.OnFrameTick,Campaign.Tick), MapView overrides 3/3 patched, CampaignEvents.Tick sites 0/1, listener walk unbound, allocation counter na",
            line);
    }

    [TestMethod]
    public void BuildMapProfile_NoAllocationCounter_WritesNaForEveryKb()
    {
        var line = MapProfileLines.BuildMapProfile(65.2, SampleWindow(), 2091, false);

        StringAssert.Contains(line, " allocKB=na ");
        StringAssert.Contains(line, "top=FieldCommissionBehavior:60.10/300/1.25/na,RealmBordersMapView:40.50/300/0.90/na");
        Assert.IsFalse(Regex.IsMatch(line, @"/\d+(,|$)"), "Every entry ends /na: " + line);
    }

    [TestMethod]
    public void BuildMapProfile_NoEntries_WritesTopNone()
    {
        var line = MapProfileLines.BuildMapProfile(65.2, SampleWindow(Array.Empty<BehaviorTotal>()), 2091, true);

        Assert.IsTrue(line.EndsWith(" top=none", StringComparison.Ordinal), line);
    }

    [TestMethod]
    public void BuildMapProfile_PartiesUnreadable_WritesNa()
    {
        StringAssert.Contains(MapProfileLines.BuildMapProfile(65.2, SampleWindow(), -1, true), " parties=na ");
    }

    [TestMethod]
    public void BuildSummary_NoEntries_WritesTopNone()
    {
        var line = MapProfileLines.BuildSummary("gameEnd", SampleSummary(Array.Empty<BehaviorTotal>()), true);

        Assert.IsTrue(line.EndsWith(" top=none", StringComparison.Ordinal), line);
    }

    [TestMethod]
    public void Build_CommaDecimalCulture_StillWritesInvariantPoints()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.AreEqual(MapProfileLiteral, MapProfileLines.BuildMapProfile(65.2, SampleWindow(), 2091, true));
            Assert.AreEqual(SummaryLiteral, MapProfileLines.BuildSummary("gameEnd", SampleSummary(), true));
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    private static string[] StatusLines() => new[]
    {
        MapProfileLines.OffLine,
        MapProfileLines.RestartNeededLine,
        MapProfileLines.NotInstalledLine,
        MapProfileLines.BuildInstallLine(true, true, 6, 6, Array.Empty<string>(), 3, 3, 1, true, true),
        MapProfileLines.BuildWalkerUnboundLine("x"),
        MapProfileLines.BuildTickEventVanillaLine(0),
        MapProfileLines.BuildViewsShortLine(1, 3),
        MapProfileLines.BuildSessionStartLine(1, 8, 4, 8),
        MapProfileLines.BuildSessionOffLine(1),
        MapProfileLines.BuildNoFramesLine(1, "gameEnd"),
        MapProfileLines.BuildFault("frame boundary", new InvalidOperationException("[x]")),
        MapProfileLines.BuildTopNFallbackLine(0, 8),
        MapProfileLines.BuildInstallFault(new InvalidOperationException("[x]")),
        MapProfileLines.BuildHooksLostLine(1, new[] { "Campaign.RealTick", "MapScreen.OnFrameTick" }),
    };

    [TestMethod]
    public void StatusLines_NeverContainADataTag()
    {
        foreach (var line in StatusLines())
        {
            Assert.IsTrue(line.StartsWith(MapProfileLines.StatusTag + " ", StringComparison.Ordinal), line);
            foreach (var tag in DataTags)
                Assert.IsFalse(line.Contains(tag), $"'{line}' contains the data tag {tag}");
            var body = line.Substring(MapProfileLines.StatusTag.Length + 1);
            Assert.IsFalse(Regex.IsMatch(body, @"^\w+="), "A status line's body must not start with key=value: " + line);
        }
    }

    // taom_debug.log is the maintainer's record (DECISIONS D6): every status line is pinned literally too,
    // so a reword is a deliberate change to the feature doc's log section.
    [TestMethod]
    public void StatusLines_MatchTheirPinnedLiterals()
    {
        var expected = new[]
        {
            "[MapProfiler] off: 'Enable Map Profiler' is off at game start (or MCM was not ready); no patches installed",
            "[MapProfiler] on in MCM but it was off at the first game start, so no patches are installed and nothing is measured; restart the game to measure",
            "[MapProfiler] on in MCM but the install at the first game start failed, so nothing is measured; see the [MapProfiler] install line and [PatchApply]",
            InstallLiteral,
            "[MapProfiler] TickEvent listener walk not bound (x); tickEventMs still times the whole dispatch, but no listener is attributed and taomMs leaves the TAOM listeners out",
            "[MapProfiler] CampaignEvents.Tick left vanilla (sites 0/1); tickEventMs reads 0, no listener is attributed, and the dispatch stays inside campaignTickMs",
            "[MapProfiler] 1 of 3 TAOM map view overrides patched; the others run unattributed inside mapScreenMs",
            "[MapProfiler] session 1: measuring, top 8 entries per line, a window line every 5 s of wall clock while the map ticks, speed classes FF up to 4x, FF2 up to 8x, FF3 above",
            "[MapProfiler] session 1: not measuring, 'Enable Map Profiler' is off in MCM; the patches stay installed and only call through until a restart",
            "[MapProfiler] session 1 end (gameEnd): no frame closed while measuring, so no summary",
            "[MapProfiler] frame boundary failed, measuring stopped for this campaign session: InvalidOperationException: (x)",
            "[MapProfiler] MCM 'Tick Profiler Top Behaviours' reads 0, out of range, so 8 is used for the map lines",
            "[MapProfiler] install failed, nothing is measured in this process: InvalidOperationException: (x)",
            "[MapProfiler] session 1: required hooks no longer patched (Campaign.RealTick, MapScreen.OnFrameTick), most likely stripped by PatchShield after a swallowed exception (see the 'swallowed' and 'unpatched owner' lines in diag.log), so the numbers would no longer be sound: measuring stopped for this campaign session, and nothing reinstalls the hooks until a restart",
        };

        CollectionAssert.AreEqual(expected, StatusLines());
    }
}
