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
/// flagged moveable, a floor fitted to the elite howdah's measured deck, no rails (deleted 2026-09-20: they held nobody and may have
/// stood in the line of fire), and two crew frames tagged for the crew spawn, one behind the other so their body capsules do not overlap. The root is placed at the
/// elephant's origin plus ElephantConfig.HowdahHeightAboveGround, facing the elephant
/// (TaomHowdahMachine.RepositionToFixedOffset), so every number below is in that frame: +y forward, z up from 3.2 m.
/// The prefab lives in LOTRLOME_Armory/Prefabs (moved from the TAOM module 2026-09-18) and is named
/// taom_howdah_platform (renamed 2026-09-19: TAOM installs from v2.0.22 to v2.0.30 keep an old taom_howdah_agent in
/// Modules/TAOM/Prefabs, and two prefabs of one name have no defined winner). The Armory is unversioned, so these tests
/// read the repo snapshot, and the live tests prove the installed copy matches it and no other module declares the name.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class HowdahPrefabTests
{
    // The deck, re-measured 2026-09-19 in Blender from the TRUE vertex extents of the elite howdah's upward faces at
    // 3.15 m (SK_Elephant_Armor_Variations.fbx): x -0.68 to 0.68, y -2.026 to -0.270. The first build took the centre
    // from face CENTRES and got -0.805, which placed the platform 0.34 m ahead of the real deck and stood the front
    // pair of archers outside the howdah's front wall in the first crew test (#627).
    //
    // Every length below was measured at the FBX's own 1.0x and is kept exactly as measured. The elephant is 1.3x in
    // game since 2026-09-22 (body_length 130, Mike), so each expected value is that measurement times
    // ElephantConfig.AuthoredScale. The prefab is authored at that final size and nothing scales it at runtime: a
    // runtime-scaled entity carrying a physics body is the construct that dropped the mumakil's whole crew (#627).
    private static readonly float S = ElephantConfig.AuthoredScale;
    // 3.15 m is the elite howdah deck measured off SK_Elephant_Armor_Variations.fbx in the REST pose; the lift is how
    // much higher the live standing pose carries it, measured in game (ElephantConfig.HowdahLivePoseLift).
    private static readonly float MeasuredFloorHeight = (3.15f + ElephantConfig.HowdahLivePoseLift) * S;
    private static readonly float DeckCentreY = -1.148f * S;        // behind the elephant's origin
    private static readonly float DeckHalfWidth = 0.68f * S;        // 1.36 m across at 1.0x
    private static readonly float DeckHalfLength = 0.878f * S;      // 1.756 m along at 1.0x
    // Physics-shape bounding boxes, measured 2026-09-18/19 from the manifold data in Native/AssetPackages:
    // bo_empire_keep_a_door_top in bodies_shared.tpac, bo_barrier in core_game.tpac (a plane, zero thickness along y).
    private const float FloorShapeMinX = -0.81f, FloorShapeMaxX = 0.80f;
    private const float FloorShapeMinY = -0.89f, FloorShapeMaxY = 0.87f;
    private const float FloorShapeTop = 0.32f;
    private const float BarrierShapeBottom = -0.06f;
    private const float HumanCapsuleRadius = 0.37f;       // Native monsters.xml, human body_capsule
    // The WALLS, not the deck, decide where a body can stand, measured 2026-09-19 from the mesh's vertical faces
    // between 3.25 and 3.85 m: inner faces at x -0.63 and 0.63, y -1.98 at the back and -0.50 at the front. The deck
    // runs about 0.17 m further forward than the front wall, which is how frames placed on the deck put an archer's
    // capsule through it. Interior 1.26 x 1.48 m, and two 0.37 m capsules side by side need 1.48 m of width, so four
    // cannot fit, and the deck's length holds exactly TWO in a line (#627).
    private static readonly float InteriorMinX = -0.63f * S, InteriorMaxX = 0.63f * S;
    private static readonly float InteriorMinY = -1.98f * S, InteriorMaxY = -0.50f * S;
    private const int CrewFrameCount = 2;
    // How far a body capsule may overlap the cosmetic rim. Nothing pushes back (no rails, no collision on the harness
    // mesh), and archer-to-archer spacing is the rule that actually matters (HowdahSeatMotion).
    private const float AcceptedRimOverlap = 0.03f;
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
        Assert.AreEqual(1, bodies.Count, "the floor is the platform's only body since the rails went (#627)");
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
        Assert.AreEqual(CrewFrameCount, frames.Count);
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
    public void TheElephantItem_DeclaresTheScaleThePrefabIsAuthoredAt()
    {
        // The prefab's heights and footprint are final metres at ElephantConfig.AuthoredScale, so they silently
        // encode taom_war_elephant's body_length. Change one without the other and the crew stand inside the
        // elephant's back or float above the howdah, with no error anywhere. This is the gate that stops it.
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string horses = System.IO.Path.Combine(string.IsNullOrWhiteSpace(env)
                ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env,
            "Modules", "LOTRLOME_Armory", "ModuleData", "LOTRLOME_items", "LOTRAOM_horses.xml");
        if (!System.IO.File.Exists(horses))
            Assert.Inconclusive("LOTRLOME_Armory not installed on this machine; the item cannot be checked here.");

        var item = System.Xml.Linq.XDocument.Load(horses).Descendants("Item")
            .FirstOrDefault(i => (string?)i.Attribute("id") == "taom_war_elephant");
        Assert.IsNotNull(item, "the taom_war_elephant Horse item is missing from the Armory");
        string? bodyLength = item!.Descendants("Horse").FirstOrDefault()?.Attribute("body_length")?.Value;
        Assert.AreEqual(((int)Math.Round(ElephantConfig.AuthoredScale * 100f)).ToString(), bodyLength,
            "taom_war_elephant's body_length no longer matches ElephantConfig.AuthoredScale: regenerate the howdah " +
            "prefab at the new scale and change the constant, because nothing scales the platform at runtime");
    }

    [TestMethod]
    public void TheTrampleReach_GrowsWithTheBody()
    {
        // Tuned at 1.0x as 3 m trigger and 4 m radius; Mike chose to scale both with the body (2026-09-22) so the
        // tusks strike what they visibly reach. Trigger must stay inside the radius or the beast swings at air.
        Assert.AreEqual(3f * ElephantConfig.AuthoredScale, ElephantConfig.TrampleTriggerRange, 0.001f);
        Assert.AreEqual(4f * ElephantConfig.AuthoredScale, ElephantConfig.TrampleRadius, 0.001f);
        Assert.IsTrue(ElephantConfig.TrampleTriggerRange <= ElephantConfig.TrampleRadius);
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
    public void EveryCrewFrame_IsTaggedAndStandsOnTheFloor_InsideTheWalls()
    {
        var frames = _root.Descendants("game_entity").Where(IsCrewFrame).ToList();
        Assert.AreEqual(CrewFrameCount, frames.Count);
        foreach (var f in frames)
        {
            float[] p = Vec(f.Element("transform"), "position", 0f);
            Assert.AreEqual(MeasuredFloorHeight, ElephantConfig.HowdahHeightAboveGround + p[2], PlacementTolerance, "on the floor");
            // Against the WALLS, with a small tolerance. The walls are cosmetic: the rails are gone and the harness
            // mesh carries no collision, so a capsule may overlap the rim by a couple of centimetres and nothing
            // pushes back. What must NOT happen is crowding between archers, which is a separate test, and that rule
            // wins where the two conflict: the shipped pair sit 0.78 m apart and each overruns the rim by 2 cm.
            // A body is visibly narrower than its 0.37 m capsule, so that overrun does not show.
            Assert.IsTrue(p[0] - HumanCapsuleRadius >= InteriorMinX - AcceptedRimOverlap, $"x {p[0]}: well outside the left wall");
            Assert.IsTrue(p[0] + HumanCapsuleRadius <= InteriorMaxX + AcceptedRimOverlap, $"x {p[0]}: well outside the right wall");
            Assert.IsTrue(p[1] - HumanCapsuleRadius >= InteriorMinY - AcceptedRimOverlap, $"y {p[1]}: well outside the back wall");
            Assert.IsTrue(p[1] + HumanCapsuleRadius <= InteriorMaxY + AcceptedRimOverlap, $"y {p[1]}: well outside the front wall (#627)");
        }
    }

    [TestMethod]
    public void CrewFrames_StandFarEnoughApartForTheirCapsules()
    {
        // The rule that decides how many archers a platform can carry, here and on the mumakil's decks: two 0.37 m
        // capsules closer than 0.74 m overlap, the engine shoves them apart every frame and the seat teleports them
        // back, which is the movement that stops a bow draw completing (HowdahSeatMotion).
        var frames = _root.Descendants("game_entity").Where(IsCrewFrame)
            .Select(f => Vec(f.Element("transform"), "position", 0f)).ToList();
        for (int i = 0; i < frames.Count; i++)
            for (int j = i + 1; j < frames.Count; j++)
            {
                float dx = frames[i][0] - frames[j][0], dy = frames[i][1] - frames[j][1];
                float apart = (float)Math.Sqrt(dx * dx + dy * dy);
                Assert.IsTrue(HowdahSeatMotion.FramesAreClear(apart),
                    $"crew frames {i} and {j} are {apart:F2} m apart; they need {HowdahSeatMotion.MinimumFrameSeparation:F2} m");
            }
    }

    [TestMethod]
    public void NothingStandsInTheArchersLineOfFire()
    {
        // The four bo_barrier rails were deleted 2026-09-20 (#627). They were the vanilla siege tower's railing,
        // there to keep agents aboard, and the seat's per-frame teleport already does that, so they held nobody.
        // What they could still do is stand chest-high in front of a drawn bow: an arrow ignores a barrier body
        // (BodyFlags.CommonCollisionExcludeFlagsForMissile contains Barrier), but no managed code reads
        // CommonCollisionExcludeFlagsForCombat, which does not, so whether a clear-shot check hits them cannot be
        // settled from the decompile. The archers drew to 85 percent and re-nocked forever with ammo, a target and
        // 75 m of credited range, so the rails go until they shoot.
        var bodies = _root.Descendants("game_entity").Where(e => e.Element("physics") != null).ToList();
        Assert.AreEqual(1, bodies.Count, "the floor is the only body the platform should carry");
        Assert.AreEqual(ElephantConfig.HowdahFloorEntityName, (string?)bodies[0].Attribute("name"));
        Assert.AreEqual(0, _root.Descendants("game_entity")
                .Count(e => ((string?)e.Attribute("name") ?? "").Contains("rail")),
            "a rail entity is back: read this test before restoring one");
    }

    [TestMethod]
    public void TheFloor_LetsMissilesThrough()
    {
        // An archer shooting at anything below the deck fires through its own platform. Barrier is in
        // CommonCollisionExcludeFlagsForMissile, moveable is not, so the floor needs both: moveable because the
        // entity is re-framed every tick, barrier so the arrow passes.
        var physics = Floor().Element("physics");
        Assert.IsTrue(HasFlag(physics, "barrier"), "without barrier, a downward shot hits the howdah's own floor");
        Assert.IsTrue(HasFlag(physics, "moveable"), "a body re-framed every tick must be moveable");
    }
}
