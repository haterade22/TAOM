using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;
using TAOM.Features.Mumakil;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Mumakil;

/// <summary>
/// Pins the Mûmakil war tower's crew platform (#627 phase 2). The prefab is authored MOUNT-LOCAL, 1:1 with
/// sk_mumakil_harad_01.fbx, and the engine grows the mount to 3.0x from the Horse item's BodyLength=300, which
/// <see cref="TaomMumakilPlatform"/> applies at runtime from Agent.AgentScale. So every check here that concerns a
/// human body is done in WORLD metres, after scaling: the archers do not grow with the beast.
///
/// The Armory is unversioned, so these read the repo snapshot and prove the installed copy matches it.
/// </summary>
[TestClass]
public class MumakilPlatformTests
{
    // Measured in Blender 2026-09-20 from the platform mesh's upward faces, at authoring scale.
    private const float MainDeckZ = 3.00f, UpperDeckZ = 3.80f, NestDeckZ = 4.60f;
    private const float MountScale = 3.0f;          // Horse item taom_mumakil, BodyLength=300
    private const int ExpectedCrewFrames = 8;       // 5 main, 2 upper, 1 nest (Mike, 2026-09-20)
    private const int ExpectedDecks = 3;

    // The human body capsule, Native/ModuleData/monsters.xml id="human": radius 0.37, pos1 z 1.55, so the capsule
    // top is 1.92 m. Archers are 1.0x on every beast (no Horse slot, and the spawner passes NoHorses), so these are
    // world metres on any platform.
    private const float CapsuleTop = 1.55f + HowdahSeatMotion.HumanCapsuleRadius;

    // Half-extents of the shared floor mesh (bo_empire_keep_a_door_top) at scale 1, in authoring units. Derived
    // from the three measured footprints in the prefab header divided by their authored scales; all three decks
    // agree to three decimals, which is what makes this a measurement rather than a guess, and what
    // TheThreeFloors_AgreeOnTheSharedMeshSize re-checks from the file so a re-authored deck cannot silently drift.
    private const float FloorHalfX = 0.8064f, FloorHalfY = 0.8807f, FloorHalfZ = 0.3196f;
    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";

    private static XElement _root = null!;
    private static string _repo = null!;

