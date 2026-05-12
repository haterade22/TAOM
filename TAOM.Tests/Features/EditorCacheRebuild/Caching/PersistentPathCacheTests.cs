using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Caching;

namespace TAOM.Tests.Features.EditorCacheRebuild.Caching;

[TestClass]
public class PersistentPathCacheTests
{
    private string _tempDir = null!;
    private string _filePath = null!;
    private IModLogger _logger = null!;
    private PersistentPathCache _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_PersistentPathCache_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "paths.bin");
        _logger = Substitute.For<IModLogger>();
        _sut = new PersistentPathCache(_logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static NavigationPath MakePath(params (float x, float y)[] points)
    {
        var path = new NavigationPath { Size = points.Length };
        for (int i = 0; i < points.Length; i++)
            path.PathPoints[i] = new Vec2(points[i].x, points[i].y);
        return path;
    }

    [TestMethod]
    public void TryLoad_MissingFile_ReturnsFalse()
    {
        var target = new PathReuseCache();

        var loaded = _sut.TryLoad(_filePath, 1u, 2u, target);

        Assert.IsFalse(loaded);
        Assert.AreEqual(0, target.Count);
    }

    [TestMethod]
    public void SaveLoadRoundTrip_PreservesAllData()
    {
        var source = new PathReuseCache();
        source.Store("alpha", false, "bravo", false, MakePath((1f, 2f), (3f, 4f)));
        source.Store("charlie", true, "delta", false, MakePath((5f, 6f)));

        _sut.Save(_filePath, 0xDEADBEEF, 0xCAFEBABE, source);

        var target = new PathReuseCache();
        var loaded = _sut.TryLoad(_filePath, 0xDEADBEEF, 0xCAFEBABE, target);

        Assert.IsTrue(loaded);
        Assert.AreEqual(2, target.Count);

        Assert.IsTrue(target.TryGet("alpha", false, "bravo", false, out var p1));
        Assert.AreEqual(2, p1.Size);
        Assert.AreEqual(1f, p1.PathPoints[0].x, 0.0001f);
        Assert.AreEqual(4f, p1.PathPoints[1].y, 0.0001f);

        Assert.IsTrue(target.TryGet("charlie", true, "delta", false, out var p2));
        Assert.AreEqual(1, p2.Size);
        Assert.AreEqual(5f, p2.PathPoints[0].x, 0.0001f);
    }

    [TestMethod]
    public void TryLoad_SceneCrcMismatch_ReturnsFalse()
    {
        var source = new PathReuseCache();
        source.Store("a", false, "b", false, MakePath((1f, 2f)));
        _sut.Save(_filePath, 0x1111u, 0x2222u, source);

        var target = new PathReuseCache();
        var loaded = _sut.TryLoad(_filePath, 0x9999u, 0x2222u, target);

        Assert.IsFalse(loaded);
        Assert.AreEqual(0, target.Count);
    }

    [TestMethod]
    public void TryLoad_NavMeshCrcMismatch_ReturnsFalse()
    {
        var source = new PathReuseCache();
        source.Store("a", false, "b", false, MakePath((1f, 2f)));
        _sut.Save(_filePath, 0x1111u, 0x2222u, source);

        var target = new PathReuseCache();
        var loaded = _sut.TryLoad(_filePath, 0x1111u, 0x9999u, target);

        Assert.IsFalse(loaded);
    }

    [TestMethod]
    public void TryLoad_GarbageFile_ReturnsFalseAndLogsError()
    {
        File.WriteAllText(_filePath, "not a binary cache file at all");

        var target = new PathReuseCache();
        var loaded = _sut.TryLoad(_filePath, 0u, 0u, target);

        Assert.IsFalse(loaded);
    }

    [TestMethod]
    public void TryLoad_WrongMagic_ReturnsFalse()
    {
        using (var stream = File.Create(_filePath))
        using (var writer = new System.IO.BinaryWriter(stream))
        {
            writer.Write(0xDEADBEEFu);
            writer.Write(1);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0);
        }

        var target = new PathReuseCache();
        var loaded = _sut.TryLoad(_filePath, 0u, 0u, target);

        Assert.IsFalse(loaded);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("magic")));
    }

    [TestMethod]
    public void Save_AtomicWrite_NoTempFileRemains()
    {
        var source = new PathReuseCache();
        source.Store("a", false, "b", false, MakePath((1f, 2f)));

        _sut.Save(_filePath, 1u, 2u, source);

        Assert.IsTrue(File.Exists(_filePath));
        Assert.IsFalse(File.Exists(_filePath + ".tmp"));
    }

    [TestMethod]
    public void Save_OverwritesExistingFile()
    {
        var source1 = new PathReuseCache();
        source1.Store("a", false, "b", false, MakePath((1f, 2f)));
        _sut.Save(_filePath, 1u, 2u, source1);
        var size1 = new FileInfo(_filePath).Length;

        var source2 = new PathReuseCache();
        source2.Store("a", false, "b", false, MakePath((1f, 2f)));
        source2.Store("c", false, "d", false, MakePath((3f, 4f)));
        _sut.Save(_filePath, 1u, 2u, source2);
        var size2 = new FileInfo(_filePath).Length;

        Assert.IsTrue(size2 > size1);

        var target = new PathReuseCache();
        _sut.TryLoad(_filePath, 1u, 2u, target);
        Assert.AreEqual(2, target.Count);
    }
}
