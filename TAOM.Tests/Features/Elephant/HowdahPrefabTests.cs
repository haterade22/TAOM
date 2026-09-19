using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Elephant;

namespace TAOM.Tests.Features.Elephant;

/// <summary>
/// Pins the war elephant's howdah platform prefab (#627), rebuilt on the moving-platform pattern the vanilla siege
/// tower and the War Sails ships use (docs/features/elephant/howdah-ship-research-2026-09-18.md): every physics body
/// flagged moveable, a floor fitted to the elite howdah's measured deck, rails as bo_barrier scaled (width, 1, height)
/// and flagged barrier on the floor's edges, and four crew frames tagged for the crew spawn. The root is placed at the
/// elephant's origin plus ElephantConfig.HowdahHeightAboveGround, facing the elephant
/// (TaomHowdahMachine.RepositionToFixedOffset), so every number below is in that frame: +y forward, z up from 3.2 m.
/// The prefab lives in LOTRLOME_Armory/Prefabs (moved from the TAOM module 2026-09-18) and is named
/// taom_howdah_platform (renamed 2026-09-19: TAOM installs from v2.0.22 to v2.0.30 keep an old taom_howdah_agent in
/// Modules/TAOM/Prefabs, and two prefabs of one name have no defined winner). The Armory is unversioned, so these tests
/// read the repo snapshot, and the live tests prove the installed copy matches it and no other module declares the name.
/// </summary>
[TestClass]
public class HowdahPrefabTests
{
    private const float MeasuredFloorHeight = 3.15f;      // elite howdah deck, SK_Elephant_Armor_Variations.fbx
    private const float DeckCentreY = -0.805f;            // behind the elephant's origin
    private const float DeckHalfWidth = 0.70f;            // 1.4 m across
    private const float DeckHalfLength = 0.80f;           // 1.6 m along
    // Physics-shape bounding boxes, measured 2026-09-18/19 from the manifold data in Native/AssetPackages:
    // bo_empire_keep_a_door_top in bodies_shared.tpac, bo_barrier in core_game.tpac (a plane, zero thickness along y).
    private const float FloorShapeMinX = -0.81f, FloorShapeMaxX = 0.80f;
    private const float FloorShapeMinY = -0.89f, FloorShapeMaxY = 0.87f;
    private const float FloorShapeTop = 0.32f;
    private const float BarrierShapeBottom = -0.06f;
    private const float HumanCapsuleRadius = 0.37f;       // Native monsters.xml, human body_capsule
    // Four 0.37 m capsules cannot fit a 1.4 m deck clear (side by side 0.70 m apart, 0.74 m needed). Mike kept four
    // until the first crew test settles the count (2026-09-19, #627), so a frame may press its side rail this much.
    private const float AcceptedRailPress = 0.03f;
    private const float PlacementTolerance = 0.02f;

    private const string DefaultGameDir = @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord";
    private const string LegacyPrefabName = "taom_howdah_agent";
    private static readonly string PrefabFile = ElephantConfig.HowdahPrefabName + ".xml";
    private static readonly string[] EngineDefaultMachineTags = { "PilotStandingPointTag", "AmmoPickUpTag", "WaitStandingPointTag" };

    private static XElement _root = null!;
    private static string _repo = null!;

