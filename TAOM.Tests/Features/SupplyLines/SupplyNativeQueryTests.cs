using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.Library;
using TAOM.Features.SupplyLines;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The route path query and the route arrows call native map code that a managed catch cannot recover
/// from. PR #1064 reported a 1.4.8 crash in each: a remote supply order in the path query, and the
/// return to a siege after a defensive ambush in the arrows' terrain height query. The path query runs
/// through <c>AddLandPath</c>, which these tests drive with a fake <see cref="IMapScene"/>; its caller and
/// the arrows need a live campaign or a native scene, so their tests read the IL fingerprint (calls,
/// constants and branches, in order) and pin only call order and the branch.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // builds TaleWorlds structs and resolves TaleWorlds members while reading the IL
public class SupplyNativeQueryTests
{
    private static readonly PathFaceRecord Valid = new PathFaceRecord(5, 1, 0);
    private static readonly PathFaceRecord Invalid = PathFaceRecord.NullFaceRecord;
    private static readonly Vec2 From = new Vec2(0f, 0f);
    private static readonly Vec2 To = new Vec2(8f, 0f);
    private static readonly int[] Excluded = { 7, 11 };

    private static List<Vec2> Run(IMapScene scene, PathFaceRecord fromFace, PathFaceRecord toFace)
    {
        var points = new List<Vec2>();
        SupplyCaravanService.AddLandPath(scene, fromFace, toFace, From, To, Excluded, 37, 59, points);
        return points;
    }

    // Native reads the face table at both indices with no bounds check: face -1 must never reach it.
    [DataTestMethod]
    [DataRow(false, true)] // start off the land navmesh
    [DataRow(true, false)] // end off the land navmesh
    [DataRow(false, false)]
    public void AddLandPath_AnInvalidEndpointFace_NeverQueriesNativeCode(bool fromValid, bool toValid)
    {
        var scene = Substitute.For<IMapScene>();

        var points = Run(scene, fromValid ? Valid : Invalid, toValid ? Valid : Invalid);

        scene.DidNotReceiveWithAnyArgs().GetPathBetweenAIFaces(default, default, default, default, default, default!, default!, default, default, default);
        Assert.AreEqual(0, points.Count, "no path, so the caller takes its straight-line fallback");
    }

    [TestMethod]
    public void AddLandPath_ValidFaces_PassesTheLandRulesInTheirSlots_AndCopiesThePath()
    {
        var scene = Substitute.For<IMapScene>();
        scene.GetPathBetweenAIFaces(Valid, Valid, From, To, 0.1f, Arg.Any<NavigationPath>(), Excluded, 1f, 37, 59)
            .Returns(call =>
            {
                var path = call.ArgAt<NavigationPath>(5);
                path.OverridePathPointAtIndex(0, From);
                path.OverridePathPointAtIndex(1, new Vec2(4f, 5f));
                path.OverridePathPointAtIndex(2, To);
                path.Size = 3;
                return true;
            });

        var points = Run(scene, Valid, Valid);

        CollectionAssert.AreEqual(new[] { From, new Vec2(4f, 5f), To }, points,
            "the exact exclusions, land-to-sea and sea-to-land costs reached the query, and its path came back");
    }

    [DataTestMethod]
    [DataRow(false, 3)] // no path
    [DataRow(true, 1)] // a one-point path is no route
    public void AddLandPath_NoUsablePath_AddsNothing(bool found, int size)
    {
        var scene = Substitute.For<IMapScene>();
        scene.GetPathBetweenAIFaces(default, default, default, default, default, default!, default!, default, default, default)
            .ReturnsForAnyArgs(call => { call.ArgAt<NavigationPath>(5).Size = size; return found; });

        Assert.AreEqual(0, Run(scene, Valid, Valid).Count);
    }

    private static string[] FingerprintOf(System.Type type, string name, BindingFlags flags)
    {
        var method = type.GetMethod(name, flags);
        Assert.IsNotNull(method, type.Name + "." + name);
        return IlCallScanner.Fingerprint(method, method!.GetMethodBody().GetILAsByteArray()).ToArray();
    }

    private static int IndexOf(string[] tokens, string fragment)
    {
        int index = System.Array.FindIndex(tokens, t => t.Contains(fragment));
        Assert.IsTrue(index >= 0, "IL contains " + fragment);
        return index;
    }

    // The caller reads the land rules from the active models and hands them to AddLandPath. Order only, so
    // caching a value in a local still passes; dropping or replacing a model read fails.
    [TestMethod]
    public void ComputeNavPathPoints_ReadsTheActiveModelsLandRules_ForAddLandPath()
    {
        var tokens = FingerprintOf(typeof(SupplyCaravanService), "ComputeNavPathPoints", BindingFlags.NonPublic | BindingFlags.Static);

        int excluded = IndexOf(tokens, "PartyNavigationModel::GetInvalidTerrainTypesForNavigationType(");
        int toSea = IndexOf(tokens, "MapDistanceModel::get_RegionSwitchCostFromLandToSea()");
        int toLand = IndexOf(tokens, "MapDistanceModel::get_RegionSwitchCostFromSeaToLand()");
        int query = IndexOf(tokens, "SupplyCaravanService::AddLandPath(");

        Assert.AreEqual("ldc.i4 1", tokens[excluded - 1], "exclusions are asked for NavigationType.Default (1)");
        Assert.IsTrue(excluded < toSea && toSea < toLand && toLand < query,
            "exclusions, land-to-sea, then sea-to-land are read before AddLandPath, matching its parameter order");
        Assert.IsFalse(tokens.Any(t => t.Contains("GetPathBetweenAIFaces(")), "the native query runs only inside AddLandPath");
    }

    // Scene is sealed and native, so this pins the shape: the heightmap read decides a branch before the one
    // terrain query, and the fallback is vanilla's land height (isOnLand: true) through the map wrapper.
    [TestMethod]
    public void SyncArrows_BranchesOnTheHeightmap_BeforeTheTerrainQuery_AndFallsBackToVanillaLandHeight()
    {
        var tokens = FingerprintOf(typeof(SupplyRouteVisualService), "SyncArrows", BindingFlags.NonPublic | BindingFlags.Instance);

        int check = IndexOf(tokens, "Scene::get_HasTerrainHeightmap()");
        int terrain = IndexOf(tokens, "Scene::GetTerrainHeightAndNormal(");
        int landPoint = IndexOf(tokens, "CampaignVec2::.ctor(Vec2,Boolean)");
        int fallback = IndexOf(tokens, "IMapScene::GetHeightAtPoint(");

        Assert.IsTrue(check < terrain && terrain < fallback, "check, terrain query, then the fallback");
        Assert.IsTrue(Enumerable.Range(check + 1, terrain - check - 1).Any(i => tokens[i] == "brtrue" || tokens[i] == "brfalse"),
            "a conditional branch between the heightmap read and the terrain query");
        Assert.AreEqual("ldc.i4 1", tokens[landPoint - 1], "the fallback point is a land point (isOnLand: true)");
        Assert.IsTrue(landPoint < fallback, "the land point is built for the fallback call");
        Assert.IsFalse(tokens.Any(t => t.Contains("IMapScene::GetTerrainHeightAndNormal(")),
            "the unguarded map-wrapper terrain query is not called");
        Assert.AreEqual(1, tokens.Count(t => t.Contains("GetTerrainHeightAndNormal(")), "one terrain query, behind the check");
    }
}
