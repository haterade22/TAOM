using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Features.EditorCacheRebuild.Caching;

namespace TAOM.Tests.Features.EditorCacheRebuild.Caching;

[TestClass]
public class PathReuseCacheTests
{
    private PathReuseCache _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new PathReuseCache();

    private static NavigationPath MakePath(params (float x, float y)[] points)
    {
        var path = new NavigationPath { Size = points.Length };
        for (int i = 0; i < points.Length; i++)
            path.PathPoints[i] = new Vec2(points[i].x, points[i].y);
        return path;
    }

    [TestMethod]
    public void TryGet_EmptyCache_ReturnsFalseAndIncrementsMiss()
    {
        var found = _sut.TryGet("a", false, "b", false, out _);

        Assert.IsFalse(found);
        Assert.AreEqual(0, _sut.HitCount);
        Assert.AreEqual(1, _sut.MissCount);
    }

    [TestMethod]
    public void Store_ThenTryGet_ReturnsTrueAndIncrementsHit()
    {
        var path = MakePath((1f, 2f), (3f, 4f));
        _sut.Store("a", false, "b", false, path);

        var found = _sut.TryGet("a", false, "b", false, out var retrieved);

        Assert.IsTrue(found);
        Assert.AreEqual(2, retrieved.Size);
        Assert.AreEqual(1, _sut.HitCount);
        Assert.AreEqual(0, _sut.MissCount);
    }

    [TestMethod]
    public void TryGet_SymmetricLookup_Hits()
    {
        var path = MakePath((1f, 2f));
        _sut.Store("a", false, "b", false, path);

        var found = _sut.TryGet("b", false, "a", false, out _);

        Assert.IsTrue(found);
    }

    [TestMethod]
    public void Store_ClonesPath_SourceMutationDoesNotAffectStored()
    {
        var path = MakePath((1f, 2f));
        _sut.Store("a", false, "b", false, path);

        path.PathPoints[0] = new Vec2(99f, 99f);

        _sut.TryGet("a", false, "b", false, out var retrieved);
        Assert.AreEqual(1f, retrieved.PathPoints[0].x, 0.0001f);
    }

    [TestMethod]
    public void Count_TracksStoredEntries()
    {
        _sut.Store("a", false, "b", false, MakePath((1f, 2f)));
        _sut.Store("c", false, "d", false, MakePath((3f, 4f)));

        Assert.AreEqual(2, _sut.Count);
    }

    [TestMethod]
    public void Clear_ResetsAllState()
    {
        _sut.Store("a", false, "b", false, MakePath((1f, 2f)));
        _sut.TryGet("a", false, "b", false, out _);
        _sut.TryGet("x", false, "y", false, out _);

        _sut.Clear();

        Assert.AreEqual(0, _sut.Count);
        Assert.AreEqual(0, _sut.HitCount);
        Assert.AreEqual(0, _sut.MissCount);
    }
}
