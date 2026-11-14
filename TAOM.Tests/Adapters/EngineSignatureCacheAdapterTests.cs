using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>
/// The real file and loader calls of the signature cache adapter, over a temporary folder: a missing folder or file, the
/// replace of an existing file, a leftover temporary file, the read cap and the module stamp lookup.
/// </summary>
[TestClass]
public class EngineSignatureCacheAdapterTests
{
    private string _dir = null!;
    private EngineSignatureCacheAdapter _adapter = null!;

    [TestInitialize]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom-sigcache-" + Guid.NewGuid().ToString("N"), "TAOM");
        _adapter = new EngineSignatureCacheAdapter(_dir);
    }

    [TestCleanup]
    public void TearDown()
    {
        var root = Path.GetDirectoryName(_dir)!;
        if (!Directory.Exists(root))
            return;
        if (Directory.Exists(_dir))
        {
            foreach (var file in Directory.GetFiles(_dir))
                File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(root, true);
    }

    [TestMethod]
    public void ReadCache_NoFolderNoFile_ReturnsNull()
    {
        Assert.IsNull(_adapter.ReadCache(100));
    }

    [TestMethod]
    public void WriteCache_MissingFolder_CreatesItAndTheFileReadsBack()
    {
        _adapter.WriteCache("hello\nworld\n");

        Assert.AreEqual("hello\nworld\n", _adapter.ReadCache(100));
        Assert.IsFalse(File.Exists(_adapter.FilePath + ".tmp"), "the temporary file was moved into place");
    }

    [TestMethod]
    public void WriteCache_ExistingFile_ReplacesItsText()
    {
        _adapter.WriteCache("first");
        _adapter.WriteCache("second");

        Assert.AreEqual("second", _adapter.ReadCache(100));
        Assert.IsFalse(File.Exists(_adapter.FilePath + ".tmp"));
    }

    [TestMethod]
    public void WriteCache_LeftoverTemporaryFile_IsOverwritten()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_adapter.FilePath + ".tmp", "junk from a crashed write");

        _adapter.WriteCache("fresh");

        Assert.AreEqual("fresh", _adapter.ReadCache(100));
    }

    [TestMethod]
    public void WriteCache_ReadOnlyTemporaryFile_Throws()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_adapter.FilePath + ".tmp", "locked");
        File.SetAttributes(_adapter.FilePath + ".tmp", FileAttributes.ReadOnly);

        Assert.ThrowsException<UnauthorizedAccessException>(() => _adapter.WriteCache("x"));
    }

    [TestMethod]
    public void ReadCache_FileLongerThanTheCap_ReturnsExactlyTheCap()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_adapter.FilePath, new string('a', 5000));

        var text = _adapter.ReadCache(100);

        Assert.AreEqual(100, text!.Length);
    }

    [TestMethod]
    public void ModuleFileStamp_LoadedModule_IsSizeAndWriteTimeOfItsFile()
    {
        var stamp = _adapter.ModuleFileStamp("kernel32.dll");

        Assert.IsNotNull(stamp);
        var parts = stamp!.Split('|');
        Assert.AreEqual(2, parts.Length);
        Assert.IsTrue(long.Parse(parts[0]) > 0);
        Assert.IsTrue(long.Parse(parts[1]) > 0);
    }

    [TestMethod]
    public void ModuleFileStamp_ModuleNotLoaded_ReturnsNull()
    {
        Assert.IsNull(_adapter.ModuleFileStamp("NoSuchModuleLoadedHere.dll"));
    }
}
