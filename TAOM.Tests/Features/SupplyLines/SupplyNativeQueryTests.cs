using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.Library;
using TAOM.Features.SupplyLines;

namespace TAOM.Tests.Features.SupplyLines;

/// <summary>
/// The route path query calls native map code that a managed catch cannot recover from, and PR #1064
/// reported a 1.4.8 crash in it on a remote supply order. These tests drive <c>AddLandPath</c> with a fake
/// <see cref="IMapScene"/>. The IL pins for its caller and for the route arrows come with WP118.
/// </summary>
[TestClass]
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
}
