using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Features.EditorCacheRebuild.Caching;

namespace TAOM.Tests.Features.EditorCacheRebuild.Caching;

[TestClass]
public class NavigationPathClonerTests
{
    [TestMethod]
    public void Clone_PreservesSize()
    {
        var src = new NavigationPath { Size = 3 };
        src.PathPoints[0] = new Vec2(1f, 2f);
        src.PathPoints[1] = new Vec2(3f, 4f);
        src.PathPoints[2] = new Vec2(5f, 6f);

        var clone = NavigationPathCloner.Clone(src);

        Assert.AreEqual(3, clone.Size);
    }

    [TestMethod]
    public void Clone_PreservesWaypoints()
    {
        var src = new NavigationPath { Size = 2 };
        src.PathPoints[0] = new Vec2(1.5f, 2.5f);
        src.PathPoints[1] = new Vec2(3.5f, 4.5f);

        var clone = NavigationPathCloner.Clone(src);

        Assert.AreEqual(1.5f, clone.PathPoints[0].x, 0.0001f);
        Assert.AreEqual(2.5f, clone.PathPoints[0].y, 0.0001f);
        Assert.AreEqual(3.5f, clone.PathPoints[1].x, 0.0001f);
        Assert.AreEqual(4.5f, clone.PathPoints[1].y, 0.0001f);
    }

    [TestMethod]
    public void Clone_IndependentFromSource()
    {
        var src = new NavigationPath { Size = 1 };
        src.PathPoints[0] = new Vec2(1f, 2f);

        var clone = NavigationPathCloner.Clone(src);
        src.PathPoints[0] = new Vec2(99f, 99f);
        src.Size = 0;

        Assert.AreEqual(1f, clone.PathPoints[0].x, 0.0001f);
        Assert.AreEqual(2f, clone.PathPoints[0].y, 0.0001f);
        Assert.AreEqual(1, clone.Size);
    }

    [TestMethod]
    public void Clone_EmptyPath_ProducesEmptyClone()
    {
        var src = new NavigationPath { Size = 0 };

        var clone = NavigationPathCloner.Clone(src);

        Assert.AreEqual(0, clone.Size);
    }
}
