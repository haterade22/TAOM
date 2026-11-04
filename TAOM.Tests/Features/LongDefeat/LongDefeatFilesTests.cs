using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
public class LongDefeatFilesTests
{
    private string _dir = null!;

    [TestInitialize]
    public void Init() => _dir = Path.Combine(Path.GetTempPath(), "taom_ld_files_" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Clean()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [TestMethod]
    public void WriteAtomic_MakesTheFolderAndTheFile_AndLeavesNoTempBehind()
    {
        var path = Path.Combine(_dir, "deeper", "a.txt");

        LongDefeatFiles.WriteAtomic(path, "one");

        Assert.AreEqual("one", File.ReadAllText(path));
        CollectionAssert.AreEqual(new[] { path }, Directory.GetFiles(Path.GetDirectoryName(path)!).ToArray());
    }

    [TestMethod]
    public void WriteAtomic_OverAnExistingFile_ReplacesItsText()
    {
        var path = Path.Combine(_dir, "a.txt");
        LongDefeatFiles.WriteAtomic(path, "one");

        LongDefeatFiles.WriteAtomic(path, "two");

        Assert.AreEqual("two", File.ReadAllText(path));
        Assert.AreEqual(1, Directory.GetFiles(_dir).Length, "the temp file is gone after a replace");
    }
}