    [ClassInitialize]
    public static void Load(TestContext _)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        _repo = dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
        _root = XDocument.Load(SnapshotPath()).Root?.Element("game_entity")
                ?? throw new InvalidDataException("the prefab has no root game_entity");
    }

    private static string SnapshotPath() => Path.Combine(_repo, "docs", "reference", "lotrlome-armory-snapshot",
        "Prefabs", MumakilConfig.PlatformPrefabName + ".xml");

    private static float[] Position(XElement e) =>
        (e.Element("transform")?.Attribute("position")?.Value ?? "0,0,0")
        .Split(',').Select(v => float.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    private static XElement[] CrewFrames() => _root.Descendants("game_entity")
        .Where(e => e.Element("tags")?.Elements("tag").Any(t => (string?)t.Attribute("name") == MumakilConfig.CrewTag) == true)
        .ToArray();

    [TestMethod]
    public void TheRootEntityName_IsTheOneTheCodeInstantiates()
    {
        Assert.AreEqual(MumakilConfig.PlatformPrefabName, (string?)_root.Attribute("name"),
            "MumakilMissionBehavior instantiates MumakilConfig.PlatformPrefabName; a mismatch logs 'not loaded' and no crew appear");
        Assert.AreEqual(nameof(TaomMumakilPlatform),
            (string?)_root.Element("scripts")?.Element("script")?.Attribute("name"));
    }

    [TestMethod]
    public void EightCrewFrames_SitOnTheThreeMeasuredDecks()
    {
        XElement[] frames = CrewFrames();
        Assert.AreEqual(ExpectedCrewFrames, frames.Length, "the crew count Mike settled on 2026-09-20");

        var byDeck = frames.GroupBy(f => Position(f)[2]).ToDictionary(g => g.Key, g => g.Count());
        Assert.AreEqual(ExpectedDecks, byDeck.Count, "five on the main deck, two on the upper, one in the nest");
        Assert.AreEqual(5, byDeck[MainDeckZ]);
        Assert.AreEqual(2, byDeck[UpperDeckZ]);
        Assert.AreEqual(1, byDeck[NestDeckZ]);

        foreach (XElement f in frames)
            Assert.AreEqual("TaomMumakilStandingPoint",
                (string?)f.Element("scripts")?.Element("script")?.Attribute("name"),
                $"{f.Attribute("name")?.Value}: the seat code only drives TaomMumakilStandingPoint frames");
    }

    [TestMethod]
    public void CrewFrames_AreSpacedForTheirCapsules_AFTER_TheMountScalesThem()
    {
        // The rule #627 cost nine in-game rounds to find: two 0.37 m capsules closer than 0.74 m overlap, the engine
        // shoves them apart every frame, the seat puts them back, and the engine then reads tens of m/s of movement,
        // at which point no bow draw ever completes. Archers do NOT scale with the mount, so this is checked in
        // world metres, which is where the authoring-scale trap lives: 0.25 m apart on the mesh is fine at 3.0x and
        // would be a collision at 1.0x.
        float[][] frames = CrewFrames().Select(Position).ToArray();
        for (int i = 0; i < frames.Length; i++)
            for (int j = i + 1; j < frames.Length; j++)
            {
                if (Math.Abs(frames[i][2] - frames[j][2]) > 0.01f) continue;   // different decks cannot collide
                float dx = (frames[i][0] - frames[j][0]) * MountScale;
                float dy = (frames[i][1] - frames[j][1]) * MountScale;
                float apart = (float)Math.Sqrt(dx * dx + dy * dy);
                Assert.IsTrue(HowdahSeatMotion.FramesAreClear(apart),
                    $"frames {i} and {j} are {apart:F2} m apart in game; they need {HowdahSeatMotion.MinimumFrameSeparation:F2} m");
            }
    }

    [TestMethod]
    public void EveryDeck_HasAFloorThatLetsMissilesThrough()
    {
        // An archer 9 m up shooting at anything below fires through its own deck unless the floor is barrier, which
        // is in the engine's missile-exclusion mask. Moveable because the entity is re-framed every tick.
        var floors = _root.Descendants("game_entity").Where(e => e.Element("physics") != null).ToArray();
        Assert.AreEqual(ExpectedDecks, floors.Length, "one floor per deck, and nothing else with a body");
        foreach (XElement floor in floors)
        {
            var flags = floor.Element("physics")!.Element("body_flags")!.Elements("body_flag")
                .Select(f => (string?)f.Attribute("name")).ToArray();
            CollectionAssert.Contains(flags, "barrier", $"{floor.Attribute("name")?.Value}: a downward shot would hit our own deck");
            CollectionAssert.Contains(flags, "moveable", $"{floor.Attribute("name")?.Value}: a body re-framed every tick must be moveable");
        }
    }

    [TestMethod]
    public void EveryEntity_IsVisibleOnlyWhenEditing()
    {
        // The tower the player sees is sk_mumakil_platform_a1 on the Horse item; this prefab must render nothing.
        foreach (XElement e in _root.DescendantsAndSelf("game_entity"))
            Assert.IsTrue(
                e.Element("visibility_masks")?.Elements("visibility_mask")
                    .Any(m => (string?)m.Attribute("name") == "visible_only_when_editing" && (string?)m.Attribute("value") == "true") == true,
                $"{e.Attribute("name")?.Value}: marker meshes must not render in battle");
    }

    [TestMethod]
    public void NothingBakesInTheMountScale()
    {
        // The whole point of authoring mount-local: the 3.0x comes from BodyLength at runtime. A frame at a deck
        // height already multiplied by three would put the crew three times too high the day that value changes.
        foreach (XElement f in CrewFrames())
        {
            float z = Position(f)[2];
            Assert.IsTrue(z >= MainDeckZ - 0.01f && z <= NestDeckZ + 0.01f,
                $"{f.Attribute("name")?.Value}: z {z} is not an authoring-scale deck height (3.00, 3.80 or 4.60)");
        }
    }

    private static XElement[] Floors() =>
        _root.Descendants("game_entity").Where(e => e.Element("physics") != null).ToArray();

    private static float[] Scale(XElement e)
    {
        string? s = e.Element("transform")?.Attribute("scale")?.Value;
        return s == null
            ? new[] { 1f, 1f, 1f }
            : s.Split(',').Select(v => float.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    }

    [TestMethod]
    public void TheThreeFloors_AgreeOnTheSharedMeshSize()
    {
        // Every gate below converts a floor's authored scale into a footprint through one shared constant. If a deck
        // is ever re-authored from a different mesh that conversion silently stops meaning anything, and the
        // headroom and containment gates would pass while measuring the wrong rectangle. This is the check that
        // notices, and it reads the file rather than the header comment.
        foreach (XElement floor in Floors())
        {
            float[] s = Scale(floor);
            Assert.AreEqual("bo_empire_keep_a_door_top", (string?)floor.Element("physics")!.Attribute("shape"),
                $"{floor.Attribute("name")?.Value}: a different body means FloorHalfX/Y/Z no longer describe it");
            Assert.IsTrue(s[0] > 0f && s[1] > 0f && s[2] > 0f,
                $"{floor.Attribute("name")?.Value}: a non-positive scale would invert its footprint");
        }
    }

    [TestMethod]
    public void NoCrewFrame_StandsUnderTheDeckAbove()
    {
        // The rule a multi-deck platform adds and the howdah never had, and it caught mumakil_crew_main_4 on
        // 2026-09-20. BodyFlags.Barrier is in the engine's missile-exclusion mask but NOT in its agent mask, so an
        // archer whose capsule reaches into the floor above is shoved by the engine every frame and put back by its
        // seat, which reads as tens of m/s and stops every bow draw: the same failure as bad spacing, and just as
        // silent. The footprint a frame must clear is inflated by the capsule RADIUS, because a shoulder under the
        // deck collides exactly as a head does.
        float radiusAuthored = HowdahSeatMotion.HumanCapsuleRadius / MountScale;
        foreach (XElement frame in CrewFrames())
        {
            float[] f = Position(frame);
            foreach (XElement floor in Floors())
            {
                float[] p = Position(floor), s = Scale(floor);
                float underside = p[2] - s[2] * FloorHalfZ;
                if (underside <= f[2] + 0.001f) continue;              // at or below this frame: not overhead
                bool overlaps = Math.Abs(f[0] - p[0]) <= s[0] * FloorHalfX + radiusAuthored
                             && Math.Abs(f[1] - p[1]) <= s[1] * FloorHalfY + radiusAuthored;
                if (!overlaps) continue;
                float clearance = (underside - f[2]) * MountScale;
                Assert.IsTrue(clearance >= CapsuleTop,
                    $"{frame.Attribute("name")?.Value} stands under {floor.Attribute("name")?.Value} with " +
                    $"{clearance:F2} m of headroom; a {CapsuleTop:F2} m archer needs to move out from under it, " +
                    "because no deck in this prefab is high enough above another to clear one");
            }
        }
    }

    [TestMethod]
    public void EveryCrewFrame_StandsOnADeckAndNotOverTheEdge()
    {
        // Containment is checked on the frame ORIGIN, deliberately not on the capsule: the feet must be on the
        // floor, while a shoulder overhanging a floor edge is not a collision (mumakil_crew_main_0 is one such,
        // 4.5 cm over the rear edge, and correct). Clearance from the WALLS is a Blender measurement and is not
        // re-derived here; this gate catches a frame that is off its deck entirely.
        foreach (XElement frame in CrewFrames())
        {
            float[] f = Position(frame);
            XElement? deck = Floors().FirstOrDefault(fl =>
            {
                float[] p = Position(fl), s = Scale(fl);
                return Math.Abs(p[2] + s[2] * FloorHalfZ - f[2]) <= 0.005f;
            });
            Assert.IsNotNull(deck, $"{frame.Attribute("name")?.Value}: no floor's top surface is at z {f[2]}");
            float[] dp = Position(deck!), ds = Scale(deck!);
            Assert.IsTrue(Math.Abs(f[0] - dp[0]) <= ds[0] * FloorHalfX && Math.Abs(f[1] - dp[1]) <= ds[1] * FloorHalfY,
                $"{frame.Attribute("name")?.Value} is off {deck!.Attribute("name")?.Value}: an archer placed there " +
                "stands in the air until the engine drops it");
        }
    }

    [TestMethod]
    public void TheDeadband_CannotEatTheSpacingMargin()
    {
        // The two tuning numbers are set independently and would meet only in game. The seat lets an archer drift
        // SeatDeadbandFor(scale) before correcting, so the conservative bound is two archers at full drift heading
        // straight at each other. That is not what a shared deck actually does to them (it carries them the same
        // way), so this is a design margin rather than a prediction: it exists so that widening the deadband or
        // tightening the frames can never quietly reintroduce the overlap the spacing rule was written for.
        float deadband = HowdahSeatMotion.SeatDeadbandFor(MountScale);
        float closest = float.MaxValue;
        float[][] frames = CrewFrames().Select(Position).ToArray();
        for (int i = 0; i < frames.Length; i++)
            for (int j = i + 1; j < frames.Length; j++)
            {
                if (Math.Abs(frames[i][2] - frames[j][2]) > 0.01f) continue;
                float dx = (frames[i][0] - frames[j][0]) * MountScale;
                float dy = (frames[i][1] - frames[j][1]) * MountScale;
                closest = Math.Min(closest, (float)Math.Sqrt(dx * dx + dy * dy));
            }
        Assert.IsTrue(closest - 2f * deadband >= HowdahSeatMotion.MinimumFrameSeparation,
            $"closest frames are {closest:F2} m apart and each archer may drift {deadband:F2} m, leaving " +
            $"{closest - 2f * deadband:F2} m against the {HowdahSeatMotion.MinimumFrameSeparation:F2} m capsule rule");
    }

    [TestMethod]
    public void TheTaggedFrames_AndTheScriptedFrames_AreTheSameEntities()
    {
        // The tag is what every test here selects on; the SCRIPT is what the game selects on, because
        // MumakilCrewSpawner filters platform.StandingPoints for TaomMumakilStandingPoint and UsableMachine fills
        // that list from the script components. Nothing reads the tag at runtime. So a ninth entity carrying the
        // script without the tag would get a live archer and pass every other test in this file, including the
        // spacing and headroom gates. This is the assertion that keeps the two sets one set.
        string[] tagged = CrewFrames().Select(e => (string?)e.Attribute("name") ?? "?").OrderBy(n => n).ToArray();
        string[] scripted = _root.Descendants("game_entity")
            .Where(e => e.Element("scripts")?.Elements("script")
                .Any(s => (string?)s.Attribute("name") == "TaomMumakilStandingPoint") == true)
            .Select(e => (string?)e.Attribute("name") ?? "?").OrderBy(n => n).ToArray();
        CollectionAssert.AreEqual(tagged, scripted,
            "the tagged set and the script-bearing set differ: the game seats the SCRIPT-bearing entities, so an " +
            "untagged one is an archer no test in this file measures");
    }

    [TestMethod]
    public void TheLiveArmoryCopy_MatchesTheSnapshot()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string armory = Path.Combine(string.IsNullOrWhiteSpace(env) ? DefaultGameDir : env, "Modules", "LOTRLOME_Armory");
        if (!Directory.Exists(armory))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live copy cannot be checked here.");

        string live = Path.Combine(armory, "Prefabs", MumakilConfig.PlatformPrefabName + ".xml");
        Assert.IsTrue(File.Exists(live), "the platform prefab is missing from the live Armory (a reinstall drops it)");
        CollectionAssert.AreEqual(File.ReadAllBytes(SnapshotPath()), File.ReadAllBytes(live),
            "the live Armory copy and the repo snapshot have diverged; the Armory is unversioned, so the snapshot is the record");
    }

    [TestMethod]
    public void ThePlatform_EmptiesItsSeats_BeforeVanillaCanDeactivateThem()
    {
        // Both copies of the hang that cost two dumps on the elephant (#627): OnMissionEnded and Disable each end in
        // the IsDeactivated setter, which spins on a MovingAgent registered without AIMoveToGameObjectEnable.
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        foreach (string name in new[] { "OnMissionEnded", "Disable" })
        {
            MethodInfo method = typeof(TaomMumakilPlatform).GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            Assert.IsNotNull(method, $"TaomMumakilPlatform must override {name}; vanilla's deactivates occupied seats");
            var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray())
                .Select(m => m.Name).ToList();
            CollectionAssert.Contains(calls, "ReleaseAllSeats", $"{name} must empty the seats before calling base");
        }
    }
}
