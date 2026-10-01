using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.RealmBorders;
using TAOM.Features.RealmBorders.Domain;
using static TAOM.Tests.Features.RealmBorders.RealmTerritoryServiceTests;

namespace TAOM.Tests.Features.RealmBorders;

/// <summary>
/// Pins the drawn borders against the campaign: where the line lands, that a capture moves it and
/// nothing else redraws, the fade, the toggle, the map modes, the player's gold frontier and the
/// crossing notice. Runs the real territory, selection and painting over fake adapters.
/// </summary>
[TestClass]
public class RealmBorderServiceTests
{
    private static readonly string ModuleData = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Main", "_Module", "ModuleData"));

    private static readonly BorderLook Look = new BorderLook();

    private sealed class Rig
    {
        public readonly Dictionary<(int X, int Y), IReadOnlyList<BorderQuad>> Drawn = new Dictionary<(int X, int Y), IReadOnlyList<BorderQuad>>();
        public readonly Dictionary<string, string?> Owner = new Dictionary<string, string?>();
        public readonly IRealmMapAdapter Map;
        public readonly IBorderRenderAdapter Renderer = Substitute.For<IBorderRenderAdapter>();
        public readonly IRealmBordersSettings Settings = Substitute.For<IRealmBordersSettings>();
        public readonly IAlignmentService Alignment = Substitute.For<IAlignmentService>();
        public readonly IRealmNoticeAdapter Notices = Substitute.For<IRealmNoticeAdapter>();
        public readonly FakeTerrain Terrain = new FakeTerrain();
        public readonly IModLogger Logger = Substitute.For<IModLogger>();
        public readonly RealmTerritoryService Territory;
        public readonly RealmPaletteProvider Palettes;
        public readonly RealmBorderService Service;
        public int Uploads;

        public Rig(params (string Id, float X, string? Realm)[] fiefs)
            : this(new SyncWorker(), fiefs)
        {
        }

        public Rig(ITerritoryWorker worker, params (string Id, float X, string? Realm)[] fiefs)
        {
            Map = MapWith(fiefs.Select(f => Town(f.Id, f.X, 100)).ToArray());
            foreach (var f in fiefs)
                Owner[f.Id] = f.Realm;
            Map.RealmOf(Arg.Any<string>()).Returns(ci => Owner.TryGetValue(ci.Arg<string>(), out var r) ? r : null);
            Map.RealmName(Arg.Any<string>()).Returns(ci => "Name of " + ci.Arg<string>());
            Map.CultureOfRealm(Arg.Any<string>()).Returns(ci => ci.Arg<string>());

            Renderer.IsAvailable.Returns(true);
            Renderer.When(r => r.SetTile(Arg.Any<(int X, int Y)>(), Arg.Any<IReadOnlyList<BorderQuad>>(), Arg.Any<float>(), Arg.Any<bool>()))
                .Do(ci => { Drawn[ci.ArgAt<(int X, int Y)>(0)] = ci.ArgAt<IReadOnlyList<BorderQuad>>(1); Uploads++; });
            Renderer.When(r => r.RemoveTile(Arg.Any<(int X, int Y)>())).Do(ci => Drawn.Remove(ci.ArgAt<(int X, int Y)>(0)));
            Renderer.When(r => r.Clear()).Do(_ => Drawn.Clear());

            Settings.Enabled.Returns(true);
            Settings.GildPlayerRealm.Returns(true);
            Settings.WidthScale.Returns(1f);
            Settings.FadeStartDistance.Returns(45f);
            Settings.FullOpacityDistance.Returns(110f);
            Settings.DrawThroughTerrain.Returns(true);
            Settings.RealmNames.Returns(true);
            Settings.CrossingNotices.Returns(true);
            Settings.MaterialName.Returns((string?)null); // the provider's "Automatic"; a substitute would say ""
            Settings.BlendMode.Returns((string?)null);

            var paths = Substitute.For<IPathService>();
            paths.ModuleDataPath.Returns(ModuleData);
            Territory = ReadyTerritory(Terrain, Map);
            Palettes = new RealmPaletteProvider(paths, Substitute.For<IModLogger>());
            Service = new RealmBorderService(Territory, Map, Renderer, Settings, Palettes, Alignment, Notices, worker, Logger);
            Service.OnSessionStart();
        }

        public void Settle(float distance = 200f)
        {
            for (int i = 0; i < 60; i++)
                Service.OnMapFrame(distance);
        }

        public List<BorderQuad> Quads => Drawn.Values.SelectMany(q => q).ToList();