    [ClassInitialize]
    public static void Load(TestContext _)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        if (dir == null) throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
        _repo = dir.FullName;
        XElement? root = XDocument.Load(SnapshotPath()).Root?.Element("game_entity");
        _root = root ?? throw new InvalidDataException($"{SnapshotPath()} has no root game_entity");
    }

    private static string SnapshotPath() =>
        Path.Combine(_repo, "docs", "reference", "lotrlome-armory-snapshot", "Prefabs", PrefabFile);

    private static string ModulesDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return Path.Combine(string.IsNullOrWhiteSpace(env) ? DefaultGameDir : env, "Modules");
    }

    // A prefab is identified by its root game_entity name, not its file name, so a copy under any file name counts.
    // A text scan, not an XML parse: the sweep reads every installed module's prefabs (TAOM_Map alone is 52 MB), and a
    // malformed third-party prefab must not fail a TAOM test. Nested entities carry names too, so the hit is checked to
    // be a root (a direct child of <prefabs>) only when the text matches at all.
    private static bool DeclaresPrefab(string prefabsDir, string prefabName)
    {
        if (!Directory.Exists(prefabsDir)) return false;
        string needle = $"<game_entity name=\"{prefabName}\"";
        foreach (string file in Directory.EnumerateFiles(prefabsDir, "*.xml", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            if (!text.Contains(needle)) continue;
            try
            {
                if (XDocument.Parse(text).Root?.Elements("game_entity").Any(e => (string?)e.Attribute("name") == prefabName) == true)
                    return true;
            }
            catch (System.Xml.XmlException)
            {
                return true; // it names the prefab and cannot be parsed: treat it as declaring it, the stricter reading
            }
        }
        return false;
    }

    [TestMethod]
    public void TheRootEntityName_IsTheOneTheCodeInstantiates()
    {
        Assert.AreEqual(ElephantConfig.HowdahPrefabName, (string?)_root.Attribute("name"),
            "ElephantMissionBehavior instantiates ElephantConfig.HowdahPrefabName; a mismatch logs 'not found' and no platform spawns");
    }

    [TestMethod]
    public void TheNewName_IsNotTheLegacyOne()
    {
        Assert.AreNotEqual(LegacyPrefabName, ElephantConfig.HowdahPrefabName,
            "installs from v2.0.22 to v2.0.30 still carry a taom_howdah_agent in Modules/TAOM/Prefabs");
    }

    [TestMethod]
    public void TheTaomModule_ShipsNoCopy_ThePrefabLivesInTheArmory()
    {
        string repoPrefabs = Path.Combine(_repo, "Main", "_Module", "Prefabs");
        Assert.IsFalse(DeclaresPrefab(repoPrefabs, ElephantConfig.HowdahPrefabName),
            "a second prefab of the same name in the TAOM module would compete with the Armory's");
        Assert.IsFalse(DeclaresPrefab(repoPrefabs, LegacyPrefabName), "the legacy prefab must not come back into the TAOM module");
    }

    [TestMethod]
    public void TheLiveArmoryCopy_MatchesTheSnapshot()
    {
        string armory = Path.Combine(ModulesDir(), "LOTRLOME_Armory");
        if (!Directory.Exists(armory))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the live copy cannot be checked here.");
        string live = Path.Combine(armory, "Prefabs", PrefabFile);
        Assert.IsTrue(File.Exists(live), "the howdah prefab is missing from the live Armory (a reinstall drops it)");
        string snapshot = SnapshotPath();
        bool liveIsNewer = File.GetLastWriteTimeUtc(live) > File.GetLastWriteTimeUtc(snapshot);
        CollectionAssert.AreEqual(File.ReadAllBytes(snapshot), File.ReadAllBytes(live),
            liveIsNewer
                ? "the live Armory prefab is newer than the snapshot (a Kit save?): copy the live file over the snapshot"
                : "the snapshot is newer than the live Armory prefab: copy the snapshot over the live file");
    }

    [TestMethod]
    public void TheDeployedTaomModule_CarriesNoCopy()
    {
        string modules = ModulesDir();
        if (!Directory.Exists(modules))
            Assert.Inconclusive("No game install on this machine; the deployed module cannot be checked here.");
        Assert.IsFalse(DeclaresPrefab(Path.Combine(modules, "TAOM", "Prefabs"), ElephantConfig.HowdahPrefabName),
            "the deployed TAOM module carries the prefab (deployment is additive): delete it");
    }

    [TestMethod]
    public void NoOtherInstalledModule_DeclaresThePrefabName()
    {
        string modules = ModulesDir();
        if (!Directory.Exists(Path.Combine(modules, "LOTRLOME_Armory")))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the module sweep cannot run here.");
        var declaring = Directory.EnumerateDirectories(modules)
            .Where(m => DeclaresPrefab(Path.Combine(m, "Prefabs"), ElephantConfig.HowdahPrefabName))
            .Select(Path.GetFileName)
            .ToList();
        CollectionAssert.AreEqual(new[] { "LOTRLOME_Armory" }, declaring,
            "exactly one module may declare the howdah prefab; two of one name have no defined winner");
    }

    private static float[] Vec(XElement? transform, string attr, float fallback)
    {
        string? v = transform?.Attribute(attr)?.Value;
        if (v == null) return new[] { fallback, fallback, fallback };
        return v.Split(',').Select(p => float.Parse(p.Trim(), CultureInfo.InvariantCulture)).ToArray();
    }

    private static bool HasFlag(XElement? physics, string flag) =>
        physics?.Element("body_flags")?.Elements("body_flag").Any(f => (string?)f.Attribute("name") == flag) == true;

    private static string? ScriptName(XElement entity) =>
        (string?)entity.Element("scripts")?.Element("script")?.Attribute("name");

    private static XElement Floor() =>
        _root.Descendants("game_entity").Single(e => (string?)e.Element("physics")?.Attribute("shape") == "bo_empire_keep_a_door_top");

    private static bool IsCrewFrame(XElement e) =>
        e.Element("tags")?.Elements("tag").Any(t => (string?)t.Attribute("name") == ElephantConfig.HowdahCrewTag) == true;

    [TestMethod]
    public void EveryPhysicsBody_IsMoveable_AsOnEveryVanillaMovingPlatform()
    {
        var bodies = _root.DescendantsAndSelf("physics").ToList();
        Assert.IsTrue(bodies.Count >= 5, "floor plus four rails expected");
        foreach (var body in bodies)
            Assert.IsTrue(HasFlag(body, "moveable"), $"{body.Parent?.Attribute("name")?.Value}: a body re-framed every tick must be moveable");
    }

    [TestMethod]
    public void TheRootCarriesNoBody_TheFloorIsAChild()
    {
        Assert.IsNull(_root.Element("physics"), "the floor sits on a child so it can be placed and scaled on its own");
        Assert.AreEqual(ElephantConfig.HowdahFloorEntityName, (string?)Floor().Attribute("name"),
            "the diagnostics log finds the floor by this name");
    }

    [TestMethod]
    public void TheRootCarriesTheMachine_AndEveryCrewFrameASeat()
    {
        Assert.AreEqual(nameof(TaomHowdahMachine), ScriptName(_root),
            "ElephantMissionBehavior logs an error and spawns nothing without the machine on the root");
        var frames = _root.Descendants("game_entity").Where(IsCrewFrame).ToList();
        Assert.AreEqual(4, frames.Count);
        foreach (var frame in frames)
            Assert.AreEqual("TaomHowdahStandingPoint", ScriptName(frame),
                $"{frame.Attribute("name")?.Value}: the seat code only drives TaomHowdahStandingPoint frames");
    }

    [TestMethod]
    public void EveryEntity_IsVisibleOnlyWhenEditing()
    {
        foreach (var entity in _root.DescendantsAndSelf("game_entity"))
        {
            bool editingOnly = entity.Element("visibility_masks")?.Elements("visibility_mask")
                .Any(m => (string?)m.Attribute("name") == "visible_only_when_editing" && (string?)m.Attribute("value") == "true") == true;
            Assert.IsTrue(editingOnly, $"{entity.Attribute("name")?.Value}: marker meshes and barrier planes must not render in battle");
        }
    }

    [TestMethod]
    public void TheMachine_LeavesThePilotAmmoAndWaitTagsAtTheEngineDefaults()
    {
        var overridden = _root.Element("scripts")?.Element("script")?.Element("variables")?.Elements("variable")
            .Select(v => (string?)v.Attribute("name"))
            .Where(n => EngineDefaultMachineTags.Contains(n))
            .ToList();
        Assert.AreEqual(0, overridden?.Count ?? 0,
            "an empty tag reaches native HasTag(\"\") with unknown semantics; the defaults match no child here");
    }

    [TestMethod]
    public void TheFloorTop_IsTheEliteHowdahDeck()
    {
        var t = Floor().Element("transform");
        float top = Vec(t, "position", 0f)[2] + FloorShapeTop * Vec(t, "scale", 1f)[2];
        Assert.AreEqual(MeasuredFloorHeight, ElephantConfig.HowdahHeightAboveGround + top, PlacementTolerance);
        Assert.AreEqual(DeckCentreY, Vec(t, "position", 0f)[1], 0.05f);
    }

    [TestMethod]
    public void FourCrewFrames_AreTaggedAndStandOnTheFloor_InsideTheRails()
    {
        var frames = _root.Descendants("game_entity").Where(IsCrewFrame).ToList();
        Assert.AreEqual(4, frames.Count);
        foreach (var f in frames)
        {
            float[] p = Vec(f.Element("transform"), "position", 0f);
            Assert.AreEqual(MeasuredFloorHeight, ElephantConfig.HowdahHeightAboveGround + p[2], PlacementTolerance, "on the floor");
            Assert.IsTrue(Math.Abs(p[0]) + HumanCapsuleRadius <= DeckHalfWidth + AcceptedRailPress, $"x {p[0]} presses the side rail by more than 3 cm");
            Assert.IsTrue(Math.Abs(p[1] - DeckCentreY) + HumanCapsuleRadius <= DeckHalfLength + AcceptedRailPress, $"y {p[1]} presses the end rail by more than 3 cm");
        }
    }

    [TestMethod]
    public void Rails_AreScaledBarrierPlanes_NotASolidCage()
    {
        var rails = _root.Descendants("game_entity").Where(e => (string?)e.Element("physics")?.Attribute("shape") == "bo_barrier").ToList();
        Assert.AreEqual(4, rails.Count);
        foreach (var rail in rails)
        {
            Assert.IsTrue(HasFlag(rail.Element("physics"), "barrier"), "rails hold agents, the vanilla siege-tower flag");
            float[] s = Vec(rail.Element("transform"), "scale", 1f);
            Assert.AreEqual(1f, s[1], 1e-6f, "bo_barrier has zero thickness along y; scaling it does nothing");
            Assert.IsTrue(s[2] >= 0.9f && s[2] <= 1.3f, $"rail height scale {s[2]} (bo_barrier is 1 m tall)");
        }
    }

    [TestMethod]
    public void Rails_StandOnTheFloorsEdges()
    {
        var ft = Floor().Element("transform");
        float[] fp = Vec(ft, "position", 0f), fs = Vec(ft, "scale", 1f);
        float left = fp[0] + FloorShapeMinX * fs[0], right = fp[0] + FloorShapeMaxX * fs[0];
        float back = fp[1] + FloorShapeMinY * fs[1], front = fp[1] + FloorShapeMaxY * fs[1];
        float floorTop = fp[2] + FloorShapeTop * fs[2];

        var rails = _root.Descendants("game_entity")
            .Where(e => (string?)e.Element("physics")?.Attribute("shape") == "bo_barrier")
            .ToDictionary(e => (string?)e.Attribute("name") ?? "", e => e.Element("transform"));
        Assert.AreEqual(front, Vec(rails["howdah_rail_front"], "position", 0f)[1], PlacementTolerance, "front rail on the front edge");
        Assert.AreEqual(back, Vec(rails["howdah_rail_back"], "position", 0f)[1], PlacementTolerance, "back rail on the back edge");
        Assert.AreEqual(left, Vec(rails["howdah_rail_left"], "position", 0f)[0], PlacementTolerance, "left rail on the left edge");
        Assert.AreEqual(right, Vec(rails["howdah_rail_right"], "position", 0f)[0], PlacementTolerance, "right rail on the right edge");
        foreach (var (name, t) in rails.Select(kv => (kv.Key, kv.Value)))
        {
            float bottom = Vec(t, "position", 0f)[2] + BarrierShapeBottom * Vec(t, "scale", 1f)[2];
            Assert.AreEqual(floorTop, bottom, PlacementTolerance, $"{name}: rail bottom on the floor top");
        }
    }
}