        public float InkX()
        {
            var ink = Quads.Where(q => q.NearStart.Colour == Look.InkColour).ToList();
            Assert.IsTrue(ink.Count > 0, "no ink drawn");
            return ink.Average(q => q.NearStart.Position.X);
        }
    }

    [TestMethod]
    public void OnMapFrame_TwoRealms_DrawTheBorderBetweenThem()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));

        rig.Settle();

        Assert.AreEqual(200f, rig.InkX(), 3f);
    }

    [TestMethod]
    public void OnMapFrame_OneRealmOnBothSides_DrawsNothing()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_w"));

        rig.Settle();

        Assert.AreEqual(0, rig.Drawn.Count);
    }

    [TestMethod]
    public void MarkDirty_AfterACapture_TheBorderMovesWithTheFief()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("middle", 200, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        Assert.AreEqual(250f, rig.InkX(), 3f);

        rig.Owner["middle"] = "empire_s";
        rig.Service.MarkDirty();
        rig.Settle();

        Assert.AreEqual(150f, rig.InkX(), 3f);
    }

    [TestMethod]
    public void MarkDirty_NothingChanged_UploadsNoTile()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        int uploads = rig.Uploads;

        rig.Service.MarkDirty();
        rig.Settle();

        Assert.AreEqual(uploads, rig.Uploads);
    }

    [TestMethod]
    public void OnMapFrame_Disabled_ClearsTheMap()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();

        rig.Settings.Enabled.Returns(false);
        rig.Settle();

        Assert.AreEqual(0, rig.Drawn.Count);
    }

    [TestMethod]
    public void OnMapFrame_CameraDistance_FadesTheBorders()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle(200f);
        Assert.AreEqual(1f, rig.Service.Alpha);

        rig.Service.OnMapFrame(30f);
        Assert.AreEqual(0f, rig.Service.Alpha, "zoomed in below the fade start");

        rig.Service.OnMapFrame(77.5f);
        Assert.AreEqual(0.5f, rig.Service.Alpha, 0.02f);
    }

    [TestMethod]
    public void OnMapFrame_SteadyCamera_SetsTheAlphaOnlyWhenItChanges()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle(200f);
        rig.Renderer.ClearReceivedCalls();

        rig.Service.OnMapFrame(30f);
        rig.Service.OnMapFrame(30f);
        rig.Service.OnMapFrame(30f);

        rig.Renderer.Received(1).SetAlpha(Arg.Any<float>());
    }

    [TestMethod]
    public void OnMapFrame_NaNCameraDistance_HidesTheBorders()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle(200f);

        rig.Service.OnMapFrame(float.NaN);

        Assert.AreEqual(0f, rig.Service.Alpha);
    }

    [TestMethod]
    public void ToggleVisible_HidesThenShows()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle(200f);

        rig.Service.ToggleVisible();
        rig.Service.OnMapFrame(200f);
        Assert.AreEqual(0f, rig.Service.Alpha);

        rig.Service.ToggleVisible();
        rig.Service.OnMapFrame(200f);
        Assert.AreEqual(1f, rig.Service.Alpha);
    }

    [TestMethod]
    public void CycleMode_AnnouncesTheModeAndRepaints()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();

        rig.Service.CycleMode();

        Assert.AreEqual(MapMode.Alignment, rig.Service.Mode);
        rig.Notices.Received(1).ShowMapMode(MapMode.Alignment);
    }

    [TestMethod]
    public void AlignmentMode_TwoFreeRealms_DrawNoLineBetweenThem()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "vlandia"));
        rig.Alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(FactionSide.Free);
        rig.Settle();
        Assert.IsTrue(rig.Drawn.Count > 0, "Gondor and Rohan are two realms");

        rig.Service.CycleMode();
        rig.Settle();

        Assert.AreEqual(0, rig.Drawn.Count, "but one side");
    }

    [TestMethod]
    public void AlignmentMode_NeutralRealm_StandsOnNeitherSideOfTheFront()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "battania"));
        rig.Alignment.ResolveSide("empire_w", Arg.Any<string>()).Returns(FactionSide.Free);
        rig.Alignment.ResolveSide("battania", Arg.Any<string>()).Returns(FactionSide.Neutral);

        rig.Service.CycleMode();
        rig.Settle();

        Assert.AreEqual(0, rig.Drawn.Count, "the mode draws one front line, and a neutral realm is on neither side of it");
    }

    [TestMethod]
    public void AlignmentMode_FreeAgainstShadow_DrawsTheFrontInSideColours()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Alignment.ResolveSide("empire_w", Arg.Any<string>()).Returns(FactionSide.Free);
        rig.Alignment.ResolveSide("empire_s", Arg.Any<string>()).Returns(FactionSide.Evil);

        rig.Service.CycleMode();
        rig.Settle();

        Assert.IsTrue(rig.Quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == (RealmBorderService.FreePeoplesColour & 0xFFFFFF)));
        Assert.IsTrue(rig.Quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == (RealmBorderService.ShadowColour & 0xFFFFFF)));
    }

    private static uint RelationColour(string group) => RealmBorderService.RelationColours[group] & 0xFFFFFF;

    [TestMethod]
    public void WarMode_TheEnemyOfThePlayer_BurnsAlongTheFront()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Map.RelationToPlayer("empire_w").Returns(RealmRelation.Own);
        rig.Map.RelationToPlayer("empire_s").Returns(RealmRelation.Enemy);

        rig.Service.CycleMode();
        rig.Service.CycleMode();
        rig.Settle();

        Assert.AreEqual(MapMode.War, rig.Service.Mode);
        Assert.IsTrue(rig.Quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == (Look.EmberColour & 0xFFFFFF)));
    }

    [TestMethod]
    public void WarMode_ANeutralNeighbour_KeepsTheLookInTheNameplateColours()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "vlandia"));
        rig.Map.RelationToPlayer("empire_w").Returns(RealmRelation.Own);
        rig.Map.RelationToPlayer("vlandia").Returns(RealmRelation.Neutral);

        rig.Service.CycleMode();
        rig.Service.CycleMode();
        rig.Settle();

        Assert.IsFalse(rig.Quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == (Look.EmberColour & 0xFFFFFF)), "no front without an enemy");
        Assert.IsTrue(rig.Quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == RelationColour(RelationGroups.Own)));
        Assert.IsTrue(rig.Quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == RelationColour(RelationGroups.Neutral)));
    }

    [TestMethod]
    public void WarMode_TwoRealmsOfTheSameStanding_DrawNoLineBetweenThem()
    {
        var rig = new Rig(("west", 100, "empire_s"), ("east", 300, "isengard"));
        rig.Map.RelationToPlayer(Arg.Any<string>()).Returns(RealmRelation.Enemy);

        rig.Service.CycleMode();
        rig.Service.CycleMode();
        rig.Settle();

        Assert.AreEqual(0, rig.Drawn.Count, "Mordor and Isengard are both the player's enemies");
    }

    [TestMethod]
    public void HeraldicSetting_DrawsTheBandsWithKeylines()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settings.HeraldicBands.Returns(true);

        rig.Settle();

        Assert.IsTrue(rig.Quads.Any(q => q.NearStart.Colour == Look.KeylineColour));
        Assert.IsFalse(rig.Quads.Any(q => q.NearStart.Colour == Look.InkColour));
    }

    [TestMethod]
    public void PlayersOwnFrontier_TakesTheGoldCord()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Map.PlayerRealm.Returns("empire_w");

        rig.Settle();

        Assert.IsTrue(rig.Quads.Any(q => q.NearStart.Colour == Look.GoldColour));
    }

    [TestMethod]
    public void OnHourlyTick_PartyRidesIntoAnotherRealm_AnnouncesIt()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        float x = 100f, y = 100f;
        rig.Map.TryGetMainPartyPosition(out Arg.Any<float>(), out Arg.Any<float>())
            .Returns(ci => { ci[0] = x; ci[1] = y; return true; });

        rig.Service.OnHourlyTick();
        x = 300f;
        rig.Service.OnHourlyTick();

        rig.Notices.Received(1).ShowEnteringRealm("Name of empire_s");
        rig.Notices.DidNotReceive().ShowEnteringRealm("Name of empire_w");
    }

    [TestMethod]
    public void OnHourlyTick_NoticesOff_StaysSilent()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settings.CrossingNotices.Returns(false);
        rig.Settle();
        float x = 100f;
        rig.Map.TryGetMainPartyPosition(out Arg.Any<float>(), out Arg.Any<float>())
            .Returns(ci => { ci[0] = x; ci[1] = 100f; return true; });

        rig.Service.OnHourlyTick();
        x = 300f;
        rig.Service.OnHourlyTick();

        rig.Notices.DidNotReceive().ShowEnteringRealm(Arg.Any<string>());
    }

    [TestMethod]
    public void OnSessionStart_ForgetsTheDrawnBordersAndTheMode()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Service.CycleMode();
        rig.Settle();

        rig.Service.OnSessionStart();

        Assert.AreEqual(MapMode.Political, rig.Service.Mode);
        Assert.AreEqual(0, rig.Drawn.Count);
        Assert.AreEqual(0, rig.Service.DrawnTiles);
    }

    [TestMethod]
    public void Repaint_PlacesANamePerRealm()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));

        rig.Settle();

        CollectionAssert.AreEquivalent(new[] { "empire_s", "empire_w" }, rig.Service.Labels.Select(l => l.Realm).ToArray());
    }

    [TestMethod]
    public void RealmNamesSwitchedOff_TheNamesGoOnTheNextFrame()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        Assert.AreEqual(2, rig.Service.Labels.Count);

        rig.Settings.RealmNames.Returns(false);
        rig.Service.OnMapFrame(200f);

        Assert.AreEqual(0, rig.Service.Labels.Count);
    }

    [TestMethod]
    public void Rebuild_RecomputesTheProvinces()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();

        int queries = rig.Terrain.Queries;

        rig.Service.Rebuild();

        Assert.AreEqual(0, rig.Drawn.Count);
        rig.Service.OnMapFrame(200f);
        Assert.IsTrue(rig.Terrain.Queries > queries, "the terrain is sampled again");
    }

    [TestMethod]
    public void ProvinceImage_BeforeTheProvincesAreReady_IsNull()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));

        rig.Service.Rebuild();

        Assert.IsNull(rig.Service.ProvinceImage());
    }

    [TestMethod]
    public void ProvinceImage_PaintsEachRealmItsEdgesTheOwnerlessFiefAndTheSea()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, null));
        rig.Terrain.TerrainAt = (x, y) => y > 180f ? -1 : 1; // a sea along the northern edge
        rig.Service.Rebuild();
        rig.Settle();

        var image = rig.Service.ProvinceImage();

        Assert.IsNotNull(image);
        var (columns, rows, pixels) = image!.Value;
        Assert.AreEqual((512, 256), (columns, rows));
        float cell = 400f / 512;
        uint At(float x, float y) => pixels[(int)(y / cell) * columns + (int)(x / cell)];
        Assert.AreEqual(rig.Palettes.NewPalette().ColourOf("empire_w"), At(50, 100), "the realm");
        Assert.AreEqual(RealmBorderService.PictureUnownedFief, At(350, 100), "a fief without an owner");
        Assert.AreEqual(RealmBorderService.PictureWater, At(200, 195), "the sea, row 0 being the south");
        Assert.AreEqual(RealmBorderService.PictureEdge, At(199.8f, 100), "the realm's edge against the landless fief");
    }

    // --- review fixes (2026-09-30) ---

    /// <summary>Holds each repaint until the test releases it, as the real worker thread would.</summary>
    private sealed class ManualWorker : ITerritoryWorker
    {
        private readonly List<Action> _queued = new List<Action>();

        public Task<T> Run<T>(Func<T> work)
        {
            var done = new TaskCompletionSource<T>();
            _queued.Add(() => done.SetResult(work()));
            return done.Task;
        }

        public int Queued => _queued.Count;

        public void RunAll()
        {
            var queued = _queued.ToList();
            _queued.Clear();
            foreach (var run in queued)
                run();
        }
    }

    [TestMethod]
    public void OnMapFrame_DisabledThenEnabled_RedrawsTheBordersAndNames()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        rig.Settings.Enabled.Returns(false);
        rig.Settle();
        Assert.AreEqual(0, rig.Drawn.Count);

        rig.Settings.Enabled.Returns(true);
        rig.Settle();

        Assert.IsTrue(rig.Drawn.Count > 0, "the borders come back");
        Assert.AreEqual(2, rig.Service.Labels.Count, "and so do the names");
    }

    [TestMethod]
    public void Keys_WhileTheFeatureIsOff_DoNothing()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settings.Enabled.Returns(false);

        rig.Service.ToggleVisible();
        rig.Service.CycleMode();

        Assert.IsTrue(rig.Service.Visible);
        Assert.AreEqual(MapMode.Political, rig.Service.Mode);
        rig.Notices.DidNotReceive().ShowMapMode(Arg.Any<MapMode>());
    }

    [TestMethod]
    public void OnSessionStart_NextCampaign_HandsOutTheReserveAfresh()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "rebels_a"));
        rig.Settle();
        uint first = PixelAt(rig, 350, 100);

        rig.Owner["east"] = "rebels_b";
        rig.Service.OnSessionStart();
        rig.Settle();

        Assert.AreEqual(first, PixelAt(rig, 350, 100), "a new campaign starts the reserve from its best colour again");
    }

    [TestMethod]
    public void Alpha_FeatureSwitchedOff_IsNothingWhateverWasDrawnLast()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        Assert.IsTrue(rig.Service.Alpha > 0f);

        rig.Settings.Enabled.Returns(false);

        Assert.AreEqual(0f, rig.Service.Alpha, "the parchment map reads this to know the borders are showing");
    }

    private static uint PixelAt(Rig rig, float x, float y)
    {
        var (columns, _, pixels) = rig.Service.ProvinceImage()!.Value;
        float cell = 400f / 512;
        return pixels[(int)(y / cell) * columns + (int)(x / cell)];
    }

    [TestMethod]
    public void OnSessionStart_NextCampaign_StartsVisibleAndItsFirstHourIsSilent()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        float x = 100f;
        rig.Map.TryGetMainPartyPosition(out Arg.Any<float>(), out Arg.Any<float>())
            .Returns(ci => { ci[0] = x; ci[1] = 100f; return true; });
        rig.Service.OnHourlyTick();
        x = 300f;
        rig.Service.OnHourlyTick();
        rig.Service.ToggleVisible();
        rig.Notices.ClearReceivedCalls();

        rig.Service.OnSessionStart();
        x = 100f;
        rig.Service.OnHourlyTick();

        Assert.IsTrue(rig.Service.Visible);
        rig.Notices.DidNotReceive().ShowEnteringRealm(Arg.Any<string>());
    }

    [TestMethod]
    public void OnMapFrame_UploadAtASteadyCamera_DoesNotReapplyTheFade()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("middle", 200, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle(200f);
        rig.Renderer.ClearReceivedCalls();
        int uploads = rig.Uploads;

        rig.Owner["middle"] = "empire_s";
        rig.Service.MarkDirty();
        rig.Settle(200f);

        Assert.IsTrue(rig.Uploads > uploads, "the capture redrew tiles");
        rig.Renderer.DidNotReceive().SetAlpha(Arg.Any<float>());
    }

    [TestMethod]
    public void MarkDirty_NothingOnScreenChanged_SkipsTheRepaint()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("middle", 200, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        var labels = rig.Service.Labels;
        int uploads = rig.Uploads;

        rig.Service.MarkDirty(); // a mercenary contract, a re-grant inside one realm
        rig.Settle();

        Assert.AreSame(labels, rig.Service.Labels, "nothing was repainted");
        Assert.AreEqual(uploads, rig.Uploads);
    }

    [TestMethod]
    public void CycleMode_KeepsTheRealmNamesWithoutPlacingThemAgain()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        var labels = rig.Service.Labels;

        rig.Service.CycleMode();
        rig.Settle();

        Assert.AreSame(labels, rig.Service.Labels);
    }

    [TestMethod]
    public void OnMapScreenClosed_ReleasesTheSceneAndDrawsIntoTheNextScreen()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();

        rig.Service.OnMapScreenClosed();

        rig.Renderer.Received(1).Release();
        Assert.AreEqual(0, rig.Service.DrawnTiles);
        rig.Settle();
        Assert.IsTrue(rig.Drawn.Count > 0);
    }

    [TestMethod]
    public void UseMaterial_Known_RedrawsEveryTile()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        int uploads = rig.Uploads;
        rig.Renderer.UseMaterial("vertex_color_lighting").Returns(true);

        Assert.IsTrue(rig.Service.UseMaterial("vertex_color_lighting"));
        rig.Settle();

        Assert.IsTrue(rig.Uploads >= 2 * uploads && uploads > 0, "every tile was built again");
    }

    [TestMethod]
    public void UseMaterial_Unknown_KeepsTheTiles()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        int drawn = rig.Drawn.Count;

        Assert.IsFalse(rig.Service.UseMaterial("no_such_material"));

        Assert.AreEqual(drawn, rig.Drawn.Count);
    }

    [TestMethod]
    public void OnMapFrame_NoMapScene_DrawsNothing()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Renderer.IsAvailable.Returns(false);

        rig.Settle();

        Assert.AreEqual(0, rig.Uploads);
    }

    [TestMethod]
    public void DrawThroughTerrainSwitched_RedrawsEveryTileWithTheNewDepth()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        int tiles = rig.Drawn.Count;
        rig.Renderer.ClearReceivedCalls();

        rig.Settings.DrawThroughTerrain.Returns(false);
        rig.Settle();

        rig.Renderer.Received(tiles).SetTile(Arg.Any<(int X, int Y)>(), Arg.Any<IReadOnlyList<BorderQuad>>(), Arg.Any<float>(), false);
        rig.Renderer.DidNotReceive().SetTile(Arg.Any<(int X, int Y)>(), Arg.Any<IReadOnlyList<BorderQuad>>(), Arg.Any<float>(), true);
    }

    [TestMethod]
    public void OnHourlyTick_FeatureOff_StaysSilent()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        rig.Settings.Enabled.Returns(false);

        RideFromWestToEast(rig);

        rig.Notices.DidNotReceive().ShowEnteringRealm(Arg.Any<string>());
    }

    [TestMethod]
    public void OnHourlyTick_ProvincesNotReady_StaysSilent()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Service.Rebuild(); // the provinces are recomputed on the next map frame

        RideFromWestToEast(rig);

        rig.Notices.DidNotReceive().ShowEnteringRealm(Arg.Any<string>());
    }

    [TestMethod]
    public void OnHourlyTick_NoPartyOnTheMap_StaysSilent()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        rig.Map.TryGetMainPartyPosition(out Arg.Any<float>(), out Arg.Any<float>()).Returns(false);

        rig.Service.OnHourlyTick();
        rig.Service.OnHourlyTick();

        rig.Notices.DidNotReceive().ShowEnteringRealm(Arg.Any<string>());
    }

    [TestMethod]
    public void OnHourlyTick_NaNPartyPosition_StaysSilentAndDoesNotThrow()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        float x = 100f;
        rig.Map.TryGetMainPartyPosition(out Arg.Any<float>(), out Arg.Any<float>())
            .Returns(ci => { ci[0] = x; ci[1] = 100f; return true; });
        rig.Service.OnHourlyTick();

        x = float.NaN;
        rig.Service.OnHourlyTick();

        rig.Notices.DidNotReceive().ShowEnteringRealm(Arg.Any<string>());
    }

    private static void RideFromWestToEast(Rig rig)
    {
        float x = 100f;
        rig.Map.TryGetMainPartyPosition(out Arg.Any<float>(), out Arg.Any<float>())
            .Returns(ci => { ci[0] = x; ci[1] = 100f; return true; });
        rig.Service.OnHourlyTick();
        x = 300f;
        rig.Service.OnHourlyTick();
    }

    [TestMethod]
    public void Repaint_ForgottenWhileOnTheWorker_IsDroppedAndPaintedAgain()
    {
        var worker = new ManualWorker();
        var rig = new Rig(worker, ("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Service.OnMapFrame(200f);
        Assert.AreEqual(1, worker.Queued, "the repaint waits on the worker");
        rig.Renderer.UseMaterial("vertex_color_lighting").Returns(true);
        rig.Service.UseMaterial("vertex_color_lighting"); // forgets the drawing while the repaint runs

        worker.RunAll();
        rig.Service.OnMapFrame(200f);

        Assert.AreEqual(0, rig.Drawn.Count, "the stale repaint is dropped");
        Assert.AreEqual(1, worker.Queued, "and a fresh one started");
        worker.RunAll();
        rig.Service.OnMapFrame(200f);
        Assert.IsTrue(rig.Drawn.Count > 0);
    }

    /// <summary>Every repaint throws, as a deterministic geometry bug would.</summary>
    private sealed class CountingFailingWorker : ITerritoryWorker
    {
        public int Runs;

        public Task<T> Run<T>(Func<T> work)
        {
            Runs++;
            return Task.FromException<T>(new InvalidOperationException("boom"));
        }
    }

    [TestMethod]
    public void Repaint_Faulted_LogsTheWholeExceptionAndWaitsForTheNextChange()
    {
        var worker = new CountingFailingWorker();
        var rig = new Rig(worker, ("west", 100, "empire_w"), ("east", 300, "empire_s"));

        rig.Settle();

        Assert.AreEqual(1, worker.Runs, "a pure repaint that failed would fail again: no retry every frame");
        Assert.AreEqual(0, rig.Drawn.Count);
        rig.Logger.Received().LogError(Arg.Is<string>(m => m.Contains("InvalidOperationException") && m.Contains("boom")));

        rig.Service.MarkDirty();
        rig.Settle();

        Assert.AreEqual(2, worker.Runs, "the next change tries again");
    }

    // --- the fill, the player's colours, MCM's blend and material ---

    [TestMethod]
    public void FillLands_On_TintsEachRealmBetweenTheLines()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settings.FillLands.Returns(true);
        rig.Settings.FillStrength.Returns(0.3f);

        rig.Settle();

        uint gondor = rig.Palettes.NewPalette().ColourOf("empire_w");
        Assert.IsTrue(rig.Quads.Any(q => q.NearStart.Colour == BorderPainter.WithAlpha(gondor, 0.3f)), "Gondor's land is tinted");
    }

    [TestMethod]
    public void FillLands_Off_LeavesTheLandClear()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settings.FillLands.Returns(false);
        rig.Settings.FillStrength.Returns(0.3f);

        rig.Settle();

        uint gondor = rig.Palettes.NewPalette().ColourOf("empire_w");
        Assert.IsFalse(rig.Quads.Any(q => q.NearStart.Colour == BorderPainter.WithAlpha(gondor, 0.3f)));
    }

    [TestMethod]
    public void WarMode_NeutralLand_IsNotTinted()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "vlandia"));
        rig.Settings.FillLands.Returns(true);
        rig.Settings.FillStrength.Returns(0.3f);
        rig.Map.RelationToPlayer("empire_w").Returns(RealmRelation.Own);
        rig.Map.RelationToPlayer("vlandia").Returns(RealmRelation.Neutral);

        rig.Service.CycleMode();
        rig.Service.CycleMode();
        rig.Settle();

        uint neutral = RealmBorderService.RelationColours[RelationGroups.Neutral];
        uint own = RealmBorderService.RelationColours[RelationGroups.Own];
        Assert.IsTrue(rig.Quads.Any(q => q.NearStart.Colour == BorderPainter.WithAlpha(own, 0.3f)));
        Assert.IsFalse(rig.Quads.Any(q => q.NearStart.Colour == BorderPainter.WithAlpha(neutral, 0.3f)));
    }

    [TestMethod]
    public void ColourOverride_Changed_RepaintsInThePlayersColour()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        const uint Chosen = 0xFF12AB34;

        rig.Settings.ColourOverride("empire_w").Returns(Chosen);
        rig.Settings.ColourVersion.Returns(1);
        rig.Settle();

        Assert.IsTrue(rig.Quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == (Chosen & 0xFFFFFF)));
        Assert.AreEqual(Chosen, PixelAt(rig, 50, 100), "the province picture follows the choice too");
    }

    [TestMethod]
    public void McmBlendMode_Changed_RedrawsEveryTileWithIt()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        int uploads = rig.Uploads;
        rig.Renderer.UseMaterial(null).Returns(true);
        rig.Renderer.UseBlendMode("Modulate").Returns(true);

        rig.Settings.BlendMode.Returns("Modulate");
        rig.Settle();

        rig.Renderer.Received(1).UseBlendMode("Modulate");
        Assert.IsTrue(rig.Uploads >= 2 * uploads && uploads > 0, "every tile was built again");
    }

    [TestMethod]
    public void McmMaterial_Unknown_WarnsAndStillDraws()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Renderer.UseBlendMode(null).Returns(true);
        rig.Settings.MaterialName.Returns("no_such_material");

        rig.Settle();

        rig.Logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("no_such_material")));
        Assert.IsTrue(rig.Drawn.Count > 0);
    }

    [TestMethod]
    public void ChangingOneMcmRenderChoice_KeepsTheOther()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Settle();
        rig.Renderer.UseBlendMode("Modulate").Returns(true);

        rig.Settings.BlendMode.Returns("Modulate");
        rig.Settle();

        rig.Renderer.DidNotReceive().UseMaterial(Arg.Any<string?>());
    }

    [TestMethod]
    public void RealmCreatedInPlay_AvoidsThePlayersColours()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "clan:rebels"));
        uint firstReserve = rig.Palettes.NewPalette().ColourOf("clan:someone");
        rig.Settings.ColourOverride("empire_w").Returns(firstReserve);
        rig.Settings.ColourVersion.Returns(1);

        rig.Settle();

        Assert.AreNotEqual(firstReserve, PixelAt(rig, 350, 100), "the rebels do not take the colour the player gave Gondor");
        Assert.AreEqual(firstReserve, PixelAt(rig, 50, 100));
    }

    // --- Your Realm: the player's own realm when the palette does not name it ---

    private const uint YourColour = 0xFF12AB34;

    private static Rig PlayerWithTheirOwnClan()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "clan:player_faction"));
        rig.Map.PlayerRealm.Returns("clan:player_faction");
        return rig;
    }

    [TestMethod]
    public void YourRealm_ColoursTheLandYouHoldOutsideAnyKingdom()
    {
        var rig = PlayerWithTheirOwnClan();
        rig.Settings.YourRealmColour.Returns(YourColour);
        rig.Settings.ColourVersion.Returns(1);

        rig.Settle();

        Assert.AreEqual(YourColour, PixelAt(rig, 350, 100));
        Assert.IsTrue(rig.Quads.Any(q => (q.NearStart.Colour & 0xFFFFFF) == (YourColour & 0xFFFFFF)), "the border's wash too");
    }

    [TestMethod]
    public void YourRealm_KeepsItsColourWhenYouFoundAKingdom()
    {
        var rig = PlayerWithTheirOwnClan();
        rig.Settings.YourRealmColour.Returns(YourColour);
        rig.Settings.ColourVersion.Returns(1);
        rig.Settle();

        rig.Owner["east"] = "new_kingdom";
        rig.Map.PlayerRealm.Returns("new_kingdom");
        rig.Service.MarkDirty(); // KingdomCreatedEvent
        rig.Settle();

        Assert.AreEqual(YourColour, PixelAt(rig, 350, 100));
    }

    [TestMethod]
    public void YourRealm_LeaveAKingdomThatKeepsLand_ItTakesAFreeColourAndYourLandKeepsYours()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("middle", 200, "new_kingdom"), ("east", 300, "new_kingdom"));
        rig.Map.PlayerRealm.Returns("new_kingdom");
        rig.Settings.YourRealmColour.Returns(YourColour);
        rig.Settings.ColourVersion.Returns(1);
        rig.Settle();
        Assert.AreEqual(YourColour, PixelAt(rig, 200, 100));

        rig.Owner["east"] = "clan:player_faction"; // abdicate, then leave the kingdom with a fief
        rig.Map.PlayerRealm.Returns("clan:player_faction");
        rig.Service.MarkDirty(); // OnClanChangedKingdomEvent
        rig.Settle();

        Assert.AreNotEqual(YourColour, PixelAt(rig, 200, 100), "the kingdom left behind takes a free colour");
        Assert.AreEqual(YourColour, PixelAt(rig, 350, 100), "your colour follows you");
    }

    [TestMethod]
    public void YourRealm_DoesNotRecolourAKingdomThePaletteNames()
    {
        var rig = new Rig(("west", 100, "empire_w"), ("east", 300, "empire_s"));
        rig.Map.PlayerRealm.Returns("empire_w");
        rig.Settings.YourRealmColour.Returns(YourColour);
        rig.Settings.ColourVersion.Returns(1);

        rig.Settle();

        Assert.AreEqual(rig.Palettes.NewPalette().ColourOf("empire_w"), PixelAt(rig, 50, 100), "Gondor's own field applies, not Your Realm");
    }

    [TestMethod]
    public void YourRealm_Cleared_TakesAFreeColourAgain()
    {
        var rig = PlayerWithTheirOwnClan();
        rig.Settle();
        uint free = PixelAt(rig, 350, 100);
        rig.Settings.YourRealmColour.Returns(YourColour);
        rig.Settings.ColourVersion.Returns(1);
        rig.Settle();
        Assert.AreEqual(YourColour, PixelAt(rig, 350, 100));

        rig.Settings.YourRealmColour.Returns((uint?)null);
        rig.Settings.ColourVersion.Returns(2);
        rig.Settle();

        Assert.AreEqual(free, PixelAt(rig, 350, 100), "the free colour it had comes back");
    }

    [TestMethod]
    public void OnSessionStart_NextCampaign_YourRealmAppliesAgain()
    {
        var rig = PlayerWithTheirOwnClan();
        rig.Settings.YourRealmColour.Returns(YourColour);
        rig.Settings.ColourVersion.Returns(1);
        rig.Settle();

        rig.Service.OnSessionStart(); // every kingdomless player's realm is clan:player_faction again
        rig.Settle();

        Assert.AreEqual(YourColour, PixelAt(rig, 350, 100), "a second campaign or a loaded save keeps the player's colour");
    }

    [TestMethod]
    public void YourRealm_Blank_OtherColourChangesLeaveYourFreeColourAlone()
    {
        var rig = PlayerWithTheirOwnClan();
        rig.Settle();
        uint free = PixelAt(rig, 350, 100);

        rig.Settings.ColourOverride("empire_s").Returns(free); // a fresh pick would now keep clear of it
        rig.Settings.ColourVersion.Returns(1);
        rig.Settle();

        Assert.AreEqual(free, PixelAt(rig, 350, 100), "the realm keeps its colour; nothing re-picks it");
    }
}
